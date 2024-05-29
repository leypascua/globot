using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;

namespace Globot.Web;

public class EnvironmentContext
{
    public const string DEVELOPMENT_ENV = "Development";
    public const string STAGING_ENV = "Staging";
    public const string PRODUCTION_ENV = "Production";

    static EnvironmentContext()
    {
        string envName = 
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            DEVELOPMENT_ENV;

            Current = new EnvironmentContext(envName);
            
    }

    private EnvironmentContext(string name)
    {
        Name = name;
    }

    public static EnvironmentContext Current { get; private set; }

    public string Name { get; private set; }

    public bool IsDevelopment()
    {
        return this.Name.Equals(DEVELOPMENT_ENV);
    }
}
