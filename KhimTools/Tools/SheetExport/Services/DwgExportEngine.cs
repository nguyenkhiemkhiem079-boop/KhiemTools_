using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.SheetExport.Services
{
    public static class DwgExportEngine
    {
        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt)
        {
            if (doc == null || sheet == null) throw new ArgumentNullException(nameof(doc));
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            var dwgOpt = DWGExportOptions.GetPredefinedOptions(doc, "In-Session Setup");
            if (dwgOpt == null) dwgOpt = new DWGExportOptions();

            dwgOpt.MergedViews = true;

            var viewIds = new List<ElementId> { sheet.Id };
            doc.Export(outputFolder, fileNameWithoutExt, viewIds, dwgOpt);

            string targetPath = Path.Combine(outputFolder, fileNameWithoutExt + ".dwg");
            if (File.Exists(targetPath)) return targetPath;

            var files = Directory.GetFiles(outputFolder, "*.dwg")
                .Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddMinutes(-2))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (files.Any()) return files.First();

            return targetPath;
        }
    }
}
