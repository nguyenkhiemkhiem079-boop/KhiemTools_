using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterVerificationService
    {
        public static bool VerifySourceUnchanged(Document doc, FilterCopyPlan plan)
        {
            View source = doc == null || plan == null ? null : doc.GetElement(plan.SourceViewId) as View;
            return source != null && string.Equals(FilterStateService.ComputeViewFingerprint(source), plan.SourceFingerprint, StringComparison.Ordinal);
        }
        public static bool VerifyTarget(Document doc, FilterTargetPlan target, FilterCopyPlan plan)
        {
            ElementId mutationId = target == null || target.MutationViewId == ElementId.InvalidElementId ? (target == null ? ElementId.InvalidElementId : target.TargetViewId) : target.MutationViewId;
            View view = doc == null || target == null ? null : doc.GetElement(mutationId) as View;
            if (view == null) return false;
            var expected = new HashSet<ElementId>(plan.SourceFilters.Select(x => x.FilterId));
            if (!expected.IsSubsetOf(new HashSet<ElementId>(ViewFilterApiAdapter.GetAppliedFilters(view)))) return false;
            return plan.Options.Mode != FilterCopyMode.EXACT_SYNC || new HashSet<ElementId>(ViewFilterApiAdapter.GetAppliedFilters(view)).SetEquals(expected);
        }
        public static bool IsPlanCurrent(Document doc, FilterCopyPlan plan)
        {
            if (doc == null || plan == null || plan.IsStale) return false;
            if (!VerifySourceUnchanged(doc, plan)) return false;
            return plan.Targets.All(x => { ElementId mutationId = x.MutationViewId == ElementId.InvalidElementId ? x.TargetViewId : x.MutationViewId; View target = doc.GetElement(mutationId) as View; return target != null && string.Equals(FilterStateService.ComputeViewFingerprint(target), x.TargetFingerprint, StringComparison.Ordinal); });
        }
        public static IList<string> VerifyGlobalDefinitionsUnchanged(Document doc, IEnumerable<FilterDefinitionInfo> before)
        {
            var failures = new List<string>();
            foreach (FilterDefinitionInfo item in before ?? Enumerable.Empty<FilterDefinitionInfo>())
            {
                FilterDefinitionInfo now = FilterCollectorService.Describe(doc, item.Id);
                if (now == null || !string.Equals(now.UniqueId, item.UniqueId, StringComparison.Ordinal)) failures.Add(item.Name + " (" + item.Id + ")");
            }
            return failures;
        }
    }
}
