using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSplitRequest
    {
        public Document Document { get; set; }
        public ElementId SourceScheduleId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceInstanceId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public List<ScheduleTargetSheet> TargetSheets { get; } = new List<ScheduleTargetSheet>();
        public ScheduleSplitOptions Options { get; set; } = new ScheduleSplitOptions();
    }
}
