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

            string fileName = $"QA_Report_{projectCode}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            string filePath = Path.Combine(outputFolder, fileName);

            using (var writer = new SimpleExcelWriter("QA Export Log"))
            {
                // Header Section
                writer.AddRow("BÁO CÁO KỸ THUẬT QUÁ TRÌNH IN & EXPORT (QA TECHNICAL REPORT)");
                writer.AddEmptyRow();
                writer.AddRow("Thời gian xuất:", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), "", "Tổng số item:", qaEntries.Count.ToString());
                writer.AddEmptyRow();

                // Columns Header
                writer.AddRow(
                    "STT",
                    "Số Hiệu Sheet",
                    "Tên Sheet",
                    "Định Dạng",
                    "Kết Quả",
                    "Thời Gian (s)",
                    "Dung Lượng (KB)",
                    "Số Lần Retry",
                    "Đường Dẫn File",
                    "Ghi Chú / Lỗi"
                );

                // Table Rows
                int stt = 1;
                foreach (var item in qaEntries)
                {
                    writer.AddRow(
                        (stt++).ToString(),
                        item.SheetNumber ?? "",
                        item.SheetName ?? "",
                        item.Format ?? "",
                        item.Success ? "✔ Thành công" : "✘ Lỗi",
                        Math.Round(item.DurationSeconds, 2).ToString(),
                        Math.Round((double)item.FileSizeBytes / 1024.0, 1).ToString(),
                        item.Retries.ToString(),
                        item.OutputFilePath ?? "",
                        item.Message ?? ""
                    );
                }

                writer.Save(filePath);
            }

            return filePath;
        }
    }
}
