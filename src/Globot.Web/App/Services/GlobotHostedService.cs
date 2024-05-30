using Globot.Web.App.Services;
using Globot.Web.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Globot.Web;

public class GlobotHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly GlobRequestService _globRequests;
    private readonly ILogger<GlobotHostedService> _log;

    public GlobotHostedService(IConfiguration configuration, GlobRequestService globRequestService, ILoggerFactory logFactory)
    {
        _configuration = configuration;
        _globRequests = globRequestService;
        _log = logFactory.CreateLogger<GlobotHostedService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("GlobotHostedService is starting up.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork(stoppingToken);
        }

        _log.LogInformation("GlobotHostedService is shutting down.");
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        var requestContext = await _globRequests.GetNext(cancellationToken);

        if (requestContext == null) return;

        var globot = _configuration.GlobotConfiguration();

        string globDirName = $"{DateTime.UtcNow.ToString("yyyyMMddTHHmmss")}.{requestContext.Id}";
        string globPath = Path.Combine(globot.GlobDumpPath ?? Path.Combine(Environment.CurrentDirectory, "output"), globDirName);
        if (!Directory.Exists(globPath))
        {
            Directory.CreateDirectory(globPath);
        }

        requestContext.Status = PushGlobRequestContext.PushGlobRequestStatus.Running;
        bool errorOccurred = false;

        foreach (string knownSourceName in requestContext.Request.KnownSources)
        {
            if (!globot.KnownSources.ContainsKey(knownSourceName)) 
            {
                _log.LogWarning("The requested source '{knownSourceName}' is undefined in configuration.", knownSourceName);
                continue;
            }
            
            var knownSource = globot.KnownSources[knownSourceName];

            if (!Directory.Exists(knownSource.Path))
            {
                _log.LogWarning("The path '{Path}' defined for source '{knownSourceName}' does not exist", knownSource.Path, knownSourceName);
                return;
            }

            _log.LogInformation("Performing glob work for request [{Id}] on known source [{knownSourceName}]", requestContext.Id, knownSourceName);

            try
            {
                DoGlob(knownSourceName, knownSource, globPath);
            }
            catch (Exception ex)
            {
                _log.LogCritical("Error occurred. Reason: {Message}", ex.Message);
                errorOccurred = true;
            }
        }

        requestContext.Status = errorOccurred ? PushGlobRequestContext.PushGlobRequestStatus.FinishedWithErrors : PushGlobRequestContext.PushGlobRequestStatus.Finished;
    }

    private void DoGlob(string knownSourceName, GlobotConfiguration.KnownSourceConfiguration knownSource, string globPath)
    {
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

        foreach (var file in globs.Files)
        {
            var sourceFileName = Path.Combine(sourceDir.FullName, file.Path);
            var destFileName = Path.Combine(globPath, knownSourceName, file.Path);
            var destFile = new FileInfo(destFileName);
            if (!destFile.Directory!.Exists)
            {
                destFile.Directory.Create();
            }

            File.Copy(sourceFileName, destFileName);
        }

        _log.LogInformation("Done copying globbed files from '{path}' to {destination}.", knownSource.Path, globPath);
    }
}
