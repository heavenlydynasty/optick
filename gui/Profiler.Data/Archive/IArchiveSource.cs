namespace Profiler.Archive
{
    public enum FrameTreeType
    {
        None,
        Function,
        SelfDuration,
        SelfPercent,
        Total,
        TotalPercent,
        Path,
        Tags,
        MaxType
    }

    public enum FrameTableType
    {
        None,
        Function,
        SelfDuration,
        SelfPercent,
        Total,
        Max,
        Count,
        Path,
        MaxType
    }

    public enum ArchiveSourceType
    {
        Node,
        Frame,
        Group,
        View,
        Tag
    }

    public interface IArchiveSource
    {
        ArchiveSourceType SourceType { get; } 
    }
}
