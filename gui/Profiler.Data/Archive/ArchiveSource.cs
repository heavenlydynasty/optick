using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Profiler.Data;

namespace Profiler.Archive
{
    public class NodeArchiveSource : IArchiveSource
    {
        public ArchiveSourceType SourceType
        {
            get
            {
                return ArchiveSourceType.Node;
            }
        }

        public BaseTreeNode Node { get; }

        public NodeArchiveSource(BaseTreeNode node)
        {
            Node = node;
        }
    }

    public class FrameArchiveSource : IArchiveSource
    {
        public ArchiveSourceType SourceType
        {
            get
            {
                return ArchiveSourceType.Frame;
            }
        }

        public FrameCollection Frames { get; }

        public FrameArchiveSource(FrameCollection frames)
        {
            Frames = frames;
        }
    }
    public class GroupArchiveSource : IArchiveSource
    {
        public ArchiveSourceType SourceType
        {
            get
            {
                return ArchiveSourceType.Group;
            }
        }

        public GroupArchiveSource(FrameGroup group)
        {

        }
    }

    public class ViewArchiveSource : IArchiveSource
    {
        public ArchiveSourceType SourceType
        {
            get
            {
                return ArchiveSourceType.View;
            }
        }

        public ICollectionView View { get; }

        public ViewArchiveSource(ICollectionView view)
        {
            View = view;
        }
    }
}
