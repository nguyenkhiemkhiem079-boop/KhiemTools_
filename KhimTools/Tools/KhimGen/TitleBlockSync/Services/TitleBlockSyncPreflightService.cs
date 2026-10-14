using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockSyncPreflightService
    {
        public static IList<TitleBlockSyncPreflightResult> Validate(Document doc, TitleBlockSyncPlan plan)
        {
            var results = new List<TitleBlockSyncPreflightResult>();
            if (doc == null || plan == null) { results.Add(Fail(TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Document or plan is missing.")); return results; }
            TitleBlockSyncStatusCode sourceStatus;
            TitleBlockInfo source = TitleBlockCollector.FindExplicit(doc, plan.SourceSheetId, plan.SourceTitleBlockId, out sourceStatus);
            if (source == null) { results.Add(Fail(sourceStatus, "Source cannot be resolved immediately before execution.")); plan.IsStale = true; plan.Status = sourceStatus; return results; }
            if (!string.Equals(source.Sheet.UniqueId, plan.SourceSheetUniqueId, StringComparison.Ordinal) || source.TitleBlock.GetTypeId() != plan.SourceTitleBlockTypeId || !string.Equals(TitleBlockCollector.RevisionFingerprint(source.Sheet), plan.SourceRevisionFingerprint, StringComparison.Ordinal))
            { results.Add(Fail(TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Source identity, type or revision state changed.")); plan.IsStale = true; plan.Status = TitleBlockSyncStatusCode.STALE_SYNC_PLAN; return results; }
            string currentFingerprint = TitleBlockSyncPlanner.ComputeCurrentFingerprint(doc, plan);
            if (string.IsNullOrEmpty(currentFingerprint) || !string.Equals(currentFingerprint, plan.Fingerprint, StringComparison.Ordinal))
            { results.Add(Fail(TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Source, target or selected parameter values changed after Preview.")); plan.IsStale = true; plan.Status = TitleBlockSyncStatusCode.STALE_SYNC_PLAN; return results; }
            foreach (TitleBlockTargetPlan target in plan.Targets)
            {
                ViewSheet sheet = doc.GetElement(target.TargetSheetId) as ViewSheet;
                if (sheet == null || sheet.IsPlaceholder) { results.Add(Fail(TitleBlockSyncStatusCode.TARGET_SHEET_MISSING, "Target sheet is missing or placeholder.", target)); continue; }
                var blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet);
                if (blocks.Count == 0) { results.Add(Fail(TitleBlockSyncStatusCode.TARGET_TITLEBLOCK_MISSING, "Target title block is missing.", target)); continue; }
                if (blocks.Count > 1) { results.Add(Fail(TitleBlockSyncStatusCode.TARGET_MULTIPLE_TITLEBLOCKS, "Target has multiple title blocks.", target)); continue; }
                if (target.TargetTitleBlockId != blocks[0].Id || target.OldTypeId != blocks[0].GetTypeId()) results.Add(Fail(TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Target identity or type changed.", target));
                ValidateParameters(results, doc, plan, target, blocks[0], sheet);
            }
            if (results.Exists(x => x.Status == TitleBlockSyncStatusCode.STALE_SYNC_PLAN)) plan.IsStale = true;
            return results;
        }

        private static void ValidateParameters(List<TitleBlockSyncPreflightResult> results, Document doc, TitleBlockSyncPlan plan, TitleBlockTargetPlan target, FamilyInstance block, ViewSheet sheet)
        {
            foreach (ParameterSyncTargetPlan item in target.InstanceParameters) ValidateParameter(results, plan, target, block, item, TitleBlockSyncScope.TITLE_BLOCK_INSTANCE);
            foreach (ParameterSyncTargetPlan item in target.SheetParameters) ValidateParameter(results, plan, target, sheet, item, TitleBlockSyncScope.SHEET);
        }
        private static void ValidateParameter(List<TitleBlockSyncPreflightResult> results, TitleBlockSyncPlan plan, TitleBlockTargetPlan target, Element element, ParameterSyncTargetPlan item, TitleBlockSyncScope scope)
        {
            Parameter parameter = ParameterTransferService.FindMatchingParameter(element, item.Key);
            var check = new TitleBlockSyncPreflightResult { TargetSheetId = target.TargetSheetId, TargetTitleBlockId = target.TargetTitleBlockId, Scope = scope, ParameterKey = item.Key, CanExecute = false, Severity = TitleBlockSyncSeverity.ERROR };
            if (parameter == null) { check.Status = TitleBlockSyncStatusCode.PARAMETER_MISSING; check.Message = "Target parameter is missing."; results.Add(check); return; }
            if (parameter.IsReadOnly) { check.Status = TitleBlockSyncStatusCode.PARAMETER_READ_ONLY; check.Message = "Target parameter is read-only."; results.Add(check); return; }
            if (parameter.StorageType == StorageType.ElementId) { check.Status = TitleBlockSyncStatusCode.UNSAFE_ELEMENT_REFERENCE; check.Message = "ElementId transfer is blocked by default."; results.Add(check); return; }
            if (TitleBlockCollector.IsProtectedSheetParameter(parameter) && scope == TitleBlockSyncScope.SHEET) { check.Status = TitleBlockSyncStatusCode.PARAMETER_PROTECTED; check.Message = "Sheet identity or revision parameter is protected."; results.Add(check); return; }
            if (TitleBlockCollector.IsProtectedTitleBlockParameter(parameter) && scope == TitleBlockSyncScope.TITLE_BLOCK_INSTANCE) { check.Status = TitleBlockSyncStatusCode.PARAMETER_PROTECTED; check.Message = "Title block system parameter is protected."; results.Add(check); return; }
            check.Status = TitleBlockSyncStatusCode.READY; check.Severity = TitleBlockSyncSeverity.INFO; check.CanExecute = true; check.Message = plan.Options.AllowPartialTarget ? "Compatible target parameter." : "Compatible target parameter; target remains atomic."; results.Add(check);
        }
        private static TitleBlockSyncPreflightResult Fail(TitleBlockSyncStatusCode status, string message, TitleBlockTargetPlan target = null)
        { return new TitleBlockSyncPreflightResult { Status = status, Severity = TitleBlockSyncSeverity.ERROR, Message = message, TargetSheetId = target == null ? ElementId.InvalidElementId : target.TargetSheetId, TargetTitleBlockId = target == null ? ElementId.InvalidElementId : target.TargetTitleBlockId }; }
    }
}
