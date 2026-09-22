using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyVerificationService
    {
        public static bool Verify(Document doc, ViewSheet source, ViewSheet target, SheetCopyItem item, SheetCopyExecutionResult result, out string message)
        {
            message = string.Empty;
            if (target == null) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": target Sheet does not exist."; return false; }
            if (!string.Equals(target.SheetNumber, item.TargetSheetNumber, StringComparison.OrdinalIgnoreCase)) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": target number mismatch."; return false; }
            if (!string.Equals(target.Name ?? string.Empty, item.TargetSheetName ?? string.Empty, StringComparison.Ordinal)) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": target name mismatch."; return false; }
            if (item.SourceHasTitleBlock)
            {
                int targetTitleBlocks = new FilteredElementCollector(doc, target.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().GetElementCount();
                if (targetTitleBlocks != 1) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": expected one title block."; return false; }
            }
            int expectedViewports = item.Contents.Count(c => (c.ContentKind == SheetCopyContentKind.NORMAL_VIEWPORT || c.ContentKind == SheetCopyContentKind.LEGEND_VIEWPORT) && c.Action != SheetCopyAction.SKIP);
            int actualViewports = new FilteredElementCollector(doc, target.Id).OfClass(typeof(Viewport)).GetElementCount();
            if (actualViewports < expectedViewports) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": required Viewports are missing."; return false; }
            int expectedSchedules = item.Contents.Count(c => c.ContentKind == SheetCopyContentKind.SCHEDULE && c.Action == SheetCopyAction.REUSE && !c.IsSegmented);
            int actualSchedules = new FilteredElementCollector(doc, target.Id).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>().Count(s => !SafeRevision(s));
            if (actualSchedules < expectedSchedules) { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": required Schedules are missing."; return false; }
            SheetCopyItem currentSource = SheetCopyCollector.AnalyzeSheet(doc, source);
            if (!string.IsNullOrWhiteSpace(item.SourceFingerprint) && !string.Equals(item.SourceFingerprint, currentSource.SourceFingerprint, StringComparison.Ordinal))
            { message = SheetCopyStatusCode.POST_VERIFY_FAILED + ": source Sheet changed during copy."; return false; }
            return true;
        }

        public static string CaptureSourceFingerprint(Document doc, ViewSheet source) => SheetCopyCollector.BuildSourceFingerprint(SheetCopyCollector.AnalyzeSheet(doc, source));
        private static bool SafeRevision(ScheduleSheetInstance schedule) { try { return schedule.IsTitleblockRevisionSchedule; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] revision verify probe: " + ex.Message); return false; } }
    }
}
