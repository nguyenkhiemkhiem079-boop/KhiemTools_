using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSplitPlan
    {
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string PlanVersion { get; set; } = "3.2";
        public string Fingerprint { get; set; } = string.Empty;
        public string SourceFingerprint { get; set; } = string.Empty;
        public ElementId SourceScheduleId { get; set; } = ElementId.InvalidElementId;
        public string SourceScheduleUniqueId { get; set; } = string.Empty;
        public ElementId WorkingScheduleId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceInstanceId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public ScheduleSplitMode Mode { get; set; }
        public ScheduleSplitSourceMode SourceMode { get; set; }
        public int SegmentCount { get; set; }
        public List<double> SegmentHeightsInternal { get; } = new List<double>();
        public List<ScheduleSegmentPlan> Segments { get; } = new List<ScheduleSegmentPlan>();
        public List<ScheduleTargetSheet> TargetSheets { get; } = new List<ScheduleTargetSheet>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> BlockedConditions { get; } = new List<string>();
        public bool IsStale { get; set; }
        public ScheduleSplitStatusCode Status { get; set; } = ScheduleSplitStatusCode.READY;
        public string Message { get; set; } = string.Empty;
    }
}
