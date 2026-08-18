using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public class ExportRetryQueue
    {
        private readonly int _maxRetries;
        public ExportRetryQueue(int maxRetries = 2)
        {
            _maxRetries = Math.Max(1, maxRetries);
        }

        public List<QaReportEntry> ProcessBatch(
            Document doc,
            List<SheetExportItem> items,
            ExportOptions options,
            Action<string> logProgress)
        {
            var results = new List<QaReportEntry>();
            var failedQueue = new Queue<SheetExportItem>();

            // Primary Pass
            foreach (var item in items)
            {
                var entry = ExecuteSingleExport(doc, item, options, logProgress);
                results.Add(entry);

                if (!entry.Success)
                {
                    item.IsFailed = true;
                    item.ErrorMessage = entry.Message;
                    failedQueue.Enqueue(item);
                }
            }

            // Retry Pass
            int currentRetry = 1;
            while (failedQueue.Any() && currentRetry <= _maxRetries)
            {
                int count = failedQueue.Count;
                logProgress?.Invoke($"\n🔄 Đang thực hiện Retry lần {currentRetry} cho {count} sheet bị lỗi...");

                for (int i = 0; i < count; i++)
                {
                    var item = failedQueue.Dequeue();
                    item.RetryCount++;

                    logProgress?.Invoke($"  - Retrying [{item.SheetNumber}] (Lần {currentRetry})...");
                    var entry = ExecuteSingleExport(doc, item, options, logProgress);

                    var existingEntry = results.FirstOrDefault(r => r.SheetNumber == item.SheetNumber);
                    if (existingEntry != null)
                    {
                        results.Remove(existingEntry);
                    }
                    results.Add(entry);

                    if (entry.Success)
                    {
                        item.IsFailed = false;
                        item.ExportStatusText = "✔ Thành công (Retry)";
                        item.ErrorMessage = "";
                        logProgress?.Invoke($"    ✓ Thành công sau retry!");
                    }
                    else
                    {
                        item.IsFailed = true;
                        item.ErrorMessage = entry.Message;
                        if (currentRetry < _maxRetries)
                        {
                            failedQueue.Enqueue(item);
                        }
                    }
                }

                currentRetry++;
            }

            return results;
        }

        private QaReportEntry ExecuteSingleExport(
            Document doc,
            SheetExportItem item,
            ExportOptions options,
            Action<string> logProgress)
        {
            var entry = new QaReportEntry
            {
                SheetNumber = item.SheetNumber,
                SheetName = item.SheetName,
                Format = options.ExportPdf ? "PDF" : "DWG",
                Retries = item.RetryCount
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                logProgress?.Invoke($"Đang xử lý [{item.SheetNumber}] - {item.SheetName}...");
                item.ExportStatusText = "Đang xuất...";

                string outPath = "";
                if (options.ExportPdf)
                {
                    outPath = PdfExportEngine.ExportSingleSheet(doc, item.Sheet, options.OutputDirectory, item.ComputedFileName);

                    if (options.ApplyWatermark && !string.IsNullOrWhiteSpace(options.WatermarkText))
                    {
                        PdfPostProcessService.ApplyWatermark(outPath, options.WatermarkText);
                    }
                }
                else if (options.ExportDwg)
                {
                    outPath = DwgExportEngine.ExportSingleSheet(doc, item.Sheet, options.OutputDirectory, item.ComputedFileName);
                }

                sw.Stop();
                entry.DurationSeconds = sw.Elapsed.TotalSeconds;
                entry.OutputFilePath = outPath;
                entry.Success = true;
                entry.Message = "Thành công";

                if (System.IO.File.Exists(outPath))
                {
                    var fi = new System.IO.FileInfo(outPath);
                    entry.FileSizeBytes = fi.Length;
                    item.FileSizeBytes = fi.Length;
                }

                item.DurationSeconds = entry.DurationSeconds;
                item.ExportStatusText = "✔ Hoàn tất";
                item.IsFailed = false;

                logProgress?.Invoke($"  ✓ Hoàn tất [{item.SheetNumber}] ({Math.Round(entry.DurationSeconds, 1)}s)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                entry.DurationSeconds = sw.Elapsed.TotalSeconds;
                entry.Success = false;
                entry.Message = ex.Message;
                item.ExportStatusText = "✘ Lỗi";
                item.IsFailed = true;

                logProgress?.Invoke($"  ✘ Lỗi [{item.SheetNumber}]: {ex.Message}");
            }

            return entry;
        }
    }
}
