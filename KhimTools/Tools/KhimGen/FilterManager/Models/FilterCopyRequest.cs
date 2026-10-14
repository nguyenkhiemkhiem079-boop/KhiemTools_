using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterCopyRequest
    {
        public Document Document { get; set; }
        public ElementId SourceViewId { get; set; } = ElementId.InvalidElementId;
        public IList<ElementId> TargetViewIds { get; } = new List<ElementId>();
        public IList<ElementId> FilterIds { get; } = new List<ElementId>();
        public FilterSyncOptions Options { get; set; } = new FilterSyncOptions();
    }
}
