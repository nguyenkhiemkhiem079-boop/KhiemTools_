using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.SheetExport.Services
{
    /// <summary>
    /// Engine xuất bản vẽ sang định dạng AutoCAD DWG (CAD) trực tiếp từ Revit,
    /// hỗ trợ chọn thiết lập Layer mapping (Setup) và gộp toàn bộ Xref vào 1 file DWG sạch sẽ.
    /// </summary>
    public static class DwgExportEngine
    {
        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt, string dwgSetupName = "In-Session Setup")
        {
            if (doc == null || sheet == null) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            if (string.IsNullOrWhiteSpace(fileNameWithoutExt))
            {
                fileNameWithoutExt = $"{sheet.SheetNumber} - {sheet.Name}";
            }
            fileNameWithoutExt = Sanitize(fileNameWithoutExt);

            DWGExportOptions dwgOpt = null;
            if (!string.IsNullOrWhiteSpace(dwgSetupName) && !dwgSetupName.StartsWith("In-Session"))
            {
                try { dwgOpt = DWGExportOptions.GetPredefinedOptions(doc, dwgSetupName); } catch { }
            }

            if (dwgOpt == null)
            {
                try { dwgOpt = DWGExportOptions.GetPredefinedOptions(doc, "In-Session Setup"); } catch { }
            }

            if (dwgOpt == null) dwgOpt = new DWGExportOptions();

            dwgOpt.MergedViews = true; // Gộp tất cả view vào file DWG, không tạo Xref rời

            var viewIds = new List<ElementId> { sheet.Id };
            var beforeFiles = new HashSet<string>(Directory.GetFiles(outputFolder, "*.dwg"), StringComparer.OrdinalIgnoreCase);

            try
            {
                doc.Export(outputFolder, fileNameWithoutExt, viewIds, dwgOpt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi Revit Export DWG cho sheet [{sheet.SheetNumber}]: {ex.Message}", ex);
            }

            string targetPath = Path.Combine(outputFolder, fileNameWithoutExt + ".dwg");
            if (File.Exists(targetPath)) return targetPath;

            var afterFiles = Directory.GetFiles(outputFolder, "*.dwg");
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

            return targetPath;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet";
            var invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
