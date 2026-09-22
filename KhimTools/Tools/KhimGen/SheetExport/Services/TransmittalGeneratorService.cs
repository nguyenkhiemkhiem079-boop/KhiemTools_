using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class TransmittalGeneratorService
    {
        public static string GenerateExcelTransmittal(string outputFolder, string issueSetName, string projectCode, List<SheetExportItem> sheets)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || sheets == null) return null;
            var options = new ExportJobOptions { OutputDirectory = outputFolder, IssueSetName = issueSetName, ProjectCode = projectCode, IssueDate = DateTime.Today };
            return GenerateExcelTransmittal(options, sheets, Path.Combine(outputFolder, "Transmittal_" + (projectCode ?? "PROJECT") + "_" + options.IssueDate.ToString("yyyyMMdd_HHmm") + ".xlsx"));
        }

        public static string GenerateExcelTransmittal(ExportJobOptions options, ExportBatchResult batch, ExportPathResolver paths)
        {
            string path = paths == null ? null : paths.ResolveTransmittalPath(options.IssueDate);
            if (options == null || batch == null || string.IsNullOrWhiteSpace(path)) return null;
            List<ExportItemResult> successful = batch.Results.Where(item => item.Success && File.Exists(item.FinalPath))
                .OrderBy(item => item.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SheetUniqueId, StringComparer.Ordinal)
                .ThenBy(item => item.Format).ToList();
            if (successful.Count == 0) return null;
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            using (var writer = new SimpleExcelWriter("Drawing Register"))
            {
                writer.AddRow("BẢNG KÊ PHÁT HÀNH BẢN VẼ (DRAWING TRANSMITTAL REGISTER)"); writer.AddEmptyRow();
                writer.AddRow("Dự án / Project:", options.ProjectCode ?? "", "", "Ngày phát hành:", options.IssueDate.ToString("dd/MM/yyyy"));
                writer.AddRow("Đợt phát hành:", options.IssueSetName ?? "", "", "Người thực hiện:", Environment.UserName); writer.AddEmptyRow();
                writer.AddRow("STT", "Số Hiệu Bản Vẽ (Sheet No.)", "Tên Bản Vẽ (Sheet Name)", "Revision", "Ngày Rev", "Khổ Giấy", "Định Dạng", "Trạng Thái", "Tên File Xuất");
                int index = 1;
                foreach (ExportItemResult result in successful)
                    writer.AddRow((index++).ToString(), result.SheetNumber ?? "", "", result.RevisionNumber ?? "", result.RevisionDate ?? "", "Unknown", result.Format.ToString(), result.Status.ToString(), Path.GetFileName(result.FinalPath) ?? "");
                writer.Save(path);
            }
            return path;
        }

        private static string GenerateExcelTransmittal(ExportJobOptions options, List<SheetExportItem> sheets, string path)
        {
            if (options == null || sheets == null || string.IsNullOrWhiteSpace(path)) return null;
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            using (var writer = new SimpleExcelWriter("Drawing Register"))
            {
                writer.AddRow("BẢNG KÊ PHÁT HÀNH BẢN VẼ (DRAWING TRANSMITTAL REGISTER)"); writer.AddEmptyRow();
                writer.AddRow("Dự án / Project:", options.ProjectCode ?? "", "", "Ngày phát hành:", options.IssueDate.ToString("dd/MM/yyyy"));
                writer.AddRow("Đợt phát hành:", options.IssueSetName ?? "", "", "Người thực hiện:", Environment.UserName); writer.AddEmptyRow();
                writer.AddRow("STT", "Số Hiệu Bản Vẽ (Sheet No.)", "Tên Bản Vẽ (Sheet Name)", "Revision", "Ngày Rev", "Khổ Giấy", "Định Dạng", "Trạng Thái", "Tên File Xuất");
                int index = 1;
                foreach (SheetExportItem item in sheets.OrderBy(item => item.SheetNumber, StringComparer.OrdinalIgnoreCase))
                    writer.AddRow((index++).ToString(), item.SheetNumber ?? "", item.SheetName ?? "", item.CurrentRevisionNumber ?? "", item.CurrentRevisionDate ?? "", item.PaperSize ?? "Unknown", "actual successful output", "SUCCESS", item.ComputedFileName ?? "");
                writer.Save(path);
            }
            return path;
        }
    }
}
