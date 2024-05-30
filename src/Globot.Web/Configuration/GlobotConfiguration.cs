namespace Globot.Web.Configuration;

public class GlobotConfiguration
{
    readonly static string[] DEFAULT_FILE_EXTENSIONS = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.js", "*.txt", "*.pdf", ".ttf", ".otf", ".woff", ".woff2", ".eot", ".svg", "*.html", "*.htm"];

    public GlobotConfiguration()
    {
        Azure = new AzureConfiguration();
        KnownSources = new Dictionary<string, KnownSourceConfiguration>();
        GlobDumpPath = Path.Combine(Environment.CurrentDirectory, "GlobDump");
    }

    public string? AppRoot { get; set; }
    public string? ApiKey { get; set; }
    public string[] FileExtensions { get; set; } = DEFAULT_FILE_EXTENSIONS;
    public string? GlobDumpPath { get; set; }

    public AzureConfiguration Azure { get; set; }

    public Dictionary<string, KnownSourceConfiguration> KnownSources { get; set; }

    public class KnownSourceConfiguration
    {
        public string? Path {get;set;}
        public string[] FileExtensions { get; set; } =  DEFAULT_FILE_EXTENSIONS;
    }

    public class BlobServiceClientConfiguration
    {
        public string? ConnectionString { get; set; }
    }

    public class AzureConfiguration 
    {
        public BlobServiceClientConfiguration? BlobServiceClient { get; set; }
    }
}
