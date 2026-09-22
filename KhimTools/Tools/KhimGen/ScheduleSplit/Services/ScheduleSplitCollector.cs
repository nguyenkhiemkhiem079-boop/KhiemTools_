using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ScheduleSplit.Models;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSplitCollector
    {
        public static List<ScheduleSourceInfo> Collect(Document doc, ViewSheet activeSheet = null)
        {
            var result = new List<ScheduleSourceInfo>();
            if (doc == null) return result;
            var allowedSheets = activeSheet == null ? null : new HashSet<ElementId> { activeSheet.Id };
            var instances = new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()
                .Where(i => !IsRevision(i) && (allowedSheets == null || allowedSheets.Contains(i.OwnerViewId)))
                .OrderBy(i => i.OwnerViewId.IntegerValue).ThenBy(i => i.Point == null ? double.MaxValue : i.Point.Y)
                .ThenBy(i => i.Point == null ? double.MaxValue : i.Point.X).ThenBy(i => i.Id.IntegerValue).ToList();
            foreach (ScheduleSheetInstance instance in instances)
            {
                ViewSchedule schedule = doc.GetElement(instance.ScheduleId) as ViewSchedule;
                ViewSheet sheet = doc.GetElement(instance.OwnerViewId) as ViewSheet;
                if (schedule == null || sheet == null) continue;
                result.Add(Analyze(doc, schedule, instance, sheet));
            }
            return result;
        }

        public static ScheduleSourceInfo Find(Document doc, ElementId scheduleId, ElementId instanceId)
        {
            if (doc == null || scheduleId == null || instanceId == null) return null;
            ViewSchedule schedule = doc.GetElement(scheduleId) as ViewSchedule;
            ScheduleSheetInstance instance = doc.GetElement(instanceId) as ScheduleSheetInstance;
            ViewSheet sheet = instance == null ? null : doc.GetElement(instance.OwnerViewId) as ViewSheet;
            return schedule == null || instance == null || sheet == null ? null : Analyze(doc, schedule, instance, sheet);
        }

        public static ScheduleSourceInfo Analyze(Document doc, ViewSchedule schedule, ScheduleSheetInstance sourceInstance, ViewSheet sourceSheet)
        {
            var info = new ScheduleSourceInfo
            {
                ScheduleId = schedule.Id,
                ScheduleUniqueId = schedule.UniqueId ?? string.Empty,
                InstanceId = sourceInstance.Id,
                SourceSheetId = sourceSheet.Id,
                SourceSheetNumber = sourceSheet.SheetNumber ?? string.Empty,
                SourceSheetName = sourceSheet.Name ?? string.Empty,
                ScheduleName = schedule.Name ?? string.Empty,
                IsSplit = ScheduleSplitApiAdapter.IsSplit(schedule),
                SegmentCount = ScheduleSplitApiAdapter.GetSegmentCount(schedule),
                SegmentIndex = ScheduleSplitApiAdapter.GetSegmentIndex(sourceInstance),
                Position = ScheduleSplitApiAdapter.GetPosition(sourceInstance),
                Bounds = SafeBounds(sourceInstance, sourceSheet),
                TotalHeightInternal = ScheduleSplitApiAdapter.GetTotalHeight(schedule),
                IsRevisionSchedule = schedule.IsTitleblockRevisionSchedule || IsRevision(sourceInstance),
                IsKeySchedule = schedule.Definition != null && schedule.Definition.IsKeySchedule,
                IsInternalSchedule = schedule.IsInternalKeynoteSchedule
            };
            foreach (ScheduleSheetInstance instance in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()
                .Where(i => i.ScheduleId == schedule.Id).OrderBy(i => i.OwnerViewId.IntegerValue).ThenBy(i => i.Id.IntegerValue))
            {
                info.PlacementInstanceIds.Add(instance.Id);
                info.PlacementSheetIds.Add(instance.OwnerViewId);
            }
            info.Fingerprint = BuildFingerprint(info);
            return info;
        }

        public static string BuildFingerprint(ScheduleSourceInfo info)
        {
            if (info == null) return string.Empty;
            string placements = string.Join(",", info.PlacementInstanceIds.Select(id => id.IntegerValue.ToString()));
            string position = info.Position == null ? string.Empty : info.Position.X + ":" + info.Position.Y + ":" + info.Position.Z;
            return string.Join("|", info.ScheduleUniqueId, info.InstanceId.IntegerValue, info.SourceSheetId.IntegerValue,
                info.IsSplit, info.SegmentCount, info.SegmentIndex, position, placements);
        }

        public static bool IsRevision(ScheduleSheetInstance instance)
        {
            try { return instance != null && instance.IsTitleblockRevisionSchedule; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ScheduleSplit] revision probe: " + ex.Message); return false; }
        }

        private static BoundingBoxXYZ SafeBounds(Element instance, ViewSheet sheet)
        {
            try { return instance == null || sheet == null ? null : instance.get_BoundingBox(sheet); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ScheduleSplit] bounds probe: " + ex.Message); return null; }
        }
    }
}
