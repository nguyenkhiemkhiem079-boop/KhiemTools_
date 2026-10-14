using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterExecutionService
    {
        public static FilterManagerResult Execute(Document doc, FilterCopyPlan plan)
        {
            var batchTimer = System.Diagnostics.Stopwatch.StartNew();
            var result = new FilterManagerResult { Status = FilterManagerStatusCode.READY, RequestedTargets = plan == null ? 0 : plan.Targets.Count };
            if (doc == null || plan == null) { batchTimer.Stop(); result.Status = FilterManagerStatusCode.PREFLIGHT_FAILED; result.Message = "Document or plan is unavailable."; result.Diagnostics.Add(WorkflowDiagnostic.Error("FILTER_PLAN_UNAVAILABLE", result.Message)); result.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdFilterManager", "COPY_FILTERS", "planUnavailable=true", WorkflowExecutionState.VALIDATION_FAILURE, result.Outcome, 0, 1, "FILTER_PLAN_UNAVAILABLE", null, batchTimer.Elapsed); return result; }
            IList<FilterManagerStatus> checks = FilterPreflightService.Validate(doc, plan);
            if (checks.Any(x => x.IsError)) { batchTimer.Stop(); result.Status = FilterManagerStatusCode.PREFLIGHT_FAILED; result.Message = string.Join("; ", checks.Where(x => x.IsError).Select(x => x.Code + ": " + x.Message)); result.Diagnostics.Add(WorkflowDiagnostic.Error("FILTER_PREFLIGHT_FAILED", result.Message)); result.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdFilterManager", "COPY_FILTERS", "targets=" + result.RequestedTargets, WorkflowExecutionState.VALIDATION_FAILURE, result.Outcome, result.RequestedTargets, checks.Count(x => x.IsError), "FILTER_PREFLIGHT_FAILED", DocumentIdentity.From(doc).StableKey, batchTimer.Elapsed); return result; }
            foreach (FilterTargetPlan targetPlan in plan.Targets)
            {
                var targetResult = new FilterTargetResult { TargetViewId = targetPlan.TargetViewId, Status = targetPlan.Status };
                if (targetPlan.TemplateControlled && !plan.Options.RedirectTemplateControlledTarget) { targetResult.Status = FilterManagerStatusCode.TARGET_FILTERS_CONTROLLED_BY_TEMPLATE; targetResult.Message = targetPlan.Message; result.FailedTargets++; result.Targets.Add(targetResult); continue; }
                if (targetPlan.Status == FilterManagerStatusCode.NO_CHANGE) { targetResult.Status = FilterManagerStatusCode.NO_CHANGE; targetResult.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdFilterManager", "COPY_FILTERS", "targetView=" + targetPlan.TargetViewId, WorkflowExecutionState.NO_CHANGE, targetResult.Outcome, 1, 0, "FILTER_STATE_ALREADY_MATCHES", DocumentIdentity.From(doc).StableKey); result.NoChangeTargets++; result.Targets.Add(targetResult); continue; }
                ElementId mutationId = targetPlan.MutationViewId == ElementId.InvalidElementId ? targetPlan.TargetViewId : targetPlan.MutationViewId;
                View target = doc.GetElement(mutationId) as View;
                if (target == null) { targetResult.Status = FilterManagerStatusCode.STALE_SYNC_PLAN; targetResult.Message = "Target view no longer exists."; result.FailedTargets++; result.Targets.Add(targetResult); continue; }
                Transaction transaction = null;
                bool transactionStarted = false;
                bool verificationPassed = false;
                TransactionStatus? transactionStatus = null;
                Exception failure = null;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    transaction = new Transaction(doc, "K-TOOLS Filter Manager - " + target.Name);
                    KhimTools.Core.Revit.TransactionBoundary.Start(transaction, "FilterManager target " + target.Name);
                    transactionStarted = true;
                    foreach (ElementId removeId in targetPlan.RemoveFilterIds) if (ViewFilterApiAdapter.IsApplied(target, removeId)) { ViewFilterApiAdapter.RemoveFilter(target, removeId); targetResult.RemovedCount++; }
                    foreach (AppliedFilterState source in plan.SourceFilters)
                    {
                        FilterDefinitionInfo definition = FilterCollectorService.Describe(doc, source.FilterId);
                        if (definition == null || definition.DefinitionType == FilterDefinitionType.Unsupported) throw new InvalidOperationException("UNSUPPORTED_FILTER_TYPE: " + source.Name);
                        bool applied = ViewFilterApiAdapter.IsApplied(target, source.FilterId);
                        if (!applied && plan.Options.AddMissingFilters) { ViewFilterApiAdapter.AddFilter(target, source.FilterId); targetResult.AppliedCount++; }
                        if (!applied && !plan.Options.AddMissingFilters) { targetResult.Message += "ADD_FILTER_REQUIRED; "; continue; }
                        if (plan.Options.CopyVisibility) ViewFilterApiAdapter.SetVisibility(target, source.FilterId, source.Visible);
                        if (plan.Options.CopyEnabled)
                        {
                            bool supported;
                            if (!ViewFilterApiAdapter.SetEnabled(target, source.FilterId, source.Enabled, out supported) && source.EnabledStateSupported && !supported) targetResult.Message += "FILTER_ENABLED_STATE_UNSUPPORTED; ";
                        }
                        if (plan.Options.ClearOverrides) ViewFilterApiAdapter.SetOverrides(target, source.FilterId, new OverrideGraphicSettings());
                        else if (plan.Options.CopyGraphicOverrides) ViewFilterApiAdapter.SetOverrides(target, source.FilterId, source.Overrides == null ? null : source.Overrides.Overrides);
                    }
                    if (plan.Options.CopyOrder)
                    {
                        var ordered = plan.SourceFilters.OrderBy(x => x.OrderIndex).Select(x => x.FilterId).ToList();
                        foreach (ElementId id in ViewFilterApiAdapter.GetAppliedFilters(target)) if (!ordered.Contains(id)) ordered.Add(id);
                        if (!ViewFilterApiAdapter.SetOrder(target, ordered)) targetResult.Message += "FILTER_ORDER_WRITE_UNSUPPORTED; ";
                    }
                    verificationPassed = FilterVerificationService.VerifyTarget(doc, targetPlan, plan) && FilterVerificationService.VerifySourceUnchanged(doc, plan);
                    if (!verificationPassed) throw new InvalidOperationException("POST_VERIFY_FAILED: target filter state or source fingerprint does not match the plan.");
                    KhimTools.Core.Revit.TransactionBoundary.Commit(transaction, "FilterManager target " + target.Name);
                    transactionStatus = transaction.GetStatus();
                    if (transactionStatus != TransactionStatus.Committed) throw new InvalidOperationException("TRANSACTION_COMMIT_FAILED: Filter Manager target transaction was not committed.");
                    targetResult.Status = FilterManagerStatusCode.APPLIED; result.AppliedTargets++;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(transaction, "FilterManager target " + target.Name);
                        transactionStatus = transaction.GetStatus();
                    }
                    targetResult.Status = ex.Message.StartsWith("POST_VERIFY_FAILED", StringComparison.Ordinal) ? FilterManagerStatusCode.POST_VERIFY_FAILED : FilterManagerStatusCode.FAILED;
                    targetResult.Message = ex.Message; result.FailedTargets++;
                }
                finally { if (transaction != null) transaction.Dispose(); }
                timer.Stop();
                var recordState = targetResult.Status == FilterManagerStatusCode.APPLIED ? WorkflowExecutionState.SUCCESS : WorkflowExecutionRecord.Classify(failure);
                targetResult.ExecutionDiagnostics = WorkflowExecutionRecord.Create("FilterManager", "CopyFilters", "targetView=" + targetPlan.TargetViewId,
                    recordState, targetResult.Outcome, transactionStatus,
                    targetResult.Status == FilterManagerStatusCode.APPLIED ? WorkflowPostconditionState.PASSED :
                        (verificationPassed ? WorkflowPostconditionState.PASSED : WorkflowPostconditionState.FAILED),
                    "target-filter-state-and-source-fingerprint", 1, targetResult.Status == FilterManagerStatusCode.APPLIED ? 1 : 0,
                    0, targetResult.Status == FilterManagerStatusCode.APPLIED ? 0 : 1, timer.Elapsed,
                    transactionStarted, transactionStatus == TransactionStatus.RolledBack,
                    targetResult.Status != FilterManagerStatusCode.APPLIED && transactionStatus == TransactionStatus.RolledBack, failure, DocumentIdentity.From(doc).StableKey);
                if (targetResult.Status == FilterManagerStatusCode.POST_VERIFY_FAILED)
                    targetResult.Diagnostics.Add(WorkflowDiagnostic.Error("POST_VERIFY_FAILED", targetResult.Message, false, context: "FilterManager"));
                result.Targets.Add(targetResult);
            }
            batchTimer.Stop();
            result.Status = result.FailedTargets > 0 && result.AppliedTargets == 0 ? FilterManagerStatusCode.FAILED : (result.FailedTargets > 0 ? FilterManagerStatusCode.PARTIAL : FilterManagerStatusCode.APPLIED);
            if (result.AppliedTargets > 0)
                result.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdFilterManager", "COPY_FILTERS", "targets=" + result.RequestedTargets, WorkflowExecutionState.SUCCESS, result.Outcome, TransactionStatus.Committed, WorkflowPostconditionState.PASSED, "each applied view and source fingerprint verified", result.RequestedTargets, result.AppliedTargets, 0, result.FailedTargets, batchTimer.Elapsed, true, false, false, null, DocumentIdentity.From(doc).StableKey);
            else if (result.FailedTargets == 0)
                result.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdFilterManager", "COPY_FILTERS", "targets=" + result.RequestedTargets, WorkflowExecutionState.NO_CHANGE, WorkflowOutcome.NoChange, result.RequestedTargets, 0, "FILTER_NO_TARGET_MUTATION", DocumentIdentity.From(doc).StableKey, batchTimer.Elapsed);
            else result.Diagnostics.Add(WorkflowDiagnostic.Error("FILTER_TARGET_FAILURE", "No target filter copy completed successfully.", false, context: "FilterManager"));
            return result;
        }
    }
}
