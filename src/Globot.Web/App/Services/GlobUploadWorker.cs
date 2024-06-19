using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Globot.Web.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.Json;

namespace Globot.Web.App.Services;

public class GlobUploadWorker
{
    private readonly GlobotConfiguration _globot;
    private readonly ILogger _log;
    private readonly string _knownSourceName;

    public string KnownSourceName => _knownSourceName;

    public GlobUploadWorker(GlobotConfiguration globot, ILogger log, string knownSourceName)
    {
        _globot = globot;
        _log = log;
        _knownSourceName = knownSourceName;
    }

    public async Task UploadGlobs(CancellationToken cancellationToken)
    {
        var knownSource = _globot.KnownSources[_knownSourceName];

        var matcher = new Matcher();
        var includedPatterns = knownSource.FileExtensions
            .Select(fe => $"**/{fe}")
            .ToArray();

        matcher.AddIncludePatterns(includedPatterns);
        
        var sourceDir = new DirectoryInfo(knownSource.Path!);
        var globs = matcher.Execute(new DirectoryInfoWrapper(sourceDir));

        if (!globs.HasMatches)
        {
            _log.LogInformation("No files found in path '{Path}'", knownSource.Path);
            return;
        }

        var container = GetBlobContainer();
        var manifestFile = GetManifestFile(_knownSourceName);
        var manifest = await GlobotFileManifest.CreateFrom(manifestFile);

        _log.LogInformation("  > Starting blob uploads for known source: " + _knownSourceName);

        foreach (var file in globs.Files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            
            var sourceFileName = Path.Combine(sourceDir.FullName, file.Path);
            var sourceFileInfo = new FileInfo(sourceFileName);

            // skip uploading empty files
            if (!sourceFileInfo.Exists || sourceFileInfo.Length == 0)
            {
                continue;
            }
            
            string mimeType = MimeTypes.GetMimeType(sourceFileName);
            string destBlobName = file.Path.ToLowerInvariant();
            string blobPath = Path
                .Combine(_knownSourceName, destBlobName)
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
                    await UploadBlob(sourceFileName, blobPath, mimeType, container, cancellationToken);
                }
            }
        }
        
        if (manifest.HasChanged())
        {
            await SaveManifestFile(manifest, manifestFile);
        }
        
        _log.LogInformation("  > Finished blob upload for known source: " + _knownSourceName);
    }

    private async Task SaveManifestFile(GlobotFileManifest manifest, FileInfo manifestFile)
    {
        string manifestFileName = manifestFile.FullName;
        if (manifestFile.Exists)
        {
            Rename(manifestFile);
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
            string knownSourceName = _knownSourceName;
            _log.LogInformation("  > [{knownSourceName}] Manifest file saved at [{FullName}]", knownSourceName, manifestFile.FullName);
        }
    }

    private void Rename(FileInfo manifestFile)
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

        _log.LogInformation("  > Blob uploaded to '{blobPath}' on container [{Name}].", blobPath, container.Name);
    }

    private BlobContainerClient GetBlobContainer()
    {
        var clientConf = _globot.Azure.BlobServiceClient!;

        var storageContainer = new BlobContainerClient(clientConf.ConnectionString, clientConf.ContainerName);

        return storageContainer;
    }
}
