using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterManagerAuditService
    {
        public static IList<FilterManagerStatus> Compare(Document doc, ElementId sourceId, IEnumerable<ElementId> targetIds, IEnumerable<ElementId> filterIds, FilterSyncOptions options)
        {
            var request = new FilterCopyRequest { Document = doc, SourceViewId = sourceId, Options = options ?? new FilterSyncOptions() };
            foreach (ElementId id in targetIds ?? Enumerable.Empty<ElementId>()) request.TargetViewIds.Add(id);
            foreach (ElementId id in filterIds ?? Enumerable.Empty<ElementId>()) request.FilterIds.Add(id);
            FilterCopyPlan plan = FilterCopyPlanner.BuildPlan(doc, request);
            return plan.Targets.Select(x => new FilterManagerStatus { Code = x.Status, Message = x.TargetViewName }).ToList();
        }
        public static bool IsNoChange(AppliedFilterState source, AppliedFilterState target, FilterSyncOptions options)
        {
            return FilterComparisonService.Classify(source, target, options ?? new FilterSyncOptions()) == FilterManagerStatusCode.NO_CHANGE;
        }
    }
}
