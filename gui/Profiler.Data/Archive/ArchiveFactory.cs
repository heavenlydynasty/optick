using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Profiler.Data;

namespace Profiler.Archive
{
    public class ArchiveFactory
    {
        class ArchiveCreator
        {
            public string Extension { get; }
            public string Description { get; }

            public ArchiveCreator(string extension, string description, Type type)
            {
                Extension = extension;
                Description = description;
                ArchiveFactory.Instance().Register(extension, type);
            }

            ~ArchiveCreator()
            {
                ArchiveFactory.Instance().Unregister(Extension);
            }
        }

        private Dictionary<string, Type> archives_ = new Dictionary<string, Type>();
        private List<ArchiveCreator> creators_ = new List<ArchiveCreator>();
        private static ArchiveFactory factory_;
        public string Extentions
        {
            get
            {
                var builder = new StringBuilder();
                foreach (var creator in creators_)
                {
                    builder.Append(creator.Description);
                    builder.Append(@"|");
                }

                return builder.ToString().TrimEnd('|');
            }
        }

        public static ArchiveFactory Instance()
        {
            if (null == factory_)
                factory_ = new ArchiveFactory();
            return factory_;
        }

        public void Initialize()
        {
            creators_.Add(new ArchiveCreator(@".opt", "Optick Performance Capture (*.opt)|*.opt", Type.GetType(@"Profiler.Archive.OptFileArchive")));
            creators_.Add(new ArchiveCreator(@".xlsx", "Excel Worksheets (*.xlsx)|*.xlsx", Type.GetType(@"Profiler.Archive.XlsxFileArchive")));
            creators_.Add(new ArchiveCreator(@".csv", "Comma-Separated Values (*.csv)|*.csv", Type.GetType(@"Profiler.Archive.CsvFileArchive")));
        }

        public void Register(string ext, Type type)
        {
            archives_[ext] = type;
        }

        public void Unregister(string ext)
        {
            archives_.Remove(ext);
        }

        private IArchive CreateArchive(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return archives_.ContainsKey(ext) ? (IArchive)Activator.CreateInstance(archives_[ext]) : null;
        }

        private bool Open(ref ArchiveOption option)
        {
            if (null == option.FileName)
            {
                var dlg = new OpenFileDialog { Filter = Extentions, Title = "Where should I open profiler results?" };
                if (dlg.ShowDialog() == true)
                {
                    option.FileName = dlg.FileName;
                }
            }

            if (option.FileName != null)
            {
                var archive = CreateArchive(option.FileName);
                if (archive != null)
                    return archive.Open(ref option);
            }

            return false;
        }

        private bool Save(ref ArchiveOption option)
        {
            var dlg = new SaveFileDialog { Filter = Extentions, Title = "Where should I save profiler results?" };

            if (dlg.ShowDialog() == true)
            {
                var archive = CreateArchive(dlg.FileName);
                option.FileName = dlg.FileName;
                if (archive != null)
                    return archive.Save(ref option);
            }

            return false;
        }

        public bool Archive(ref ArchiveOption option)
        {
            var result = false;
            switch (option.Mode)
            {
                case ArchiveMode.Open:
                    result = Open(ref option);
                    break;
                case ArchiveMode.Save:
                    result = Save(ref option);
                    if (result)
                        MessageBox.Show(option.FileName, @"Well Done!");
                    break;
            }

            return result;
        }
    }
}
