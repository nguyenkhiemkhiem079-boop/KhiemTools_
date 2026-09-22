using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ScheduleSplit.Models;
using KhimTools.SheetGen.Services;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSplitPlanner
    {
        public static ScheduleSplitPlan BuildPlan(Document doc, ScheduleSplitRequest request)
        {
            var plan = new ScheduleSplitPlan { SourceMode = request == null || request.Options == null ? ScheduleSplitSourceMode.WORKING_COPY : request.Options.SourceMode,
                Mode = request == null || request.Options == null ? ScheduleSplitMode.BY_SEGMENT_COUNT : request.Options.Mode };
            if (doc == null || request == null) return Block(plan, ScheduleSplitStatusCode.SOURCE_MISSING, "Document or request is unavailable.");
            ScheduleSourceInfo source = ScheduleSplitCollector.Find(doc, request.SourceScheduleId, request.SourceInstanceId);
            if (source == null) return Block(plan, ScheduleSplitStatusCode.SOURCE_INSTANCE_MISSING, "The selected ScheduleSheetInstance no longer exists.");
            plan.SourceScheduleId = source.ScheduleId; plan.SourceScheduleUniqueId = source.ScheduleUniqueId;
            plan.SourceInstanceId = source.InstanceId; plan.SourceSheetId = source.SourceSheetId;
            plan.WorkingScheduleId = source.ScheduleId;
            plan.SourceFingerprint = source.Fingerprint;
            plan.TargetSheets.AddRange((request.TargetSheets ?? new List<ScheduleTargetSheet>()).Where(s => s != null && s.IsSelected).OrderBy(s => s.Order).ThenBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase));
            if (source.IsRevisionSchedule) return Block(plan, ScheduleSplitStatusCode.SYSTEM_REVISION_SCHEDULE, "System revision schedules are managed by Revit and are not split.");
            if (source.IsKeySchedule || source.IsInternalSchedule) return Block(plan, ScheduleSplitStatusCode.UNSUPPORTED_SCHEDULE_TYPE, "Key/internal schedules are not supported by this workflow.");
            if (!ScheduleSplitApiAdapter.CanSplit(doc.GetElement(source.ScheduleId) as ViewSchedule, out string capabilityReason)) return Block(plan, source.IsRevisionSchedule ? ScheduleSplitStatusCode.SYSTEM_REVISION_SCHEDULE : ScheduleSplitStatusCode.UNSUPPORTED_SCHEDULE_TYPE, capabilityReason);
            if (request.Options.SourceMode == ScheduleSplitSourceMode.MODIFY_SOURCE_SCHEDULE && source.PlacementInstanceIds.Count > 1 && !request.Options.AllowModifySharedSource)
                return Block(plan, ScheduleSplitStatusCode.SCHEDULE_SHARED_ELSEWHERE, "The source Schedule is placed on multiple Sheets; explicit shared-source approval is required.");
            if (source.IsSplit && !request.Options.ReLayoutExistingSegments) return Block(plan, ScheduleSplitStatusCode.ALREADY_SPLIT, "The source Schedule is already split. Enable relayout to use existing segments.");

            int segmentCount = ResolveSegmentCount(source, request.Options);
            if (source.IsSplit && request.Options.ReLayoutExistingSegments)
            {
                segmentCount = source.SegmentCount;
                if (request.Options.Mode == ScheduleSplitMode.MANUAL_SEGMENT_HEIGHTS && request.Options.ManualSegmentHeightsMillimeters.Count != segmentCount)
                    return Block(plan, ScheduleSplitStatusCode.RESPLIT_UNSUPPORTED, "Relayout uses the existing segment count; manual heights must match it.");
            }
            if (segmentCount < 2) return Block(plan, ScheduleSplitStatusCode.INVALID_SEGMENT_COUNT, "Splitting requires at least two segments.");
            string heightError = string.Empty;
            List<double> heights = source.IsSplit && request.Options.ReLayoutExistingSegments
                ? ScheduleSplitApiAdapter.GetSegmentHeights(doc.GetElement(source.ScheduleId) as ViewSchedule).ToList()
                : ResolveHeights(source, request.Options, segmentCount, out heightError);
            if (heights == null || heights.Count < 2) heightError = "Existing segment heights are unavailable.";
            if (heights == null || heights.Count < 2) return Block(plan, ScheduleSplitStatusCode.INVALID_SEGMENT_HEIGHT, heightError);
            plan.SegmentCount = heights.Count; plan.SegmentHeightsInternal.AddRange(heights);
            if (plan.TargetSheets.Count == 0) return Block(plan, ScheduleSplitStatusCode.INSUFFICIENT_TARGET_SHEETS, "Select at least one existing target Sheet.");
            if (request.Options.Distribution == ScheduleSplitDistribution.ONE_SEGMENT_PER_SHEET && plan.TargetSheets.Count < plan.SegmentCount)
                return Block(plan, ScheduleSplitStatusCode.INSUFFICIENT_TARGET_SHEETS, "One segment per Sheet requires at least one target Sheet per segment.");
            BuildSegments(doc, source, plan, request.Options);
            plan.Fingerprint = BuildFingerprint(source, plan, request.Options);
            return plan;
        }

        public static int ResolveSegmentCount(ScheduleSourceInfo source, ScheduleSplitOptions options)
        {
            if (options == null) return 0;
            if (options.Mode == ScheduleSplitMode.BY_SEGMENT_COUNT) return options.SegmentCount;
            if (options.Mode == ScheduleSplitMode.MANUAL_SEGMENT_HEIGHTS) return options.ManualSegmentHeightsMillimeters.Count;
            if (options.MaxHeightMillimeters <= 0 || source == null) return 0;
            double maxHeight = UnitUtils.ConvertToInternalUnits(options.MaxHeightMillimeters, UnitTypeId.Millimeters);
            return (int)Math.Ceiling(source.TotalHeightInternal / maxHeight);
        }

        private static List<double> ResolveHeights(ScheduleSourceInfo source, ScheduleSplitOptions options, int segmentCount, out string error)
        {
            error = string.Empty;
            if (source == null || options == null) { error = "Schedule geometry is unavailable."; return null; }
            if (options.Mode == ScheduleSplitMode.MANUAL_SEGMENT_HEIGHTS)
            {
                if (options.ManualSegmentHeightsMillimeters.Count != segmentCount) { error = "Manual segment heights do not match the requested segment count."; return null; }
                var manual = options.ManualSegmentHeightsMillimeters.Select(v => UnitUtils.ConvertToInternalUnits(v, UnitTypeId.Millimeters)).ToList();
                if (manual.Any(v => v <= 0)) { error = "Segment heights must be positive."; return null; }
                return manual;
            }
            if (source.TotalHeightInternal <= 0) { error = "Schedule height is unavailable."; return null; }
            double each = source.TotalHeightInternal / segmentCount;
            return Enumerable.Repeat(each, segmentCount).ToList();
        }

        private static void BuildSegments(Document doc, ScheduleSourceInfo source, ScheduleSplitPlan plan, ScheduleSplitOptions options)
        {
            ViewSheet sourceSheet = doc.GetElement(source.SourceSheetId) as ViewSheet;
            XYZ sourcePosition = source.Position;
            for (int index = 0; index < plan.SegmentCount; index++)
            {
                ScheduleTargetSheet target = ResolveTarget(plan.TargetSheets, options.Distribution, index);
                ViewSheet targetSheet = target == null ? null : doc.GetElement(target.SheetId) as ViewSheet;
                XYZ position = ResolvePosition(sourceSheet, targetSheet, sourcePosition, index, plan.SegmentCount, options, plan.SegmentHeightsInternal[index]);
                var segment = new ScheduleSegmentPlan { SegmentIndex = index, HeightInternal = plan.SegmentHeightsInternal[index],
                    TargetSheetId = target == null ? ElementId.InvalidElementId : target.SheetId,
                    TargetSheetNumber = target == null ? string.Empty : target.SheetNumber, PlannedPosition = position };
                if (target == null) { segment.Status = ScheduleSplitStatusCode.TARGET_SHEET_MISSING; segment.Message = "Target Sheet is missing."; }
                else if (position == null) { segment.Status = ScheduleSplitStatusCode.UNKNOWN_SHEET_BOUNDS; segment.Message = "Usable Sheet bounds are unavailable."; }
                plan.Segments.Add(segment);
            }
        }

        public static ScheduleTargetSheet ResolveTarget(IList<ScheduleTargetSheet> targets, ScheduleSplitDistribution distribution, int index)
        {
            if (targets == null || targets.Count == 0) return null;
            if (distribution == ScheduleSplitDistribution.ALL_SEGMENTS_ON_SAME_SHEET) return targets[0];
            return targets[index % targets.Count];
        }

        private static XYZ ResolvePosition(ViewSheet sourceSheet, ViewSheet targetSheet, XYZ sourcePosition, int index, int count, ScheduleSplitOptions options, double height)
        {
            if (targetSheet == null) return null;
            if (options.Anchor == ScheduleSplitAnchor.USER_DEFINED_OFFSET && options.UserDefinedOffset != null) return options.UserDefinedOffset;
            XYZ basePoint = targetSheet.Id == (sourceSheet == null ? ElementId.InvalidElementId : sourceSheet.Id) && sourcePosition != null
                ? sourcePosition : SheetPlacementService.GetPlacementPoint(targetSheet, SheetPlacementAnchor.TopLeft, 25.0);
            if (basePoint == null) return null;
            double gap = UnitUtils.ConvertToInternalUnits(options.SegmentGapMillimeters, UnitTypeId.Millimeters);
            if (options.Direction == ScheduleSplitDirection.HORIZONTAL_COLUMNS) return basePoint + new XYZ(index * gap, 0, 0);
            return basePoint + new XYZ(0, -index * (height + gap), 0);
        }

        public static string BuildFingerprint(ScheduleSourceInfo source, ScheduleSplitPlan plan, ScheduleSplitOptions options)
        {
            return string.Join("|", source == null ? string.Empty : source.Fingerprint, plan.SourceScheduleId.IntegerValue,
                plan.SourceInstanceId.IntegerValue, plan.SegmentCount, options == null ? string.Empty : options.Distribution.ToString(),
                string.Join(",", plan.TargetSheets.Select(s => s.SheetId.IntegerValue.ToString())));
        }

        private static ScheduleSplitPlan Block(ScheduleSplitPlan plan, ScheduleSplitStatusCode status, string message)
        {
            plan.Status = status; plan.Message = message; plan.BlockedConditions.Add(message); return plan;
        }
    }
}
