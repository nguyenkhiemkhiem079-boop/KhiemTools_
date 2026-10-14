using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ScheduleSplit.Models;
using KhimTools.ViewportAlign.Services;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSplitVerificationService
    {
        public static bool Verify(Document doc, ScheduleSplitPlan plan, ScheduleSplitExecutionResult result, out string message)
        {
            message = string.Empty;
            if (doc == null || plan == null || result == null) { message = "Verification inputs are unavailable."; return false; }
            ViewSchedule schedule = doc.GetElement(plan.WorkingScheduleId) as ViewSchedule;
            if (schedule == null) { message = "POST_VERIFY_FAILED: working Schedule is missing."; return false; }
            if (!ScheduleSplitApiAdapter.IsSplit(schedule) || ScheduleSplitApiAdapter.GetSegmentCount(schedule) != plan.SegmentCount)
            { message = "POST_VERIFY_FAILED: segment count does not match the plan."; return false; }
            double tolerance = ViewportAlignService.SheetCoordinateTolerance;
            foreach (ScheduleSegmentPlan segment in plan.Segments)
            {
                ScheduleSegmentResult actual = null;
                foreach (ScheduleSegmentResult candidate in result.SegmentResults) if (candidate.SegmentIndex == segment.SegmentIndex) { actual = candidate; break; }
                if (actual == null || actual.InstanceId == null || actual.InstanceId == ElementId.InvalidElementId)
                { message = "POST_VERIFY_FAILED: segment " + segment.SegmentIndex + " has no placed instance."; return false; }
                ScheduleSheetInstance instance = doc.GetElement(actual.InstanceId) as ScheduleSheetInstance;
                if (instance == null || instance.OwnerViewId != segment.TargetSheetId || ScheduleSplitApiAdapter.GetSegmentIndex(instance) != segment.SegmentIndex)
                { message = "POST_VERIFY_FAILED: segment " + segment.SegmentIndex + " target mismatch."; return false; }
                XYZ point = ScheduleSplitApiAdapter.GetPosition(instance);
                if (point == null || segment.PlannedPosition == null || point.DistanceTo(segment.PlannedPosition) > tolerance)
                { message = "POST_VERIFY_FAILED: segment " + segment.SegmentIndex + " position mismatch."; return false; }
            }
            if (plan.SourceMode == ScheduleSplitSourceMode.WORKING_COPY)
            {
                ScheduleSourceInfo source = ScheduleSplitCollector.Find(doc, plan.SourceScheduleId, plan.SourceInstanceId);
                if (source == null || !string.Equals(source.Fingerprint, plan.SourceFingerprint, StringComparison.Ordinal))
                { message = "POST_VERIFY_FAILED: source Schedule or source placement changed in WORKING_COPY mode."; return false; }
            }
            return true;
        }
    }
}
