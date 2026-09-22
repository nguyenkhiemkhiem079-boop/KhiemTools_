using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterDefinitionInfo
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string UniqueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FilterDefinitionType DefinitionType { get; set; }
        public bool SupportsEnabledState { get; set; }
        public string DisplayType { get { return DefinitionType.ToString(); } }
        public override string ToString() { return Name + " (" + DisplayType + ")"; }
    }
}
