using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Profiler.Archive
{
    public enum ArchiveMode
    {
        Open,
        Save
    }

    public enum ArchivePipeline
    {
        Internel,
        Standalone
    }

    public class  ArchiveOption
    {
        public string FileName { get; set; }
        public ArchiveMode Mode { get; set; }
        public ArchivePipeline Pipeline { get; set; }
        public List<IArchiveSource> Sources { get; set; }
        public ArchiveSourceType ArchiveType { get; set; }

        public Stream InternelStream { get; set; }
    }
}
