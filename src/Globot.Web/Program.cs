using Globot.Web;
using Globot.Web.App;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile($"appsettings.{EnvironmentContext.Current.Name}.json", optional: true);

builder.Services.AddGlobot();

var app = builder.Build();

app.MapGet("/", () => "I am Globot. Use me as you please.");
app.AddGlobotRoutes();

app.Run();
