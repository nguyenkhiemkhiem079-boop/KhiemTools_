using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.ViewportAlign.Services
{
    /// <summary>
    /// Service xử lý đồng bộ vị trí Viewport trên nhiều Sheet (Bản vẽ).
    /// Triển khai từ tham chiếu pyRevit và mở rộng an toàn chống lỗi.
    /// </summary>
    public static class ViewportAlignService
    {
        /// <summary>
        /// Kiểm tra xem View có phải là loại cần bỏ qua không (Legend, Note, Schedule, Keynote...).
        /// </summary>
        public static bool IsSkipView(View view)
        {
            if (view == null) return true;

            ViewType vt = view.ViewType;
            if (vt == ViewType.Legend ||
                vt == ViewType.DraftingView ||
                vt == ViewType.Schedule ||
                vt == ViewType.PanelSchedule ||
                vt == ViewType.ColumnSchedule)
            {
                return true;
            }

            string name = (view.Name ?? "").ToLowerInvariant();
            if (name.Contains("legend") ||
                name.Contains("note") ||
                name.Contains("general") ||
                name.Contains("symbol") ||
                name.Contains("keynote") ||
                name.Contains("schedule") ||
                name.Contains("list"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Di chuyển Viewport đích (vpResult) đến đúng vị trí của Viewport nguồn (vpSource) trên Sheet.
        /// </summary>
        public static void MoveViewportToSource(Document doc, Viewport vpResult, Viewport vpSource)
        {
            if (doc == null || vpResult == null || vpSource == null) return;

            View viewResult = doc.GetElement(vpResult.ViewId) as View;
            View viewSource = doc.GetElement(vpSource.ViewId) as View;
            if (viewResult == null || viewSource == null) return;

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
        }
    }
}
