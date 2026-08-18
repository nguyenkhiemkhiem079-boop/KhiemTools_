using System;
using System.Collections.Generic;
using System.IO;
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

            using (var writer = new SimpleExcelWriter("Drawing Register"))
            {
                // Header Section
                writer.AddRow("BẢNG KÊ PHÁT HÀNH BẢN VẼ (DRAWING TRANSMITTAL REGISTER)");
                writer.AddEmptyRow();
                writer.AddRow("Dự án / Project:", projectCode ?? "", "", "Ngày phát hành:", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                writer.AddRow("Đợt phát hành:", issueSetName ?? "", "", "Người thực hiện:", Environment.UserName);
                writer.AddEmptyRow();

                // Table Header
                writer.AddRow(
                    "STT",
                    "Số Hiệu Bản Vẽ (Sheet No.)",
                    "Tên Bản Vẽ (Sheet Name)",
                    "Revision",
                    "Ngày Rev",
                    "Khổ Giấy",
                    "Định Dạng",
                    "Trạng Thái",
                    "Tên File Xuất"
                );

                // Table Rows
                int stt = 1;
                foreach (var item in sheets)
                {
                    writer.AddRow(
                        (stt++).ToString(),
                        item.SheetNumber ?? "",
                        item.SheetName ?? "",
                        item.CurrentRevisionNumber ?? "",
                        item.CurrentRevisionDate ?? "",
                        item.PaperSize ?? "",
                        "PDF / DWG",
                        item.StatusBadgeText ?? "",
                        item.ComputedFileName ?? ""
                    );
                }

                writer.Save(filePath);
            }

            return filePath;
        }
    }
}
