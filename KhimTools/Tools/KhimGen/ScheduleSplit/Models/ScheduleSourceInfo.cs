using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSourceInfo
    {
        public ElementId ScheduleId { get; set; } = ElementId.InvalidElementId;
        public string ScheduleUniqueId { get; set; } = string.Empty;
        public ElementId InstanceId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public string SourceSheetNumber { get; set; } = string.Empty;
        public string SourceSheetName { get; set; } = string.Empty;
        public string ScheduleName { get; set; } = string.Empty;
        public int SegmentCount { get; set; }
        public bool IsSplit { get; set; }
        public int SegmentIndex { get; set; } = -1;
        public XYZ Position { get; set; }
        public BoundingBoxXYZ Bounds { get; set; }
        public double TotalHeightInternal { get; set; }
        public bool IsRevisionSchedule { get; set; }
        public bool IsKeySchedule { get; set; }
        public bool IsInternalSchedule { get; set; }
        public List<ElementId> PlacementInstanceIds { get; } = new List<ElementId>();
        public List<ElementId> PlacementSheetIds { get; } = new List<ElementId>();
        public string Fingerprint { get; set; } = string.Empty;
    }
}
