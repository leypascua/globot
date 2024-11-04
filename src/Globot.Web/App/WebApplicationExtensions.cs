using Globot.Web.Configuration;
using Globot.Web.App.Services;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Globot.Web.App.Routes;

namespace Globot.Web.App;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddGlobot(this IServiceCollection services)
    {
        services.AddSingleton<GlobRequestQueue>();
        services.AddSingleton<GlobotUploadWorker>();
        services.AddSingleton<GlobotHostedRequestWorkerService>();
        services.AddHostedService<GlobotHostedRequestWorkerService>();

        services.Configure<JsonOptions>(options => {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }

    public static WebApplication AddGlobotRoutes(this WebApplication app)
    {
        GlobotConfiguration globot = app.Services
            .GetService<IConfiguration>()!
            .GlobotConfiguration();

        var hostEnv = app.Services.GetRequiredService<IWebHostEnvironment>();

        string appRoot = globot.AppRoot ?? Guid.NewGuid().ToString();

        // add known routes
        var routes = app
            .MapGroup($"/{appRoot}")
            .RegisterRoutes();

        var logFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var log = logFactory.CreateLogger("Globot");
        log.LogInformation("Globot AppRoot accessible on: https://your.host.name:port/{appRoot}/", appRoot);

        return app;
    }
}
