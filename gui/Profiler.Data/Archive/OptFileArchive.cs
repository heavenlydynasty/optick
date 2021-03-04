using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using Profiler.Data;

namespace Profiler.Archive
{
    public class OptFileArchive : IArchive
    {
        public bool Open(ref ArchiveOption option)
        {
            if (File.Exists(option.FileName))
            {
                FileStream stream = new FileStream(option.FileName, FileMode.Open);
                Capture.OptickHeader header = new Capture.OptickHeader(stream);
                if (header.IsValid)
                {
                    option.InternelStream =  (header.IsZip ? (Stream)new GZipStream(stream, CompressionMode.Decompress, false) : stream);
                    return true;
                }

                stream.Close();
            }

            return false;
        }

        public bool Save(ref ArchiveOption option)
        {
            var result = false;
            switch (option.ArchiveType)
            {
                case ArchiveSourceType.Node:
                    break;
                case ArchiveSourceType.Frame:
                    result = WriteFrame(option.FileName, option.Sources[0]as FrameArchiveSource);
                    break;
                case ArchiveSourceType.Group:
                    break;
                case ArchiveSourceType.View:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return result;
        }

        public bool WriteFrame(string fileName, FrameArchiveSource source)
        {
            var fileStream = new FileStream(fileName, FileMode.Create);
            Stream zipStream = null;   
            Stream stream = fileStream;
            Capture.OptickHeader header = new Capture.OptickHeader();
            header.Write(fileStream);
            if (header.IsZip)
            {
                zipStream = new GZipStream(fileStream, CompressionLevel.Fastest);
                stream = zipStream;
            }

            FrameGroup currentGroup = null;
            foreach (Frame frame in source.Frames)
            {
                if (frame is EventFrame)
                {
                    EventFrame eventFrame = frame as EventFrame;
                    if (eventFrame.Group != currentGroup && currentGroup != null)
                    {
                        currentGroup.Responses.ForEach(response => response.Serialize(stream));
                    }
                    currentGroup = eventFrame.Group;
                }
                else if (frame is SamplingFrame)
                {
                    if (currentGroup != null)
                    {
                        currentGroup.Responses.ForEach(response => response.Serialize(stream));
                        currentGroup = null;
                    }

                    var sampleingFrame = frame as SamplingFrame;
                    sampleingFrame.Response.Serialize(stream);
                }
            }

            currentGroup?.Responses.ForEach(response => response.Serialize(stream));

            stream.Close();

                return true;
        }
    }
}
