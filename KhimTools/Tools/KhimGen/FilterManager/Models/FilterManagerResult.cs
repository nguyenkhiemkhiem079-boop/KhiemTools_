using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterTargetResult
    {
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public FilterManagerStatusCode Status { get; set; }
        public int AppliedCount { get; set; }
        public int RemovedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FilterManagerResult
    {
        public FilterManagerStatusCode Status { get; set; }
        public int RequestedTargets { get; set; }
        public int AppliedTargets { get; set; }
        public int FailedTargets { get; set; }
        public int NoChangeTargets { get; set; }
        public IList<FilterTargetResult> Targets { get; } = new List<FilterTargetResult>();
        public string Message { get; set; } = string.Empty;
    }
}
