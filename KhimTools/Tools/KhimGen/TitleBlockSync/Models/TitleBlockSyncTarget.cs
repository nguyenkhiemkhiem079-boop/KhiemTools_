using Autodesk.Revit.DB;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class TitleBlockSyncTarget
    {
        public ElementId SheetId { get; set; } = ElementId.InvalidElementId;
        public string SheetUniqueId { get; set; } = string.Empty;
        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public string CurrentFamilyName { get; set; } = string.Empty;
        public string CurrentTypeName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
