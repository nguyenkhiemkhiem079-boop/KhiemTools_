using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterCopyPlanner
    {
        public static FilterCopyPlan BuildPlan(Document doc, FilterCopyRequest request)
        {
            var plan = new FilterCopyPlan { Options = request == null || request.Options == null ? new FilterSyncOptions() : request.Options };
            if (doc == null || request == null) { plan.Status = FilterManagerStatusCode.PREFLIGHT_FAILED; return plan; }
            View source = doc.GetElement(request.SourceViewId) as View;
            if (source == null || source is ViewSheet || source.ViewType == ViewType.Schedule) { plan.Status = FilterManagerStatusCode.INCOMPATIBLE_VIEW; return plan; }
            plan.SourceViewId = source.Id; plan.SourceViewUniqueId = source.UniqueId; foreach (AppliedFilterState state in FilterStateService.Capture(source, request.FilterIds.Count == 0 ? null : request.FilterIds)) { plan.SourceFilters.Add(state); plan.SelectedFilterIds.Add(state.FilterId); }
            plan.SourceFingerprint = FilterStateService.ComputeViewFingerprint(source); plan.PlanFingerprint = FilterComparisonService.PlanFingerprint(plan.SourceFilters, plan.Options);
            foreach (ElementId targetId in request.TargetViewIds.Distinct())
            {
                View target = doc.GetElement(targetId) as View;
                if (target == null || target.Id == source.Id) continue;
                var targetPlan = new FilterTargetPlan { TargetViewId = target.Id, TargetViewName = target.Name, TargetKind = target.IsTemplate ? "Template" : "View", ControllingTemplateId = target.ViewTemplateId, SourceFingerprint = plan.SourceFingerprint, TargetFingerprint = FilterStateService.ComputeViewFingerprint(target) };
                string templateReason;
                targetPlan.TemplateControlled = ViewTemplateFilterControlService.AreFiltersControlledByTemplate(target, out templateReason);
                if (targetPlan.TemplateControlled && plan.Options.RedirectTemplateControlledTarget && target.ViewTemplateId != ElementId.InvalidElementId) { targetPlan.MutationViewId = target.ViewTemplateId; targetPlan.TargetKind = "TemplateRedirect"; }
                else targetPlan.MutationViewId = target.Id;
                View mutationView = doc.GetElement(targetPlan.MutationViewId) as View; targetPlan.TargetFingerprint = FilterStateService.ComputeViewFingerprint(mutationView);
                foreach (AppliedFilterState sourceState in plan.SourceFilters)
                {
                    AppliedFilterState targetState = FilterStateService.CaptureOne(target, sourceState.FilterId);
                    FilterManagerStatusCode status = FilterComparisonService.Classify(sourceState, targetState, plan.Options);
                    AppliedFilterState desired = Clone(sourceState, status); targetPlan.Filters.Add(desired); targetPlan.DesiredState.Add(desired); AppliedFilterState current = FilterStateService.CaptureOne(target, sourceState.FilterId); if (current != null) targetPlan.CurrentState.Add(current);
                    targetPlan.Actions.Add(status == FilterManagerStatusCode.ADD_FILTER_REQUIRED ? "ADD" : status == FilterManagerStatusCode.NO_CHANGE ? "NO_CHANGE" : "UPDATE");
                    if (status != FilterManagerStatusCode.NO_CHANGE && targetPlan.Status == FilterManagerStatusCode.READY) targetPlan.Status = status;
                }
                if (plan.Options.Mode == FilterCopyMode.EXACT_SYNC)
                {
                    var selected = new HashSet<ElementId>(plan.SourceFilters.Select(x => x.FilterId));
                    foreach (ElementId extra in ViewFilterApiAdapter.GetAppliedFilters(target).Where(x => !selected.Contains(x))) { targetPlan.RemoveFilterIds.Add(extra); targetPlan.Actions.Add("REMOVE_FROM_TARGET"); }
                }
                if (targetPlan.Status == FilterManagerStatusCode.READY) targetPlan.Status = targetPlan.RemoveFilterIds.Count > 0 ? FilterManagerStatusCode.EXACT_SYNC_REMOVE_REQUIRED : FilterManagerStatusCode.NO_CHANGE;
                if (targetPlan.TemplateControlled && !plan.Options.RedirectTemplateControlledTarget) { targetPlan.Status = FilterManagerStatusCode.TARGET_FILTERS_CONTROLLED_BY_TEMPLATE; targetPlan.Message = templateReason; }
                plan.Targets.Add(targetPlan);
            }
            if (plan.Targets.Count == 0) plan.Status = FilterManagerStatusCode.PREFLIGHT_FAILED;
            else if (plan.Targets.Any(t => t.Status != FilterManagerStatusCode.NO_CHANGE)) plan.Status = FilterManagerStatusCode.STATE_DIFF;
            else plan.Status = FilterManagerStatusCode.NO_CHANGE;
            return plan;
        }

        private static AppliedFilterState Clone(AppliedFilterState source, FilterManagerStatusCode status)
        {
            return new AppliedFilterState { FilterId = source.FilterId, UniqueId = source.UniqueId, Name = source.Name, DefinitionType = source.DefinitionType, IsApplied = source.IsApplied, OrderIndex = source.OrderIndex, Visible = source.Visible, Enabled = source.Enabled, EnabledStateSupported = source.EnabledStateSupported, Overrides = source.Overrides == null ? new GraphicOverrideSnapshot() : source.Overrides.Clone(), SourceViewId = source.SourceViewId, StateFingerprint = status.ToString() + ":" + source.StateFingerprint };
        }
    }
}
