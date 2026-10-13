using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Services
{
    /// <summary>Runtime capability boundary; older Revit versions fail closed when schedule-segment APIs are absent.</summary>
    public static class ScheduleSplitApiAdapter
    {
        public const string Capability = "ViewSchedule.IsSplit/GetSegmentCount/GetSegmentHeight/Split(IList<double>)/SetSegmentHeight and ScheduleSheetInstance.Create(..., segmentIndex)";

        public static bool IsAvailable => typeof(ViewSchedule).GetMethod("Split", new[] { typeof(IList<double>) }) != null &&
            typeof(ViewSchedule).GetMethod("GetSegmentCount", Type.EmptyTypes) != null &&
            typeof(ScheduleSheetInstance).GetMethod("Create", new[] { typeof(Document), typeof(ElementId), typeof(ElementId), typeof(XYZ), typeof(int) }) != null &&
            typeof(ScheduleSheetInstance).GetProperty("SegmentIndex") != null;

        public static bool CanSplit(ViewSchedule schedule, out string reason)
        {
            reason = string.Empty;
            if (schedule == null) { reason = "Schedule is missing."; return false; }
            if (!IsAvailable) { reason = "The loaded Revit version does not expose schedule segment APIs; Split & Place is unavailable (Revit 2023+ required)."; return false; }
            if (schedule.IsTitleblockRevisionSchedule) { reason = "System revision schedules cannot be split."; return false; }
            if (schedule.Definition != null && schedule.Definition.IsKeySchedule) { reason = "Key schedules are not supported by this workflow."; return false; }
            if (schedule.IsInternalKeynoteSchedule) { reason = "Internal keynote schedules are not supported by this workflow."; return false; }
            return true;
        }

        public static bool IsSplit(ViewSchedule schedule) => schedule != null && IsAvailable && (bool)Invoke(schedule, "IsSplit", Type.EmptyTypes);
        public static int GetSegmentCount(ViewSchedule schedule) => schedule == null ? 0 : (IsSplit(schedule) ? (int)Invoke(schedule, "GetSegmentCount", Type.EmptyTypes) : 1);

        public static IList<double> GetSegmentHeights(ViewSchedule schedule)
        {
            var result = new List<double>();
            if (schedule == null) return result;
            if (IsSplit(schedule))
            {
                int count = GetSegmentCount(schedule);
                for (int index = 0; index < count; index++) result.Add((double)Invoke(schedule, "GetSegmentHeight", new[] { typeof(int) }, index));
            }
            else result.Add(GetTotalHeight(schedule));
            return result;
        }

        public static double GetTotalHeight(ViewSchedule schedule)
        {
            if (schedule == null || !IsAvailable) return 0;
            try
            {
                object heights = Invoke(schedule, "GetScheduleHeightsOnSheet", Type.EmptyTypes);
                try
                {
                    if (heights == null) return 0;
                    Type heightType = heights.GetType();
                    if (!(bool)heightType.GetProperty("IsValidObject").GetValue(heights)) return 0;
                    return (double)heightType.GetProperty("TitleHeight").GetValue(heights) +
                        (double)heightType.GetProperty("ColumnHeaderHeight").GetValue(heights) +
                        ((IEnumerable<double>)Invoke(heights, "GetBodyRowHeights", Type.EmptyTypes)).Sum();
                }
                finally { (heights as IDisposable)?.Dispose(); }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ScheduleSplit] height probe: " + ex.Message); return 0; }
        }

        public static void Split(ViewSchedule schedule, IList<double> segmentHeights)
        {
            if (!CanSplit(schedule, out string reason)) throw new InvalidOperationException(reason);
            if (segmentHeights == null || segmentHeights.Count < 2) throw new ArgumentException("At least two segment heights are required.", nameof(segmentHeights));
            Invoke(schedule, "Split", new[] { typeof(IList<double>) }, segmentHeights);
        }

        public static void SplitByCount(ViewSchedule schedule, int segmentCount, double totalHeight)
        {
            if (segmentCount < 2) throw new ArgumentOutOfRangeException(nameof(segmentCount));
            double each = totalHeight / segmentCount;
            Split(schedule, Enumerable.Repeat(each, segmentCount).ToList());
        }

        public static void SetSegmentHeight(ViewSchedule schedule, int segmentIndex, double height) => Invoke(schedule, "SetSegmentHeight", new[] { typeof(int), typeof(double) }, segmentIndex, height);
        public static void DeleteSegment(ViewSchedule schedule, int segmentIndex) => Invoke(schedule, "DeleteSegment", new[] { typeof(int) }, segmentIndex);
        public static void MergeSegments(ViewSchedule schedule, int movedSegmentIndex, int targetSegmentIndex) => Invoke(schedule, "MergeSegments", new[] { typeof(int), typeof(int) }, movedSegmentIndex, targetSegmentIndex);
        public static IList<ElementId> GetScheduleInstances(ViewSchedule schedule, int segmentIndex) => ((IEnumerable<ElementId>)Invoke(schedule, "GetScheduleInstances", new[] { typeof(int) }, segmentIndex)).ToList();

        public static ScheduleSheetInstance CreateSegmentInstance(Document doc, ElementId sheetId, ElementId scheduleId, XYZ point, int segmentIndex)
        {
            if (doc == null || sheetId == null || scheduleId == null || point == null) throw new ArgumentNullException("Schedule segment placement arguments are incomplete.");
            return (ScheduleSheetInstance)Invoke(null, "Create", new[] { typeof(Document), typeof(ElementId), typeof(ElementId), typeof(XYZ), typeof(int) },
                doc, sheetId, scheduleId, point, segmentIndex);
        }

        public static int GetSegmentIndex(ScheduleSheetInstance instance) => instance == null || !IsAvailable ? -1 : (int)typeof(ScheduleSheetInstance).GetProperty("SegmentIndex").GetValue(instance);
        public static void SetSegmentIndex(ScheduleSheetInstance instance, int index)
        {
            if (instance == null || !IsAvailable) throw new NotSupportedException("Schedule segment reassignment is unavailable in the loaded Revit API.");
            typeof(ScheduleSheetInstance).GetProperty("SegmentIndex").SetValue(instance, index);
        }
        public static XYZ GetPosition(ScheduleSheetInstance instance) => instance == null ? null : instance.Point;

        private static object Invoke(object target, string name, Type[] parameterTypes, params object[] arguments)
        {
            Type apiType = target?.GetType() ?? typeof(ScheduleSheetInstance);
            MethodInfo method = apiType.GetMethod(name, parameterTypes);
            if (method == null) throw new NotSupportedException("Loaded Revit API does not expose " + apiType.Name + "." + name + "; no schedule mutation was performed.");
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
