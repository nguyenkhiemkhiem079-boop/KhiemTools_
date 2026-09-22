using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterStateService
    {
        public static IList<AppliedFilterState> Capture(View view, IEnumerable<ElementId> filterIds = null)
        {
            var result = new List<AppliedFilterState>();
            if (view == null) return result;
            var ids = (filterIds == null ? ViewFilterApiAdapter.GetOrderedFilters(view) : filterIds).ToList();
            int index = 0;
            foreach (ElementId id in ids)
            {
                if (id == null || id == ElementId.InvalidElementId || !ViewFilterApiAdapter.IsApplied(view, id)) continue;
                FilterDefinitionInfo definition = FilterCollectorService.Describe(view.Document, id);
                bool enabledSupported;
                bool enabled = ViewFilterApiAdapter.GetEnabled(view, id, out enabledSupported);
                var state = new AppliedFilterState
                {
                    FilterId = id,
                    UniqueId = definition == null ? string.Empty : definition.UniqueId,
                    Name = definition == null ? string.Empty : definition.Name,
                    DefinitionType = definition == null ? FilterDefinitionType.Unsupported : definition.DefinitionType,
                    IsApplied = true,
                    OrderIndex = index++,
                    Visible = ViewFilterApiAdapter.GetVisibility(view, id),
                    Enabled = enabled,
                    EnabledStateSupported = enabledSupported,
                    Overrides = GraphicOverrideSnapshotService.Capture(ViewFilterApiAdapter.GetOverrides(view, id)),
                    SourceViewId = view.Id
                };
                state.StateFingerprint = FilterComparisonService.StateFingerprint(state);
                result.Add(state);
            }
            return result;
        }

        public static AppliedFilterState CaptureOne(View view, ElementId filterId)
        {
            return Capture(view, new[] { filterId }).FirstOrDefault();
        }

        public static IList<FilterDefinitionInfo> CollectAddableFilters(Document doc, View view)
        {
            var applied = new HashSet<ElementId>(ViewFilterApiAdapter.GetAppliedFilters(view));
            return FilterCollectorService.CollectFilterDefinitions(doc).Where(x => !applied.Contains(x.Id)).ToList();
        }

        public static void AddExistingFilter(View view, ElementId filterId) { if (view != null && !ViewFilterApiAdapter.IsApplied(view, filterId)) ViewFilterApiAdapter.AddFilter(view, filterId); }
        public static void RemoveAppliedFilter(View view, ElementId filterId) { if (view != null && ViewFilterApiAdapter.IsApplied(view, filterId)) ViewFilterApiAdapter.RemoveFilter(view, filterId); }
        public static void ToggleVisibility(View view, ElementId filterId, bool visible) { ViewFilterApiAdapter.SetVisibility(view, filterId, visible); }
        public static bool ToggleEnabled(View view, ElementId filterId, bool enabled) { bool supported; return ViewFilterApiAdapter.SetEnabled(view, filterId, enabled, out supported) && supported; }
        public static void ResetFilterOverrides(View view, ElementId filterId) { ViewFilterApiAdapter.SetOverrides(view, filterId, new OverrideGraphicSettings()); }

        public static string ComputeViewFingerprint(View view)
        {
            if (view == null) return "<missing>";
            return string.Join("||", Capture(view).Select(FilterComparisonService.StateFingerprint));
        }
    }
}
