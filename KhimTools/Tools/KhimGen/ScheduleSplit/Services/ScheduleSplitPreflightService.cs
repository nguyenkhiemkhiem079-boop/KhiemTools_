using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ScheduleSplit.Models;
using KhimTools.ViewportAlign.Services;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSplitPreflightService
    {
        public static List<ScheduleSplitPreflightResult> Validate(Document doc, ScheduleSplitPlan plan, ScheduleSplitOptions options)
        {
            var results = new List<ScheduleSplitPreflightResult>();
            if (doc == null || plan == null) { results.Add(Result(plan == null ? ElementId.InvalidElementId : plan.SourceScheduleId, ElementId.InvalidElementId, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.STALE_PLAN, ScheduleSplitSeverity.ERROR, "Schedule split plan is unavailable.", false)); return results; }
            if (plan.Status != ScheduleSplitStatusCode.READY) { results.Add(Result(plan.SourceScheduleId, plan.SourceInstanceId, -1, ElementId.InvalidElementId, plan.Status, ScheduleSplitSeverity.ERROR, plan.Message, false)); return results; }
            ViewSchedule schedule = doc.GetElement(plan.SourceScheduleId) as ViewSchedule;
            ScheduleSheetInstance sourceInstance = doc.GetElement(plan.SourceInstanceId) as ScheduleSheetInstance;
            if (schedule == null) { results.Add(Result(plan.SourceScheduleId, plan.SourceInstanceId, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.SCHEDULE_MISSING, ScheduleSplitSeverity.ERROR, "Source ViewSchedule is missing.", false)); return results; }
            if (sourceInstance == null) { results.Add(Result(plan.SourceScheduleId, plan.SourceInstanceId, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.SOURCE_INSTANCE_MISSING, ScheduleSplitSeverity.ERROR, "Source ScheduleSheetInstance is missing.", false)); return results; }
            if (!ScheduleSplitApiAdapter.CanSplit(schedule, out string capabilityReason))
            { results.Add(Result(plan.SourceScheduleId, plan.SourceInstanceId, -1, ElementId.InvalidElementId, schedule.IsTitleblockRevisionSchedule ? ScheduleSplitStatusCode.SYSTEM_REVISION_SCHEDULE : ScheduleSplitStatusCode.UNSUPPORTED_SCHEDULE_TYPE, ScheduleSplitSeverity.ERROR, capabilityReason, false)); return results; }
            if (options == null) options = new ScheduleSplitOptions();
            if (options.SourceMode == ScheduleSplitSourceMode.MODIFY_SOURCE_SCHEDULE && ScheduleSplitCollector.Analyze(doc, schedule, sourceInstance, doc.GetElement(sourceInstance.OwnerViewId) as ViewSheet).PlacementInstanceIds.Count > 1 && !options.AllowModifySharedSource)
            { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.SCHEDULE_SHARED_ELSEWHERE, ScheduleSplitSeverity.ERROR, "Modify-source mode is blocked for a shared Schedule.", false)); return results; }
            if (ScheduleSplitCollector.IsRevision(sourceInstance))
            { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.SYSTEM_REVISION_SCHEDULE, ScheduleSplitSeverity.ERROR, "Revision Schedule instances are system-managed.", false)); return results; }
            if (plan.Segments.Count < 2) { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.INVALID_SEGMENT_COUNT, ScheduleSplitSeverity.ERROR, "At least two segments are required.", false)); return results; }
            if (IsStale(doc, plan)) { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, -1, ElementId.InvalidElementId, ScheduleSplitStatusCode.STALE_PLAN, ScheduleSplitSeverity.ERROR, "Source or target state changed after Preview.", false)); return results; }
            foreach (ScheduleSegmentPlan segment in plan.Segments)
            {
                ViewSheet target = doc.GetElement(segment.TargetSheetId) as ViewSheet;
                if (target == null) { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, segment.SegmentIndex, segment.TargetSheetId, ScheduleSplitStatusCode.TARGET_SHEET_MISSING, ScheduleSplitSeverity.ERROR, "Target Sheet is missing.", false)); continue; }
                if (segment.HeightInternal <= 0) { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, segment.SegmentIndex, target.Id, ScheduleSplitStatusCode.INVALID_SEGMENT_HEIGHT, ScheduleSplitSeverity.ERROR, "Segment height must be positive.", false)); continue; }
                if (segment.PlannedPosition == null) { results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, segment.SegmentIndex, target.Id, ScheduleSplitStatusCode.UNKNOWN_SHEET_BOUNDS, ScheduleSplitSeverity.ERROR, "Target Sheet bounds could not be resolved.", false)); continue; }
                if (HasCollision(doc, target, sourceInstance, segment, options.AllowWithWarningOnCollision, out string collisionMessage))
                    results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, segment.SegmentIndex, target.Id, ScheduleSplitStatusCode.PLACEMENT_COLLISION, options.AllowWithWarningOnCollision ? ScheduleSplitSeverity.WARNING : ScheduleSplitSeverity.ERROR, collisionMessage, options.AllowWithWarningOnCollision));
                else results.Add(Result(plan.SourceScheduleId, sourceInstance.Id, segment.SegmentIndex, target.Id, ScheduleSplitStatusCode.READY, ScheduleSplitSeverity.INFO, "Ready", true));
            }
            return results;
        }

        public static bool IsStale(Document doc, ScheduleSplitPlan plan)
        {
            if (doc == null || plan == null) return true;
            ViewSchedule schedule = doc.GetElement(plan.SourceScheduleId) as ViewSchedule;
            ScheduleSheetInstance instance = doc.GetElement(plan.SourceInstanceId) as ScheduleSheetInstance;
            ViewSheet sheet = instance == null ? null : doc.GetElement(instance.OwnerViewId) as ViewSheet;
            if (schedule == null || instance == null || sheet == null) return true;
            ScheduleSourceInfo current = ScheduleSplitCollector.Analyze(doc, schedule, instance, sheet);
            if (!string.Equals(current.ScheduleUniqueId, plan.SourceScheduleUniqueId, StringComparison.Ordinal)) return true;
            return !string.Equals(current.Fingerprint, plan.SourceFingerprint, StringComparison.Ordinal);
        }

        private static bool HasCollision(Document doc, ViewSheet target, ScheduleSheetInstance sourceInstance, ScheduleSegmentPlan segment, bool allowWarning, out string message)
        {
            message = string.Empty;
            SheetBounds sourceBounds = ViewportAlignService.GetScheduleBounds(sourceInstance, doc.GetElement(sourceInstance.OwnerViewId) as ViewSheet);
            double width = Math.Max(0.0, sourceBounds.Right - sourceBounds.Left);
            double height = segment.HeightInternal;
            var planned = new SheetBounds(segment.PlannedPosition.X - width / 2.0, segment.PlannedPosition.X + width / 2.0,
                segment.PlannedPosition.Y - height / 2.0, segment.PlannedPosition.Y + height / 2.0);
            foreach (Viewport viewport in new FilteredElementCollector(doc, target.Id).OfClass(typeof(Viewport)).Cast<Viewport>())
            {
                if (Intersects(planned, ViewportAlignService.GetViewportBounds(viewport, target))) { message = "Planned segment intersects an existing Viewport."; return true; }
            }
            foreach (ScheduleSheetInstance existing in new FilteredElementCollector(doc, target.Id).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
            {
                if (existing.Id == sourceInstance.Id && target.Id == sourceInstance.OwnerViewId) { message = "Planned segment intersects the source Schedule instance."; return true; }
                if (Intersects(planned, ViewportAlignService.GetScheduleBounds(existing, target))) { message = "Planned segment intersects an existing Schedule instance."; return true; }
            }
            return false;
        }

        private static bool Intersects(SheetBounds left, SheetBounds right)
        {
            return left.IsValid && right.IsValid && left.Left < right.Right && left.Right > right.Left && left.Bottom < right.Top && left.Top > right.Bottom;
        }

        private static ScheduleSplitPreflightResult Result(ElementId scheduleId, ElementId instanceId, int segmentIndex, ElementId targetSheetId, ScheduleSplitStatusCode status, ScheduleSplitSeverity severity, string message, bool canExecute)
        {
            return new ScheduleSplitPreflightResult { ScheduleId = scheduleId, SourceInstanceId = instanceId, SegmentIndex = segmentIndex, TargetSheetId = targetSheetId, Status = status, Severity = severity, Message = message, CanExecute = canExecute };
        }
    }
}
