using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public class FileLockedException : IOException
    {
        public string LockedFilePath { get; private set; }
        public FileLockedException(string filePath, string message) : base(message) { LockedFilePath = filePath; }
    }

    public class ExportOutputMissingException : IOException { public ExportOutputMissingException(string message) : base(message) { } }
    public class AmbiguousExportOutputException : IOException { public AmbiguousExportOutputException(string message) : base(message) { } }

    public static class PdfExportEngine
    {
        public static bool IsFileLocked(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            try { using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) return false; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] lock probe failed: " + ex); return false; }
        }

        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt, ExportOptions options = null)
        {
            if (doc == null || sheet == null) throw new ArgumentNullException("doc");
            string baseName = Sanitize(string.IsNullOrWhiteSpace(fileNameWithoutExt) ? sheet.SheetNumber + " - " + sheet.Name : fileNameWithoutExt);
            string expected = Path.Combine(outputFolder, baseName + ".pdf");
            PrepareFolder(outputFolder, expected);
            Export(doc, new List<ElementId> { sheet.Id }, outputFolder, baseName, options, false, sheet.SheetNumber);
            return VerifyOutput(outputFolder, expected, "PDF");
        }

        public static string ExportCombinedSheets(Document doc, List<ViewSheet> sheets, string outputFolder, string combinedFileNameWithoutExt, ExportOptions options = null)
        {
            if (doc == null || sheets == null || !sheets.Any()) throw new ArgumentNullException("sheets");
            string baseName = Sanitize(string.IsNullOrWhiteSpace(combinedFileNameWithoutExt) ? "Combined_Sheets" : combinedFileNameWithoutExt);
            string expected = Path.Combine(outputFolder, baseName + ".pdf");
            PrepareFolder(outputFolder, expected);
            Export(doc, sheets.Select(sheet => sheet.Id).ToList(), outputFolder, baseName, options, true, "combined");
            return VerifyOutput(outputFolder, expected, "combined PDF");
        }

        private static void Export(Document doc, IList<ElementId> viewIds, string folder, string baseName, ExportOptions options, bool combine, string subject)
        {
            PDFExportOptions pdfOpt = CreateStandardPdfOptions(baseName, combine, options);
            try { doc.Export(folder, viewIds, pdfOpt); }
            catch (Exception ex)
            {
                string expected = Path.Combine(folder, baseName + ".pdf");
                if (IsFileLocked(expected) || ex.Message.IndexOf("being used", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("close the following", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new FileLockedException(expected, "PDF output is locked: " + ex.Message);
                throw new InvalidOperationException("Lỗi Revit Export PDF (" + subject + "): " + ex.Message, ex);
            }
            string expectedPath = Path.Combine(folder, baseName + ".pdf");
            if (File.Exists(expectedPath)) return;
            string[] candidates = Directory.GetFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly);
            if (candidates.Length == 0) throw new ExportOutputMissingException("OUTPUT_MISSING: PDF export produced no file.");
            // A non-matching file is never accepted as the primary output, even
            // when it is the only file. Revit must honor PDFExportOptions.FileName.
            if (candidates.Length > 1)
                throw new AmbiguousExportOutputException("AMBIGUOUS_EXPORT_OUTPUT: multiple PDF files in isolated staging folder.");
            throw new ExportOutputMissingException("OUTPUT_MISSING: expected PDF name was not produced; unrelated staging file ignored.");
        }

        public static string VerifyOutput(string folder, string expectedPath, string format)
        {
            if (!File.Exists(expectedPath)) throw new ExportOutputMissingException("OUTPUT_MISSING: " + format + " expected output does not exist.");
            FileInfo info = new FileInfo(expectedPath);
            if (info.Length <= 0) throw new ExportOutputMissingException("OUTPUT_EMPTY: " + format + " output is empty.");
            return expectedPath;
        }

        private static void PrepareFolder(string folder, string expected)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            if (IsFileLocked(expected)) throw new FileLockedException(expected, "LOCKED_OUTPUT: " + Path.GetFileName(expected));
            foreach (string file in Directory.GetFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly))
                try { File.Delete(file); } catch (Exception ex) { throw new IOException("Staging folder is not clean: " + ex.Message, ex); }
        }

        private static PDFExportOptions CreateStandardPdfOptions(string fileName, bool combine, ExportOptions options)
        {
            var opt = new PDFExportOptions { FileName = fileName, Combine = combine, PaperFormat = ExportPaperFormat.Default,
                ExportQuality = GetExportQuality(options?.PdfExportDpi ?? 300), StopOnError = false };
            if (options == null)
            {
                opt.ColorDepth = ColorDepthType.Color; opt.RasterQuality = RasterQualityType.High; opt.HideUnreferencedViewTags = true;
                opt.HideScopeBoxes = true; opt.HideCropBoundaries = true; opt.HideReferencePlane = true;
                opt.MaskCoincidentLines = true; opt.ZoomType = ZoomType.Zoom; opt.ZoomPercentage = 100; opt.PaperPlacement = PaperPlacementType.LowerLeft;
                return opt;
            }
            opt.ColorDepth = options.ColorMode switch { "Grayscale" => ColorDepthType.GrayScale, "Black & White" => ColorDepthType.BlackLine, _ => ColorDepthType.Color };
            opt.RasterQuality = options.RasterQuality switch { "Presentation" => RasterQualityType.Presentation, "Medium" => RasterQualityType.Medium, "Low" => RasterQualityType.Low, _ => RasterQualityType.High };
            opt.AlwaysUseRaster = !options.VectorProcessing;
            opt.PaperOrientation = PageOrientationType.Auto;
            opt.HideUnreferencedViewTags = options.HideUnreferencedViewTags;
            opt.HideScopeBoxes = options.HideScopeBoxes; opt.HideCropBoundaries = options.HideCropBoundaries;
            opt.HideReferencePlane = options.HideRefPlanes; opt.MaskCoincidentLines = options.MaskCoincidentLines;
            opt.ReplaceHalftoneWithThinLines = options.ReplaceHalftoneWithThinLines; opt.ViewLinksInBlue = options.ViewLinksInBlue;
            opt.ZoomType = options.ZoomFitToPage ? ZoomType.FitToPage : ZoomType.Zoom;
            opt.ZoomPercentage = options.ZoomPercentage > 0 ? options.ZoomPercentage : 100;
            opt.PaperPlacement = options.PaperPlacementCenter ? PaperPlacementType.Center : PaperPlacementType.LowerLeft;
            if (options.MarginOffsetX != 0) opt.OriginOffsetX = options.MarginOffsetX / 304.8;
            if (options.MarginOffsetY != 0) opt.OriginOffsetY = options.MarginOffsetY / 304.8;
            return opt;
        }

        private static PDFExportQualityType GetExportQuality(int dpi)
        {
            switch (dpi) { case 72: return PDFExportQualityType.DPI72; case 144: return PDFExportQualityType.DPI144; case 600: return PDFExportQualityType.DPI600; default: return PDFExportQualityType.DPI300; }
        }

        private static string Sanitize(string name)
        {
            string value = NamingPlanService.Sanitize(name);
            return string.IsNullOrWhiteSpace(value) ? "Sheet" : value;
        }
    }
}
