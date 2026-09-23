using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerExecutionService
    {
        public static ParameterManagerResult Execute(Document doc, ParameterManagerPlan plan)
        {
            var result = new ParameterManagerResult { RequestedTargets = plan == null ? 0 : plan.Items.Count, Status = ParameterManagerStatus.READY };
            if (doc == null || plan == null || plan.Request == null) { result.Status = ParameterManagerStatus.BLOCKED; result.Messages.Add("Plan is unavailable."); return result; }
            ParameterManagerOptions options = plan.Request.Options == null ? new ParameterManagerOptions() : plan.Request.Options.CreateExecutionOptions();
            ParameterManagerPreflightResult preflight = ParameterManagerPreflightService.Validate(doc, plan); if (!preflight.IsValid && options.AllOrNothing) { result.Status = preflight.Status; result.BlockedTargets = preflight.Errors.Count; return result; }
            if (options.AllOrNothing) return ExecuteStrict(doc, plan, result, options);
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
                        using (var tx = new Transaction(doc, "Parameter Manager item " + item.ElementId.IntegerValue))
                        {
                            KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ParameterManager target " + item.ElementId.IntegerValue); ParameterTransferResult applied = ParameterTransferService.TryApplyValue(parameter, item.ProposedValue, new ParameterTransferOptions { AllowElementId = options.AllowElementId, ProtectIdentity = !options.AllowProtectedParameters });
                            if (applied.Status == ParameterTransferStatus.COPIED) { KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ParameterManager target " + item.ElementId.IntegerValue); item.Status = ParameterManagerStatus.UPDATED; item.Action = ParameterManagerAction.UPDATE; result.UpdatedTargets++; }
                            else { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager target " + item.ElementId.IntegerValue); item.Status = MapStatus(applied.Status); item.Action = ParameterEditItemAction(item.Status); result.BlockedTargets++; item.Message = applied.Message; }
                        }
                    }
                    catch (Exception ex) { item.Status = ParameterManagerStatus.FAILED; item.Action = ParameterManagerAction.FAILED; item.Message = ex.Message; result.FailedTargets++; }
                }
                KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ParameterManager per-item batch");
            }
            result.VerificationPassed = ParameterManagerVerificationService.Verify(doc, plan, result);
            result.Status = result.FailedTargets > 0 ? ParameterManagerStatus.PARTIAL : ParameterManagerStatus.UPDATED; return result;
        }
        private static ParameterManagerResult ExecuteStrict(Document doc, ParameterManagerPlan plan, ParameterManagerResult result, ParameterManagerOptions options)
        {
            using (var group = new TransactionGroup(doc, "K-TOOLS Parameter Manager strict"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "ParameterManager all-or-nothing group"); using (var tx = new Transaction(doc, "K-TOOLS Parameter Manager all-or-nothing"))
                {
                    try { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ParameterManager all-or-nothing transaction"); foreach (ParameterEditItem item in plan.Items.Where(i => i.IsSelected && i.Action == ParameterManagerAction.UPDATE)) { Element target = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId); Parameter p = ParameterTransferService.FindMatchingParameter(target, plan.SelectedParameterKey); ParameterTransferResult applied = ParameterTransferService.TryApplyValue(p, item.ProposedValue, new ParameterTransferOptions { AllowElementId = options.AllowElementId, ProtectIdentity = !options.AllowProtectedParameters }); if (applied.Status != ParameterTransferStatus.COPIED) throw new InvalidOperationException(applied.Status.ToString()); item.Status = ParameterManagerStatus.UPDATED; result.UpdatedTargets++; } KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ParameterManager all-or-nothing transaction"); KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ParameterManager all-or-nothing group"); result.Status = ParameterManagerStatus.UPDATED; }
                    catch (Exception ex) { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ParameterManager all-or-nothing transaction"); KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ParameterManager all-or-nothing group"); result.RolledBack = true; result.Status = ParameterManagerStatus.ALL_OR_NOTHING_ROLLED_BACK; result.Messages.Add(ex.Message); }
                }
            }
            result.VerificationPassed = !result.RolledBack && ParameterManagerVerificationService.Verify(doc, plan, result); return result;
        }
        private static ParameterManagerStatus MapStatus(ParameterTransferStatus status) { switch (status) { case ParameterTransferStatus.READ_ONLY: return ParameterManagerStatus.PARAMETER_READ_ONLY; case ParameterTransferStatus.SKIPPED_PROTECTED: return ParameterManagerStatus.PARAMETER_PROTECTED; case ParameterTransferStatus.UNSAFE_ELEMENT_REFERENCE: return ParameterManagerStatus.UNSAFE_ELEMENT_REFERENCE; case ParameterTransferStatus.NO_CHANGE: return ParameterManagerStatus.NO_CHANGE; default: return ParameterManagerStatus.FAILED; } }
        private static ParameterManagerAction ParameterEditItemAction(ParameterManagerStatus status) { return status == ParameterManagerStatus.NO_CHANGE ? ParameterManagerAction.NO_CHANGE : ParameterManagerAction.BLOCKED; }
    }
}
