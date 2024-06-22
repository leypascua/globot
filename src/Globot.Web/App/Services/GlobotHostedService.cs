using System.ComponentModel;
using Globot.Web.App.Services;
using Globot.Web.Configuration;

namespace Globot.Web;

public class GlobotHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly GlobRequestService _globRequests;
    private readonly ILoggerFactory _logFactory;
    private readonly ILogger<GlobotHostedService> _log;
    private readonly GlobotConfiguration _globot;

    public GlobotHostedService(IConfiguration configuration, GlobRequestService globRequestService, ILoggerFactory logFactory)
    {
        _configuration = configuration;
        _globot = configuration.GlobotConfiguration();
        _globRequests = globRequestService;
        _logFactory = logFactory;
        _log = logFactory.CreateLogger<GlobotHostedService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("GlobotHostedService is starting up.");

        var workers = InitializeWorkers();

        await Task.Delay(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork(workers, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        _log.LogInformation("GlobotHostedService is shutting down.");
    }

    private async Task DoWork(IEnumerable<GlobUploadWorker> workers, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var worker in workers) 
        {
            tasks.Add(
                ExecuteWorker(worker, cancellationToken)
            );
        }

        await Task
            .WhenAll(tasks.ToArray())
            .WaitAsync(cancellationToken);

        tasks.ForEach(t => t.Dispose());

        tasks.Clear();
        tasks = null;
    }

    private async Task ExecuteWorker(GlobUploadWorker worker, CancellationToken cancellationToken)
    {
        try 
        {
            _log.LogDebug("ExecuteWorker: Running for known source: [{KnownSourceName}]", worker.KnownSourceName);
            await worker.UploadGlobs(cancellationToken);
            _log.LogDebug("ExecuteWorker: Completed for known source: [{KnownSourceName}]", worker.KnownSourceName);            
        }
        catch (Exception ex)
        {
            _log.LogError("ExecuteWorker failed on worker [{KnownSourceName}]. Reason: {Message}", worker.KnownSourceName, ex.Message);
        }
    }

    private IEnumerable<GlobUploadWorker> InitializeWorkers()
    {
        foreach (string knownSourceName in _globot.KnownSources.Keys)
        {
            var logger = _logFactory.CreateLogger($"Globot.Web.App.Services.GlobUploadWorker[\"{knownSourceName}\"]");
            yield return new GlobUploadWorker(_globot, logger, knownSourceName);
        }
    }
}
