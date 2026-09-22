using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterPreflightResult
    {
        public ElementId SourceViewId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public ElementId FilterId { get; set; } = ElementId.InvalidElementId;
        public string Action { get; set; } = string.Empty;
        public FilterManagerStatusCode Status { get; set; }
        public string Severity { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
    }
}
