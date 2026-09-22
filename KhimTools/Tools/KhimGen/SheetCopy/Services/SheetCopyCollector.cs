using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;
using KhimTools.DetailNumberUpdater.Services;
using KhimTools.ScheduleSplit.Services;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyCollector
    {
        public static List<SheetCopyItem> CollectSheets(Document doc, ViewSheet activeSheet = null)
        {
            var result = new List<SheetCopyItem>();
            if (doc == null) return result;
            var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .Where(s => s != null && !IsPlaceholder(s))
                .OrderBy(s => s.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Id.IntegerValue).ToList();
            foreach (ViewSheet sheet in sheets) result.Add(AnalyzeSheet(doc, sheet, activeSheet != null && sheet.Id == activeSheet.Id));
            return result;
        }

        public static SheetCopyItem AnalyzeSheet(Document doc, ViewSheet sheet, bool selected = true)
        {
            if (doc == null || sheet == null) return new SheetCopyItem { Status = SheetCopyStatusCode.SOURCE_SHEET_MISSING };
            var item = new SheetCopyItem
            {
                SourceSheetId = sheet.Id,
                SourceSheetUniqueId = sheet.UniqueId ?? string.Empty,
                SourceSheetNumber = sheet.SheetNumber ?? string.Empty,
                SourceSheetName = sheet.Name ?? string.Empty,
                IsSelected = selected,
                ViewPolicy = ViewCopyPolicy.DUPLICATE_VIEWS,
                TargetSheetName = sheet.Name ?? string.Empty,
                TargetSheetNumber = SheetCopyNamingService.SuggestTargetNumber(doc, sheet)
            };

            List<FamilyInstance> titleBlocks = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            item.SourceHasTitleBlock = titleBlocks.Count > 0;
            item.SourceTitleBlockCount = titleBlocks.Count;
            if (titleBlocks.Count == 1)
            {
                FamilyInstance block = titleBlocks[0];
                item.SourceTitleBlockTypeId = block.GetTypeId();
                Element type = doc.GetElement(item.SourceTitleBlockTypeId);
                item.SourceTitleBlockTypeUniqueId = type == null ? string.Empty : type.UniqueId;
                item.Contents.Add(new SheetCopyContentItem { SourceElementId = block.Id, SourceUniqueId = block.UniqueId,
                    SourceName = block.Name ?? string.Empty, ContentKind = SheetCopyContentKind.TITLE_BLOCK, Action = SheetCopyAction.CREATE, IsRequired = true });
            }
            else if (titleBlocks.Count > 1)
            {
                item.Status = SheetCopyStatusCode.MULTIPLE_TITLE_BLOCKS;
                item.Message = "The source sheet contains multiple title block instances.";
            }
            else if (titleBlocks.Count == 0)
            {
                item.SourceHasTitleBlock = false;
            }

            foreach (Viewport viewport in new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(Viewport)).Cast<Viewport>()
                .OrderBy(v => v.GetBoxCenter().Y).ThenBy(v => v.GetBoxCenter().X).ThenBy(v => v.Id.IntegerValue))
            {
                View view = doc.GetElement(viewport.ViewId) as View;
                if (view == null) continue;
                bool legend = view.ViewType == ViewType.Legend;
                var content = new SheetCopyContentItem
                {
                    SourceElementId = viewport.Id, SourceUniqueId = viewport.UniqueId,
                    SourceViewId = viewport.ViewId, SourceViewUniqueId = view.UniqueId,
                    SourceName = view.Name ?? string.Empty,
                    ContentKind = legend ? SheetCopyContentKind.LEGEND_VIEWPORT : SheetCopyContentKind.NORMAL_VIEWPORT,
                    Action = legend ? SheetCopyAction.REUSE : SheetCopyAction.CREATE,
                    BoxCenter = viewport.GetBoxCenter(), LabelOffset = SafeLabelOffset(viewport),
                    LabelLineLength = SafeLabelLineLength(viewport), ViewportTypeId = viewport.GetTypeId(),
                    ViewportRotation = SafeRotation(viewport), IsPinned = viewport.Pinned, DetailNumber = ReadDetailNumber(viewport), IsRequired = !legend
                };
                item.Contents.Add(content);
                if (legend) item.LegendCount++; else item.ViewportCount++;
            }

            foreach (ScheduleSheetInstance schedule in new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()
                .OrderBy(s => s.Id.IntegerValue))
            {
                bool revision = false;
                try { revision = schedule.IsTitleblockRevisionSchedule; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] revision schedule probe: " + ex.Message); }
                var content = new SheetCopyContentItem
                {
                    SourceElementId = schedule.Id, SourceUniqueId = schedule.UniqueId,
                    SourceViewId = schedule.ScheduleId, ContentKind = revision ? SheetCopyContentKind.REVISION_RELATED : SheetCopyContentKind.SCHEDULE,
                    Action = revision ? SheetCopyAction.SKIP : SheetCopyAction.REUSE,
                    BoxCenter = TryGetPoint(schedule), IsRequired = !revision,
                    IsSegmented = IsSegmentedSchedule(schedule),
                    SegmentIndex = IsSegmentedSchedule(schedule) ? ScheduleSplitApiAdapter.GetSegmentIndex(schedule) : -1
                };
                item.Contents.Add(content);
                if (revision) item.Message = AppendMessage(item.Message, "SYSTEM_REVISION_SCHEDULE");
                else item.ScheduleCount++;
            }

            foreach (Element element in new FilteredElementCollector(doc, sheet.Id).WhereElementIsNotElementType().ToElements()
                .OrderBy(e => e.Id.IntegerValue))
            {
                if (element is Viewport || element is ScheduleSheetInstance || element is FamilyInstance && element.Category != null && element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_TitleBlocks) continue;
                SheetCopyContentKind kind = ClassifySheetOwnedAnnotation(element);
                var content = new SheetCopyContentItem { SourceElementId = element.Id, SourceUniqueId = element.UniqueId,
                    SourceName = element.Name ?? string.Empty, ContentKind = kind,
                    Action = kind == SheetCopyContentKind.SHEET_ANNOTATION ? SheetCopyAction.COPY : SheetCopyAction.SKIP,
                    IsRequired = false };
                item.Contents.Add(content);
                if (kind == SheetCopyContentKind.SHEET_ANNOTATION) item.AnnotationCount++;
            }
            item.SourceFingerprint = BuildSourceFingerprint(item);
            return item;
        }

        public static string BuildSourceFingerprint(SheetCopyItem item)
        {
            if (item == null) return string.Empty;
            return string.Join("|", item.SourceSheetUniqueId, item.SourceSheetNumber, item.SourceSheetName,
                item.Contents.OrderBy(c => c.SourceUniqueId, StringComparer.Ordinal).Select(c => string.Join(":", c.SourceUniqueId, c.ContentKind, c.BoxCenter == null ? "" : c.BoxCenter.ToString(), c.DetailNumber, c.ViewportTypeId, c.IsPinned)));
        }

        public static SheetCopyItem FindById(Document doc, ElementId id)
        {
            return AnalyzeSheet(doc, doc == null ? null : doc.GetElement(id) as ViewSheet);
        }

        private static SheetCopyContentKind ClassifySheetOwnedAnnotation(Element element)
        {
            if (element == null || element.Category == null) return SheetCopyContentKind.UNSUPPORTED;
            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;
            if (element is TextNote || element is DetailCurve || element is FilledRegion ||
                category == BuiltInCategory.OST_GenericAnnotation || category == BuiltInCategory.OST_IOSDetailGroups ||
                category == BuiltInCategory.OST_RasterImages) return SheetCopyContentKind.SHEET_ANNOTATION;
            if (category == BuiltInCategory.OST_RevisionClouds || category == BuiltInCategory.OST_Revisions) return SheetCopyContentKind.REVISION_RELATED;
            return SheetCopyContentKind.UNSUPPORTED;
        }

        private static bool IsPlaceholder(ViewSheet sheet) { try { return sheet.IsPlaceholder; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] placeholder probe: " + ex.Message); return false; } }
        private static XYZ SafeLabelOffset(Viewport viewport) { try { return viewport.LabelOffset; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] label offset probe: " + ex.Message); return null; } }
        private static double SafeLabelLineLength(Viewport viewport) { try { return viewport.LabelLineLength; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] label length probe: " + ex.Message); return 0; } }
        private static object SafeRotation(Viewport viewport)
        {
            try { var property = typeof(Viewport).GetProperty("Rotation"); return property == null ? null : property.GetValue(viewport, null); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] rotation probe: " + ex.Message); return null; }
        }
        private static string ReadDetailNumber(Viewport viewport)
        {
            try { return DetailNumberService.GetDetailNumber(viewport) ?? string.Empty; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] detail number probe: " + ex.Message); return string.Empty; }
        }
        private static XYZ TryGetPoint(ScheduleSheetInstance schedule)
        {
            try { return schedule.Point; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] schedule point probe: " + ex.Message); return null; }
        }
        private static bool IsSegmentedSchedule(ScheduleSheetInstance schedule)
        {
            try
            {
                ViewSchedule view = schedule.Document.GetElement(schedule.ScheduleId) as ViewSchedule;
                if (view != null && ScheduleSplitApiAdapter.IsSplit(view)) return ScheduleSplitApiAdapter.GetSegmentIndex(schedule) >= 0;
                return false;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] segmented schedule probe: " + ex.Message); return false; }
        }
        private static string AppendMessage(string current, string value) => string.IsNullOrWhiteSpace(current) ? value : current + "; " + value;
    }
}
