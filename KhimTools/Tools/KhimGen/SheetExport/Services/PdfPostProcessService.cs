using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class PdfPostProcessService
    {
        // ── 2.1 Add Bookmarks / Outlines ─────────────────────────────────────
        public static bool AddBookmarks(string pdfFilePath, List<SheetExportItem> orderedSheets)
        {
            if (!File.Exists(pdfFilePath) || orderedSheets == null || !orderedSheets.Any()) return false;

            try
            {
                using var doc = PdfReader.Open(pdfFilePath, PdfDocumentOpenMode.Modify);
                doc.Outlines.Clear();

                int pageCount = doc.PageCount;
                for (int i = 0; i < Math.Min(pageCount, orderedSheets.Count); i++)
                {
                    var sheet = orderedSheets[i];
                    var page = doc.Pages[i];
                    string bookmarkTitle = $"{sheet.SheetNumber} - {sheet.SheetName}";
                    doc.Outlines.Add(bookmarkTitle, page);
                }

                doc.Save(pdfFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── 2.2 Watermark / Status Stamp ─────────────────────────────────────
        public static bool ApplyWatermark(string pdfFilePath, string watermarkText)
        {
            if (!File.Exists(pdfFilePath) || string.IsNullOrWhiteSpace(watermarkText)) return false;

            try
            {
                using var doc = PdfReader.Open(pdfFilePath, PdfDocumentOpenMode.Modify);
                var font = new XFont("Arial", 48, XFontStyleEx.Bold);
                var brush = new XSolidBrush(XColor.FromArgb(50, 220, 50, 50)); // Semi-transparent Red

                foreach (PdfPage page in doc.Pages)
                {
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    var size = gfx.PageSize;

                    double cx = size.Width / 2;
                    double cy = size.Height / 2;

                    var state = gfx.Save();
                    gfx.RotateAtTransform(-35, new XPoint(cx, cy));

                    var textSize = gfx.MeasureString(watermarkText, font);
                    gfx.DrawString(watermarkText, font, brush,
                        new XPoint(cx - textSize.Width / 2, cy + textSize.Height / 4));

                    gfx.Restore(state);
                }

                doc.Save(pdfFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── 2.3 Auto Cover Sheet (Mục lục bản vẽ) ─────────────────────────────
        public static bool InsertCoverSheet(string pdfFilePath, string issueSetName, List<SheetExportItem> sheets)
        {
            if (!File.Exists(pdfFilePath) || sheets == null || !sheets.Any()) return false;

            try
            {
                using var doc = PdfReader.Open(pdfFilePath, PdfDocumentOpenMode.Modify);

                // Insert a new blank A4/A3 page at index 0
                var coverPage = doc.InsertPage(0);
                coverPage.Size = PdfSharp.PageSize.A4;
                coverPage.Orientation = PdfSharp.PageOrientation.Portrait;

                using var gfx = XGraphics.FromPdfPage(coverPage);
                var fontTitle = new XFont("Arial", 18, XFontStyleEx.Bold);
                var fontSubtitle = new XFont("Arial", 12, XFontStyleEx.Bold);
                var fontBody = new XFont("Arial", 9, XFontStyleEx.Regular);
                var fontHeader = new XFont("Arial", 9, XFontStyleEx.Bold);

                double margin = 40;
                double y = 50;

                // Header / Title
                gfx.DrawString("BỘ HỒ SƠ BẢN VẼ PHÁT HÀNH", fontTitle, XBrushes.Navy, new XPoint(margin, y));
                y += 25;
                gfx.DrawString($"Đợt phát hành: {issueSetName}", fontSubtitle, XBrushes.DarkSlateGray, new XPoint(margin, y));
                y += 18;
                gfx.DrawString($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Tổng số bản vẽ: {sheets.Count}", fontBody, XBrushes.Gray, new XPoint(margin, y));
                y += 30;

                // Table Header
                gfx.DrawRectangle(XPens.DarkGray, XBrushes.LightGray, margin, y, 515, 20);
                gfx.DrawString("STT", fontHeader, XBrushes.Black, new XPoint(margin + 5, y + 14));
                gfx.DrawString("Số Hiệu Bản Vẽ", fontHeader, XBrushes.Black, new XPoint(margin + 40, y + 14));
                gfx.DrawString("Tên Bản Vẽ", fontHeader, XBrushes.Black, new XPoint(margin + 160, y + 14));
                gfx.DrawString("Rev", fontHeader, XBrushes.Black, new XPoint(margin + 420, y + 14));
                gfx.DrawString("Trang", fontHeader, XBrushes.Black, new XPoint(margin + 470, y + 14));
                y += 20;

                // Rows
                int stt = 1;
                foreach (var s in sheets)
                {
                    if (y > 780) break; // A4 page limit

                    var penRow = XPens.LightGray;
                    gfx.DrawLine(penRow, margin, y + 15, margin + 515, y + 15);

                    gfx.DrawString(stt.ToString(), fontBody, XBrushes.Black, new XPoint(margin + 5, y + 11));
                    gfx.DrawString(s.SheetNumber ?? "", fontBody, XBrushes.Black, new XPoint(margin + 40, y + 11));

                    string sheetNameTrunc = s.SheetName ?? "";
                    if (sheetNameTrunc.Length > 45) sheetNameTrunc = sheetNameTrunc.Substring(0, 42) + "...";
                    gfx.DrawString(sheetNameTrunc, fontBody, XBrushes.Black, new XPoint(margin + 160, y + 11));

                    gfx.DrawString(s.CurrentRevisionNumber ?? "0", fontBody, XBrushes.Black, new XPoint(margin + 420, y + 11));
                    gfx.DrawString((stt + 1).ToString(), fontBody, XBrushes.Black, new XPoint(margin + 470, y + 11));

                    y += 18;
                    stt++;
                }

                doc.Save(pdfFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
