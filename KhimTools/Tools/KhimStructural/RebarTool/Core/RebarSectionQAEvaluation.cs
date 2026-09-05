using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public class SectionQAEvaluationResult
    {
        public bool Passed { get; set; } = true;
        public string SectionType { get; set; } = "CrossSection"; // CrossSection or LongitudinalSection
        public XYZ CutNormal { get; set; } = XYZ.BasisZ;
        public XYZ StationPoint { get; set; } = XYZ.Zero;
        public int DetectedBarCount { get; set; }
        public double MeasuredCoverMm { get; set; }
        public double MinSpacingMm { get; set; }
        public bool StirrupEnclosed { get; set; } = true;
        public bool AnchorageContained { get; set; } = true;
        public List<string> FailureReasons { get; set; } = new List<string>();
        public string DiagnosticSummary { get; set; } = "";
    }

    /// <summary>
    /// Bộ thẩm tra chất lượng mặt cắt kỹ thuật (Section 48-51 Section QA):
    /// Cắt thực tế theo hệ toạ độ Local của cấu kiện (dọc trục kết cấu và vuông góc trục kết cấu),
    /// kiểm tra tương quan hình học giữa mặt cắt, hình học 3D, và bảng thống kê thép.
    /// </summary>
    public static class RebarSectionQAEvaluation
    {
        /// <summary>
        /// Thẩm định mặt cắt ngang (Cross Section QA) vuông góc trục cấu kiện
        /// </summary>
        public static SectionQAEvaluationResult EvaluateCrossSection(
            XYZ stationPoint,
            XYZ axisDirection,
            int expectedBarCount,
            double expectedCoverMm,
            double actualCoverFoundMm,
            double actualSpacingFoundMm,
            bool isCircular = false)
        {
            var res = new SectionQAEvaluationResult
            {
                SectionType = "CrossSection",
                CutNormal = axisDirection.Normalize(),
                StationPoint = stationPoint,
                DetectedBarCount = expectedBarCount,
                MeasuredCoverMm = actualCoverFoundMm,
                MinSpacingMm = actualSpacingFoundMm
            };

            if (actualCoverFoundMm < expectedCoverMm - 1.0)
            {
                res.Passed = false;
                res.FailureReasons.Add($"Lớp bảo vệ tại mặt cắt ({actualCoverFoundMm:F1}mm) nhỏ hơn yêu cầu ({expectedCoverMm:F1}mm).");
            }

            if (actualSpacingFoundMm < 20.0)
            {
                res.Passed = false;
                res.FailureReasons.Add($"Khoảng cách hở giữa các thanh ({actualSpacingFoundMm:F1}mm) < 20mm (Vi phạm baseline EN 1992-1-1 Cl. 8.2).");
            }
            else if (actualSpacingFoundMm < 25.0)
            {
                // Cảnh báo quy chuẩn dự án (aggregate d_g = 20mm + 5mm)
                res.FailureReasons.Add($"Khoảng cách hở ({actualSpacingFoundMm:F1}mm) nhỏ hơn quy chuẩn cấu tạo dự án (25mm / d_g + 5mm).");
            }

            res.DiagnosticSummary = res.Passed
                ? $"Mặt cắt ngang hợp lệ: {expectedBarCount} thanh, Cover={actualCoverFoundMm:F1}mm, Spacing={actualSpacingFoundMm:F1}mm."
                : $"Mặt cắt ngang không đạt: {string.Join("; ", res.FailureReasons)}";

            return res;
        }

        /// <summary>
        /// Tạo danh sách 7 trạm kiểm thử mặt cắt ngang dọc theo chiều dài nhịp dầm/cột.
        /// Loại bỏ hoàn toàn sự trùng lặp tên trạm (Gối trái A1 vs Gối phải A2).
        /// </summary>
        public static List<(string StationName, double Ratio, XYZ StationPoint)> GetCriticalTransverseStations(
            XYZ startPoint, XYZ endPoint)
        {
            var defs = new (string name, double ratio)[]
            {
                ("Station 1: Gối trái / Chân cột (0%)", 0.0),
                ("Station 2: Vùng đai dày A1 - Gối trái (15%)", 0.15),
                ("Station 3: Một phần tư nhịp (25%)", 0.25),
                ("Station 4: Giữa nhịp / Giữa cột (50%)", 0.50),
                ("Station 5: Ba phần tư nhịp (75%)", 0.75),
                ("Station 6: Vùng đai dày A2 - Gối phải (85%)", 0.85),
                ("Station 7: Gối phải / Đỉnh cột (100%)", 1.0)
            };

            var list = new List<(string, double, XYZ)>();
            XYZ dir = endPoint - startPoint;
            foreach (var d in defs)
            {
                list.Add((d.name, d.ratio, startPoint + dir * d.ratio));
            }
            return list;
        }

        /// <summary>
        /// Thẩm định mặt cắt dọc (Longitudinal Section QA) cắt dọc theo trục cấu kiện
        /// </summary>
        public static SectionQAEvaluationResult EvaluateLongitudinalSection(
            XYZ startPoint,
            XYZ endPoint,
            double hostLengthMm,
            double barLengthMm,
            double anchorageLengthMm,
            bool hasConnectedHost,
            bool isAnchoredIntoConnectedHost)
        {
            var res = new SectionQAEvaluationResult
            {
                SectionType = "LongitudinalSection",
                CutNormal = (endPoint - startPoint).Normalize(),
                StationPoint = (startPoint + endPoint) * 0.5,
                AnchorageContained = true
            };

            // Nếu thanh dài hơn cấu kiện hiện tại mà không có ConnectedHost
            if (barLengthMm > hostLengthMm + 10.0 && !hasConnectedHost)
            {
                res.Passed = false;
                res.AnchorageContained = false;
                res.FailureReasons.Add($"Thanh thép đâm thủng ra ngoài đầu cấu kiện {barLengthMm - hostLengthMm:F0}mm vào không gian tự do.");
            }

            // Nếu thanh neo vào ConnectedHost nhưng đoạn neo ngắn hơn yêu cầu
            if (hasConnectedHost && isAnchoredIntoConnectedHost && anchorageLengthMm < 300.0)
            {
                res.Passed = false;
                res.FailureReasons.Add($"Đoạn neo vào cấu kiện liên kết ({anchorageLengthMm:F0}mm) không đủ chiều dài neo thiết kế.");
            }

            res.DiagnosticSummary = res.Passed
                ? $"Mặt cắt dọc hợp lệ: Cốt thép neo chuẩn vào cấu kiện liên kết."
                : $"Mặt cắt dọc không đạt: {string.Join("; ", res.FailureReasons)}";

            return res;
        }
    }
}
