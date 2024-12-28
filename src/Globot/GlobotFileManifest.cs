
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Globot;

public class GlobotFileManifest
{
    private bool _hasChanged = false;
    public string? ContainerName { get; set; }
    public string? SourcePath { get; set; }
    public string[]? FileExtensions { get; set; }
    public Dictionary<string, Entry> Entries { get; set; } = new Dictionary<string, Entry>();
    public static GlobotFileManifest Null => new NullManifest();

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

    public virtual bool TryAdd(string sourceFilePath, string destPath, string contentType)
    {
        bool isNewFile = !this.Entries.ContainsKey(sourceFilePath);

        if (isNewFile)
        {
            this.Entries.Add(sourceFilePath, new Entry
            {
                Path = destPath,
                ContentType = contentType,
                Md5Hash = ComputeFileHash(sourceFilePath)
            });
        }

        var entry = this.Entries[sourceFilePath];

        string sourceFileHash = isNewFile ?
            entry.Md5Hash! :
            ComputeFileHash(sourceFilePath);

        if (isNewFile || !sourceFileHash.Equals(entry.Md5Hash))
        {
            entry.Md5Hash = sourceFileHash;
            _hasChanged = true;
            return true;
        }

        return false;
    }

    public virtual bool HasChanged()
    {
        return _hasChanged;
    }

    private static string ComputeFileHash(string filePath)
    {
        using (var fileStream = File.OpenRead(filePath))
        using (MD5 md5 = MD5.Create())
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }

            md5.TransformFinalBlock(buffer, 0, 0);

            byte[] hashBytes = md5.Hash!;
            var hashStringBuilder = new StringBuilder();

            foreach (byte b in hashBytes)
            {
                hashStringBuilder.Append(b.ToString("x2"));
            }

            return hashStringBuilder.ToString();
        }
    }

    class NullManifest : GlobotFileManifest
    {
        public override bool HasChanged() => false;
        public override bool TryAdd(string sourceFilePath, string destPath, string contentType) => true;
    }

    public class Entry
    {
        public string? Path { get; set; }
        public string? ContentType { get; set; }
        public string? Md5Hash { get; set; }
    }
}
