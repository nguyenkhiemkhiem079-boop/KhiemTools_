using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Kết quả đánh giá nhanh an toàn cốt thép (Hàm lượng thép & Hình học Containment)
    /// </summary>
    public class RebarSafetyResult
    {
        public bool IsValid => IsRatioValid && IsContainmentValid;
        public bool IsRatioValid { get; set; } = true;
        public bool IsContainmentValid { get; set; } = true;
        public double RatioPercent { get; set; }
        public int OutOfBoundsCount { get; set; }
        public string RatioMessage { get; set; }
        public string ContainmentMessage { get; set; }
        public string FullDisplayText { get; set; }
        public System.Drawing.Color StatusColor { get; set; } = System.Drawing.Color.FromArgb(46, 125, 50); // Green
    }

    /// <summary>
    /// Tiện ích kiểm tra nhanh an toàn cốt thép cho Revit Elements:
    /// 1. Kiểm tra hàm lượng thép (gọi từ IRebarDesignStandard).
    /// 2. So sánh BoundingBox của các thanh Rebar với Host Element (kèm dung sai an toàn).
    /// </summary>
    public static class RebarSafetyValidator
    {
        private static readonly System.Drawing.Color ColorSuccess = System.Drawing.Color.FromArgb(46, 125, 50);    // Green
        private static readonly System.Drawing.Color ColorWarning = System.Drawing.Color.FromArgb(198, 40, 40);   // Dark Red / Orange

        /// <summary>
        /// Kiểm tra nhanh hình học: Xem có thanh thép nào vượt ra ngoài BoundingBox của cấu kiện host (kèm dung sai cover ~15-20mm) hay không.
        /// </summary>
        public static (int outCount, string warning) CheckRebarContainment(Element host, IEnumerable<Rebar> rebars, double toleranceMm = 20.0)
        {
            if (host == null || rebars == null) return (0, null);

            BoundingBoxXYZ hostBox = host.get_BoundingBox(null);
            if (hostBox == null) return (0, null);

            double tolFeet = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            XYZ minBound = new XYZ(hostBox.Min.X - tolFeet, hostBox.Min.Y - tolFeet, hostBox.Min.Z - tolFeet);
            XYZ maxBound = new XYZ(hostBox.Max.X + tolFeet, hostBox.Max.Y + tolFeet, hostBox.Max.Z + tolFeet);

            bool isColumn = host.Category != null &&
                (host.Category.BuiltInCategory == BuiltInCategory.OST_Columns ||
                 host.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns);

            int outCount = 0;
            foreach (var r in rebars)
            {
                if (r == null || !r.IsValidObject) continue;
                BoundingBoxXYZ rBox = r.get_BoundingBox(null);
                if (rBox == null) continue;

                // Kiểm tra phương X, Y (tiết diện ngang)
                bool isOut = (rBox.Min.X < minBound.X || rBox.Max.X > maxBound.X ||
                              rBox.Min.Y < minBound.Y || rBox.Max.Y > maxBound.Y);

                // Kiểm tra phương Z: Với dầm/sàn, kiểm tra chặt Z. Với cột, cho phép thép chờ (dowel) nhô lên trên/dưới một khoảng hợp lý
                if (!isColumn)
                {
                    if (rBox.Min.Z < minBound.Z || rBox.Max.Z > maxBound.Z)
                    {
                        isOut = true;
                    }
                }

                if (isOut)
                {
                    outCount++;
                }
            }

            string warn = outCount > 0
                ? $"⚠ Phát hiện {outCount} thanh thép có thể vượt ra ngoài biên cấu kiện — kiểm tra lại kích thước/cover"
                : null;

            return (outCount, warn);
        }

        /// <summary>
        /// Đánh giá toàn diện an toàn cho Cột (Hàm lượng thép + Hình học).
        /// </summary>
        public static RebarSafetyResult EvaluateColumn(
            Element host,
            IEnumerable<Rebar> createdRebars,
            double totalAsMm2,
            double sectionAreaMm2,
            IRebarDesignStandard standard,
            double toleranceMm = 20.0)
        {
            var result = new RebarSafetyResult();
            standard = standard ?? new EurocodeRebarStandard();

            // 1. Kiểm tra hàm lượng thép
            var (isRatioValid, ratioPercent, ratioMsg) = standard.ValidateColumnSteelRatio(totalAsMm2, sectionAreaMm2);
            result.IsRatioValid = isRatioValid;
            result.RatioPercent = ratioPercent;
            result.RatioMessage = ratioMsg;

            // 2. Kiểm tra hình học
            var (outCount, containmentMsg) = CheckRebarContainment(host, createdRebars, toleranceMm);
            result.OutOfBoundsCount = outCount;
            result.IsContainmentValid = (outCount == 0);
            result.ContainmentMessage = containmentMsg;

            // 3. Định dạng chuỗi hiển thị & màu sắc
            var messages = new List<string>();
            if (isRatioValid)
            {
                messages.Add($"✓ Hàm lượng thép: {ratioPercent:F2}% (Đạt)");
            }
            else
            {
                messages.Add($"⚠ Hàm lượng thép: {ratioPercent:F2}% ({ratioMsg ?? "Không đạt tiêu chuẩn"})");
            }

            if (!result.IsContainmentValid && !string.IsNullOrEmpty(containmentMsg))
            {
                messages.Add(containmentMsg);
            }

            result.FullDisplayText = string.Join("\n", messages);
            result.StatusColor = (result.IsRatioValid && result.IsContainmentValid) ? ColorSuccess : ColorWarning;

            return result;
        }

        /// <summary>
        /// Đánh giá toàn diện an toàn cho Dầm (Hàm lượng thép + Hình học).
        /// </summary>
        public static RebarSafetyResult EvaluateBeam(
            Element host,
            IEnumerable<Rebar> createdRebars,
            double topAsMm2,
            double botAsMm2,
            double bMm,
            double dMm,
            IRebarDesignStandard standard,
            double toleranceMm = 20.0)
        {
            var result = new RebarSafetyResult();
            standard = standard ?? new EurocodeRebarStandard();

            // 1. Kiểm tra hàm lượng thép
            var (isRatioValid, topRatio, botRatio, ratioMsg) = standard.ValidateBeamSteelRatio(topAsMm2, botAsMm2, bMm, dMm);
            result.IsRatioValid = isRatioValid;
            result.RatioPercent = Math.Max(topRatio, botRatio);
            result.RatioMessage = ratioMsg;

            // 2. Kiểm tra hình học
            var (outCount, containmentMsg) = CheckRebarContainment(host, createdRebars, toleranceMm);
            result.OutOfBoundsCount = outCount;
            result.IsContainmentValid = (outCount == 0);
            result.ContainmentMessage = containmentMsg;

            // 3. Định dạng chuỗi hiển thị & màu sắc
            var messages = new List<string>();
            if (isRatioValid)
            {
                messages.Add($"✓ Hàm lượng thép: Top {topRatio:F2}% | Bot {botRatio:F2}% (Đạt)");
            }
            else
            {
                messages.Add($"⚠ Hàm lượng thép: Top {topRatio:F2}% | Bot {botRatio:F2}% ({ratioMsg ?? "Không đạt"})");
            }

            if (!result.IsContainmentValid && !string.IsNullOrEmpty(containmentMsg))
            {
                messages.Add(containmentMsg);
            }

            result.FullDisplayText = string.Join("\n", messages);
            result.StatusColor = (result.IsRatioValid && result.IsContainmentValid) ? ColorSuccess : ColorWarning;

            return result;
        }

        /// <summary>
        /// Đánh giá hình học an toàn cho Sàn.
        /// </summary>
        public static RebarSafetyResult EvaluateSlab(
            Element host,
            IEnumerable<Rebar> createdRebars,
            double toleranceMm = 20.0)
        {
            var result = new RebarSafetyResult();
            var (outCount, containmentMsg) = CheckRebarContainment(host, createdRebars, toleranceMm);
            result.OutOfBoundsCount = outCount;
            result.IsContainmentValid = (outCount == 0);
            result.ContainmentMessage = containmentMsg;

            if (result.IsContainmentValid)
            {
                result.FullDisplayText = "✓ Cốt thép sàn nằm trọn trong hình học cấu kiện (Đạt)";
                result.StatusColor = ColorSuccess;
            }
            else
            {
                result.FullDisplayText = containmentMsg;
                result.StatusColor = ColorWarning;
            }

            return result;
        }
    }
}