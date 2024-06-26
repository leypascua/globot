using Globot.Web.App.Services;
using Globot.Web.Configuration;
using System.Collections.Concurrent;

namespace Globot.Web.App.Services;

public class GlobotHostedRequestWorkerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly GlobotConfiguration _globot;
    private readonly GlobRequestQueue _requestQueue;
    private readonly GlobotUploadWorker _uploader;
    private readonly ILoggerFactory _logFactory;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<Task> _runningTasks;

    public GlobotHostedRequestWorkerService(GlobRequestQueue requestQueue, GlobotUploadWorker uploader, IConfiguration configuration, ILoggerFactory logFactory)
    {
        _configuration = configuration;
        _globot = configuration.GlobotConfiguration();
        _requestQueue = requestQueue;
        _uploader = uploader;
        _logFactory = logFactory;
        _log = logFactory.CreateLogger<GlobotHostedRequestWorkerService>();
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        _runningTasks = new ConcurrentBag<Task>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork(stoppingToken);
        }

        _log.LogWarning("GlobotHostedRequestWorkerService is terminating...");
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        var requestContext = await _requestQueue.GetNext(stoppingToken);
        if (requestContext == null) return;

        await _semaphore.WaitAsync(stoppingToken);

        var task = StartProcessRequestTask(requestContext, stoppingToken);

        _runningTasks.Add(task);
    }

    private Task StartProcessRequestTask(PushGlobRequestContext requestContext, CancellationToken stoppingToken)
    {
        var task = Task.Factory
            .StartNew(
                async (_) => await ProcessRequest(requestContext, stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning
            )
            .ContinueWith(t => {
                _semaphore.Release();
                _runningTasks.TryTake(out _);
                if (t.IsFaulted)
                {
                    _log.LogError("Error when processing request for known source [{KnownSource}]. Reason: {Message}", requestContext.Request.KnownSource, t.Exception.Message);
                }
            }, stoppingToken);

        return task;
    }

    private async Task ProcessRequest(PushGlobRequestContext requestContext, CancellationToken stoppingToken)
    {
        try 
        {
            requestContext.Status = PushGlobRequestContext.PushGlobRequestStatus.Running;

            await _uploader.UploadGlobs(requestContext.Request.KnownSource, stoppingToken);

            requestContext.Status = PushGlobRequestContext.PushGlobRequestStatus.Finished;
        }
        catch (Exception ex)
        {
            requestContext.Status = PushGlobRequestContext.PushGlobRequestStatus.FinishedWithErrors;
            _log.LogError("Request worker for known source [{KnownSource}] failed. Reason: {Message}", requestContext.Request.KnownSource, ex.Message);
        }
    }
}