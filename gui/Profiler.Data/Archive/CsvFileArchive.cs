using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Profiler.Data;

namespace Profiler.Archive
{
    public class CsvFileArchive : IArchive
    {
        private string spliter_ = @",";
        private string tagSpliter_ = @"|";

        public bool Open(ref ArchiveOption option)
        {
            throw new NotImplementedException();
        }

        public bool Save(ref ArchiveOption option)
        {
            var result = false;
            switch (option.ArchiveType)
            {
                case ArchiveSourceType.Node:
                    result = WriteNode(option.FileName, option.Sources.First() as NodeArchiveSource);
                    break;
                case ArchiveSourceType.Frame:
                    result = WriteFrame(option.FileName, option.Sources.First() as FrameArchiveSource);
                    break;
                case ArchiveSourceType.Group:
                    break;
                case ArchiveSourceType.View:
                    result = WriteView(option.FileName, option.Sources.First() as ViewArchiveSource);
                    break;
                case ArchiveSourceType.Tag:
                    return WriteFrameTag(option.FileName, option.Sources);
            }
            return true;
        }

        private bool WriteNode(string fileName, NodeArchiveSource source)
        {
            var node = source.Node;
            var writer = new StreamWriter(fileName, false, Encoding.UTF8);

            writer.WriteLine(@"FUNCTION,SELFDURATION(MS),SELFPERCENT,TOTAL(MS),TOTALPERCENT%,PATH,TAGS");

            if (node is EventNode)
                WriteEventNode(writer, node as EventNode);
            writer.Close();

            return true;
        }

        private bool WriteFrame(string fileName, FrameArchiveSource source)
        {
            var frames = source.Frames;
            var writer = new StreamWriter(fileName, false, Encoding.UTF8);
            foreach (var frame in frames)
            {
                if (frame is EventFrame)
                {
                    var eventFrame = frame as EventFrame;
                    if (0 != eventFrame.Root.Children.Count())
                    {
                        var eventNode = eventFrame.Root.Children[0];
                        writer.WriteLine(@"FUNCTION,SELFDURATION(MS),SELFPERCENT,TOTAL(MS),TOTALPERCENT%,PATH,TAGS");
                        WriteEventNode(writer, eventNode);
                    }
                }
            }

            return true;
        }

        private bool WriteView(string fileName, ViewArchiveSource source)
        {
            var view = source.View;
            var writer = new StreamWriter(fileName, false, Encoding.UTF8);
            writer.WriteLine("FUNCTION,SELFDURATION,SELFPERCENT%,TOTAL(MS),MAX(MS),COUNT,PATH");

            foreach (var item in view)
            {
                if (item is EventBoardItem)
                {
                    var board = item as EventBoardItem;
                    var builder = new StringBuilder();
                    builder.Append(board.Function);
                    builder.Append(spliter_);
                    builder.Append(board.SelfTime);
                    builder.Append(spliter_);
                    builder.Append(board.SelfPercent);
                    builder.Append(spliter_);
                    builder.Append(board.Total);
                    builder.Append(spliter_);
                    builder.Append(board.MaxTime);
                    builder.Append(spliter_);
                    builder.Append(board.Count);
                    builder.Append(spliter_);
                    builder.Append(board.Path);
                    writer.WriteLine(builder.ToString());
                }
            }

            return true;
        }
        private void WriteEventNode(StreamWriter writer, BaseTreeNode treeNode)
        {
            if (treeNode is EventNode)
            {
                var eventNode = treeNode as EventNode;
                var builder = new StringBuilder();
                builder.Append(eventNode.Name);
                builder.Append(spliter_);
                builder.Append(eventNode.SelfDuration);
                builder.Append(spliter_);
                builder.Append(eventNode.SelfPercent);
                builder.Append(spliter_);
                builder.Append(eventNode.Duration);
                builder.Append(spliter_);
                builder.Append(eventNode.TotalPercent);
                builder.Append(spliter_);
                builder.Append(eventNode.Path);
                builder.Append(spliter_);

                if (null != treeNode.Tags)
                {
                    var tags = treeNode.Tags;
                    foreach (var tag in tags)
                    {
                        //builder.Append(tag.Name);
                        builder.Append(tag.FormattedValue);
                        builder.Append(tagSpliter_);
                    }
                }

                writer.WriteLine(builder.ToString().TrimEnd(tagSpliter_[0]));

                var children = treeNode.Children;
                foreach (var child in children)
                {
                    WriteEventNode(writer, child);
                }
            }
        }

        private void WriteEventTag(StreamWriter writer, EventNode sourceNode, EventNode currentNode)
        {
            foreach (var child in currentNode.Children)
            {
                if (sourceNode.Name == child.Name && null != child.Tags && 0 != child.Tags.Count)
                {
                    var builder = new StringBuilder();
                    var tags = child.Tags;
                    foreach (var tag in tags)
                    {
                        builder.Append(tag.FormattedValue);
                        builder.Append(spliter_);
                    }

                    writer.WriteLine(builder.ToString());
                }
                WriteEventTag(writer, sourceNode, child as EventNode);
            }
        }

        private bool WriteFrameTag(string fileName, List<IArchiveSource> Sources)
        {
            // TODO
            const int tagSourceCount = 2;
            if (tagSourceCount != Sources.Count)
                return false;

            var eventNode = (Sources[0] as NodeArchiveSource).Node as EventNode;
            var frames = (Sources[1] as FrameArchiveSource).Frames;

            if (null == eventNode && null == frames)
                return false;

            if (null != eventNode && null != eventNode.Tags)
            {
                var writer = new StreamWriter(fileName, false, Encoding.UTF8);

                var tagHeaderBuilder = new StringBuilder();
                foreach (var tag in eventNode.Tags)
                {
                    tagHeaderBuilder.Append(tag.Name);
                    tagHeaderBuilder.Append(spliter_);
                }
                writer.WriteLine(tagHeaderBuilder.ToString());

                foreach (var frame in frames)
                {
                    var eventFrame = frame as EventFrame;
                    var root = eventFrame.Root;
                    var children = root.Children;
                    foreach (var child in children)
                    {
                        if (eventNode.Name == child.Name && null != child.Tags && 0 != child.Tags.Count)
                        {
                            var builder = new StringBuilder();
                            var tags = child.Tags;
                            foreach (var tag in tags)
                            {
                                builder.Append(tag.FormattedValue);
                                builder.Append(spliter_);
                            }
                            writer.WriteLine(builder.ToString());
                        }

                        WriteEventTag(writer, eventNode, child as EventNode);
                    }
                }
                writer.Close();
            }

            return true;
        }
    }
}
