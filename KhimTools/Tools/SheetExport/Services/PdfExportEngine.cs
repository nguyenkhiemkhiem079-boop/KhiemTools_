using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class PdfExportEngine
    {
        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt)
        {
            if (doc == null || sheet == null) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string targetPath = Path.Combine(outputFolder, fileNameWithoutExt + ".pdf");

            var pdfOpt = new PDFExportOptions
            {
                FileName = fileNameWithoutExt,
                Combine = false
            };

            var viewIds = new List<ElementId> { sheet.Id };
            doc.Export(outputFolder, viewIds, pdfOpt);

            // Revit API exports to folder with fileNameWithoutExt.pdf
            if (File.Exists(targetPath)) return targetPath;

            // Search for generated file in output folder
            var files = Directory.GetFiles(outputFolder, "*.pdf")
                .Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddMinutes(-2))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (files.Any()) return files.First();

            throw new FileNotFoundException($"Không tìm thấy file PDF được tạo tại {targetPath}");
        }

        public static string ExportCombinedSheets(Document doc, List<ViewSheet> sheets, string outputFolder, string combinedFileNameWithoutExt)
        {
            if (doc == null || sheets == null || !sheets.Any()) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            string targetPath = Path.Combine(outputFolder, combinedFileNameWithoutExt + ".pdf");

            var pdfOpt = new PDFExportOptions
            {
                FileName = combinedFileNameWithoutExt,
                Combine = true
            };

            var viewIds = sheets.Select(s => s.Id).ToList();
            doc.Export(outputFolder, viewIds, pdfOpt);

            if (File.Exists(targetPath)) return targetPath;

            var files = Directory.GetFiles(outputFolder, "*.pdf")
                .Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddMinutes(-2))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (files.Any()) return files.First();

            throw new FileNotFoundException($"Không tìm thấy file PDF gộp được tạo tại {targetPath}");
        }
    }
}
