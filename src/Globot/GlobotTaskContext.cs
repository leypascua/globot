using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Globot
{
    public class GlobotTaskContext
    {
        private readonly GlobotOptions _options;
        private readonly ILogger<GlobotTaskContext> _log;

        public GlobotTaskContext(GlobotOptions options, ILogger<GlobotTaskContext> log)
        {
            _options = options;
            _log = log;
        }

        public async Task UploadAsync(CancellationToken cancellationToken)
        {
            var sourceDir = new DirectoryInfo(_options.SourcePath!);
            var matcher = PrepareGlobMatcher(_options.IncludedFileExtensions!);
            var globs = matcher.Execute(new DirectoryInfoWrapper(sourceDir));

            if (!globs.HasMatches)
            {
                _log.LogWarning("No files found in path '{Path}'", _options.SourcePath);
                return;
            }

            var (manifest, manifestFile) = await GetManifestFile(_options.Prefix, _options.ManifestPath);
            var container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);

            string prefixLabel = !string.IsNullOrEmpty(_options.Prefix) ?
                $" Prefix: {_options.Prefix}" : string.Empty;

            _log.LogDebug($"  > Starting blob uploads.{prefixLabel}");

            foreach (var file in globs.Files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await UploadGlob(_options.Prefix, sourceDir, file, manifest, container, cancellationToken);
            }

            if (manifest.HasChanged() && !string.IsNullOrEmpty(_options.Prefix))
            {
                await SaveManifestFile(manifest, manifestFile);
            }

            _log.LogDebug("  > Finished blob upload." + prefixLabel);
        }

        private async Task SaveManifestFile(GlobotFileManifest manifest, FileInfo manifestFile)
        {
            string manifestFileName = manifestFile.FullName;
            if (manifestFile.Exists)
            {
                RenameManifestFile(manifestFile);
            }

            var newManifestFile = new FileInfo(manifestFileName);

            if (!manifestFile.Directory!.Exists)
            {
                manifestFile.Directory.Create();
            }

            using (var fs = newManifestFile.CreateText())
            {
                string json = JsonSerializer.Serialize(manifest);
                await fs.WriteAsync(json);
                _log.LogInformation("  > Manifest file saved at [{FullName}]", manifestFile.FullName);
            }
        }

        private static void RenameManifestFile(FileInfo manifestFile)
        {
            string newFileName = $"{Path.GetFileNameWithoutExtension(manifestFile.Name)}.{DateTime.UtcNow.ToString("yyyyMMddTHHmmss")}{manifestFile.Extension}";
            string newFilePath = Path.Combine(
                manifestFile.Directory!.FullName,
                newFileName
            );

            manifestFile.MoveTo(newFilePath);
        }

        private async Task UploadGlob(string? prefix, DirectoryInfo sourceDir, FilePatternMatch file, GlobotFileManifest manifest, BlobContainerClient container, CancellationToken cancellationToken)
        {
            var sourceFileName = Path.Combine(sourceDir.FullName, file.Path);
            var sourceFileInfo = new FileInfo(sourceFileName);

            // skip uploading empty files
            if (!sourceFileInfo.Exists || sourceFileInfo.Length == 0)
            {
                return;
            }

            string mimeType = MimeTypes.GetMimeType(sourceFileName);
            bool forceLowerCase = _options.ForceLowerCase;
            string destBlobName = forceLowerCase ? file.Path.ToLowerInvariant() : file.Path;

            string prefixedPath = string.IsNullOrEmpty(prefix) ?
                destBlobName :
                Path.Combine(prefix, destBlobName);

            string blobPath = prefixedPath
                .Replace("\\", "/");

            bool isUploadRequired = manifest.TryAdd(
                sourceFilePath: sourceFileName,
                destPath: blobPath,
                contentType: mimeType
            );

            if (isUploadRequired)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _log.LogInformation("  > Uploading blob: [{blobPath}] to container [{container}] with ForceLowerCase=[{forceLowerCase}]", blobPath, container, forceLowerCase);
                    await UploadBlob(sourceFileName, blobPath, mimeType, container, cancellationToken);
                    _log.LogDebug("    >> Upload completed: [{blobPath}] from to container [{container}]", blobPath, container);
                }
            }
        }

        private async Task UploadBlob(string sourceFileName, string blobPath, string mimeType, BlobContainerClient container, CancellationToken cancellationToken)
        {
            var blob = container.GetBlobClient(blobPath);
            var opts = new BlobUploadOptions
            {
                Conditions = null,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = mimeType
                }
            };

            await blob.UploadAsync(
                sourceFileName,
                opts,
                cancellationToken
            );
        }

        private static async Task<(GlobotFileManifest manifest, FileInfo manifestFile)> GetManifestFile(string? prefix, string? inputPath)
        {
            bool skipManifestFile = string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(inputPath);
            if (skipManifestFile)
            {
                return (GlobotFileManifest.Null, new FileInfo(Path.GetTempFileName()));
            }

            string directoryPath = Path.IsPathRooted(inputPath) ?
                inputPath :
                Path.Combine(Environment.CurrentDirectory, ".globot", "GlobUploadWorker");

            string finalManifestDir = Directory.Exists(directoryPath) ?
                directoryPath :
                Path.Combine(
                    Environment.CurrentDirectory,
                        ".globot",
                        "GlobUploadWorker"
                );

            string manifestPath = Path.Combine(
                finalManifestDir,
                $"{prefix}.manifest.json"
            );

            var file = new FileInfo(manifestPath);

            var manifest = file.Exists ?
                await GlobotFileManifest.CreateFrom(file) :
                new GlobotFileManifest();

            return (manifest, file);
        }

        private static BlobContainerClient GetBlobContainer(string connectionString, string containerName)
        {
            var storageContainer = new BlobContainerClient(connectionString, containerName);
            return storageContainer;
        }

        private static Matcher PrepareGlobMatcher(string[] fileExtensions)
        {
            var matcher = new Matcher();
            var includedPatterns = fileExtensions
                .Select(fe => {
                    string trimmed = (fe ?? string.Empty).Trim();
                    return trimmed.StartsWith("*") ?
                         trimmed :
                         $"*{trimmed}";
                })
                .Select(fe => $"**/{fe}")
                .ToArray();

            matcher.AddIncludePatterns(includedPatterns);

            return matcher;
        }
    }
}
