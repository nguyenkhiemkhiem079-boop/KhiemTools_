using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterTargetPlan
    {
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public ElementId MutationViewId { get; set; } = ElementId.InvalidElementId;
        public string TargetViewName { get; set; } = string.Empty;
        public bool TemplateControlled { get; set; }
        public string TargetKind { get; set; } = "View";
        public ElementId ControllingTemplateId { get; set; } = ElementId.InvalidElementId;
        public string SourceFingerprint { get; set; } = string.Empty;
        public string TargetFingerprint { get; set; } = string.Empty;
        public IList<AppliedFilterState> Filters { get; } = new List<AppliedFilterState>();
        public IList<AppliedFilterState> CurrentState { get; } = new List<AppliedFilterState>();
        public IList<AppliedFilterState> DesiredState { get; } = new List<AppliedFilterState>();
        public IList<string> Actions { get; } = new List<string>();
        public IList<string> Warnings { get; } = new List<string>();
        public IList<string> BlockedItems { get; } = new List<string>();
        public IList<ElementId> RemoveFilterIds { get; } = new List<ElementId>();
        public FilterManagerStatusCode Status { get; set; } = FilterManagerStatusCode.READY;
        public string Message { get; set; } = string.Empty;
        public override string ToString() { return TargetViewName + " - " + Status; }
    }
}
