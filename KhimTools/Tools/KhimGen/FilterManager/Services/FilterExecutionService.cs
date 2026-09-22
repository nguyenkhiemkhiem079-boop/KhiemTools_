using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterExecutionService
    {
        public static FilterManagerResult Execute(Document doc, FilterCopyPlan plan)
        {
            var result = new FilterManagerResult { Status = FilterManagerStatusCode.READY, RequestedTargets = plan == null ? 0 : plan.Targets.Count };
            if (doc == null || plan == null) { result.Status = FilterManagerStatusCode.PREFLIGHT_FAILED; result.Message = "Document or plan is unavailable."; return result; }
            IList<FilterManagerStatus> checks = FilterPreflightService.Validate(doc, plan);
            if (checks.Any(x => x.IsError)) { result.Status = FilterManagerStatusCode.PREFLIGHT_FAILED; result.Message = string.Join("; ", checks.Where(x => x.IsError).Select(x => x.Code + ": " + x.Message)); return result; }
            foreach (FilterTargetPlan targetPlan in plan.Targets)
            {
                var targetResult = new FilterTargetResult { TargetViewId = targetPlan.TargetViewId, Status = targetPlan.Status };
                if (targetPlan.TemplateControlled && !plan.Options.RedirectTemplateControlledTarget) { targetResult.Status = FilterManagerStatusCode.TARGET_FILTERS_CONTROLLED_BY_TEMPLATE; targetResult.Message = targetPlan.Message; result.FailedTargets++; result.Targets.Add(targetResult); continue; }
                if (targetPlan.Status == FilterManagerStatusCode.NO_CHANGE) { targetResult.Status = FilterManagerStatusCode.NO_CHANGE; result.NoChangeTargets++; result.Targets.Add(targetResult); continue; }
                ElementId mutationId = targetPlan.MutationViewId == ElementId.InvalidElementId ? targetPlan.TargetViewId : targetPlan.MutationViewId;
                View target = doc.GetElement(mutationId) as View;
                if (target == null) { targetResult.Status = FilterManagerStatusCode.STALE_SYNC_PLAN; targetResult.Message = "Target view no longer exists."; result.FailedTargets++; result.Targets.Add(targetResult); continue; }
                Transaction transaction = null;
                try
                {
                    transaction = new Transaction(doc, "K-TOOLS Filter Manager - " + target.Name);
                    transaction.Start();
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
                    transaction.Commit(); targetResult.Status = FilterManagerStatusCode.APPLIED; result.AppliedTargets++;
                }
                catch (Exception ex)
                {
                    if (transaction != null && transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    targetResult.Status = FilterManagerStatusCode.FAILED; targetResult.Message = ex.Message; result.FailedTargets++;
                }
                finally { if (transaction != null) transaction.Dispose(); }
                result.Targets.Add(targetResult);
            }
            result.Status = result.FailedTargets > 0 && result.AppliedTargets == 0 ? FilterManagerStatusCode.FAILED : (result.FailedTargets > 0 ? FilterManagerStatusCode.PARTIAL : FilterManagerStatusCode.APPLIED);
            return result;
        }
    }
}
