using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterCopyPlan
    {
        public ElementId SourceViewId { get; set; } = ElementId.InvalidElementId;
        public string SourceViewUniqueId { get; set; } = string.Empty;
        public string SourceFingerprint { get; set; } = string.Empty;
        public FilterSyncOptions Options { get; set; } = new FilterSyncOptions();
        public IList<AppliedFilterState> SourceFilters { get; } = new List<AppliedFilterState>();
        public IList<AppliedFilterState> SourceStates { get { return SourceFilters; } }
        public IList<ElementId> SelectedFilterIds { get; } = new List<ElementId>();
        public IList<FilterTargetPlan> Targets { get; } = new List<FilterTargetPlan>();
        public IList<string> Actions { get; } = new List<string>();
        public IList<string> Warnings { get; } = new List<string>();
        public IList<string> BlockedItems { get; } = new List<string>();
        public FilterManagerStatusCode Status { get; set; } = FilterManagerStatusCode.READY;
        public string PlanFingerprint { get; set; } = string.Empty;
        public bool IsStale { get; set; }
    }
}
