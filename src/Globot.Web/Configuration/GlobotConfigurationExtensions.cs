using Globot.Web.Configuration;
using Globot.Web.App.Services;

namespace Globot.Web;

public static class GlobotConfigurationExtensions
{
    public static IEnumerable<GlobUploadWorker> CreateGlobUploadWorkers(this GlobotConfiguration globot, ILoggerFactory logFactory)
    {
        var _ = logFactory.CreateLogger("GlobotConfigurationExtensions");

        foreach (string knownSourceName in globot.KnownSources.Keys)
        {
            var (worker, err) = CreateGlobUploadWorker(globot, logFactory, knownSourceName);
            if (err != null)
            {
                _.LogWarning("Unable to create GlobUploadWorker instance for [{knownSourceName}]. Reason: {message}", knownSourceName, err.Message);
                continue;
            }

            yield return worker!;
        }
    }

    public static (GlobUploadWorker?, Exception?) CreateGlobUploadWorker(this GlobotConfiguration globot, ILoggerFactory logFactory, string knownSourceName)
    {
        if (!globot.KnownSources.ContainsKey(knownSourceName))
        {
            return (null, new ArgumentOutOfRangeException("Invalid KnownSource key: " + knownSourceName));
        }

        var knownSource = globot.KnownSources[knownSourceName];

        if (!Directory.Exists(knownSource.Path))
        {
            return (null, new DirectoryNotFoundException($"THe path '{knownSource.Path}' for source '{knownSourceName}' cannot be located."));
        }

        var log = logFactory.CreateLogger($"{typeof(GlobUploadWorker).FullName}.{knownSourceName}");

        return (new GlobUploadWorker(globot, log, knownSourceName), null);
    }
}
