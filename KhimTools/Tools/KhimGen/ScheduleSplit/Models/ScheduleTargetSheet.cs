using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleTargetSheet
    {
        public ElementId SheetId { get; set; } = ElementId.InvalidElementId;
        public string SheetUniqueId { get; set; } = string.Empty;
        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsSelected { get; set; } = true;
        public string Status { get; set; } = string.Empty;
    }
}
