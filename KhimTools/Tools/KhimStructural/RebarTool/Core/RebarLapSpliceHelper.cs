using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tính toán chiều dài nối chồng (Lap Splice) và vùng nối an toàn cho thép cột.
    /// Tuân thủ TCVN 5574:2018 / ACI 318.
    /// </summary>
    public static class RebarLapSpliceHelper
    {
        public static double CalculateLapLength(double barDiameterFeet, double multiplier = 30)
        {
            return barDiameterFeet * multiplier;
        }

        public static double CalculateLapLength(
            double barDiameterFeet,
            double multiplier,
            ConcreteGrade concrete,
            SteelGrade steel,
            AnchorageType type,
            DesignCode code,
            double percentLappedFactor = 1.5)
        {
            double barDiameterMm = UnitUtils.ConvertFromInternalUnits(barDiameterFeet, UnitTypeId.Millimeters);
            double lapMm = RebarAnchorageCalculator.CalculateLapLength(
                barDiameterMm, concrete, steel, type, code, multiplier, percentLappedFactor);
            return UnitUtils.ConvertToInternalUnits(lapMm, UnitTypeId.Millimeters);
        }

        /// <summary>
        /// Xác định vùng an toàn cho phép nối thép trên trục Z của cột.
        /// Vùng nối = giữa thân cột (tránh A1 ở chân và đỉnh cột — vùng moment lớn).
        /// Trả về (spliceZBottom, spliceZTop) = phạm vi Z cho phép đặt mối nối.
        /// </summary>
        public static (double spliceZBottom, double spliceZTop) GetSafeSpliceZone(
            double columnBaseZ, double columnTopZ, double zoneA1Length)
        {
            double colHeight = columnTopZ - columnBaseZ;
            if (colHeight <= 0) return (columnBaseZ, columnTopZ);

            // Vùng an toàn = từ (chân + A1) đến (đỉnh - A1)
            double safeBottom = columnBaseZ + zoneA1Length;
            double safeTop = columnTopZ - zoneA1Length;

            // Nếu 2*A1 > chiều cao cột, cho phép nối ở giữa
            if (safeBottom >= safeTop)
            {
                double mid = (columnBaseZ + columnTopZ) / 2.0;
                safeBottom = mid - colHeight * 0.1;
                safeTop = mid + colHeight * 0.1;
            }

            return (safeBottom, safeTop);
        }

        /// <summary>
        /// Tính offset so le 50% (Staggered Splice):
        /// Nhóm A: nối tại Z = spliceZ
        /// Nhóm B: nối tại Z = spliceZ + 1.3 × Ls
        /// Trả về danh sách (barIndex, spliceZ) cho mỗi thanh.
        /// </summary>
        public static List<(int barIndex, double spliceZ)> CalculateStaggeredOffsets(
            int totalBars, double baseSpliceZ, double lapLength, bool staggered)
        {
            var offsets = new List<(int, double)>();
            double staggerOffset = staggered ? 1.3 * lapLength : 0;

            for (int i = 0; i < totalBars; i++)
            {
                // Nhóm A: thanh chẵn (index 0, 2, 4...) → nối tại baseSpliceZ
                // Nhóm B: thanh lẻ (index 1, 3, 5...) → nối tại baseSpliceZ + 1.3*Ls
                double z = (staggered && i % 2 == 1) ? baseSpliceZ + staggerOffset : baseSpliceZ;
                offsets.Add((i, z));
            }

            return offsets;
        }

        /// <summary>
        /// Nhóm các cột theo trục XY (cùng vị trí mặt bằng nhưng khác tầng/Level).
        /// Các cột cùng 1 nhóm = chuỗi cột liên tầng cần xử lý nối thép.
        /// Tolerance mặc định: 500mm (~1.64 feet) để chấp nhận lệch nhẹ giữa các tầng.
        /// </summary>
        public static List<List<FamilyInstance>> GroupColumnsByAxis(
            List<FamilyInstance> columns, Document doc, double toleranceFeet = 1.64)
        {
            var groups = new List<List<FamilyInstance>>();
            var used = new HashSet<int>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (used.Contains(i)) continue;

                var group = new List<FamilyInstance> { columns[i] };
                used.Add(i);

                var bbI = columns[i].get_BoundingBox(null);
                if (bbI == null) continue;
                double xi = (bbI.Min.X + bbI.Max.X) / 2.0;
                double yi = (bbI.Min.Y + bbI.Max.Y) / 2.0;

                for (int j = i + 1; j < columns.Count; j++)
                {
                    if (used.Contains(j)) continue;

                    var bbJ = columns[j].get_BoundingBox(null);
                    if (bbJ == null) continue;
                    double xj = (bbJ.Min.X + bbJ.Max.X) / 2.0;
                    double yj = (bbJ.Min.Y + bbJ.Max.Y) / 2.0;

                    double dist = Math.Sqrt((xi - xj) * (xi - xj) + (yi - yj) * (yi - yj));
                    if (dist <= toleranceFeet)
                    {
                        group.Add(columns[j]);
                        used.Add(j);
                    }
                }

                // Sắp xếp theo Z tăng dần (tầng thấp → tầng cao)
                group.Sort((a, b) =>
                {
                    var bbA = a.get_BoundingBox(null);
                    var bbB = b.get_BoundingBox(null);
                    double zA = bbA?.Min.Z ?? 0;
                    double zB = bbB?.Min.Z ?? 0;
                    return zA.CompareTo(zB);
                });

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Kiểm tra 2 cột có phải liên tầng (kế tiếp nhau theo Z).
        /// Điều kiện: đỉnh cột dưới gần chân cột trên (gap &lt; tolerance).
        /// </summary>
        public static bool AreConsecutiveColumns(FamilyInstance lower, FamilyInstance upper, double gapToleranceFeet = 2.0)
        {
            var bbLower = lower.get_BoundingBox(null);
            var bbUpper = upper.get_BoundingBox(null);
            if (bbLower == null || bbUpper == null) return false;

            double topOfLower = bbLower.Max.Z;
            double bottomOfUpper = bbUpper.Min.Z;
            double gap = Math.Abs(bottomOfUpper - topOfLower);

            return gap <= gapToleranceFeet;
        }
    }
}
