using System;
using System.Collections.Generic;
using System.IO;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    /// <summary>Single source of truth for every published, archived, and staged path.</summary>
    public sealed class ExportPathResolver
    {
        public ExportJobOptions Options { get; private set; }
        public string BatchId { get; private set; }

        public ExportPathResolver(ExportJobOptions options, string batchId = null)
        {
            Options = options ?? ExportJobOptions.Normalize(null);
            BatchId = string.IsNullOrWhiteSpace(batchId) ? Guid.NewGuid().ToString("N") : batchId;
        }

        public string OutputRoot { get { return Options.OutputDirectory; } }
        public string StagingRoot { get { return Path.Combine(Path.GetTempPath(), "KToolsTemp", "Export", BatchId); } }
        public string ArchiveRoot { get { return Path.Combine(OutputRoot, string.IsNullOrWhiteSpace(Options.PreviousExportsFolderName) ? "Previous Exports" : Options.PreviousExportsFolderName, BatchId); } }

        public string FormatFolder(ExportFormat format)
        {
            if (!Options.SplitFoldersByFormat) return OutputRoot;
            return Path.Combine(OutputRoot, format == ExportFormat.PDF ? "PDF" : "DWG");
        }

        public string StagingFormatFolder(ExportFormat format)
        {
            return Path.Combine(StagingRoot, format == ExportFormat.PDF ? "PDF" : "DWG");
        }

        public string ResolveSheetPath(SheetExportItem item, ExportFormat format)
        {
            if (item == null) return "";
            return Path.Combine(FormatFolder(format), item.ComputedFileName + (format == ExportFormat.PDF ? ".pdf" : ".dwg"));
        }

        public string ResolveCombinedPdfPath()
        {
            string name = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(Options.CombinedPdfFileName) ? "Combined_Sheets.pdf" : Options.CombinedPdfFileName);
            return Path.Combine(FormatFolder(ExportFormat.PDF), name + ".pdf");
        }

        public string ResolveStagedSheetPath(SheetExportItem item, ExportFormat format)
        {
            return Path.Combine(StagingFormatFolder(format), item.ComputedFileName + (format == ExportFormat.PDF ? ".pdf" : ".dwg"));
        }

        public string ResolveStagedCombinedPdfPath()
        {
            string name = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(Options.CombinedPdfFileName) ? "Combined_Sheets.pdf" : Options.CombinedPdfFileName);
            return Path.Combine(StagingFormatFolder(ExportFormat.PDF), name + ".pdf");
        }

        public string ResolveArchivePath(string finalPath)
        {
            return Path.Combine(ArchiveRoot, Path.GetFileName(finalPath));
        }

        public string ResolveTransmittalPath(DateTime issueDate)
        {
            return Path.Combine(OutputRoot, "Transmittal_" + SafePart(Options.ProjectCode) + "_" + issueDate.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        public string ResolveQaReportPath(DateTime issueDate)
        {
            return Path.Combine(OutputRoot, "QA_Report_" + SafePart(Options.ProjectCode) + "_" + issueDate.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        public IEnumerable<string> ResolveExpectedFinalPaths(IEnumerable<SheetExportItem> items)
        {
            foreach (SheetExportItem item in items ?? new List<SheetExportItem>())
            {
                if (Options.ExportPdf && !Options.CombinePdf) yield return ResolveSheetPath(item, ExportFormat.PDF);
                if (Options.ExportDwg) yield return ResolveSheetPath(item, ExportFormat.DWG);
            }
            if (Options.ExportPdf && Options.CombinePdf) yield return ResolveCombinedPdfPath();
        }

        private static string SafePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "PROJECT";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim();
        }
    }
}
