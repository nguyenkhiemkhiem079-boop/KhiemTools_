using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class AppliedFilterState
    {
        public ElementId FilterId { get; set; } = ElementId.InvalidElementId;
        public string UniqueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FilterDefinitionType DefinitionType { get; set; }
        public bool IsApplied { get; set; }
        public int OrderIndex { get; set; } = -1;
        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public bool EnabledStateSupported { get; set; }
        public GraphicOverrideSnapshot Overrides { get; set; } = new GraphicOverrideSnapshot();
        public ElementId SourceViewId { get; set; } = ElementId.InvalidElementId;
        public string StateFingerprint { get; set; } = string.Empty;
    }
}
