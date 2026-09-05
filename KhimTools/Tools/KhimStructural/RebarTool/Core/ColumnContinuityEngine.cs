using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;

namespace KhimTools.RebarTool.Core
{
    public enum ColumnTransitionType
    {
        ContinuousSameSize,
        CrankedReduction,       // Giảm tiết diện <= 75mm (uốn 1:6)
        LargeReductionDoweled,  // Giảm tiết diện > 75mm (bẻ neo đỉnh + đặt thép chờ riêng)
        TopRoofTerminated       // Cột đỉnh / mái (bẻ móc 90° vào nắp sàn mái)
    }

    public class ColumnContinuityInfo
    {
        public FamilyInstance CurrentColumn { get; set; }
        public FamilyInstance ColumnBelow { get; set; }
        public FamilyInstance ColumnAbove { get; set; }

        public ColumnTransitionType TransitionType { get; set; } = ColumnTransitionType.ContinuousSameSize;

        /// <summary>Độ chênh lệch mép cột theo phương B (feet)</summary>
        public double OffsetB { get; set; }

        /// <summary>Độ chênh lệch mép cột theo phương H (feet)</summary>
        public double OffsetH { get; set; }

        /// <summary>Độ lệch lớn nhất tại bất kỳ mép nào (feet)</summary>
        public double MaxEdgeOffsetFeet { get; set; }

        /// <summary>Độ dốc bẻ cổ chai (mặc định 1:6 theo quy chuẩn cấu tạo dự án / ACI 318-19 §25.7.1.4 / BS 8666 / IStructE Manual)</summary>
        public double CrankSlope { get; set; } = 6.0;

        /// <summary>Chiều cao đoạn bẻ cổ chai yêu cầu = 6 × (offset + d_b)</summary>
        public double CrankHeightFeet { get; set; }

        /// <summary>Liệu có cần đặt thép chờ rời (Starter Dowels) vì độ thu tiết diện vượt quá 75mm (Project Detailing Rule)</summary>
        public bool RequiresSeparateDowels { get; set; } = false;

        /// <summary>Chiều dài nối chồng chuẩn Ls (feet)</summary>
        public double LapLengthFeet { get; set; }

        /// <summary>
        /// Khoảng cách so le tim-đến-tim giữa 2 mối nối lân cận (feet) = 1.3 × Ls.
        /// CƠ SỞ KỸ THUẬT:
        /// EN 1992-1-1 Cl. 8.7.2 & Figure 8.8 yêu cầu khoảng cách hở giữa 2 đầu mối nối so le a >= 0.3*l_0.
        /// Do đó, khoảng cách tim-đến-tim s_stagger >= 1.0*l_0 + 0.3*l_0 = 1.3*l_0 để hai mối nối không bị xem là cùng mặt cắt.
        /// (LƯU Ý: Đây là khoảng cách bố trí so le hình học, KHÔNG PHẢI hệ số nhân chiều dài nối chồng alpha_6).
        /// </summary>
        public double StaggerOffsetFeet { get; set; }
    }

    public static class ColumnContinuityEngine
    {
        private const double MaxCrankOffsetLimitMm = 75.0; // Project Detailing Practice Rule (ACI 318-19 §25.7.1.3 / IStructE Manual)

        /// <summary>
        /// Tự động tìm cột trên (ColumnAbove) và cột dưới (ColumnBelow) dọc theo trục đứng (Vertical Stack)
        /// </summary>
        public static (FamilyInstance Below, FamilyInstance Above) FindAdjacentColumns(Document doc, FamilyInstance column)
        {
            if (doc == null || column == null) return (null, null);

            BoundingBoxXYZ bb = column.get_BoundingBox(null);
            if (bb == null) return (null, null);

            double curBaseZ = bb.Min.Z;
            double curTopZ = bb.Max.Z;
            XYZ curCenter = new XYZ((bb.Min.X + bb.Max.X) / 2.0, (bb.Min.Y + bb.Max.Y) / 2.0, 0);

            var allCols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(c => c.Id != column.Id)
                .ToList();

            FamilyInstance colBelow = null;
            FamilyInstance colAbove = null;
            double minBelowDist = double.MaxValue;
            double minAboveDist = double.MaxValue;
            double tolHoriz = 2.0; // ~600mm sai số tâm

            foreach (var c in allCols)
            {
                BoundingBoxXYZ cbb = c.get_BoundingBox(null);
                if (cbb == null) continue;

                XYZ cCenter = new XYZ((cbb.Min.X + cbb.Max.X) / 2.0, (cbb.Min.Y + cbb.Max.Y) / 2.0, 0);
                if (curCenter.DistanceTo(cCenter) > tolHoriz) continue;

                // Kiểm tra cột dưới (Top của nó gần Base của cột hiện tại)
                double distBelow = Math.Abs(cbb.Max.Z - curBaseZ);
                if (distBelow < 1.0 && distBelow < minBelowDist) // sai số cao độ < 300mm
                {
                    minBelowDist = distBelow;
                    colBelow = c;
                }

                // Kiểm tra cột trên (Base của nó gần Top của cột hiện tại)
                double distAbove = Math.Abs(cbb.Min.Z - curTopZ);
                if (distAbove < 1.0 && distAbove < minAboveDist)
                {
                    minAboveDist = distAbove;
                    colAbove = c;
                }
            }

            return (colBelow, colAbove);
        }

        /// <summary>
        /// Phân tích hình học chuyển tiếp giữa cột hiện tại và cột tầng trên
        /// </summary>
        public static ColumnContinuityInfo AnalyzeTransition(
            FamilyInstance curCol,
            FamilyInstance aboveCol,
            double mainBarDiaFeet,
            double lapLengthFeet)
        {
            var info = new ColumnContinuityInfo
            {
                CurrentColumn = curCol,
                ColumnAbove = aboveCol,
                LapLengthFeet = lapLengthFeet,
                StaggerOffsetFeet = lapLengthFeet * 1.3
            };

            if (aboveCol == null)
            {
                info.TransitionType = ColumnTransitionType.TopRoofTerminated;
                return info;
            }

            // Đo kích thước cột dưới và cột trên
            var profCur = RectangularColumnGeometryHelper.GetRectangularProfile(curCol);
            var profAbove = RectangularColumnGeometryHelper.GetRectangularProfile(aboveCol);

            double diffB = (profCur.B - profAbove.B) / 2.0;
            double diffH = (profCur.H - profAbove.H) / 2.0;

            // Bổ sung độ lệch tâm theo hệ tọa độ Local của cột
            XYZ deltaCenter = profAbove.BaseCenter - profCur.TopCenter;
            double rot = profCur.RotationRad;
            XYZ basisX = new XYZ(Math.Cos(rot), Math.Sin(rot), 0);
            XYZ basisY = new XYZ(-Math.Sin(rot), Math.Cos(rot), 0);
            double deltaLocalX = deltaCenter.DotProduct(basisX);
            double deltaLocalY = deltaCenter.DotProduct(basisY);

            double offB = Math.Max(0, diffB + Math.Abs(deltaLocalX));
            double offH = Math.Max(0, diffH + Math.Abs(deltaLocalY));

            info.OffsetB = offB;
            info.OffsetH = offH;
            info.MaxEdgeOffsetFeet = Math.Max(offB, offH);

            double maxOffsetMm = UnitUtils.ConvertFromInternalUnits(info.MaxEdgeOffsetFeet, UnitTypeId.Millimeters);

            if (maxOffsetMm > MaxCrankOffsetLimitMm)
            {
                // Độ thu tiết diện > 75mm: Cấm bẻ cổ chai, bắt buộc cắm thép chờ dowels riêng!
                info.TransitionType = ColumnTransitionType.LargeReductionDoweled;
                info.RequiresSeparateDowels = true;
                info.CrankHeightFeet = 0;
            }
            else if (maxOffsetMm > 5.0) // Có thu nhỏ tiết diện nhưng <= 75mm
            {
                info.TransitionType = ColumnTransitionType.CrankedReduction;
                info.RequiresSeparateDowels = false;
                // Chiều cao bẻ cổ chai: tối thiểu 6 * (độ lệch mép + đường kính thanh)
                info.CrankHeightFeet = Math.Max(mainBarDiaFeet * 6.0, (info.MaxEdgeOffsetFeet + mainBarDiaFeet) * 6.0);
            }
            else
            {
                // Tiết diện bằng nhau: bẻ nhẹ 1*db để lồng thép vào trong lồng thép tầng trên
                info.TransitionType = ColumnTransitionType.ContinuousSameSize;
                info.RequiresSeparateDowels = false;
                info.CrankHeightFeet = mainBarDiaFeet * 6.0;
            }

            return info;
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
