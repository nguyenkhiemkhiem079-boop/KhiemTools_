using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    /// <summary>
    /// Engine xuất PDF tận dụng trực tiếp bộ xuất PDF native của Revit (Revit 2022+),
    /// tự động nhận diện khổ giấy từng Sheet, đảm bảo chất lượng Vector DPI cao nhất
    /// và định danh tên file chính xác 100%.
    /// </summary>
    public static class PdfExportEngine
    {
        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt, ColorDepthType colorDepth = ColorDepthType.Color)
        {
            if (doc == null || sheet == null) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string targetPath = Path.Combine(outputFolder, fileNameWithoutExt + ".pdf");

            var pdfOpt = CreateStandardPdfOptions(fileNameWithoutExt, false, colorDepth);
            var viewIds = new List<ElementId> { sheet.Id };

            var beforeFiles = new HashSet<string>(Directory.GetFiles(outputFolder, "*.pdf"), StringComparer.OrdinalIgnoreCase);

            try
            {
                doc.Export(outputFolder, viewIds, pdfOpt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi Revit Export PDF cho sheet [{sheet.SheetNumber}]: {ex.Message}", ex);
            }

            if (File.Exists(targetPath)) return targetPath;

            // Tìm file PDF mới sinh ra trong thư mục output
            var afterFiles = Directory.GetFiles(outputFolder, "*.pdf");
            var newFiles = afterFiles.Where(f => !beforeFiles.Contains(f)).ToList();
            if (newFiles.Any())
            {
                string exportedFile = newFiles.First();
                if (!string.Equals(exportedFile, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(exportedFile, targetPath);
                }
                return targetPath;
            }

            // Fallback: Tìm file PDF có thời gian ghi gần nhất (trong vòng 30s)
            var recentFiles = afterFiles
                .Where(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalSeconds < 30)
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (recentFiles.Any())
            {
                string recentFile = recentFiles.First();
                if (!string.Equals(recentFile, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(recentFile, targetPath);
                }
                return targetPath;
            }

            return targetPath;
        }

        public static string ExportCombinedSheets(Document doc, List<ViewSheet> sheets, string outputFolder, string combinedFileNameWithoutExt, ColorDepthType colorDepth = ColorDepthType.Color)
        {
            if (doc == null || sheets == null || !sheets.Any()) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string targetPath = Path.Combine(outputFolder, combinedFileNameWithoutExt + ".pdf");

            var pdfOpt = CreateStandardPdfOptions(combinedFileNameWithoutExt, true, colorDepth);
            var viewIds = sheets.Select(s => s.Id).ToList();

            var beforeFiles = new HashSet<string>(Directory.GetFiles(outputFolder, "*.pdf"), StringComparer.OrdinalIgnoreCase);

            try
            {
                doc.Export(outputFolder, viewIds, pdfOpt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi Revit Export PDF Gộp: {ex.Message}", ex);
            }

            if (File.Exists(targetPath)) return targetPath;

            var afterFiles = Directory.GetFiles(outputFolder, "*.pdf");
            var newFiles = afterFiles.Where(f => !beforeFiles.Contains(f)).ToList();
            if (newFiles.Any())
            {
                string exportedFile = newFiles.First();
                if (!string.Equals(exportedFile, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(exportedFile, targetPath);
                }
                return targetPath;
            }

            var recentFiles = afterFiles
                .Where(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalSeconds < 30)
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (recentFiles.Any())
            {
                string recentFile = recentFiles.First();
                if (!string.Equals(recentFile, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(recentFile, targetPath);
                }
                return targetPath;
            }

            return targetPath;
        }

        private static PDFExportOptions CreateStandardPdfOptions(string fileName, bool combine, ColorDepthType colorDepth)
        {
            return new PDFExportOptions
            {
                FileName = fileName,
                Combine = combine,
                ColorDepth = colorDepth,
                ExportQuality = PDFExportQualityType.DPI300,
                RasterQuality = RasterQualityType.High,
                PaperFormat = ExportPaperFormat.Default,
                HideUnreferencedViewTags = true,
                HideScopeBoxes = true,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                MaskCoincidentLines = true,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
                StopOnError = false
            };
        }
    }
}
