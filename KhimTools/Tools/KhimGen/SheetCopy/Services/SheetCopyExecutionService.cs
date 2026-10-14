using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Workflow;
using KhimTools.SheetCopy.Models;
using KhimTools.ScheduleSplit.Services;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyExecutionService
    {
        public static SheetCopyBatchResult Execute(Document doc, SheetCopyPlan plan)
        {
            var batch = new SheetCopyBatchResult { Requested = plan == null || plan.Items == null ? 0 : plan.Items.Count };
            if (doc == null || plan == null) return batch;
            List<SheetCopyDiagnostic> diagnostics = SheetCopyPreflightService.Validate(doc, plan);
            if (SheetCopyPreflightService.IsStale(doc, plan))
            {
                plan.IsStale = true;
                foreach (SheetCopyItem item in plan.Items) item.Status = SheetCopyStatusCode.STALE_COPY_PLAN;
                batch.Blocked = batch.Requested;
                return batch;
            }
            batch.Ready = plan.Items.Count(i => i.Status == SheetCopyStatusCode.READY);
            using (var group = new TransactionGroup(doc, "K-TOOLS Sheet Copy batch"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "SheetCopy batch");
                foreach (SheetCopyItem item in plan.Items.OrderBy(i => i.SourceSheetNumber, StringComparer.OrdinalIgnoreCase))
                {
                    if (item.Status != SheetCopyStatusCode.READY)
                    {
                        batch.Blocked++;
                        batch.Results.Add(new SheetCopyExecutionResult { SourceSheetId = item.SourceSheetId, SourceSheetNumber = item.SourceSheetNumber, TargetSheetNumber = item.TargetSheetNumber, Status = item.Status, Messages = { item.Message } });
                        continue;
                    }
                    SheetCopyExecutionResult result = ExecuteOne(doc, item, plan.Options);
                    batch.Results.Add(result);
                    if (result.Status == SheetCopyStatusCode.CREATED) batch.Created++;
                    else if (result.Status == SheetCopyStatusCode.PARTIAL) batch.Partial++;
                    else batch.Failed++;
                }
                KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "SheetCopy batch");
            }
            return batch;
        }

        public static SheetCopyExecutionResult ExecuteOne(Document doc, SheetCopyItem item, SheetCopyOptions options)
        {
            var result = new SheetCopyExecutionResult { SourceSheetId = item.SourceSheetId, SourceSheetNumber = item.SourceSheetNumber, TargetSheetNumber = item.TargetSheetNumber };
            Stopwatch timer = Stopwatch.StartNew();
            Exception failureException = null;
            using (var transaction = new Transaction(doc, "Copy Sheet " + item.TargetSheetNumber))
            {
                try
                {
                    KhimTools.Core.Revit.TransactionBoundary.Start(transaction, "SheetCopy target " + item.TargetSheetNumber); result.TransactionStarted = true;
                    ViewSheet source = doc.GetElement(item.SourceSheetId) as ViewSheet;
                    if (source == null) throw new InvalidOperationException("Source Sheet no longer exists.");
                    ElementId titleBlockTypeId = item.SourceHasTitleBlock ? item.SourceTitleBlockTypeId : ElementId.InvalidElementId;
                    ViewSheet target = ViewSheet.Create(doc, titleBlockTypeId);
                    result.TargetSheetId = target.Id;
                    target.SheetNumber = item.TargetSheetNumber.Trim();
                    target.Name = item.TargetSheetName ?? string.Empty;
                    if (options.CopySafeSheetParameters) result.ParameterResults.AddRange(SheetCopyParameterService.CopySafeParameters(source, target));
                    CopyTitleBlockParameters(doc, source, target, options, result);
                    var viewMap = new Dictionary<ElementId, ElementId>();
                    foreach (SheetCopyContentItem content in item.Contents.Where(c => c.ContentKind == SheetCopyContentKind.NORMAL_VIEWPORT || c.ContentKind == SheetCopyContentKind.LEGEND_VIEWPORT)
                        .OrderBy(c => c.BoxCenter == null ? double.MaxValue : c.BoxCenter.Y).ThenBy(c => c.BoxCenter == null ? double.MaxValue : c.BoxCenter.X).ThenBy(c => c.SourceElementId.ToLongValue()))
                    {
                        if (content.Action == SheetCopyAction.SKIP) continue;
                        View sourceView = doc.GetElement(content.SourceViewId) as View;
                        if (sourceView == null) throw new InvalidOperationException("View is missing: " + content.SourceName);
                        ElementId targetViewId = content.ContentKind == SheetCopyContentKind.LEGEND_VIEWPORT && options.ReuseLegends
                            ? sourceView.Id : DuplicateView(doc, sourceView, options.ViewPolicy, options.ApplyKToolsViewNaming);
                        View targetView = doc.GetElement(targetViewId) as View;
                        if (targetView == null) throw new InvalidOperationException("Target View could not be resolved.");
                        if (!Viewport.CanAddViewToSheet(doc, target.Id, targetViewId)) throw new InvalidOperationException("Viewport placement is unsupported for " + content.SourceName);
                        Viewport targetViewport = Viewport.Create(doc, target.Id, targetViewId, content.BoxCenter ?? XYZ.Zero);
                        result.CreatedViewIds.Add(targetViewId);
                        result.CreatedViewportIds.Add(targetViewport.Id);
                        content.TargetViewId = targetViewId; content.TargetElementId = targetViewport.Id;
                        viewMap[content.SourceViewId] = targetViewId;
                        if (options.PreserveViewportType && content.ViewportTypeId != null && content.ViewportTypeId != ElementId.InvalidElementId)
                        {
                            try { targetViewport.ChangeTypeId(content.ViewportTypeId); } catch (Exception ex) { result.Messages.Add("Viewport type not preserved: " + ex.Message); }
                        }
                        RestoreViewportGeometry(targetViewport, content, result);
                        RestoreDetailNumber(targetViewport, content, result);
                        try { targetViewport.Pinned = content.IsPinned; } catch (Exception ex) { result.Messages.Add("Viewport pin state not preserved: " + ex.Message); }
                    }
                    foreach (SheetCopyContentItem schedule in item.Contents.Where(c => c.ContentKind == SheetCopyContentKind.SCHEDULE && c.Action == SheetCopyAction.REUSE).OrderBy(c => c.SourceElementId.ToLongValue()))
                    {
                        if (schedule.IsSegmented)
                        {
                            if (schedule.BoxCenter == null || schedule.SegmentIndex < 0) { result.Messages.Add("SEGMENTED_SCHEDULE_DEFERRED: " + schedule.SourceName); continue; }
                            ScheduleSheetInstance segmented = ScheduleSegmentPlacementService.PlaceSegment(doc, target.Id, schedule.SourceViewId, schedule.BoxCenter, schedule.SegmentIndex);
                            result.CreatedScheduleInstanceIds.Add(segmented.Id); schedule.TargetElementId = segmented.Id; continue;
                        }
                        if (schedule.BoxCenter == null) { result.Messages.Add("Schedule position unavailable: " + schedule.SourceName); continue; }
                        ScheduleSheetInstance instance = ScheduleSheetInstance.Create(doc, target.Id, schedule.SourceViewId, schedule.BoxCenter);
                        result.CreatedScheduleInstanceIds.Add(instance.Id); schedule.TargetElementId = instance.Id;
                    }
                    if (options.CopySafeAnnotations)
                    {
                        List<ElementId> annotationIds = item.Contents.Where(c => c.ContentKind == SheetCopyContentKind.SHEET_ANNOTATION && c.Action == SheetCopyAction.COPY).Select(c => c.SourceElementId).ToList();
                        if (annotationIds.Count > 0)
                        {
                            ICollection<ElementId> copied = ElementTransformUtils.CopyElements(source, annotationIds, target, Transform.Identity, new CopyPasteOptions());
                            result.CopiedAnnotationIds.AddRange(copied);
                        }
                    }
                    result.VerificationAttempted = true;
                    if (!SheetCopyVerificationService.Verify(doc, source, target, item, result, out string verifyMessage))
                        throw new InvalidOperationException(verifyMessage);
                    KhimTools.Core.Revit.TransactionBoundary.Commit(transaction, "SheetCopy target " + item.TargetSheetNumber);
                    result.TransactionResult = transaction.GetStatus();
                    result.VerificationPassed = true;
                    result.Status = result.Messages.Any(m => m.IndexOf("deferred", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("not preserved", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? SheetCopyStatusCode.PARTIAL : SheetCopyStatusCode.CREATED;
                }
                catch (Exception ex)
                {
                    failureException = ex;
                    KhimTools.Core.Revit.TransactionBoundary.RollBack(transaction, "SheetCopy target " + item.TargetSheetNumber);
                    result.TransactionResult = transaction.GetStatus();
                    result.RollbackVerified = result.TransactionResult == TransactionStatus.RolledBack;
                    result.Status = SheetCopyStatusCode.FAILED;
                    result.ExceptionType = ex.GetType().FullName;
                    result.Messages.Add(ex.Message);
                }
            }
            timer.Stop(); result.Duration = timer.Elapsed;
            int affected = result.TransactionResult == TransactionStatus.Committed ? result.CreatedViewIds.Concat(result.CreatedViewportIds).Concat(result.CreatedScheduleInstanceIds).Concat(result.CopiedAnnotationIds).Distinct().Count() + (result.TargetSheetId == ElementId.InvalidElementId ? 0 : 1) : 0;
            WorkflowExecutionState state = result.Outcome == WorkflowOutcome.Succeeded || result.Outcome == WorkflowOutcome.Partial ? WorkflowExecutionState.SUCCESS : failureException != null ? WorkflowExecutionRecord.Classify(failureException) : WorkflowExecutionState.VALIDATION_FAILURE;
            result.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdSheetCopy", "COPY_SHEET", "source=" + item.SourceSheetNumber + ";target=" + item.TargetSheetNumber + ";contents=" + item.Contents.Count, state, result.Outcome, result.TransactionResult, result.VerificationAttempted ? (result.VerificationPassed ? WorkflowPostconditionState.PASSED : WorkflowPostconditionState.FAILED) : WorkflowPostconditionState.NOT_RUN, result.VerificationPassed ? "Sheet, content and source-preservation verification passed." : string.Join(";", result.Messages), item.Contents.Count, affected, result.Messages.Count, result.Status == SheetCopyStatusCode.FAILED ? 1 : 0, result.Duration, result.TransactionStarted, result.RollbackVerified, result.TransactionResult == TransactionStatus.RolledBack || !result.TransactionStarted, failureException, DocumentIdentity.From(doc).StableKey);
            Debug.WriteLine("[K-TOOLS][SheetCopy] source=" + item.SourceSheetNumber + ", target=" + item.TargetSheetNumber + ", status=" + result.Status + ", views=" + result.CreatedViewIds.Count + ", legends=" + item.LegendCount + ", schedules=" + result.CreatedScheduleInstanceIds.Count + ", annotations=" + result.CopiedAnnotationIds.Count + ", duration=" + result.Duration);
            return result;
        }

        private static ElementId DuplicateView(Document doc, View sourceView, ViewCopyPolicy policy, bool applyNaming)
        {
            ViewDuplicateOption option = SheetCopyPreflightService.ToDuplicateOption(policy);
            if (!sourceView.CanViewBeDuplicated(option)) throw new InvalidOperationException(SheetCopyStatusCode.VIEW_DUPLICATION_UNSUPPORTED.ToString() + ": " + sourceView.Name);
            ElementId id = sourceView.Duplicate(option);
            View copy = doc.GetElement(id) as View;
            if (copy != null && applyNaming) copy.Name = SheetCopyNamingService.SuggestViewName(doc, sourceView.Name);
            return id;
        }

        private static void RestoreViewportGeometry(Viewport target, SheetCopyContentItem source, SheetCopyExecutionResult result)
        {
            try { if (source.LabelOffset != null) target.LabelOffset = source.LabelOffset; } catch (Exception ex) { result.Messages.Add("Viewport title offset not preserved: " + ex.Message); }
            try { if (source.LabelLineLength > 0) target.LabelLineLength = source.LabelLineLength; } catch (Exception ex) { result.Messages.Add("Viewport title length not preserved: " + ex.Message); }
            try
            {
                PropertyInfo rotation = typeof(Viewport).GetProperty("Rotation", BindingFlags.Public | BindingFlags.Instance);
                if (rotation != null && rotation.CanWrite && source.ViewportRotation != null) rotation.SetValue(target, source.ViewportRotation, null);
            }
            catch (Exception ex) { result.Messages.Add("Viewport rotation limitation: " + ex.Message); }
        }

        private static void RestoreDetailNumber(Viewport target, SheetCopyContentItem source, SheetCopyExecutionResult result)
        {
            if (string.IsNullOrWhiteSpace(source.DetailNumber)) return;
            try
            {
                Parameter parameter = target.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (parameter == null || parameter.IsReadOnly) { result.Messages.Add("Detail number is read-only: " + source.DetailNumber); return; }
                parameter.Set(source.DetailNumber);
            }
            catch (Exception ex) { result.Messages.Add("Detail number restore failed: " + ex.Message); }
        }

        private static void CopyTitleBlockParameters(Document doc, ViewSheet source, ViewSheet target, SheetCopyOptions options, SheetCopyExecutionResult result)
        {
            if (!options.CopySafeTitleBlockParameters) return;
            List<FamilyInstance> sourceBlocks = new FilteredElementCollector(doc, source.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            List<FamilyInstance> targetBlocks = new FilteredElementCollector(doc, target.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            if (sourceBlocks.Count != 1 || targetBlocks.Count != 1) return;
            result.ParameterResults.AddRange(SheetCopyParameterService.CopySafeParameters(sourceBlocks[0], targetBlocks[0]));
        }
    }
}
