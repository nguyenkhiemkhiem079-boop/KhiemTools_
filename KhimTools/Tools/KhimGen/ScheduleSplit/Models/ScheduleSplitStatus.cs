namespace KhimTools.ScheduleSplit.Models
{
    public enum ScheduleSplitStatusCode
    {
        READY, SOURCE_MISSING, SCHEDULE_MISSING, SOURCE_INSTANCE_MISSING,
        UNSUPPORTED_SCHEDULE_TYPE, SYSTEM_REVISION_SCHEDULE, SPLIT_API_UNAVAILABLE,
        SCHEDULE_DUPLICATION_UNSUPPORTED, SCHEDULE_SHARED_ELSEWHERE, ALREADY_SPLIT,
        RESPLIT_UNSUPPORTED, INVALID_SEGMENT_COUNT, INVALID_SEGMENT_HEIGHT,
        INSUFFICIENT_TARGET_SHEETS, TARGET_SHEET_MISSING, UNKNOWN_SHEET_BOUNDS,
        PLACEMENT_COLLISION, SEGMENT_CREATE_FAILED, SEGMENT_PLACEMENT_FAILED,
        STALE_PLAN, CREATED, PARTIAL, FAILED, POST_VERIFY_FAILED
    }

    public enum ScheduleSplitMode { BY_SEGMENT_COUNT, BY_MAX_HEIGHT, MANUAL_SEGMENT_HEIGHTS }
    public enum ScheduleSplitSourceMode { WORKING_COPY, MODIFY_SOURCE_SCHEDULE }
    public enum ScheduleSplitDistribution { ONE_SEGMENT_PER_SHEET, ALL_SEGMENTS_ON_SAME_SHEET, MULTIPLE_SEGMENTS_PER_SHEET }
    public enum ScheduleSplitDirection { VERTICAL_STACK, HORIZONTAL_COLUMNS }
    public enum ScheduleSplitAnchor { SOURCE_POSITION, TARGET_SHEET_USABLE_TOP_LEFT, USER_DEFINED_OFFSET }
    public enum ScheduleSplitSeverity { INFO, WARNING, ERROR }

    public sealed class ScheduleSplitPreflightResult
    {
        public Autodesk.Revit.DB.ElementId ScheduleId { get; set; } = Autodesk.Revit.DB.ElementId.InvalidElementId;
        public Autodesk.Revit.DB.ElementId SourceInstanceId { get; set; } = Autodesk.Revit.DB.ElementId.InvalidElementId;
        public int SegmentIndex { get; set; } = -1;
        public Autodesk.Revit.DB.ElementId TargetSheetId { get; set; } = Autodesk.Revit.DB.ElementId.InvalidElementId;
        public ScheduleSplitStatusCode Status { get; set; }
        public ScheduleSplitSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
    }
}
