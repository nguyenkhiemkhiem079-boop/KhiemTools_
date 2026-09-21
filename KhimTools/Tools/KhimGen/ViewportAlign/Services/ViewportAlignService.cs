using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;

namespace KhimTools.ViewportAlign.Services
{
    public enum ArrangeMode
    {
        ViewsAndTitles,
        ViewsOnly,
        TitlesOnly
    }

    public class ViewportAlignOptions
    {
        public ArrangeMode Mode { get; set; } = ArrangeMode.ViewsAndTitles;
        public bool AlignModelViews { get; set; } = true;
        public bool AlignDraftingViews { get; set; } = true;
        public bool AlignLegends { get; set; } = false;
        public bool AlignSchedules { get; set; } = true;
        public string KeywordFilter { get; set; } = "";
    }

    public class TargetViewItem
    {
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId ViewportOrScheduleId { get; set; }
        public string ViewName { get; set; }
        public ViewType ViewType { get; set; }
        public bool IsSchedule { get; set; }
    }

    /// <summary>
    /// Service xử lý căn chỉnh và đồng bộ vị trí Viewport, View Title và Schedule theo chuẩn chuyên nghiệp.
    /// </summary>
    public static class ViewportAlignService
    {
        /// <summary>
        /// Căn chỉnh Viewport đích theo Viewport mẫu (Vị trí BoxCenter và/hoặc View Title LabelOffset).
        /// </summary>
        public static bool AlignViewport(Document doc, Viewport targetVp, Viewport sourceVp, ArrangeMode mode)
        {
            if (doc == null || targetVp == null || sourceVp == null) return false;

            bool moved = false;

            // 1. Căn chỉnh vị trí Khung nhìn (View Location)
            if (mode == ArrangeMode.ViewsAndTitles || mode == ArrangeMode.ViewsOnly)
            {
                XYZ sourceCenter = sourceVp.GetBoxCenter();
                XYZ targetCenter = targetVp.GetBoxCenter();
                XYZ diff = sourceCenter - targetCenter;

                if (diff.GetLength() > 0.001)
                {
                    targetVp.SetBoxCenter(sourceCenter);
                    moved = true;
                }
            }

            // 2. Căn chỉnh Tiêu đề Khung nhìn (View Title Label Offset & Line Length)
            if (mode == ArrangeMode.ViewsAndTitles || mode == ArrangeMode.TitlesOnly)
            {
                try
                {
                    XYZ sourceLabelOffset = sourceVp.LabelOffset;
                    XYZ targetLabelOffset = targetVp.LabelOffset;

                    if ((sourceLabelOffset - targetLabelOffset).GetLength() > 0.001)
                    {
                        targetVp.LabelOffset = sourceLabelOffset;
                        moved = true;
                    }

                    double sourceLineLength = sourceVp.LabelLineLength;
                    if (sourceLineLength > 0.001 && Math.Abs(sourceLineLength - targetVp.LabelLineLength) > 0.001)
                    {
                        targetVp.LabelLineLength = sourceLineLength;
                        moved = true;
                    }
                }
                catch { }
            }

            return moved;
        }

        /// <summary>
        /// Căn chỉnh vị trí Bảng thống kê (ScheduleSheetInstance) theo vị trí của Bảng thống kê mẫu.
        /// </summary>
        public static bool AlignSchedule(Document doc, ScheduleSheetInstance targetSched, ScheduleSheetInstance sourceSched)
        {
            if (doc == null || targetSched == null || sourceSched == null) return false;

            XYZ sourcePoint = sourceSched.Point;
            XYZ targetPoint = targetSched.Point;
            XYZ diff = sourcePoint - targetPoint;

            if (diff.GetLength() > 0.001)
            {
                targetSched.Point = sourcePoint;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Lấy tất cả các View và Schedule đặt trên 1 Sheet.
        /// </summary>
        public static List<TargetViewItem> GetViewsOnSheet(Document doc, ViewSheet sheet)
        {
            var result = new List<TargetViewItem>();
            if (doc == null || sheet == null) return result;

            // 1. Viewports
            var vpIds = sheet.GetAllViewports();
            foreach (ElementId vid in vpIds)
            {
                if (doc.GetElement(vid) is Viewport vp)
                {
                    View v = doc.GetElement(vp.ViewId) as View;
                    if (v != null)
                    {
                        result.Add(new TargetViewItem
                        {
                            SheetId = sheet.Id,
                            SheetNumber = sheet.SheetNumber,
                            SheetName = sheet.Name,
                            ViewId = v.Id,
                            ViewportOrScheduleId = vp.Id,
                            ViewName = v.Name,
                            ViewType = v.ViewType,
                            IsSchedule = false
                        });
                    }
                }
            }

            // 2. ScheduleSheetInstances
            var schedules = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            foreach (var sched in schedules)
            {
                ViewSchedule vs = doc.GetElement(sched.ScheduleId) as ViewSchedule;
                result.Add(new TargetViewItem
                {
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    ViewId = vs?.Id ?? sched.Id,
                    ViewportOrScheduleId = sched.Id,
                    ViewName = vs?.Name ?? (LanguageManager.IsEnglish ? "Schedule" : "Bảng thống kê"),
                    ViewType = ViewType.Schedule,
                    IsSchedule = true
                });
            }

            return result;
        }
    }
}
