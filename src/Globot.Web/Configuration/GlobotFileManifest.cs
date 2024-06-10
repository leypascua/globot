
namespace Globot.Web;

public class GlobotFileManifest
{
    public string? ContainerName {get;set;}
    public string? SourcePath {get;set;}
    public string[]? FileExtensions {get;set;}
    public Dictionary<string, Entry> Entries {get;set;} = new Dictionary<string, Entry>();

    public static GlobotFileManifest CreateFrom(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return new GlobotFileManifest();
        }

        throw new NotImplementedException("Creating from manifest.json file not supported at this time.");
    }

    public bool TryAdd(string sourcePath, string destPath, string contentType)
    {
        if (this.Entries.ContainsKey(sourcePath)) return false;

        this.Entries.Add(sourcePath, new Entry {
            Path = destPath,
            ContentType = contentType
        });

        return true;
    }

    public class Entry
    {
        public string? Path {get;set;}
        public string? ContentType {get;set;} 
    }
}
