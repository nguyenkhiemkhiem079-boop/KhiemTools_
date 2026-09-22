using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.SheetExport.Models;
using Autodesk.Revit.DB;

namespace KhimTools.SheetExport.Services
{
    /// <summary>Compatibility adapter for the existing form; retry and result logic live in the workflow.</summary>
    public class ExportRetryQueue
    {
        private readonly int _maxRetries;
        public ExportRetryQueue(int maxRetries = 2) { _maxRetries = Math.Max(0, maxRetries); }

        public List<QaReportEntry> ProcessBatch(Document doc, List<SheetExportItem> items, ExportOptions options, Action<string> logProgress)
        {
            if (options != null) options.MaxRetryCount = _maxRetries;
            ExportBatchResult batch = SheetExportWorkflowService.Execute(doc, items, options,
                progress => logProgress?.Invoke(progress?.Message ?? ""));
            return ToQaEntries(batch);
        }

        public static List<QaReportEntry> ToQaEntries(ExportBatchResult batch)
        {
            var entries = new List<QaReportEntry>();
            foreach (ExportItemResult result in batch?.Results ?? new List<ExportItemResult>())
            {
                entries.Add(new QaReportEntry { SheetNumber = result.SheetNumber, Format = result.Format.ToString(), OutputFilePath = result.FinalPath,
                    FileSizeBytes = result.FileSizeBytes, DurationSeconds = result.DurationSeconds, Success = result.Success,
                    IsLocked = result.Status == ExportItemStatus.LOCKED, Retries = result.RetryCount, Status = result.Status.ToString(), Message = result.Message });
            }
            return entries;
        }
    }
}
