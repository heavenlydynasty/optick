using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Office.Interop.Excel;
using Profiler.Data;

namespace Profiler.Archive
{
    class XlsxFileArchive : IArchive
    {
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return true;
        }

        private bool WriteNode(string fileName, NodeArchiveSource source)
        {
            var node = source.Node;
            var excel = new Application();
            //excel.Visible = true;
            excel.DisplayAlerts = false;
            var workbooks = excel.Workbooks.Add(XlWBATemplate.xlWBATWorksheet);
            var worksheet = (Worksheet)workbooks.Worksheets[1];
            worksheet.Name = "NodeEvent-" + node.Duration;

            var column = 1;
            worksheet.Cells[FrameTreeType.Function][column] = "FUNCTION";
            worksheet.Cells[FrameTreeType.SelfDuration][column] = "SELFDURATION(MS)";
            worksheet.Cells[FrameTreeType.SelfPercent][column] = "SELFPERCENT%";
            worksheet.Cells[FrameTreeType.Total][column] = "TOTAL(MS)";
            worksheet.Cells[FrameTreeType.TotalPercent][column] = "TOTALPERCENT%";
            worksheet.Cells[FrameTreeType.Path][column] = "PATH";
            worksheet.Cells[FrameTreeType.Tags][column] = "TAGS";

            if (node is EventNode)
                WriteEventNode(worksheet, ref column, node as EventNode);

            workbooks.SaveCopyAs(fileName);
            workbooks.Close();
            excel.Quit();

            return true;
        }

        private bool WriteFrame(string fileName, FrameArchiveSource source)
        {
            var excel = new Application();
            //excel.Visible = true;
            excel.DisplayAlerts = false;
            var workbooks = excel.Workbooks.Add(XlWBATemplate.xlWBATWorksheet);

            var frames = source.Frames;
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var worksheet = (Worksheet)workbooks.Worksheets[i + 1];
                worksheet.Name = frame.Description;

                if (frame is EventFrame)
                {
                    var eventFrame = frame as EventFrame;
                    if (0 != eventFrame.Root.Children.Count())
                    {
                        var eventNode = eventFrame.Root.Children[0];
                        var column = 1;
                        worksheet.Cells[FrameTreeType.Function][column] = "FUNCTION";
                        worksheet.Cells[FrameTreeType.SelfDuration][column] = "SELFDURATION(MS)";
                        worksheet.Cells[FrameTreeType.SelfPercent][column] = "SELFPERCENT%";
                        worksheet.Cells[FrameTreeType.Total][column] = "TOTAL(MS)";
                        worksheet.Cells[FrameTreeType.TotalPercent][column] = "TOTALPERCENT%";
                        worksheet.Cells[FrameTreeType.Path][column] = "PATH";
                        worksheet.Cells[FrameTreeType.Tags][column] = "TAGS";

                        WriteEventNode(worksheet, ref column, eventNode);
                    }
                }

                if (i +1 != frames.Count)
                    AddSheet(workbooks, "NextFrame");
            }

            workbooks.SaveCopyAs(fileName);
            workbooks.Close();
            excel.Quit();

            return true;
        }

        private bool WriteView(string fileName, ViewArchiveSource source)
        {
            var view = source.View;
            var excel = new Application();
            //excel.Visible = true;
            excel.DisplayAlerts = false;
            var workbooks = excel.Workbooks.Add(XlWBATemplate.xlWBATWorksheet);
            var worksheet = (Worksheet)workbooks.Worksheets[1];
            worksheet.Name = "Frame";
            var column = 1;

            worksheet.Cells[FrameTableType.Function][column] = "FUNCTION";
            worksheet.Cells[FrameTableType.SelfDuration][column] = "SELFDURATION(MS)";
            worksheet.Cells[FrameTableType.SelfPercent][column] = "SELFPERCENT%";
            worksheet.Cells[FrameTableType.Total][column] = "TOTAL(MS)";
            worksheet.Cells[FrameTableType.Max][column] = "MAX(MS)";
            worksheet.Cells[FrameTableType.Count][column] = "COUNT";
            worksheet.Cells[FrameTableType.Path][column] = "PATH";

            foreach (var item in view)
            {
                if (item is EventBoardItem)
                {
                    ++column;
                    var eventItem = item as EventBoardItem;
                    worksheet.Cells[FrameTableType.Function][column] = eventItem.Function;
                    worksheet.Cells[FrameTableType.SelfDuration][column] = eventItem.SelfTime;
                    worksheet.Cells[FrameTableType.SelfPercent][column] = eventItem.SelfPercent;
                    worksheet.Cells[FrameTableType.Total][column] = eventItem.Total;
                    worksheet.Cells[FrameTableType.Max][column] = eventItem.MaxTime;
                    worksheet.Cells[FrameTableType.Count][column] = eventItem.Count;
                    worksheet.Cells[FrameTableType.Path][column] = eventItem.Path;
                }
            }

            workbooks.SaveCopyAs(fileName);
            workbooks.Close();
            excel.Quit();

            return true;
        }

        private Worksheet AddSheet(Workbook workbook, string sheetName)
        {
            var currentSheet = (Worksheet)workbook.Sheets.get_Item(workbook.Sheets.Count);
            var sheet = (Worksheet)workbook.Sheets.Add(Missing.Value, currentSheet, Missing.Value, Missing.Value);
            sheet.Name = sheetName;
            //sheet.Activate();
            return sheet;
        }

        private void WriteEventNode(
            Worksheet worksheet,
            ref int column,
            BaseTreeNode treeNode)
        {
            if (treeNode is EventNode)
            {
                var eventNode = treeNode as EventNode;
                ++column;
                worksheet.Cells[FrameTreeType.Function][column] = eventNode.Name;
                worksheet.Cells[FrameTreeType.SelfDuration][column] = eventNode.SelfDuration;
                worksheet.Cells[FrameTreeType.SelfPercent][column] = eventNode.SelfPercent;
                worksheet.Cells[FrameTreeType.Total][column] = eventNode.Duration;
                worksheet.Cells[FrameTreeType.TotalPercent][column] = eventNode.TotalPercent;
                worksheet.Cells[FrameTreeType.Path][column] = eventNode.Path;

                if (null != treeNode.Tags)
                {
                    var tags = treeNode.Tags;
                    var tagBuilder = new StringBuilder();
                    foreach (var tag in tags)
                    {
                        tagBuilder.Append(tag.Name);
                        tagBuilder.Append(tag.FormattedValue);
                        tagBuilder.Append(tagSpliter_);
                    }

                    if (0 != tagBuilder.Length)
                    {
                        worksheet.Cells[FrameTreeType.Tags][column] = tagBuilder.ToString().TrimEnd(tagSpliter_[0]);
                    }
                }

                var children = treeNode.Children;
                foreach (var child in children)
                {
                    WriteEventNode(worksheet, ref column, child);
                }
            }
        }
    }
}