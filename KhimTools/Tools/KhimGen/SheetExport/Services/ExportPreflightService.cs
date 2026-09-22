using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class ExportPreflightService
    {
        public static List<ExportPreflightItem> Run(Document doc, IList<SheetExportItem> sheets, ExportJobOptions options, ExportPathResolver paths)
        {
            var issues = new List<ExportPreflightItem>();
            options = options ?? ExportJobOptions.Normalize(null);
            paths = paths ?? new ExportPathResolver(options);
            List<SheetExportItem> selected = (sheets ?? new List<SheetExportItem>()).Where(item => item != null && item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                issues.Add(Blocked(ExportPreflightCode.NO_SELECTION, "Chưa chọn sheet để xuất."));
                return issues;
            }
            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                issues.Add(Blocked(ExportPreflightCode.INVALID_OUTPUT_DIRECTORY, "Thư mục đầu ra trống."));
                return issues;
            }
            string outputDirectory;
            try { outputDirectory = Path.GetFullPath(options.OutputDirectory); }
            catch (Exception ex) { issues.Add(Blocked(ExportPreflightCode.INVALID_OUTPUT_DIRECTORY, ex.Message)); return issues; }
            if (File.Exists(outputDirectory)) issues.Add(Blocked(ExportPreflightCode.INVALID_OUTPUT_DIRECTORY, "Đường dẫn đầu ra là một file."));
            else if (!CanWriteDirectory(outputDirectory, out string writeError)) issues.Add(Blocked(ExportPreflightCode.OUTPUT_NOT_WRITABLE, writeError));

            foreach (string duplicate in NamingPlanService.FindDuplicates(selected))
            {
                issues.Add(new ExportPreflightItem { Code = ExportPreflightCode.DUPLICATE_OUTPUT, Severity = ExportPreflightSeverity.BLOCKED,
                    Path = duplicate, Message = "DUPLICATE_OUTPUT: " + duplicate, CanExecute = false });
            }
            if (options.ExportPdf && options.CombinePdf && selected.Select(item => item.PaperSize).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                issues.Add(new ExportPreflightItem { Code = ExportPreflightCode.COMBINE_MIXED_PAPER_SIZE, Severity = ExportPreflightSeverity.WARNING,
                    Message = "COMBINE_MIXED_PAPER_SIZE: combined PDF contains multiple paper sizes.", CanExecute = true });
            if (options.ExportPdf && options.CombinePdf && selected.Select(item => item.Orientation).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                issues.Add(new ExportPreflightItem { Code = ExportPreflightCode.COMBINE_MIXED_ORIENTATION, Severity = ExportPreflightSeverity.WARNING,
                    Message = "COMBINE_MIXED_ORIENTATION: combined PDF contains multiple orientations.", CanExecute = true });

            var pdfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dwgPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SheetExportItem item in selected)
            {
                if (item.Sheet == null || doc == null || doc.GetElement(item.SheetId) == null)
                {
                    item.CanExport = false;
                    issues.Add(Item(item, ExportPreflightCode.MISSING_SHEET, ExportPreflightSeverity.BLOCKED, "MISSING_SHEET: sheet no longer exists.", false));
                    continue;
                }
                if (string.Equals(item.PaperMetadataStatus, "UNKNOWN", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.PaperSize))
                    issues.Add(Item(item, ExportPreflightCode.UNKNOWN_PAPER_SIZE, ExportPreflightSeverity.WARNING, "UNKNOWN_PAPER_SIZE: title block dimensions unavailable.", true));
                if (item.TitleBlockCount > 1)
                    issues.Add(Item(item, ExportPreflightCode.MULTIPLE_TITLE_BLOCKS, ExportPreflightSeverity.WARNING, "MULTIPLE_TITLE_BLOCKS: paper metadata is ambiguous.", true));
                if (options.ExportPdf && !options.CombinePdf)
                {
                    string path = paths.ResolveSheetPath(item, ExportFormat.PDF);
                    if (!pdfPaths.Add(path)) issues.Add(Item(item, ExportPreflightCode.DUPLICATE_OUTPUT, ExportPreflightSeverity.BLOCKED, "DUPLICATE_OUTPUT: " + path, false, ExportFormat.PDF, path));
                    AddPathChecks(issues, item, path, ExportFormat.PDF, options);
                }
                if (options.ExportDwg)
                {
                    string path = paths.ResolveSheetPath(item, ExportFormat.DWG);
                    if (!dwgPaths.Add(path)) issues.Add(Item(item, ExportPreflightCode.DUPLICATE_OUTPUT, ExportPreflightSeverity.BLOCKED, "DUPLICATE_OUTPUT: " + path, false, ExportFormat.DWG, path));
                    AddPathChecks(issues, item, path, ExportFormat.DWG, options);
                }
            }
            if (options.ExportPdf && options.CombinePdf) AddPathChecks(issues, null, paths.ResolveCombinedPdfPath(), ExportFormat.PDF, options, ExportPreflightCode.LOCKED_COMBINED_PDF);
            if (options.ExportDwg)
            {
                if (string.IsNullOrWhiteSpace(options.DwgExportSetupName)) issues.Add(Blocked(ExportPreflightCode.DWG_SETUP_MISSING, "DWG_SETUP_MISSING: no DWG setup selected."));
                else if (!DwgExportEngine.TryValidateSetup(doc, options.DwgExportSetupName, out ExportPreflightCode setupCode, out string setupMessage))
                    issues.Add(Blocked(setupCode, setupMessage));
                else if (setupCode != ExportPreflightCode.DWG_SETUP_READY)
                    issues.Add(new ExportPreflightItem { Code = setupCode, Severity = ExportPreflightSeverity.WARNING, Message = setupMessage, CanExecute = true });
            }
            if (options.ExportDwg && !IsSupportedDwgVersion(options.DwgTargetVersion))
                issues.Add(Blocked(ExportPreflightCode.INVALID_DWG_VERSION, "INVALID_DWG_VERSION: " + options.DwgTargetVersion));
            if (issues.Count == 0) issues.Add(new ExportPreflightItem { Code = ExportPreflightCode.READY, Severity = ExportPreflightSeverity.INFO, Message = "READY", CanExecute = true });
            return issues;
        }

        public static bool IsBlocking(IEnumerable<ExportPreflightItem> issues)
        {
            return (issues ?? Enumerable.Empty<ExportPreflightItem>()).Any(item => item.Severity == ExportPreflightSeverity.BLOCKED || !item.CanExecute && item.Code != ExportPreflightCode.UNKNOWN_PAPER_SIZE && item.Code != ExportPreflightCode.MULTIPLE_TITLE_BLOCKS);
        }

        public static bool IsSupportedDwgVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.IndexOf("2018", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("2013", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("2010", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("2007", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddPathChecks(List<ExportPreflightItem> issues, SheetExportItem item, string path, ExportFormat format, ExportJobOptions options, ExportPreflightCode lockCode = ExportPreflightCode.LOCKED_OUTPUT)
        {
            if (path.Length >= options.MaxPathLength) issues.Add(Item(item, ExportPreflightCode.PATH_TOO_LONG, ExportPreflightSeverity.BLOCKED, "PATH_TOO_LONG: " + path, false, format, path));
            if (File.Exists(path) && PdfExportEngine.IsFileLocked(path))
                issues.Add(Item(item, lockCode == ExportPreflightCode.LOCKED_OUTPUT ? (format == ExportFormat.PDF ? ExportPreflightCode.LOCKED_EXISTING_PDF : ExportPreflightCode.LOCKED_EXISTING_DWG) : lockCode,
                    ExportPreflightSeverity.BLOCKED, "LOCKED_OUTPUT: " + path, false, format, path));
        }

        private static bool CanWriteDirectory(string path, out string error)
        {
            error = "";
            try
            {
                Directory.CreateDirectory(path);
                string probe = Path.Combine(path, ".ktools-write-probe-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex) { error = "OUTPUT_NOT_WRITABLE: " + ex.Message; return false; }
        }

        private static ExportPreflightItem Blocked(ExportPreflightCode code, string message)
        {
            return new ExportPreflightItem { Code = code, Severity = ExportPreflightSeverity.BLOCKED, Message = message, CanExecute = false };
        }

        private static ExportPreflightItem Item(SheetExportItem item, ExportPreflightCode code, ExportPreflightSeverity severity, string message, bool canExecute, ExportFormat format = ExportFormat.PDF, string path = "")
        {
            return new ExportPreflightItem { Code = code, Severity = severity, SheetId = item?.SheetId, SheetUniqueId = item?.SheetUniqueId ?? "", Format = format, Path = path, Message = message, CanExecute = canExecute };
        }
    }
}
