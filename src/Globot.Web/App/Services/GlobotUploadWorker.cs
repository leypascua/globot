using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Globot.Web.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.Json;

namespace Globot.Web.App.Services;

public class GlobotUploadWorker
{
    private readonly GlobotConfiguration _globot;
    private readonly ILogger<GlobotUploadWorker> _log;

    public GlobotUploadWorker(IConfiguration configuration, ILogger<GlobotUploadWorker> log)
    {
        _globot = configuration.GlobotConfiguration();
        _log = log;
    }

    public async Task UploadGlobs(string knownSourceName, CancellationToken cancellationToken)
    {
        var knownSource = _globot.KnownSources[knownSourceName];
        var sourceDir = new DirectoryInfo(knownSource.Path!);
        var matcher = PrepareGlobMatcher(knownSource, _globot.FileExtensions);
        var globs = matcher.Execute(new DirectoryInfoWrapper(sourceDir));

        if (!globs.HasMatches)
        {
            _log.LogWarning("No files found in path '{Path}'", knownSource.Path);
            return;
        }

        var container = GetBlobContainer(_globot.Azure.BlobServiceClient!);
        var manifestFile = GetManifestFile(knownSourceName);
        var manifest = await GlobotFileManifest.CreateFrom(manifestFile);

        _log.LogDebug("  > Starting blob uploads for known source: " + knownSourceName);

        foreach (var file in globs.Files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            
            await UploadGlob(knownSourceName, sourceDir, file, manifest, container, cancellationToken);
        }
        
        if (manifest.HasChanged())
        {
            await SaveManifestFile(knownSourceName, manifest, manifestFile);
        }
        
        _log.LogDebug("  > Finished blob upload for known source: " + knownSourceName);
    }

    private static Matcher PrepareGlobMatcher(GlobotConfiguration.KnownSourceConfiguration knownSource, string[]? fileExtensions)
    {
        var matcher = new Matcher();
        var includedPatterns = (knownSource.FileExtensions ?? fileExtensions ?? GlobotConfiguration.DEFAULT_FILE_EXTENSIONS)
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

    private async Task UploadGlob(string knownSourceName, DirectoryInfo sourceDir, FilePatternMatch file, GlobotFileManifest manifest, BlobContainerClient container, CancellationToken cancellationToken)
    {
        var knownSource = _globot.KnownSources[knownSourceName];
        var sourceFileName = Path.Combine(sourceDir.FullName, file.Path);
        var sourceFileInfo = new FileInfo(sourceFileName);

        // skip uploading empty files
        if (!sourceFileInfo.Exists || sourceFileInfo.Length == 0)
        {
            return;
        }
        
        string mimeType = MimeTypes.GetMimeType(sourceFileName);
        bool forceLowerCase = knownSource.ForceLowerCase.GetValueOrDefault();
        string destBlobName = forceLowerCase ? file.Path.ToLowerInvariant() : file.Path;
        string blobPath = Path
            .Combine(knownSourceName, destBlobName)
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
                _log.LogInformation("  > Uploading blob: [{blobPath}] from known source [{knownSourceName}] to container [{container}] with ForceLowerCase=[{forceLowerCase}]", blobPath, knownSourceName, container, forceLowerCase);
                await UploadBlob(sourceFileName, blobPath, mimeType, container, cancellationToken);
                _log.LogDebug("    >> Upload completed: [{blobPath}] from known source [{knownSourceName}] to container [{container}]", blobPath, knownSourceName, container);
            }
        }
    }

    private async Task SaveManifestFile(string knownSourceName, GlobotFileManifest manifest, FileInfo manifestFile)
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
            _log.LogInformation("  > [{knownSourceName}] Manifest file saved at [{FullName}]", knownSourceName, manifestFile.FullName);
        }
    }

    private void RenameManifestFile(FileInfo manifestFile)
    {
        string newFileName = $"{Path.GetFileNameWithoutExtension(manifestFile.Name)}.{DateTime.UtcNow.ToString("yyyyMMddTHHmmss")}{manifestFile.Extension}";
        string newFilePath = Path.Combine(
            manifestFile.Directory!.FullName,
            newFileName
        );

        manifestFile.MoveTo(newFilePath);
    }

    private FileInfo GetManifestFile(string knownSourceName)
    {
        string manifestPath = Path.Combine(
            ResolveManifestDirectory(),
            $"{knownSourceName}.manifest.json"
        );

        var file = new FileInfo(manifestPath);

        if (file.Exists)
        {
            return file;
        }

        if (!file.Directory!.Exists)
        {
            file.Directory.Create();
        }

        return file;
    }

    private string ResolveManifestDirectory()
    {
        string manifestPath = string.IsNullOrEmpty(_globot.GlobManifestPath) ?
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "output",
                "GlobUploadWorker"
            ) :
            Path.IsPathRooted(_globot.GlobManifestPath) ?
                _globot.GlobManifestPath :
                Path.Combine(Environment.CurrentDirectory, "output", "GlobUploadWorker");

        return Directory.Exists(manifestPath) ? 
            manifestPath : 
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".globot",
                    "GlobUploadWorker"
            );
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

    private static BlobContainerClient GetBlobContainer(GlobotConfiguration.BlobServiceClientConfiguration clientConf)
    {
        var storageContainer = new BlobContainerClient(clientConf.ConnectionString, clientConf.ContainerName);
        return storageContainer;
    }
}
