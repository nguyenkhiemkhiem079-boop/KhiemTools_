using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class TransmittalGeneratorService
    {
        public static string GenerateExcelTransmittal(string outputFolder, string issueSetName, string projectCode, List<SheetExportItem> sheets)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || sheets == null) return null;
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string fileName = $"Transmittal_{projectCode}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            string filePath = Path.Combine(outputFolder, fileName);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Drawing Register");

            // Header Section
            worksheet.Cell("A1").Value = "BẢNG KÊ PHÁT HÀNH BẢN VẼ (DRAWING TRANSMITTAL REGISTER)";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.Navy;

            worksheet.Cell("A3").Value = "Dự án / Project:";
            worksheet.Cell("B3").Value = projectCode;
            worksheet.Cell("B3").Style.Font.Bold = true;

            worksheet.Cell("A4").Value = "Đợt phát hành:";
            worksheet.Cell("B4").Value = issueSetName;
            worksheet.Cell("B4").Style.Font.Bold = true;

            worksheet.Cell("D3").Value = "Ngày phát hành:";
            worksheet.Cell("E3").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            worksheet.Cell("D4").Value = "Người thực hiện:";
            worksheet.Cell("E4").Value = Environment.UserName;

            // Table Columns Header
            int headerRow = 6;
            worksheet.Cell(headerRow, 1).Value = "STT";
            worksheet.Cell(headerRow, 2).Value = "Số Hiệu Bản Vẽ (Sheet No.)";
            worksheet.Cell(headerRow, 3).Value = "Tên Bản Vẽ (Sheet Name)";
            worksheet.Cell(headerRow, 4).Value = "Revision";
            worksheet.Cell(headerRow, 5).Value = "Ngày Rev";
            worksheet.Cell(headerRow, 6).Value = "Khổ Giấy";
            worksheet.Cell(headerRow, 7).Value = "Định Dạng";
            worksheet.Cell(headerRow, 8).Value = "Trạng Thái";
            worksheet.Cell(headerRow, 9).Value = "Tên File Xuất";

            var headerRange = worksheet.Range(headerRow, 1, headerRow, 9);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Rows
            int r = headerRow + 1;
            int stt = 1;
            foreach (var item in sheets)
            {
                worksheet.Cell(r, 1).Value = stt++;
                worksheet.Cell(r, 2).Value = item.SheetNumber;
                worksheet.Cell(r, 3).Value = item.SheetName;
                worksheet.Cell(r, 4).Value = item.CurrentRevisionNumber;
                worksheet.Cell(r, 5).Value = item.CurrentRevisionDate;
                worksheet.Cell(r, 6).Value = item.PaperSize;
                worksheet.Cell(r, 7).Value = "PDF / DWG";
                worksheet.Cell(r, 8).Value = item.StatusBadgeText;
                worksheet.Cell(r, 9).Value = item.ComputedFileName;

                var rowRange = worksheet.Range(r, 1, r, 9);
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
