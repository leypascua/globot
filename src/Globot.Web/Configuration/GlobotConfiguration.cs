namespace Globot.Web.Configuration;

public class GlobotConfiguration
{
    public readonly static string[] DEFAULT_FILE_EXTENSIONS = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.js", "*.txt", "*.pdf", ".ttf", ".otf", ".woff", ".woff2", ".eot", ".svg", "*.html", "*.htm"];

    public GlobotConfiguration()
    {
        Azure = new AzureConfiguration();
        KnownSources = new Dictionary<string, KnownSourceConfiguration>();
        GlobManifestPath =  Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "output",
            "GlobUploadWorker"
        );
    }

    public string? AppRoot { get; set; }
    public string? ApiKey { get; set; }
    public string[]? FileExtensions { get; set; }
    public string? GlobManifestPath { get; set; }

    public AzureConfiguration Azure { get; set; }

    public Dictionary<string, KnownSourceConfiguration> KnownSources { get; set; }

    public class KnownSourceConfiguration
    {
        public string? Path {get;set;}
        public string[]? FileExtensions { get; set; }
        public bool? ForceLowerCase { get; set; }
    }

    public class BlobServiceClientConfiguration
    {
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }
    }

    public class AzureConfiguration 
    {
        public BlobServiceClientConfiguration? BlobServiceClient { get; set; }
    }
}
