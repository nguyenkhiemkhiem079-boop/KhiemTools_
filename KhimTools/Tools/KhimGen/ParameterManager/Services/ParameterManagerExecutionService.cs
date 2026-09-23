using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.Core.Workflow;
using KhimTools.Core;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerExecutionService
    {
        public static ParameterManagerResult Execute(Document doc, ParameterManagerPlan plan)
        {
            Stopwatch timer = Stopwatch.StartNew();
            var result = new ParameterManagerResult { RequestedTargets = plan == null ? 0 : plan.Items.Count, Status = ParameterManagerStatus.READY };
            if (doc == null || plan == null || plan.Request == null) { timer.Stop(); result.Status = ParameterManagerStatus.BLOCKED; result.Messages.Add("Plan is unavailable."); result.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdParameterManager", "UPDATE_PARAMETERS", "planUnavailable=true", WorkflowExecutionState.VALIDATION_FAILURE, result.Outcome, 0, 1, "PARAMETER_MANAGER_PLAN_UNAVAILABLE", null, timer.Elapsed); return result; }
            ParameterManagerOptions options = plan.Request.Options == null ? new ParameterManagerOptions() : plan.Request.Options.CreateExecutionOptions();
            ParameterManagerPreflightResult preflight = ParameterManagerPreflightService.Validate(doc, plan); if (!preflight.IsValid && options.AllOrNothing) { timer.Stop(); result.Status = preflight.Status; result.BlockedTargets = preflight.Errors.Count; result.ExecutionDiagnostics = WorkflowExecutionRecord.RecordNonMutation("CmdParameterManager", "UPDATE_PARAMETERS", "targets=" + result.RequestedTargets, WorkflowExecutionState.VALIDATION_FAILURE, result.Outcome, result.RequestedTargets, preflight.Errors.Count, "PARAMETER_MANAGER_PREFLIGHT", DocumentIdentity.From(doc).StableKey, timer.Elapsed); return result; }
            if (options.AllOrNothing) return ExecuteStrict(doc, plan, result, options, timer);
            bool itemPostconditionsPassed = true;
            TransactionStatus? batchStatus = null;
            using (var group = new TransactionGroup(doc, "K-TOOLS Parameter Manager 3.5"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "ParameterManager per-item batch");
                foreach (ParameterEditItem item in plan.Items)
                {
                    result.Items.Add(item);
                    if (!item.IsSelected) { item.Action = ParameterManagerAction.SKIP; item.Status = ParameterManagerStatus.SKIPPED; continue; }
                    if (item.Action != ParameterManagerAction.UPDATE || item.ProposedValue == null) { if (item.Action == ParameterManagerAction.NO_CHANGE) result.NoChangeTargets++; else result.BlockedTargets++; continue; }
                    result.ReadyTargets++;
                    Element target = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId); Parameter parameter = ParameterTransferService.FindMatchingParameter(target, plan.SelectedParameterKey);
                    if (target == null || parameter == null) { item.Status = ParameterManagerStatus.ELEMENT_MISSING; item.Action = ParameterManagerAction.FAILED; result.FailedTargets++; continue; }
                    try
                    {
                        using (var tx = new Transaction(doc, "Parameter Manager item " + item.ElementId.ToLongValue()))
                        {
                            try
                            {
                                KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ParameterManager target " + item.ElementId.ToLongValue());
                                ParameterTransferResult applied = ParameterTransferService.TryApplyValue(parameter, item.ProposedValue, new ParameterTransferOptions { AllowElementId = options.AllowElementId, ProtectIdentity = !options.AllowProtectedParameters });
                                if (applied.Status == ParameterTransferStatus.COPIED)
                                {
                                    item.Status = ParameterManagerStatus.UPDATED; item.Action = ParameterManagerAction.UPDATE;
                                    if (!ParameterManagerVerificationService.VerifyTarget(doc, plan, item))
                                    {
                                        KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager postcondition " + item.ElementId.ToLongValue());
                                        itemPostconditionsPassed = tx.GetStatus() == TransactionStatus.RolledBack;
                                        item.Status = ParameterManagerStatus.VERIFICATION_FAILED; item.Action = ParameterManagerAction.FAILED; item.Message = "POST_VERIFY_FAILED: parameter value differs from the planned value."; result.FailedTargets++;
                                        if (!itemPostconditionsPassed) item.Message += " Rollback status was not verified.";
                                    }
                                    else { KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ParameterManager target " + item.ElementId.ToLongValue()); result.UpdatedTargets++; }
                                }
                                else
                                {
                                    KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager target " + item.ElementId.ToLongValue());
                                    if (tx.GetStatus() != TransactionStatus.RolledBack) { itemPostconditionsPassed = false; throw new InvalidOperationException("TRANSACTION_ROLLBACK_FAILED: Parameter Manager item transaction did not roll back."); }
                                    item.Status = MapStatus(applied.Status); item.Action = ParameterEditItemAction(item.Status); result.BlockedTargets++; item.Message = applied.Message;
                                }
                            }
                            catch
                            {
                                if (tx.GetStatus() == TransactionStatus.Started) KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager failed item " + item.ElementId.ToLongValue());
                                throw;
                            }
                        }
                    }
                    catch (Exception ex) { item.Status = ParameterManagerStatus.FAILED; item.Action = ParameterManagerAction.FAILED; item.Message = ex.Message; result.FailedTargets++; }
                }
                if (result.UpdatedTargets == 0 && result.FailedTargets > 0)
                {
                    KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ParameterManager no-success batch");
                    batchStatus = group.GetStatus(); result.RolledBack = batchStatus == TransactionStatus.RolledBack;
                    result.Status = ParameterManagerStatus.ALL_OR_NOTHING_ROLLED_BACK;
                }
                else
                {
                    KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ParameterManager per-item batch");
                    batchStatus = group.GetStatus();
                }
            }
            result.VerificationPassed = !result.RolledBack && itemPostconditionsPassed && ParameterManagerVerificationService.Verify(doc, plan, result);
            if (!result.RolledBack) result.Status = result.FailedTargets > 0 ? ParameterManagerStatus.PARTIAL : result.UpdatedTargets == 0 && result.NoChangeTargets > 0 ? ParameterManagerStatus.NO_CHANGE : result.UpdatedTargets == 0 ? ParameterManagerStatus.BLOCKED : ParameterManagerStatus.UPDATED;
            timer.Stop();
            WorkflowExecutionState itemExecutionState = result.RolledBack ? WorkflowExecutionState.KTOOL_FAILURE :
                (result.Status == ParameterManagerStatus.NO_CHANGE ? WorkflowExecutionState.NO_CHANGE :
                (result.VerificationPassed && (result.Outcome == WorkflowOutcome.Succeeded || result.Outcome == WorkflowOutcome.Partial) ? WorkflowExecutionState.SUCCESS : WorkflowExecutionState.KTOOL_FAILURE));
            WorkflowOutcome diagnosticOutcome = result.RolledBack ? WorkflowOutcome.RolledBack : result.Outcome;
            result.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdParameterManager", "UPDATE_PARAMETERS", "targets=" + result.RequestedTargets, itemExecutionState,
                diagnosticOutcome, batchStatus, result.VerificationPassed ? WorkflowPostconditionState.PASSED : WorkflowPostconditionState.FAILED, "planned parameter values verified", result.RequestedTargets, result.UpdatedTargets, 0, result.FailedTargets + result.BlockedTargets, timer.Elapsed, true, result.RolledBack, result.RolledBack || result.UpdatedTargets == 0, null, DocumentIdentity.From(doc).StableKey);
            return result;
        }
        private static ParameterManagerResult ExecuteStrict(Document doc, ParameterManagerPlan plan, ParameterManagerResult result, ParameterManagerOptions options, Stopwatch timer)
        {
            TransactionStatus? groupStatus = null;
            using (var group = new TransactionGroup(doc, "K-TOOLS Parameter Manager strict"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "ParameterManager all-or-nothing group"); using (var tx = new Transaction(doc, "K-TOOLS Parameter Manager all-or-nothing"))
                {
                    try { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ParameterManager all-or-nothing transaction"); foreach (ParameterEditItem item in plan.Items.Where(i => i.IsSelected && i.Action == ParameterManagerAction.UPDATE)) { Element target = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId); Parameter p = ParameterTransferService.FindMatchingParameter(target, plan.SelectedParameterKey); ParameterTransferResult applied = ParameterTransferService.TryApplyValue(p, item.ProposedValue, new ParameterTransferOptions { AllowElementId = options.AllowElementId, ProtectIdentity = !options.AllowProtectedParameters }); if (applied.Status != ParameterTransferStatus.COPIED) throw new InvalidOperationException(applied.Status.ToString()); item.Status = ParameterManagerStatus.UPDATED; result.UpdatedTargets++; } if (!ParameterManagerVerificationService.Verify(doc, plan, result)) throw new InvalidOperationException("POST_VERIFY_FAILED: parameter batch differs from the planned values."); KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ParameterManager all-or-nothing transaction"); KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ParameterManager all-or-nothing group"); groupStatus = group.GetStatus(); result.Status = ParameterManagerStatus.UPDATED; }
                    catch (Exception ex) { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager all-or-nothing transaction"); KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ParameterManager all-or-nothing group"); groupStatus = group.GetStatus(); result.RolledBack = true; result.UpdatedTargets = 0; result.Status = ParameterManagerStatus.ALL_OR_NOTHING_ROLLED_BACK; result.Messages.Add(ex.Message); foreach (ParameterEditItem item in plan.Items.Where(i => i.Status == ParameterManagerStatus.UPDATED)) { item.Status = ParameterManagerStatus.FAILED; item.Action = ParameterManagerAction.FAILED; } }
                }
            }
            result.VerificationPassed = !result.RolledBack && ParameterManagerVerificationService.Verify(doc, plan, result);
            timer.Stop();
            result.ExecutionDiagnostics = WorkflowExecutionRecord.Create("CmdParameterManager", "UPDATE_PARAMETERS", "targets=" + result.RequestedTargets, result.RolledBack ? WorkflowExecutionState.KTOOL_FAILURE : WorkflowExecutionState.SUCCESS,
                result.Outcome, groupStatus, result.VerificationPassed ? WorkflowPostconditionState.PASSED : WorkflowPostconditionState.FAILED, "all-or-nothing parameter values verified", result.RequestedTargets, result.UpdatedTargets, 0, result.RolledBack ? 1 : 0, timer.Elapsed, true, result.RolledBack, result.RolledBack, null, DocumentIdentity.From(doc).StableKey);
            return result;
        }
        private static ParameterManagerStatus MapStatus(ParameterTransferStatus status) { switch (status) { case ParameterTransferStatus.READ_ONLY: return ParameterManagerStatus.PARAMETER_READ_ONLY; case ParameterTransferStatus.SKIPPED_PROTECTED: return ParameterManagerStatus.PARAMETER_PROTECTED; case ParameterTransferStatus.UNSAFE_ELEMENT_REFERENCE: return ParameterManagerStatus.UNSAFE_ELEMENT_REFERENCE; case ParameterTransferStatus.NO_CHANGE: return ParameterManagerStatus.NO_CHANGE; default: return ParameterManagerStatus.FAILED; } }
        private static ParameterManagerAction ParameterEditItemAction(ParameterManagerStatus status) { return status == ParameterManagerStatus.NO_CHANGE ? ParameterManagerAction.NO_CHANGE : ParameterManagerAction.BLOCKED; }
    }
}
