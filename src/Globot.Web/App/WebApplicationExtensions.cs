using Globot.Web.Configuration;
using Globot.Web.App.Services;
using Microsoft.AspNetCore.Http.Json;

namespace Globot.Web.App;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddGlobot(this IServiceCollection services)
    {
        services.AddSingleton<GlobRequestService>();
        services.AddSingleton<GlobotHostedService>();
        services.AddHostedService<GlobotHostedService>();

        services.Configure<JsonOptions>(options => {
            options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
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

        var routes = app.MapGroup($"/{appRoot}");

        routes.MapGet("/Requests", Routes.Requests.Get);
        
        if (hostEnv.IsDevelopment())
        {
            routes.MapGet("/Requests/Submit", Routes.Requests.Post);
        }

        var logFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var log = logFactory.CreateLogger("Globot");
        log.LogInformation("Globot AppRoot accessible on: https://your.host.name:port/{appRoot}/", appRoot);

        return app;
    }
}
