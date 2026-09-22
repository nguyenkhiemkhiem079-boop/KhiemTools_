using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Services
{
    /// <summary>Compatibility boundary for the schedule segment API available in Revit 2024 and 2025.</summary>
    public static class ScheduleSplitApiAdapter
    {
        public const string Capability = "ViewSchedule.IsSplit/GetSegmentCount/GetSegmentHeight/Split(IList<double>)/SetSegmentHeight and ScheduleSheetInstance.Create(..., segmentIndex)";

        public static bool IsAvailable => true;

        public static bool CanSplit(ViewSchedule schedule, out string reason)
        {
            reason = string.Empty;
            if (schedule == null) { reason = "Schedule is missing."; return false; }
            if (schedule.IsTitleblockRevisionSchedule) { reason = "System revision schedules cannot be split."; return false; }
            if (schedule.Definition != null && schedule.Definition.IsKeySchedule) { reason = "Key schedules are not supported by this workflow."; return false; }
            if (schedule.IsInternalKeynoteSchedule) { reason = "Internal keynote schedules are not supported by this workflow."; return false; }
            return true;
        }

        public static bool IsSplit(ViewSchedule schedule) => schedule != null && schedule.IsSplit();
        public static int GetSegmentCount(ViewSchedule schedule) => schedule == null ? 0 : (schedule.IsSplit() ? schedule.GetSegmentCount() : 1);

        public static IList<double> GetSegmentHeights(ViewSchedule schedule)
        {
            var result = new List<double>();
            if (schedule == null) return result;
            if (schedule.IsSplit())
            {
                int count = schedule.GetSegmentCount();
                for (int index = 0; index < count; index++) result.Add(schedule.GetSegmentHeight(index));
            }
            else result.Add(GetTotalHeight(schedule));
            return result;
        }

        public static double GetTotalHeight(ViewSchedule schedule)
        {
            if (schedule == null) return 0;
            try
            {
                using (ScheduleHeightsOnSheet heights = schedule.GetScheduleHeightsOnSheet())
                {
                    if (heights == null || !heights.IsValidObject) return 0;
                    return heights.TitleHeight + heights.ColumnHeaderHeight + heights.GetBodyRowHeights().Sum();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ScheduleSplit] height probe: " + ex.Message); return 0; }
        }

        public static void Split(ViewSchedule schedule, IList<double> segmentHeights)
        {
            if (!CanSplit(schedule, out string reason)) throw new InvalidOperationException(reason);
            if (segmentHeights == null || segmentHeights.Count < 2) throw new ArgumentException("At least two segment heights are required.", nameof(segmentHeights));
            schedule.Split(segmentHeights);
        }

        public static void SplitByCount(ViewSchedule schedule, int segmentCount, double totalHeight)
        {
            if (segmentCount < 2) throw new ArgumentOutOfRangeException(nameof(segmentCount));
            double each = totalHeight / segmentCount;
            Split(schedule, Enumerable.Repeat(each, segmentCount).ToList());
        }

        public static void SetSegmentHeight(ViewSchedule schedule, int segmentIndex, double height) => schedule.SetSegmentHeight(segmentIndex, height);
        public static void DeleteSegment(ViewSchedule schedule, int segmentIndex) => schedule.DeleteSegment(segmentIndex);
        public static void MergeSegments(ViewSchedule schedule, int movedSegmentIndex, int targetSegmentIndex) => schedule.MergeSegments(movedSegmentIndex, targetSegmentIndex);
        public static IList<ElementId> GetScheduleInstances(ViewSchedule schedule, int segmentIndex) => schedule.GetScheduleInstances(segmentIndex);

        public static ScheduleSheetInstance CreateSegmentInstance(Document doc, ElementId sheetId, ElementId scheduleId, XYZ point, int segmentIndex)
        {
            if (doc == null || sheetId == null || scheduleId == null || point == null) throw new ArgumentNullException("Schedule segment placement arguments are incomplete.");
            return ScheduleSheetInstance.Create(doc, sheetId, scheduleId, point, segmentIndex);
        }

        public static int GetSegmentIndex(ScheduleSheetInstance instance) => instance == null ? -1 : instance.SegmentIndex;
        public static XYZ GetPosition(ScheduleSheetInstance instance) => instance == null ? null : instance.Point;
    }
}
