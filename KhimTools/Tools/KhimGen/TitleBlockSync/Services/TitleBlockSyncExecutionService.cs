using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;
using KhimTools.Core.Workflow;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockSyncExecutionService
    {
        public static TitleBlockSyncBatchResult Execute(Document doc, TitleBlockSyncPlan plan)
        {
            var batch = new TitleBlockSyncBatchResult { Requested = plan == null ? 0 : plan.Targets.Count };
            if (doc == null || plan == null) { batch.Failed++; return batch; }
            Stopwatch batchTimer = Stopwatch.StartNew();
            IList<TitleBlockSyncPreflightResult> checks = TitleBlockSyncPreflightService.Validate(doc, plan);
            if (plan.IsStale || checks.Any(x => x.Status == TitleBlockSyncStatusCode.STALE_SYNC_PLAN)) { batchTimer.Stop(); batch.Blocked = plan.Targets.Count; foreach (TitleBlockTargetPlan t in plan.Targets) batch.Results.Add(FailedResult(plan, t, TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Plan is stale.")); batch.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdTitleBlockSync", "SYNC_TITLE_BLOCKS", "targets=" + batch.Requested, WorkflowExecutionState.VALIDATION_FAILURE, batch.Outcome, batch.Requested, batch.Blocked, "TITLE_BLOCK_SYNC_STALE_PLAN", DocumentIdentity.From(doc).StableKey, batchTimer.Elapsed); return batch; }
            TransactionGroup group = null; bool groupStarted = false; TransactionStatus? groupStatus = null;
            try
            {
                group = new TransactionGroup(doc, "K-TOOLS Title Block Sync 3.3"); KhimTools.Core.Revit.TransactionBoundary.Start(group, "TitleBlockSync batch"); groupStarted = true;
                foreach (TitleBlockTargetPlan target in plan.Targets)
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    var result = new TitleBlockSyncExecutionResult { SourceSheetId = plan.SourceSheetId, TargetSheetId = target.TargetSheetId, TargetSheetNumber = target.TargetSheetNumber, OldTypeId = target.OldTypeId, NewTypeId = target.NewTypeId, Status = TitleBlockSyncStatusCode.READY };
                    IList<TitleBlockSyncPreflightResult> targetChecks = ChecksFor(checks, target.TargetSheetId);
                    bool hardBlock = !plan.Options.AllowPartialTarget && targetChecks.Any(c => c.Severity == TitleBlockSyncSeverity.ERROR);
                    if (hardBlock || target.Status == TitleBlockSyncStatusCode.TARGET_TITLEBLOCK_MISSING || target.Status == TitleBlockSyncStatusCode.TARGET_MULTIPLE_TITLEBLOCKS)
                    { result.Status = target.Status == TitleBlockSyncStatusCode.READY ? TitleBlockSyncStatusCode.SKIPPED : target.Status; result.Messages.Add("Target blocked by preflight."); batch.Blocked++; batch.Results.Add(result); continue; }
                    Transaction transaction = new Transaction(doc, "Title Block Sync - " + target.TargetSheetNumber);
                    try
                    {
                        KhimTools.Core.Revit.TransactionBoundary.Start(transaction, "TitleBlockSync target " + target.TargetSheetNumber);
                        ViewSheet sheet = doc.GetElement(target.TargetSheetId) as ViewSheet;
                        var blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet);
                        if (blocks.Count != 1) throw new InvalidOperationException("Target must have exactly one title block at execution time.");
                        FamilyInstance block = blocks[0];
                        XYZ originalPosition = (block.Location as LocationPoint) == null ? null : ((LocationPoint)block.Location).Point;
                        if (plan.Options.SyncTitleBlockType && target.TypeChange)
                        {
                            ElementId changed = block.ChangeTypeId(plan.SourceTitleBlockTypeId);
                            doc.Regenerate();
                            blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet);
                            if (blocks.Count != 1) throw new InvalidOperationException("Title block could not be re-resolved after ChangeTypeId.");
                            block = blocks[0]; result.TypeChanged = changed != null && changed != ElementId.InvalidElementId; batch.TypeChanges += result.TypeChanged ? 1 : 0;
                            if (plan.Options.PreserveTargetPosition && originalPosition != null)
                            {
                                XYZ after = (block.Location as LocationPoint) == null ? null : ((LocationPoint)block.Location).Point;
                                if (after == null || after.DistanceTo(originalPosition) > 0.001) throw new InvalidOperationException("Target title block position changed unexpectedly.");
                            }
                        }
                        ApplyParameters(doc, plan, target, block, sheet, result);
                        doc.Regenerate();
                        string verifyMessage; if (!TitleBlockSyncVerificationService.VerifyTarget(doc, target, plan, out verifyMessage)) throw new InvalidOperationException("POST_VERIFY_FAILED: " + verifyMessage);
                        KhimTools.Core.Revit.TransactionBoundary.Commit(transaction, "TitleBlockSync target " + target.TargetSheetNumber);
                        result.Status = result.TypeChanged || result.ParameterResults.Any(p => p.Changed) ? TitleBlockSyncStatusCode.SYNCED : TitleBlockSyncStatusCode.NO_CHANGE;
                        result.Messages.Add(verifyMessage); timer.Stop(); result.Duration = timer.Elapsed; batch.Results.Add(result); if (result.Status == TitleBlockSyncStatusCode.SYNCED) batch.Synced++; else batch.NoChange++;
                    }
                    catch (Exception ex)
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(transaction, "TitleBlockSync target " + target.TargetSheetNumber);
                        result.Status = TitleBlockSyncStatusCode.FAILED; result.Messages.Add(ex.Message); timer.Stop(); result.Duration = timer.Elapsed; batch.Failed++; batch.Results.Add(result);
                    }
                }
                string sourceMessage;
                if (!TitleBlockSyncVerificationService.VerifySourceUnchanged(doc, plan, out sourceMessage))
                    throw new InvalidOperationException("POST_VERIFY_FAILED: " + sourceMessage);
                if (groupStarted && group.GetStatus() == TransactionStatus.Started)
                {
                    if (batch.Synced == 0 && batch.NoChange == 0)
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "TitleBlockSync empty batch");
                    else KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "TitleBlockSync batch");
                }
            }
            catch (Exception ex)
            {
                bool rolledBack = false;
                if (groupStarted && group.GetStatus() == TransactionStatus.Started)
                {
                    KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "TitleBlockSync batch");
                    rolledBack = group.GetStatus() == TransactionStatus.RolledBack;
                }
                if (rolledBack)
                {
                    batch.Failed = Math.Max(1, batch.Failed);
                    batch.Synced = 0; batch.NoChange = 0; batch.Partial = 0; batch.TypeChanges = 0;
                    foreach (TitleBlockSyncExecutionResult item in batch.Results.Where(x => x.Status == TitleBlockSyncStatusCode.SYNCED || x.Status == TitleBlockSyncStatusCode.PARTIAL))
                    { item.Status = TitleBlockSyncStatusCode.FAILED; item.Messages.Add("Batch rollback restored the pre-execution state."); }
                }
                else batch.Failed++;
                batch.Results.Add(FailedResult(plan, null, TitleBlockSyncStatusCode.FAILED, ex.Message));
            }
            finally { if (group != null) { groupStatus = group.GetStatus(); group.Dispose(); } }
            batchTimer.Stop();
            if (groupStatus == TransactionStatus.Committed && (batch.Outcome == WorkflowOutcome.Succeeded || batch.Outcome == WorkflowOutcome.Partial))
                batch.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdTitleBlockSync", "SYNC_TITLE_BLOCKS", "targets=" + batch.Requested, WorkflowExecutionState.SUCCESS, batch.Outcome, groupStatus, WorkflowPostconditionState.PASSED, "target values and source fingerprint", batch.Requested, batch.Synced, 0, batch.Failed + batch.Blocked, batchTimer.Elapsed, true, false, batch.Synced == 0, null, DocumentIdentity.From(doc).StableKey);
            else if (groupStatus == TransactionStatus.Committed && batch.Outcome == WorkflowOutcome.NoChange)
                batch.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdTitleBlockSync", "SYNC_TITLE_BLOCKS", "targets=" + batch.Requested, WorkflowExecutionState.NO_CHANGE, WorkflowOutcome.NoChange, groupStatus, WorkflowPostconditionState.NOT_APPLICABLE, "No target required synchronization.", batch.Requested, 0, 0, 0, batchTimer.Elapsed, true, false, true, null, DocumentIdentity.From(doc).StableKey);
            else if (groupStatus == TransactionStatus.RolledBack)
                batch.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdTitleBlockSync", "SYNC_TITLE_BLOCKS", "targets=" + batch.Requested, WorkflowExecutionState.KTOOL_FAILURE, WorkflowOutcome.RolledBack, groupStatus, WorkflowPostconditionState.FAILED, "batch rollback", batch.Requested, 0, 0, Math.Max(1, batch.Failed), batchTimer.Elapsed, true, true, true, null, DocumentIdentity.From(doc).StableKey);
            else if (groupStatus.HasValue)
                batch.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdTitleBlockSync", "SYNC_TITLE_BLOCKS", "targets=" + batch.Requested, WorkflowExecutionState.KTOOL_FAILURE, WorkflowOutcome.Failed, groupStatus, WorkflowPostconditionState.FAILED, "transaction group did not reach a successful terminal state", batch.Requested, 0, 0, Math.Max(1, batch.Failed), batchTimer.Elapsed, groupStarted, false, !groupStarted, null, DocumentIdentity.From(doc).StableKey);
            return batch;
        }

        private static void ApplyParameters(Document doc, TitleBlockSyncPlan plan, TitleBlockTargetPlan target, FamilyInstance block, ViewSheet sheet, TitleBlockSyncExecutionResult result)
        {
            ParameterTransferOptions options = new ParameterTransferOptions { OverwriteBlankSource = plan.Options.OverwriteBlankSource, AllowElementId = false, ProtectIdentity = true };
            foreach (ParameterSyncTargetPlan item in target.InstanceParameters) ApplyOne(plan, item, block, TitleBlockSyncScope.TITLE_BLOCK_INSTANCE, options, result);
            foreach (ParameterSyncTargetPlan item in target.SheetParameters) ApplyOne(plan, item, sheet, TitleBlockSyncScope.SHEET, options, result);
        }
        private static void ApplyOne(TitleBlockSyncPlan plan, ParameterSyncTargetPlan item, Element target, TitleBlockSyncScope scope, ParameterTransferOptions options, TitleBlockSyncExecutionResult result)
        {
            Parameter parameter = ParameterTransferService.FindMatchingParameter(target, item.Key);
            ParameterTransferResult applied = ParameterTransferService.TryApplyValue(parameter, item.NewValue, options);
            result.ParameterResults.Add(TitleBlockParameterService.ToResult(applied, scope));
            if (applied.Status != ParameterTransferStatus.COPIED && applied.Status != ParameterTransferStatus.NO_CHANGE && !plan.Options.AllowPartialTarget) throw new InvalidOperationException(TitleBlockParameterService.Map(applied.Status) + ": " + applied.Message);
        }
        private static IList<TitleBlockSyncPreflightResult> ChecksFor(IList<TitleBlockSyncPreflightResult> checks, ElementId id)
        { var list = new List<TitleBlockSyncPreflightResult>(); foreach (TitleBlockSyncPreflightResult check in checks) if (check.TargetSheetId == id) list.Add(check); return list; }
        private static TitleBlockSyncExecutionResult FailedResult(TitleBlockSyncPlan plan, TitleBlockTargetPlan target, TitleBlockSyncStatusCode status, string message)
        { return new TitleBlockSyncExecutionResult { SourceSheetId = plan.SourceSheetId, TargetSheetId = target == null ? ElementId.InvalidElementId : target.TargetSheetId, TargetSheetNumber = target == null ? string.Empty : target.TargetSheetNumber, Status = status, Messages = { message } }; }
    }
}
