using System;
using System.Collections.Generic;
using System.IO;
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

            // Handle Combine PDF mode
            if (options.ExportPdf && options.CombinePdf && items.Any())
            {
                string pdfFolder = options.SplitFoldersByFormat ? Path.Combine(options.OutputDirectory, "PDF") : options.OutputDirectory;
                string combinedName = !string.IsNullOrWhiteSpace(options.CombinedPdfFileName) ? options.CombinedPdfFileName : "Combined_Sheets";
                logProgress?.Invoke($"Đang xuất PDF Gộp ({items.Count} sheets)...");

                try
                {
                    var sheets = items.Select(i => i.Sheet).ToList();
                    string combinedPath = PdfExportEngine.ExportCombinedSheets(doc, sheets, pdfFolder, combinedName, options);

                    foreach (var item in items)
                    {
                        item.ExportStatusText = "✔ Hoàn tất (PDF Gộp)";
                        item.IsFailed = false;
                        results.Add(new QaReportEntry
                        {
                            SheetNumber = item.SheetNumber,
                            SheetName = item.SheetName,
                            Format = "PDF (Combined)",
                            OutputFilePath = combinedPath,
                            Success = true,
                            Message = "Thành công trong file PDF gộp"
                        });
                    }
                }
                catch (Exception ex)
                {
                    foreach (var item in items)
                    {
                        item.ExportStatusText = "✘ Lỗi PDF Gộp";
                        item.IsFailed = true;
                        item.ErrorMessage = ex.Message;
                        results.Add(new QaReportEntry
                        {
                            SheetNumber = item.SheetNumber,
                            SheetName = item.SheetName,
                            Format = "PDF (Combined)",
                            Success = false,
                            Message = ex.Message
                        });
                    }
                }

                // If DWG is also requested along with Combined PDF, continue for DWG
                if (!options.ExportDwg) return results;
            }

            int total = items.Count;
            int current = 1;

            // Primary Pass
            foreach (var item in items)
            {
                logProgress?.Invoke($"({current}/{total}) [{item.SheetNumber}]");
                var entry = ExecuteSingleExport(doc, item, options, logProgress);
                results.Add(entry);

                if (!entry.Success)
                {
                    item.IsFailed = true;
                    item.ErrorMessage = entry.Message;
                    failedQueue.Enqueue(item);
                }

                current++;
                System.Windows.Forms.Application.DoEvents();
            }

            // Retry Pass
            int currentRetry = 1;
            while (failedQueue.Any() && currentRetry <= _maxRetries)
            {
                int count = failedQueue.Count;
                logProgress?.Invoke($"🔄 Retry {currentRetry}/{_maxRetries} ({count} sheets)...");

                for (int i = 0; i < count; i++)
                {
                    var item = failedQueue.Dequeue();
                    item.RetryCount++;

                    logProgress?.Invoke($"Retry [{item.SheetNumber}] (Lần {currentRetry})...");
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

                    System.Windows.Forms.Application.DoEvents();
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
                string pdfFolder = options.SplitFoldersByFormat ? Path.Combine(options.OutputDirectory, "PDF") : options.OutputDirectory;
                string dwgFolder = options.SplitFoldersByFormat ? Path.Combine(options.OutputDirectory, "DWG") : options.OutputDirectory;

                if (options.ExportPdf)
                {
                    outPath = PdfExportEngine.ExportSingleSheet(doc, item.Sheet, pdfFolder, item.ComputedFileName, options);

                    if (options.ApplyWatermark && !string.IsNullOrWhiteSpace(options.WatermarkText))
                    {
                        PdfPostProcessService.ApplyWatermark(outPath, options.WatermarkText);
                    }
                }

                if (options.ExportDwg)
                {
                    string dwgPath = DwgExportEngine.ExportSingleSheet(doc, item.Sheet, dwgFolder, item.ComputedFileName, options.DwgExportSetupName);
                    if (string.IsNullOrEmpty(outPath)) outPath = dwgPath;
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
