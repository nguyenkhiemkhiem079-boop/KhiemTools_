using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSegmentPlan
    {
        public int SegmentIndex { get; set; }
        public double HeightInternal { get; set; }
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public XYZ PlannedPosition { get; set; }
        public XYZ ActualPosition { get; set; }
        public ScheduleSplitStatusCode Status { get; set; } = ScheduleSplitStatusCode.READY;
        public string Message { get; set; } = string.Empty;
        public ElementId CreatedInstanceId { get; set; } = ElementId.InvalidElementId;
    }
}
