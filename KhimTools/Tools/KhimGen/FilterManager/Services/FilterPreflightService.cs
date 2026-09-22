using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterPreflightService
    {
        public static IList<FilterManagerStatus> Validate(Document doc, FilterCopyPlan plan)
        {
            var results = new List<FilterManagerStatus>();
            if (doc == null || plan == null) { results.Add(Error(FilterManagerStatusCode.PREFLIGHT_FAILED, "Document or plan is unavailable.")); return results; }
            View source = doc.GetElement(plan.SourceViewId) as View;
            if (source == null) { results.Add(Error(FilterManagerStatusCode.STALE_SYNC_PLAN, "Source view was deleted after preview.")); return results; }
            if (!string.Equals(FilterStateService.ComputeViewFingerprint(source), plan.SourceFingerprint, StringComparison.Ordinal)) results.Add(Error(FilterManagerStatusCode.STALE_SYNC_PLAN, "Source view changed after preview."));
            foreach (AppliedFilterState filter in plan.SourceFilters)
            {
                FilterDefinitionInfo definition = FilterCollectorService.Describe(doc, filter.FilterId);
                if (definition == null || definition.DefinitionType == FilterDefinitionType.Unsupported) results.Add(Error(FilterManagerStatusCode.UNSUPPORTED_FILTER_TYPE, "Filter definition is missing or unsupported: " + filter.Name));
            }
            foreach (FilterTargetPlan target in plan.Targets)
            {
                ElementId mutationId = target.MutationViewId == ElementId.InvalidElementId ? target.TargetViewId : target.MutationViewId;
                View view = doc.GetElement(mutationId) as View;
                if (view == null) results.Add(Error(FilterManagerStatusCode.STALE_SYNC_PLAN, "Target view was deleted: " + target.TargetViewName));
                else if (!string.Equals(FilterStateService.ComputeViewFingerprint(view), target.TargetFingerprint, StringComparison.Ordinal)) results.Add(Error(FilterManagerStatusCode.STALE_SYNC_PLAN, "Target view changed after preview: " + target.TargetViewName));
                if (target.TemplateControlled && !plan.Options.RedirectTemplateControlledTarget) results.Add(Error(FilterManagerStatusCode.TARGET_FILTERS_CONTROLLED_BY_TEMPLATE, target.TargetViewName + " is controlled by a View Template."));
            }
            if (results.Count == 0) results.Add(new FilterManagerStatus { Code = FilterManagerStatusCode.READY, Message = "All source definitions, target views, template controls and fingerprints are valid." });
            return results;
        }

        public static IList<FilterPreflightResult> ValidateDetailed(Document doc, FilterCopyPlan plan)
        {
            var detailed = new List<FilterPreflightResult>();
            foreach (FilterManagerStatus status in Validate(doc, plan)) detailed.Add(new FilterPreflightResult { SourceViewId = plan == null ? ElementId.InvalidElementId : plan.SourceViewId, Status = status.Code, Severity = status.IsError ? "ERROR" : "INFO", Message = status.Message, CanExecute = !status.IsError });
            return detailed;
        }
        private static FilterManagerStatus Error(FilterManagerStatusCode code, string message) { return new FilterManagerStatus { Code = code, Message = message, IsError = true }; }
    }
}
