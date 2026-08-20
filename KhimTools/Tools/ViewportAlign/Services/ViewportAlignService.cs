using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ViewportAlign.Services
{
    public class ViewportAlignOptions
    {
        public bool AlignModelViews { get; set; } = true;
        public bool AlignDraftingViews { get; set; } = true;
        public bool AlignLegends { get; set; } = false;
        public bool AlignSchedules { get; set; } = true;
        public bool MatchByNameOrType { get; set; } = true;
        public string KeywordFilter { get; set; } = "";
    }

    /// <summary>
    /// Service xử lý đồng bộ vị trí Viewport và Bảng thống kê (Schedules) trên nhiều Sheet.
    /// </summary>
    public static class ViewportAlignService
    {
        /// <summary>
        /// Kiểm tra xem Viewport có thỏa mãn bộ lọc tùy chọn không.
        /// </summary>
        public static bool ShouldAlignViewport(View view, ViewportAlignOptions options)
        {
            if (view == null || options == null) return false;

            // 1. Kiểm tra loại View
            ViewType vt = view.ViewType;
            if (vt == ViewType.Legend)
            {
                if (!options.AlignLegends) return false;
            }
            else if (vt == ViewType.DraftingView)
            {
                if (!options.AlignDraftingViews) return false;
            }
            else if (vt == ViewType.Schedule || vt == ViewType.PanelSchedule || vt == ViewType.ColumnSchedule)
            {
                if (!options.AlignSchedules) return false;
            }
            else
            {
                if (!options.AlignModelViews) return false;
            }

            // 2. Kiểm tra bộ lọc từ khóa nếu có (ví dụ: arc, under, over, str...)
            if (!string.IsNullOrWhiteSpace(options.KeywordFilter))
            {
                string keyword = options.KeywordFilter.Trim().ToLowerInvariant();
                string viewName = (view.Name ?? "").ToLowerInvariant();
                if (!viewName.Contains(keyword)) return false;
            }

            return true;
        }

        /// <summary>
        /// Di chuyển Viewport đích (vpResult) đến đúng vị trí của Viewport nguồn (vpSource) trên Sheet.
        /// </summary>
        public static bool MoveViewportToSource(Document doc, Viewport vpResult, Viewport vpSource)
        {
            if (doc == null || vpResult == null || vpSource == null) return false;

            View viewResult = doc.GetElement(vpResult.ViewId) as View;
            View viewSource = doc.GetElement(vpSource.ViewId) as View;
            if (viewResult == null || viewSource == null) return false;

            // Lưu trạng thái CropBox ban đầu của cả 2 view
            BoundingBoxXYZ savedBoxResult = viewResult.CropBox;
            BoundingBoxXYZ savedBoxSource = viewSource.CropBox;
            bool savedActiveResult = viewResult.CropBoxActive;
            bool savedActiveSource = viewSource.CropBoxActive;
            bool savedVisibleResult = viewResult.CropBoxVisible;
            bool savedVisibleSource = viewSource.CropBoxVisible;

            try
            {
                // Tạm thời mở rộng CropBox để tính toán chính xác gốc tọa độ
                var tempBox = new BoundingBoxXYZ
                {
                    Min = new XYZ(-1000, -1000, -1000),
                    Max = new XYZ(1000, 1000, 1000)
                };

                viewResult.CropBox = tempBox;
                viewSource.CropBox = tempBox;
                viewResult.CropBoxActive = true;
                viewSource.CropBoxActive = true;

                Outline outlineResult = vpResult.GetBoxOutline();
                Outline outlineSource = vpSource.GetBoxOutline();

                if (outlineResult != null && outlineSource != null)
                {
                    XYZ diff = outlineSource.MinimumPoint - outlineResult.MinimumPoint;
                    if (diff.GetLength() > 0.001) // Chỉ di chuyển nếu có độ lệch
                    {
                        ElementTransformUtils.MoveElement(doc, vpResult.Id, diff);
                        return true;
                    }
                }
            }
            finally
            {
                // Khôi phục lại trạng thái CropBox ban đầu an toàn
                viewResult.CropBox = savedBoxResult;
                viewSource.CropBox = savedBoxSource;
                viewResult.CropBoxActive = savedActiveResult;
                viewSource.CropBoxActive = savedActiveSource;
                viewResult.CropBoxVisible = savedVisibleResult;
                viewSource.CropBoxVisible = savedVisibleSource;
            }

            return false;
        }

        /// <summary>
        /// Đồng bộ vị trí các Bảng thống kê (ScheduleSheetInstance) trên Sheet mục tiêu theo Sheet nguồn.
        /// </summary>
        public static int AlignSchedules(Document doc, ViewSheet targetSheet, ViewSheet sourceSheet)
        {
            if (doc == null || targetSheet == null || sourceSheet == null) return 0;

            var sourceSchedules = new FilteredElementCollector(doc, sourceSheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            var targetSchedules = new FilteredElementCollector(doc, targetSheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            if (!sourceSchedules.Any() || !targetSchedules.Any()) return 0;

            int moved = 0;
            foreach (var targetSched in targetSchedules)
            {
                // Khớp Schedule theo ScheduleId hoặc tên
                var matchSource = sourceSchedules.FirstOrDefault(s => s.ScheduleId == targetSched.ScheduleId)
                                  ?? sourceSchedules.FirstOrDefault();

                if (matchSource != null)
                {
                    XYZ diff = matchSource.Point - targetSched.Point;
                    if (diff.GetLength() > 0.001)
                    {
                        ElementTransformUtils.MoveElement(doc, targetSched.Id, diff);
                        moved++;
                    }
                }
            }

            return moved;
        }
    }
}
