using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Globot
{
    public class GlobotOptions
    {
        public GlobotOptions()
        {
            IncludedFileExtensions = new string[0];
        }

        public string? SourcePath { get; set; }
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }
        public string?[] IncludedFileExtensions { get; set; }
        public string? ManifestPath { get; set; }
        public string? Prefix { get; set; }
        public bool ForceLowerCase { get; set; }
    }
}
