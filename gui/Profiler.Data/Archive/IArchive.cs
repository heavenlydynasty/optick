using System.ComponentModel;
using System.IO;
using Profiler.Data;

namespace Profiler.Archive
{
    public interface IArchive
    {
        bool Open(ref ArchiveOption option);
        bool Save(ref ArchiveOption option);
    }
}
