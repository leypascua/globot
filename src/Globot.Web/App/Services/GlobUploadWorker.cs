using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Globot.Web.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

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

    public async Task UploadGlobs(CancellationToken cancelToken)
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

    }

    private BlobContainerClient GetBlobContainer()
    {
        var clientConf = _globot.Azure.BlobServiceClient!;

        var storageContainer = new BlobContainerClient(clientConf.ConnectionString, clientConf.ContainerName);

        return storageContainer;
    }
}
