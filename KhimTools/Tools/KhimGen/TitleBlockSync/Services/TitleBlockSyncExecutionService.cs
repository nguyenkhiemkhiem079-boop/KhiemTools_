using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockSyncExecutionService
    {
        public static TitleBlockSyncBatchResult Execute(Document doc, TitleBlockSyncPlan plan)
        {
            var batch = new TitleBlockSyncBatchResult { Requested = plan == null ? 0 : plan.Targets.Count };
            if (doc == null || plan == null) { batch.Failed++; return batch; }
            IList<TitleBlockSyncPreflightResult> checks = TitleBlockSyncPreflightService.Validate(doc, plan);
            if (plan.IsStale || checks.Any(x => x.Status == TitleBlockSyncStatusCode.STALE_SYNC_PLAN)) { batch.Blocked = plan.Targets.Count; foreach (TitleBlockTargetPlan t in plan.Targets) batch.Results.Add(FailedResult(plan, t, TitleBlockSyncStatusCode.STALE_SYNC_PLAN, "Plan is stale.")); return batch; }
            TransactionGroup group = null; bool groupStarted = false;
            try
            {
                group = new TransactionGroup(doc, "K-TOOLS Title Block Sync 3.3"); group.Start(); groupStarted = true;
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
                        transaction.Start();
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
                        transaction.Commit();
                        result.Status = result.TypeChanged || result.ParameterResults.Any(p => p.Changed) ? TitleBlockSyncStatusCode.SYNCED : TitleBlockSyncStatusCode.NO_CHANGE;
                        result.Messages.Add(verifyMessage); timer.Stop(); result.Duration = timer.Elapsed; batch.Results.Add(result); if (result.Status == TitleBlockSyncStatusCode.SYNCED) batch.Synced++; else batch.NoChange++;
                    }
                    catch (Exception ex)
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                        result.Status = TitleBlockSyncStatusCode.FAILED; result.Messages.Add(ex.Message); timer.Stop(); result.Duration = timer.Elapsed; batch.Failed++; batch.Results.Add(result);
                    }
                }
                string sourceMessage; if (!TitleBlockSyncVerificationService.VerifySourceUnchanged(doc, plan, out sourceMessage)) { batch.Failed++; }
                if (groupStarted && group.GetStatus() == TransactionStatus.Started) group.Assimilate();
            }
            catch (Exception ex)
            {
                if (groupStarted && group.GetStatus() == TransactionStatus.Started) group.RollBack(); batch.Failed++; batch.Results.Add(FailedResult(plan, null, TitleBlockSyncStatusCode.FAILED, ex.Message));
            }
            finally { if (group != null) group.Dispose(); }
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
