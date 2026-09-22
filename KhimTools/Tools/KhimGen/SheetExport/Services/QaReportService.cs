using System;
using System.Collections.Generic;
using System.IO;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class QaReportService
    {
        public static string GenerateQaExcelReport(string outputFolder, string projectCode, List<QaReportEntry> qaEntries)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || qaEntries == null) return null;
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            string filePath = Path.Combine(outputFolder, "QA_Report_" + (projectCode ?? "PROJECT") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
            using (var writer = new SimpleExcelWriter("QA Export Log"))
            {
                writer.AddRow("BÁO CÁO KỸ THUẬT QUÁ TRÌNH IN & EXPORT (QA TECHNICAL REPORT)"); writer.AddEmptyRow();
                writer.AddRow("Thời gian xuất:", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), "", "Tổng số item:", qaEntries.Count.ToString()); writer.AddEmptyRow();
                writer.AddRow("STT", "Số Hiệu Sheet", "Tên Sheet", "Định Dạng", "Kết Quả", "Thời Gian (s)", "Dung Lượng (KB)", "Số Lần Retry", "Đường Dẫn File", "Ghi Chú / Lỗi");
                int index = 1;
                foreach (QaReportEntry item in qaEntries)
                    writer.AddRow((index++).ToString(), item.SheetNumber ?? "", item.SheetName ?? "", item.Format ?? "", item.Status ?? (item.Success ? "SUCCESS" : "FAILED"),
                        Math.Round(item.DurationSeconds, 2).ToString(), Math.Round((double)item.FileSizeBytes / 1024.0, 1).ToString(), item.Retries.ToString(), item.OutputFilePath ?? "", item.Message ?? "");
                writer.Save(filePath);
            }
            return filePath;
        }

        public static string GenerateQaExcelReport(ExportJobOptions options, ExportBatchResult batch, ExportPathResolver paths)
        {
            var entries = ExportRetryQueue.ToQaEntries(batch);
            string folder = options?.OutputDirectory;
            string path = paths == null ? null : paths.ResolveQaReportPath(options.IssueDate);
            return GenerateQaExcelReportAtPath(path, folder, options?.ProjectCode, entries);
        }

        private static string GenerateQaExcelReportAtPath(string requestedPath, string folder, string projectCode, List<QaReportEntry> qaEntries)
        {
            if (string.IsNullOrWhiteSpace(folder) || qaEntries == null) return null;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string filePath = string.IsNullOrWhiteSpace(requestedPath) ? Path.Combine(folder, "QA_Report_" + (projectCode ?? "PROJECT") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx") : requestedPath;
            using (var writer = new SimpleExcelWriter("QA Export Log"))
            {
                writer.AddRow("BÁO CÁO KỸ THUẬT QUÁ TRÌNH IN & EXPORT (QA TECHNICAL REPORT)"); writer.AddEmptyRow();
                writer.AddRow("Thời gian xuất:", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), "", "Tổng số item:", qaEntries.Count.ToString()); writer.AddEmptyRow();
                writer.AddRow("STT", "Số Hiệu Sheet", "Tên Sheet", "Định Dạng", "Kết Quả", "Thời Gian (s)", "Dung Lượng (KB)", "Số Lần Retry", "Đường Dẫn File", "Ghi Chú / Lỗi");
                int index = 1;
                foreach (QaReportEntry item in qaEntries)
                    writer.AddRow((index++).ToString(), item.SheetNumber ?? "", item.SheetName ?? "", item.Format ?? "", item.Status ?? (item.Success ? "SUCCESS" : "FAILED"), Math.Round(item.DurationSeconds, 2).ToString(), Math.Round((double)item.FileSizeBytes / 1024.0, 1).ToString(), item.Retries.ToString(), item.OutputFilePath ?? "", item.Message ?? "");
                writer.Save(filePath);
            }
            return filePath;
        }
    }
}
