using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
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

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("QA Export Log");

            // Header Section
            worksheet.Cell("A1").Value = "BÁO CÁO KỸ THUẬT QUÁ TRÌNH IN & EXPORT (QA TECHNICAL REPORT)";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 13;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.DarkSlateGray;

            worksheet.Cell("A3").Value = "Thời gian xuất:";
            worksheet.Cell("B3").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            worksheet.Cell("A4").Value = "Tổng số item:";
            worksheet.Cell("B4").Value = qaEntries.Count;

            // Columns Header
            int headerRow = 6;
            worksheet.Cell(headerRow, 1).Value = "STT";
            worksheet.Cell(headerRow, 2).Value = "Số Hiệu Sheet";
            worksheet.Cell(headerRow, 3).Value = "Tên Sheet";
            worksheet.Cell(headerRow, 4).Value = "Định Dạng";
            worksheet.Cell(headerRow, 5).Value = "Kết Quả";
            worksheet.Cell(headerRow, 6).Value = "Thời Gian (s)";
            worksheet.Cell(headerRow, 7).Value = "Dung Lượng (KB)";
            worksheet.Cell(headerRow, 8).Value = "Số Lần Retry";
            worksheet.Cell(headerRow, 9).Value = "Đường Dẫn File";
            worksheet.Cell(headerRow, 10).Value = "Ghi Chú / Lỗi";

            var headerRange = worksheet.Range(headerRow, 1, headerRow, 10);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSlateGray;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int r = headerRow + 1;
            int stt = 1;
            foreach (var item in qaEntries)
            {
                worksheet.Cell(r, 1).Value = stt++;
                worksheet.Cell(r, 2).Value = item.SheetNumber;
                worksheet.Cell(r, 3).Value = item.SheetName;
                worksheet.Cell(r, 4).Value = item.Format;

                var statusCell = worksheet.Cell(r, 5);
                if (item.Success)
                {
                    statusCell.Value = "✔ Thành công";
                    statusCell.Style.Font.FontColor = XLColor.DarkGreen;
                }
                else
                {
                    statusCell.Value = "✘ Lỗi";
                    statusCell.Style.Font.FontColor = XLColor.Red;
                }

                worksheet.Cell(r, 6).Value = Math.Round(item.DurationSeconds, 2);
                worksheet.Cell(r, 7).Value = Math.Round((double)item.FileSizeBytes / 1024.0, 1);
                worksheet.Cell(r, 8).Value = item.Retries;
                worksheet.Cell(r, 9).Value = item.OutputFilePath;
                worksheet.Cell(r, 10).Value = item.Message;

                var rowRange = worksheet.Range(r, 1, r, 10);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                r++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);

            return filePath;
        }
    }
}
