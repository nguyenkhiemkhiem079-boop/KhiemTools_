using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public class DwgSetupException : InvalidOperationException
    {
        public ExportPreflightCode Code { get; private set; }
        public DwgSetupException(ExportPreflightCode code, string message) : base(message) { Code = code; }
    }

    public static class DwgExportEngine
    {
        public static string ExportSingleSheet(Document doc, ViewSheet sheet, string outputFolder, string fileNameWithoutExt,
            string dwgSetupName = "In-Session Setup", ExportOptions options = null)
        {
            if (doc == null || sheet == null) throw new ArgumentNullException("doc");
            string baseName = Sanitize(string.IsNullOrWhiteSpace(fileNameWithoutExt) ? sheet.SheetNumber + " - " + sheet.Name : fileNameWithoutExt);
            string expected = Path.Combine(outputFolder, baseName + ".dwg");
            PrepareFolder(outputFolder, expected);
            DWGExportOptions dwgOptions = ResolveOptions(doc, dwgSetupName, options);
            try { doc.Export(outputFolder, baseName, new List<ElementId> { sheet.Id }, dwgOptions); }
            catch (Exception ex) { throw new InvalidOperationException("Lỗi Revit Export DWG cho sheet [" + sheet.SheetNumber + "]: " + ex.Message, ex); }
            string primary = ResolvePrimary(outputFolder, expected, baseName);
            return VerifyOutput(primary, "DWG");
        }

        public static bool TryValidateSetup(Document doc, string setupName, out ExportPreflightCode code, out string message)
        {
            code = ExportPreflightCode.DWG_SETUP_READY; message = "DWG_SETUP_READY";
            if (string.IsNullOrWhiteSpace(setupName)) { code = ExportPreflightCode.DWG_SETUP_MISSING; message = "DWG_SETUP_MISSING"; return false; }
            if (setupName.StartsWith("In-Session", StringComparison.OrdinalIgnoreCase)) { code = ExportPreflightCode.DWG_SETUP_FALLBACK; message = "DWG_SETUP_FALLBACK: In-Session Setup"; return true; }
            try { DWGExportOptions.GetPredefinedOptions(doc, setupName); return true; }
            catch (Exception ex) { code = ExportPreflightCode.DWG_SETUP_INVALID; message = "DWG_SETUP_INVALID: " + ex.Message; return false; }
        }

        private static DWGExportOptions ResolveOptions(Document doc, string setupName, ExportOptions options)
        {
            DWGExportOptions dwgOptions = null;
            if (!string.IsNullOrWhiteSpace(setupName) && !setupName.StartsWith("In-Session", StringComparison.OrdinalIgnoreCase))
            {
                try { dwgOptions = DWGExportOptions.GetPredefinedOptions(doc, setupName); }
                catch (Exception ex) { throw new DwgSetupException(ExportPreflightCode.DWG_SETUP_INVALID, "DWG_SETUP_INVALID: " + ex.Message); }
            }
            if (dwgOptions == null)
            {
                dwgOptions = new DWGExportOptions();
                Debug.WriteLine("[K-TOOLS][SheetExport] DWG_SETUP_FALLBACK: In-Session Setup");
            }
            dwgOptions.MergedViews = options == null || options.DwgMergedViews;
            string targetVersion = options == null ? "AutoCAD 2018 format" : options.DwgTargetVersion;
            if (!TrySetVersion(dwgOptions, targetVersion)) throw new DwgSetupException(ExportPreflightCode.INVALID_DWG_VERSION, "INVALID_DWG_VERSION: " + targetVersion);
            return dwgOptions;
        }

        private static bool TrySetVersion(DWGExportOptions options, string value)
        {
            try
            {
                string enumName = value.IndexOf("2013", StringComparison.OrdinalIgnoreCase) >= 0 ? "R2013" : value.IndexOf("2010", StringComparison.OrdinalIgnoreCase) >= 0 ? "R2010" : value.IndexOf("2007", StringComparison.OrdinalIgnoreCase) >= 0 ? "R2007" : "R2018";
                var property = options.GetType().GetProperty("FileVersion");
                if (property == null) return true;
                object parsed = Enum.Parse(property.PropertyType, enumName);
                property.SetValue(options, parsed, null);
                return true;
            }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] DWG version mapping failed: " + ex); return false; }
        }

        private static string ResolvePrimary(string folder, string expected, string baseName)
        {
            if (File.Exists(expected)) return expected;
            string[] drawings = Directory.GetFiles(folder, "*.dwg", SearchOption.TopDirectoryOnly);
            string[] matching = drawings.Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matching.Length == 1) { if (!string.Equals(matching[0], expected, StringComparison.OrdinalIgnoreCase)) File.Move(matching[0], expected); return expected; }
            if (matching.Length > 1 || drawings.Length > 1) throw new AmbiguousExportOutputException("AMBIGUOUS_EXPORT_OUTPUT: DWG primary drawing is not unique.");
            if (drawings.Length == 0) throw new ExportOutputMissingException("OUTPUT_MISSING: DWG export produced no primary drawing.");
            throw new ExportOutputMissingException("OUTPUT_MISSING: expected DWG primary drawing was not produced; side files ignored.");
        }

        public static string VerifyOutput(string path, string format)
        {
            if (!File.Exists(path)) throw new ExportOutputMissingException("OUTPUT_MISSING: " + format);
            if (new FileInfo(path).Length <= 0) throw new ExportOutputMissingException("OUTPUT_EMPTY: " + format);
            return path;
        }

        private static void PrepareFolder(string folder, string expected)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            if (PdfExportEngine.IsFileLocked(expected)) throw new FileLockedException(expected, "LOCKED_OUTPUT: " + Path.GetFileName(expected));
        }

        private static string Sanitize(string name)
        {
            string value = NamingPlanService.Sanitize(name);
            return string.IsNullOrWhiteSpace(value) ? "Sheet" : value;
        }
    }
}
