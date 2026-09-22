using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    /// <summary>Deterministic collect, preflight, stage, verify, post-process, publish workflow.</summary>
    public static class SheetExportWorkflowService
    {
        public static ExportBatchResult Execute(Document doc, IList<SheetExportItem> input, ExportOptions sourceOptions,
            Action<ExportProgress> progress = null, Func<bool> isCancellationRequested = null)
        {
            ExportJobOptions options = ExportJobOptions.Normalize(sourceOptions);
            var batch = new ExportBatchResult();
            List<SheetExportItem> selected = (input ?? new List<SheetExportItem>()).Where(item => item != null && item.IsSelected && item.IssueStatus != SheetIssueStatus.Deleted).ToList();
            batch.RequestedSheets = selected.Count;
            batch.RequestedOutputs = (options.ExportPdf ? (options.CombinePdf ? 1 : selected.Count) : 0) + (options.ExportDwg ? selected.Count : 0);
            string batchId = Guid.NewGuid().ToString("N");
            batch.BatchId = batchId;
            ExportPathResolver paths = new ExportPathResolver(options, batchId);
            Debug.WriteLine("[K-TOOLS][SheetExport] batch=" + batchId + " selected=" + batch.RequestedSheets + " requestedOutputs=" + batch.RequestedOutputs);
            // Re-resolve every selected sheet by UniqueId immediately before preflight. This
            // prevents a stale modeless-form reference from exporting a deleted/replaced sheet.
            foreach (SheetExportItem item in selected)
            {
                if (doc == null || string.IsNullOrWhiteSpace(item.SheetUniqueId))
                {
                    item.Sheet = null;
                    continue;
                }
                item.Sheet = doc.GetElement(item.SheetUniqueId) as ViewSheet;
            }
            NormalizeNames(selected, options, batch);
            List<ExportPreflightItem> preflight = ExportPreflightService.Run(doc, selected, options, paths);
            batch.Preflight.AddRange(preflight);
            if (ExportPreflightService.IsBlocking(batch.Preflight))
            {
                Debug.WriteLine("[K-TOOLS][SheetExport] batch=" + batchId + " preflightBlocked=" + batch.Preflight.Count);
                return batch;
            }

            TemporaryViewStateScope tempViews = null;
            bool failed = false;
            try
            {
                if (options.AutoDisableTemporaryViewProperties)
                {
                    tempViews = TemporaryViewStateScope.Capture(doc, selected.Select(item => item.Sheet));
                    if (!tempViews.DisableAll())
                    {
                        batch.Preflight.AddRange(tempViews.Diagnostics);
                        failed = true;
                        return batch;
                    }
                }
                Directory.CreateDirectory(paths.StagingRoot);
                if (options.ExportPdf && options.CombinePdf)
                    ExportCombinedPdf(doc, selected, options, paths, batch, progress, isCancellationRequested);
                foreach (SheetExportItem item in selected.OrderBy(sheet => sheet.SheetNumber, StringComparer.OrdinalIgnoreCase).ThenBy(sheet => sheet.SheetUniqueId, StringComparer.Ordinal))
                {
                    if (IsCancelled(isCancellationRequested)) { batch.Cancelled = true; break; }
                    if (options.ExportPdf && !options.CombinePdf) ExecuteWithRetry(doc, item, ExportFormat.PDF, options, paths, batch, progress, isCancellationRequested);
                    if (options.ExportDwg && !IsCancelled(isCancellationRequested)) ExecuteWithRetry(doc, item, ExportFormat.DWG, options, paths, batch, progress, isCancellationRequested);
                    if (IsCancelled(isCancellationRequested)) batch.Cancelled = true;
                }
                MarkCancelledOutputs(selected, options, batch);
                FinalizeBatch(batch);
                try
                {
                    if (options.GenerateTransmittal && batch.SuccessfulOutputs > 0)
                        TransmittalGeneratorService.GenerateExcelTransmittal(options, batch, paths);
                    if (options.GenerateQaReport)
                        QaReportService.GenerateQaExcelReport(options, batch, paths);
                    if (batch.SuccessfulOutputs > 0) RevisionSnapshotService.CreateSnapshot(doc, options, batch);
                }
                catch (Exception ex)
                {
                    batch.AuxiliaryReportFailed = true; batch.AuxiliaryReportMessage = "AUXILIARY_REPORT_FAILED: " + ex.Message;
                    Debug.WriteLine("[K-TOOLS][SheetExport] auxiliary report failed: " + ex);
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.WriteLine("[K-TOOLS][SheetExport] batch failed: " + ex);
                batch.Results.Add(new ExportItemResult { Status = ExportItemStatus.FAILED_EXPORT, Message = ex.Message });
            }
            finally
            {
                if (tempViews != null) tempViews.Dispose();
                if (!options.PreserveStagingOnFailure || !failed)
                {
                    try { if (Directory.Exists(paths.StagingRoot)) Directory.Delete(paths.StagingRoot, true); }
                    catch (Exception ex) { Debug.WriteLine("[K-TOOLS][SheetExport] staging cleanup failed: " + ex); }
                }
            }
            Debug.WriteLine("[K-TOOLS][SheetExport] batch=" + batchId + " success=" + batch.SuccessfulOutputs + " partial=" + batch.PartialSheets + " locked=" + batch.LockedOutputs + " failed=" + batch.FailedOutputs + " cancelled=" + batch.Cancelled + " auxiliary=" + batch.AuxiliaryReportFailed);
            return batch;
        }

        private static void NormalizeNames(List<SheetExportItem> items, ExportJobOptions options, ExportBatchResult batch)
        {
            NamingTemplate template = new NamingTemplate
            {
                Expression = string.IsNullOrWhiteSpace(options.NamingExpression) ? "{SheetNumber} - {SheetName}" : options.NamingExpression,
                RegexPattern = options.NamingRegexPattern
            };
            foreach (SheetExportItem item in items)
            {
                try { item.ComputedFileName = NamingPlanService.Expand(item, template, options); item.IsRegexValid = true; }
                catch (NamingPlanException ex) { item.IsRegexValid = false; batch.Preflight.Add(new ExportPreflightItem { Code = ex.Code, Severity = ExportPreflightSeverity.BLOCKED, SheetUniqueId = item.SheetUniqueId, Message = ex.Message, CanExecute = false }); }
            }
        }

        private static void ExportCombinedPdf(Document doc, List<SheetExportItem> items, ExportJobOptions options, ExportPathResolver paths,
            ExportBatchResult batch, Action<ExportProgress> progress, Func<bool> cancellation)
        {
            if (IsCancelled(cancellation)) { batch.Cancelled = true; MarkCancelledOutputs(items, options, batch); return; }
            string stageFolder = Path.Combine(paths.StagingFormatFolder(ExportFormat.PDF), "combined");
            string staged = paths.ResolveStagedCombinedPdfPath();
            string finalPath = paths.ResolveCombinedPdfPath();
            var sw = Stopwatch.StartNew();
            try
            {
                Report(progress, "Đang xuất PDF gộp...", null, ExportFormat.PDF, "EXPORT", 0, batch.RequestedOutputs);
                string output = PdfExportEngine.ExportCombinedSheets(doc, items.Select(item => item.Sheet).ToList(), stageFolder, Path.GetFileNameWithoutExtension(options.CombinedPdfFileName), options);
                VerifyAndPostProcess(output, items, options);
                Publish(output, finalPath, paths, options);
                foreach (SheetExportItem item in items) batch.Results.Add(NewResult(item, ExportFormat.PDF, output, finalPath, sw.Elapsed.TotalSeconds, 0, "PDF (Combined) SUCCESS"));
            }
            catch (Exception ex)
            {
                foreach (SheetExportItem item in items) batch.Results.Add(FailedResult(item, ExportFormat.PDF, finalPath, sw.Elapsed.TotalSeconds, ex, FailureStatus(ex)));
            }
        }

        private static void ExecuteWithRetry(Document doc, SheetExportItem item, ExportFormat format, ExportJobOptions options, ExportPathResolver paths,
            ExportBatchResult batch, Action<ExportProgress> progress, Func<bool> cancellation)
        {
            int retry = 0;
            while (true)
            {
                if (IsCancelled(cancellation)) { batch.Cancelled = true; return; }
                var sw = Stopwatch.StartNew();
                string staged = paths.ResolveStagedSheetPath(item, format);
                string finalPath = paths.ResolveSheetPath(item, format);
                try
                {
                    string operationFolder = Path.Combine(paths.StagingFormatFolder(format), SafeFolder(item.SheetUniqueId + "_" + item.ComputedFileName));
                    Directory.CreateDirectory(operationFolder);
                    string output = format == ExportFormat.PDF
                        ? PdfExportEngine.ExportSingleSheet(doc, item.Sheet, operationFolder, item.ComputedFileName, options)
                        : DwgExportEngine.ExportSingleSheet(doc, item.Sheet, operationFolder, item.ComputedFileName, options.DwgExportSetupName, options);
                    if (format == ExportFormat.PDF)
                        VerifyAndPostProcess(output, new List<SheetExportItem> { item }, options);
                    else
                        DwgExportEngine.VerifyOutput(output, "DWG");
                    Publish(output, finalPath, paths, options);
                    batch.Results.Add(NewResult(item, format, output, finalPath, sw.Elapsed.TotalSeconds, retry, "SUCCESS"));
                    Report(progress, "✓ " + item.SheetNumber + " " + format, item, format, "PUBLISH", batch.Results.Count, batch.RequestedOutputs);
                    return;
                }
            catch (FileLockedException ex) { batch.Results.Add(FailedResult(item, format, finalPath, sw.Elapsed.TotalSeconds, ex, ExportItemStatus.LOCKED, retry)); return; }
            catch (NamingPlanException ex) { batch.Results.Add(FailedResult(item, format, finalPath, sw.Elapsed.TotalSeconds, ex, ExportItemStatus.FAILED_EXPORT, retry)); return; }
            catch (DwgSetupException ex) { batch.Results.Add(FailedResult(item, format, finalPath, sw.Elapsed.TotalSeconds, ex, ExportItemStatus.FAILED_EXPORT, retry)); return; }
            catch (Exception ex)
            {
                if (retry >= options.MaxRetryCount || !IsRetryable(ex))
                    { batch.Results.Add(FailedResult(item, format, finalPath, sw.Elapsed.TotalSeconds, ex, FailureStatus(ex), retry)); return; }
                    retry++;
                    Report(progress, "Retry " + retry + "/" + options.MaxRetryCount + " " + item.SheetNumber + " " + format, item, format, "RETRY", batch.Results.Count, batch.RequestedOutputs);
                }
            }
        }

        private static void VerifyAndPostProcess(string stagedPath, List<SheetExportItem> items, ExportJobOptions options)
        {
            PdfExportEngine.VerifyOutput(Path.GetDirectoryName(stagedPath), stagedPath, "PDF");
            if (options.ApplyWatermark && !PdfPostProcessService.ApplyWatermark(stagedPath, options.WatermarkText)) throw new InvalidOperationException("POST_PROCESS_FAILED: watermark");
            if (items.Count > 1 && options.AddBookmarks && !PdfPostProcessService.AddBookmarks(stagedPath, items)) throw new InvalidOperationException("POST_PROCESS_FAILED: bookmarks");
            if (items.Count > 1 && options.AutoCoverPage && !PdfPostProcessService.InsertCoverSheet(stagedPath, options.IssueSetName, items)) throw new InvalidOperationException("POST_PROCESS_FAILED: cover");
            PdfExportEngine.VerifyOutput(Path.GetDirectoryName(stagedPath), stagedPath, "PDF after post-process");
        }

        private static void Publish(string stagedPath, string finalPath, ExportPathResolver paths, ExportJobOptions options)
        {
            if (!File.Exists(stagedPath) || new FileInfo(stagedPath).Length <= 0) throw new ExportOutputMissingException("OUTPUT_MISSING: staged output");
            string folder = Path.GetDirectoryName(finalPath);
            Directory.CreateDirectory(folder);
            if (File.Exists(finalPath))
            {
                if (PdfExportEngine.IsFileLocked(finalPath)) throw new FileLockedException(finalPath, "LOCKED_OUTPUT: " + finalPath);
                if (options.PreservePreviousExports)
                {
                    Directory.CreateDirectory(paths.ArchiveRoot);
                    File.Move(finalPath, UniqueArchivePath(paths.ResolveArchivePath(finalPath)));
                }
            }
            string tempFinal = finalPath + ".ktools-publish-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Move(stagedPath, tempFinal);
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempFinal, finalPath);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempFinal)) File.Delete(tempFinal); } catch (Exception cleanup) { Debug.WriteLine("[K-TOOLS][SheetExport] publish cleanup failed: " + cleanup); }
                throw new IOException("PUBLISH_FAILED: " + ex.Message, ex);
            }
        }

        private static string UniqueArchivePath(string path)
        {
            if (!File.Exists(path)) return path;
            string folder = Path.GetDirectoryName(path) ?? ""; string name = Path.GetFileNameWithoutExtension(path); string ext = Path.GetExtension(path);
            int i = 2; string candidate;
            do { candidate = Path.Combine(folder, name + "_v" + i++ + ext); } while (File.Exists(candidate));
            return candidate;
        }

        private static ExportItemResult NewResult(SheetExportItem item, ExportFormat format, string staged, string finalPath, double duration, int retry, string message)
        {
            return new ExportItemResult { SheetId = item.SheetId == null ? "" : item.SheetId.ToString(), SheetUniqueId = item.SheetUniqueId, SheetNumber = item.SheetNumber,
                RevisionNumber = item.CurrentRevisionNumber, RevisionDate = item.CurrentRevisionDate, Format = format, StagedPath = staged, FinalPath = finalPath,
                Status = ExportItemStatus.SUCCESS, RetryCount = retry, DurationSeconds = duration, FileSizeBytes = new FileInfo(finalPath).Length, Message = message };
        }

        private static ExportItemResult FailedResult(SheetExportItem item, ExportFormat format, string finalPath, double duration, Exception ex,
            ExportItemStatus status = ExportItemStatus.FAILED_EXPORT, int retry = 0)
        {
            return new ExportItemResult { SheetId = item?.SheetId == null ? "" : item.SheetId.ToString(), SheetUniqueId = item?.SheetUniqueId ?? "", SheetNumber = item?.SheetNumber ?? "",
                RevisionNumber = item?.CurrentRevisionNumber ?? "", RevisionDate = item?.CurrentRevisionDate ?? "", Format = format, FinalPath = finalPath,
                Status = status, RetryCount = retry, DurationSeconds = duration, Message = ex.Message };
        }

        private static ExportItemStatus FailureStatus(Exception ex)
        {
            if (ex is ExportOutputMissingException)
                return ex.Message.IndexOf("OUTPUT_EMPTY", StringComparison.OrdinalIgnoreCase) >= 0 ? ExportItemStatus.OUTPUT_EMPTY : ExportItemStatus.OUTPUT_MISSING;
            if (ex.Message.IndexOf("POST_PROCESS_FAILED", StringComparison.OrdinalIgnoreCase) >= 0) return ExportItemStatus.POST_PROCESS_FAILED;
            if (ex.Message.IndexOf("PUBLISH_FAILED", StringComparison.OrdinalIgnoreCase) >= 0) return ExportItemStatus.PUBLISH_FAILED;
            return ExportItemStatus.FAILED_EXPORT;
        }

        private static void MarkCancelledOutputs(List<SheetExportItem> items, ExportJobOptions options, ExportBatchResult batch)
        {
            if (!batch.Cancelled) return;
            if (options.ExportPdf && options.CombinePdf && !batch.Results.Any(result => result.Format == ExportFormat.PDF))
                batch.Results.Add(new ExportItemResult { SheetUniqueId = "", SheetNumber = "(combined)", Format = ExportFormat.PDF, Status = ExportItemStatus.CANCELLED, Message = "CANCELLED" });
            foreach (SheetExportItem item in items)
            {
                if (options.ExportPdf && !options.CombinePdf && !batch.Results.Any(result => result.SheetUniqueId == item.SheetUniqueId && result.Format == ExportFormat.PDF))
                    batch.Results.Add(new ExportItemResult { SheetUniqueId = item.SheetUniqueId, SheetNumber = item.SheetNumber, Format = ExportFormat.PDF, Status = ExportItemStatus.CANCELLED, Message = "CANCELLED" });
                if (options.ExportDwg && !batch.Results.Any(result => result.SheetUniqueId == item.SheetUniqueId && result.Format == ExportFormat.DWG))
                    batch.Results.Add(new ExportItemResult { SheetUniqueId = item.SheetUniqueId, SheetNumber = item.SheetNumber, Format = ExportFormat.DWG, Status = ExportItemStatus.CANCELLED, Message = "CANCELLED" });
            }
        }

        private static void FinalizeBatch(ExportBatchResult batch)
        {
            batch.SuccessfulOutputs = batch.Results.Count(result => result.Success);
            batch.LockedOutputs = batch.Results.Count(result => result.Status == ExportItemStatus.LOCKED);
            batch.FailedOutputs = batch.Results.Count(result => result.Status == ExportItemStatus.FAILED_EXPORT || result.Status == ExportItemStatus.OUTPUT_MISSING || result.Status == ExportItemStatus.OUTPUT_EMPTY || result.Status == ExportItemStatus.POST_PROCESS_FAILED || result.Status == ExportItemStatus.PUBLISH_FAILED);
            batch.SkippedOutputs = batch.Results.Count(result => result.Status == ExportItemStatus.SKIPPED || result.Status == ExportItemStatus.CANCELLED);
            batch.PartialSheets = batch.Results.GroupBy(result => result.SheetUniqueId).Count(group => group.Any(result => result.Success) && group.Any(result => !result.Success));
        }

        private static bool IsRetryable(Exception ex)
        {
            if (ex is FileLockedException || ex is NamingPlanException || ex is DwgSetupException || ex is ExportOutputMissingException || ex is AmbiguousExportOutputException) return false;
            if (ex.Message.IndexOf("POST_PROCESS_FAILED", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("PUBLISH_FAILED", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        private static bool IsCancelled(Func<bool> callback) { return callback != null && callback(); }
        private static void Report(Action<ExportProgress> callback, string message, SheetExportItem item, ExportFormat format, string stage, int current, int total)
        {
            if (callback == null) return;
            callback(new ExportProgress { Current = current, Total = total, Message = message ?? "", SheetNumber = item?.SheetNumber ?? "", Format = format, Stage = stage ?? "" });
        }
        private static string SafeFolder(string value) { string safe = NamingPlanService.Sanitize(value); return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe; }
    }
}
