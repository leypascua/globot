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
        var manifest = GlobotFileManifest.CreateFrom(manifestFile);

        foreach (var file in globs.Files)
        {
            var sourceFileName = Path.Combine(sourceDir.FullName, file.Path);
            
            string destBlobName = file.Path.ToLowerInvariant();
            string blobPath = Path.Combine(_knownSourceName, destBlobName);
            string mimeType = MimeTypes.GetMimeType(sourceFileName);

            bool isUploadRequired = manifest.TryAdd(file.Path, blobPath, mimeType);

            if (isUploadRequired)
            {
                await UploadBlob(sourceFileName, blobPath, mimeType, container, cancellationToken);
            }
        }

        await SaveManifestFile(manifest, manifestFile);
    }

    private async Task SaveManifestFile(GlobotFileManifest manifest, FileInfo manifestFile)
    {
        if (manifestFile.Exists)
        {
            Rename(manifestFile);
        }

        var newManifestFile = new FileInfo(manifestFile.FullName);

        if (!manifestFile.Directory!.Exists)
        {
            manifestFile.Directory.Create();
        }

        using (var fs = newManifestFile.CreateText())
        {
            string json = JsonSerializer.Serialize(manifest);
            await fs.WriteAsync(json);
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

        if (!Directory.Exists(Path.GetDirectoryName(file.FullName)))
        {
            Directory.CreateDirectory(file.FullName);
        }

        return file;
    }

    private static string ResolveManifestDirectory()
    {
        bool useLocalFile = false;

#if DEBUG
        if (EnvironmentContext.Current.IsDevelopment())
        {
            useLocalFile = true;
        }
#endif

        return useLocalFile ? 
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "output",
                "GlobUploadWorker"
            ) :
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
