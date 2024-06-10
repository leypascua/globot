
using System.Text.Json;

namespace Globot.Web;

public class GlobotFileManifest
{
    public string? ContainerName {get;set;}
    public string? SourcePath {get;set;}
    public string[]? FileExtensions {get;set;}
    public Dictionary<string, Entry> Entries {get;set;} = new Dictionary<string, Entry>();

    public static async Task<GlobotFileManifest> CreateFrom(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return new GlobotFileManifest();
        }

        using (var fs = fileInfo.OpenRead())
        {
            return await CreateFrom(fs);
        }
    }

    public static async Task<GlobotFileManifest> CreateFrom(Stream stream)
    {
        using (var reader = new StreamReader(stream))
        {
            string json = await reader.ReadToEndAsync();
            return CreateFrom(json);
        }
    }

    public static GlobotFileManifest CreateFrom(string json)
    {
        return JsonSerializer.Deserialize<GlobotFileManifest>(json)!;
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
