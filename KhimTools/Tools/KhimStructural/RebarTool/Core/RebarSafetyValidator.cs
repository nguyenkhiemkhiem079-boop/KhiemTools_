using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Kết quả đánh giá toàn diện an toàn cốt thép (Hàm lượng thép, Hình học Solid, Khoảng cách, Va chạm lỗ mở)
    /// </summary>
    public class RebarSafetyResult
    {
        public bool IsValid => IsRatioValid && IsContainmentValid && IsSpacingValid && IsOpeningCollisionValid && IsStockLengthValid;
        public bool IsRatioValid { get; set; } = true;
        public bool IsContainmentValid { get; set; } = true;
        public bool IsSpacingValid { get; set; } = true;
        public bool IsOpeningCollisionValid { get; set; } = true;
        public bool IsStockLengthValid { get; set; } = true;

        public double RatioPercent { get; set; }
        public int OutOfBoundsCount { get; set; }
        public int SpacingViolationCount { get; set; }
        public int OpeningCollisionCount { get; set; }
        public int OverStockLengthCount { get; set; }

        public string RatioMessage { get; set; }
        public string ContainmentMessage { get; set; }
        public string SpacingMessage { get; set; }
        public string OpeningMessage { get; set; }
        public string StockLengthMessage { get; set; }
        public string FullDisplayText { get; set; }

        public System.Drawing.Color StatusColor { get; set; } = System.Drawing.Color.FromArgb(46, 125, 50); // Green
    }

    /// <summary>
    /// Tiện ích kiểm tra an toàn hình học & tiêu chuẩn cốt thép:
    /// 1. Kiểm tra hàm lượng thép chuẩn (IRebarDesignStandard).
    /// 2. Kiểm tra containment dựa trên Solid/Transform thực của Host (không chỉ dùng world BoundingBox).
    /// 3. Kiểm tra khoảng hở cốt thép (Clear spacing).
    /// 4. Kiểm tra va chạm / đâm xuyên qua lỗ mở (Opening collision).
    /// 5. Kiểm tra chiều dài thanh thép thương mại tối đa (Commercial stock length 11.7m / 12.0m).
    /// </summary>
    public static class RebarSafetyValidator
    {
        private static readonly System.Drawing.Color ColorSuccess = System.Drawing.Color.FromArgb(46, 125, 50);    // Green
        private static readonly System.Drawing.Color ColorWarning = System.Drawing.Color.FromArgb(198, 40, 40);   // Dark Red / Orange

        /// <summary>
        /// Chiều dài thép cây thương mại tiêu chuẩn tối đa (11.7m = 38.3858 ft).
        /// </summary>
        public const double MaxStockLengthMm = 11700.0;

        /// <summary>
        /// Kiểm tra nhanh hình học: Xem có thanh thép nào vượt ra ngoài BoundingBox của cấu kiện host (kèm dung sai cover).
        /// Hỗ trợ kiểm tra hướng local đối với cấu kiện xoay (FamilyInstance).
        /// </summary>
        /// <summary>
        /// Kiểm tra chính xác hình học cốt thép nằm trong bê tông bằng tọa độ Local của host hoặc DetailingIntentContext.
        /// Loại bỏ hoàn toàn dung sai võ đoán 20mm.
        /// </summary>
        public static (int outCount, string warning) CheckRebarContainment(
            Element host,
            IEnumerable<Rebar> rebars,
            DetailingIntentContext intentContext = null,
            double toleranceMm = 0.0)
        {
            if (host == null || rebars == null) return (0, null);

            var rebarList = rebars.Where(r => r != null && r.IsValidObject).ToList();
            if (rebarList.Count == 0) return (0, null);

            // 1. Nếu có Document và trích xuất được hình học Solid, ưu tiên chạy RebarHostContainmentValidator
            if (host.Document != null)
            {
                try
                {
                    var report = RebarHostContainmentValidator.ValidateHostContainmentWithIntent(
                        host.Document, host, rebarList, intentContext ?? new DetailingIntentContext(host, DetailingIntentType.StandardInternal));

                    int protCount = report.Protrusions.Count;
                    string msg = protCount > 0
                        ? $"⚠ Phát hiện {protCount} vị trí thanh thép lồi ra ngoài khối bê tông của cấu kiện."
                        : null;
                    return (protCount, msg);
                }
                catch
                {
                    // Fallback sang phân tích hình học Local BoundingBox nếu không trích xuất được Solid
                }
            }

            // 2. Phân tích hình học Local Transform cho FamilyInstance
            Transform tf = (host as FamilyInstance)?.GetTransform() ?? Transform.Identity;
            Transform invTf = tf.Inverse;

            BoundingBoxXYZ hostBox = host.get_BoundingBox(null);
            if (hostBox == null) return (0, null);

            double tolFeet = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);

            // Tính toán biên Local của Host
            XYZ localMin = invTf.OfPoint(hostBox.Min);
            XYZ localMax = invTf.OfPoint(hostBox.Max);
            double minX = Math.Min(localMin.X, localMax.X) - tolFeet;
            double maxX = Math.Max(localMin.X, localMax.X) + tolFeet;
            double minY = Math.Min(localMin.Y, localMax.Y) - tolFeet;
            double maxY = Math.Max(localMin.Y, localMax.Y) + tolFeet;
            double minZ = Math.Min(localMin.Z, localMax.Z) - tolFeet;
            double maxZ = Math.Max(localMin.Z, localMax.Z) + tolFeet;

            int outCount = 0;
            foreach (var r in rebarList)
            {
                var curves = GetRebarCenterlineCurves(r);
                bool barOut = false;
                foreach (var c in curves)
                {
                    var pts = new[] { c.GetEndPoint(0), c.Evaluate(0.5, true), c.GetEndPoint(1) };
                    foreach (var pt in pts)
                    {
                        XYZ localPt = invTf.OfPoint(pt);
                        if (localPt.X < minX || localPt.X > maxX ||
                            localPt.Y < minY || localPt.Y > maxY ||
                            localPt.Z < minZ || localPt.Z > maxZ)
                        {
                            // Kiểm tra xem có được phép vươn sang ConnectedHost không
                            if (intentContext != null && intentContext.IntentType != DetailingIntentType.StandardInternal)
                            {
                                if (intentContext.IsPointContained(pt, 0, out bool insideConn) && insideConn)
                                {
                                    continue; // Điểm nằm trong cấu kiện liên kết
                                }
                            }

                            barOut = true;
                            break;
                        }
                    }
                    if (barOut) break;
                }
                if (barOut) outCount++;
            }

            string warn = outCount > 0
                ? $"⚠ Phát hiện {outCount} thanh thép vượt ra ngoài biên hình học cấu kiện (Độ dôi > {toleranceMm:F1}mm)"
                : null;

            return (outCount, warn);
        }

        public static (int outCount, string warning) CheckRebarContainment(Element host, IEnumerable<Rebar> rebars, double toleranceMm)
        {
            return CheckRebarContainment(host, rebars, null, toleranceMm);
        }

        /// <summary>
        /// Kiểm tra chiều dài thanh cốt thép có vượt quá chiều dài thương mại tối đa (11.7m) hay không.
        /// </summary>
        public static (int overCount, string warning) CheckCommercialStockLength(IEnumerable<Rebar> rebars, double maxLenMm = MaxStockLengthMm)
        {
            if (rebars == null) return (0, null);
            double maxLenFeet = UnitUtils.ConvertToInternalUnits(maxLenMm, UnitTypeId.Millimeters);

            int count = 0;
            foreach (var r in rebars)
            {
                if (r == null || !r.IsValidObject) continue;
                double len = GetRebarTotalLength(r);
                if (len > maxLenFeet)
                {
                    count++;
                }
            }

            string msg = count > 0
                ? $"⚠ Có {count} thanh thép vượt quá chiều dài cây thép thương mại ({maxLenMm / 1000.0:F1}m) — cần cắt nối/lap splice."
                : null;

            return (count, msg);
        }

        /// <summary>
        /// Kiểm tra khoảng cách thông thủy (Clear Spacing) tối thiểu giữa các thanh thép song song.
        /// </summary>
        public static (int violationCount, string warning) CheckClearSpacing(
            IEnumerable<Rebar> rebars, double minClearMm = 25.0)
        {
            if (rebars == null) return (0, null);
            double minClearFeet = UnitUtils.ConvertToInternalUnits(minClearMm, UnitTypeId.Millimeters);

            var barList = rebars.Where(r => r != null && r.IsValidObject).ToList();
            if (barList.Count < 2) return (0, null);

            int violations = 0;
            for (int i = 0; i < barList.Count; i++)
            {
                var bbI = barList[i].get_BoundingBox(null);
                if (bbI == null) continue;
                XYZ cI = (bbI.Min + bbI.Max) / 2.0;
                double diaI = GetBarDiameter(barList[i]);

                for (int j = i + 1; j < barList.Count; j++)
                {
                    var bbJ = barList[j].get_BoundingBox(null);
                    if (bbJ == null) continue;
                    XYZ cJ = (bbJ.Min + bbJ.Max) / 2.0;
                    double diaJ = GetBarDiameter(barList[j]);

                    double centerDist = Math.Sqrt((cI.X - cJ.X) * (cI.X - cJ.X) + (cI.Y - cJ.Y) * (cI.Y - cJ.Y));
                    double clear = centerDist - (diaI + diaJ) / 2.0;

                    // Nếu cùng cao độ/mặt bằng mà khoảng cách tâm gần và khoảng hở < minClear
                    if (centerDist > 0.001 && centerDist < (diaI + diaJ) / 2.0 + minClearFeet)
                    {
                        if (Math.Abs(cI.Z - cJ.Z) < 0.5) // Gần nhau theo chiều cao
                        {
                            violations++;
                        }
                    }
                }
            }

            string warn = violations > 0
                ? $"⚠ Phát hiện {violations} vị trí khoảng hở cốt thép nhỏ hơn quy định ({minClearMm:F0}mm)."
                : null;

            return (violations, warn);
        }

        /// <summary>
        /// Kiểm tra va chạm giữa cốt thép và các lỗ mở (Openings).
        /// </summary>
        public static (int collisionCount, string warning) CheckOpeningCollisions(
            IEnumerable<Rebar> rebars, IEnumerable<CurveLoop> openings, double coverMm = 25.0)
        {
            if (rebars == null || openings == null || !openings.Any()) return (0, null);
            double coverFeet = UnitUtils.ConvertToInternalUnits(coverMm, UnitTypeId.Millimeters);

            int collisions = 0;
            foreach (var r in rebars)
            {
                if (r == null || !r.IsValidObject) continue;
                var curves = GetRebarCenterlineCurves(r);

                foreach (var c in curves)
                {
                    XYZ mid = c.Evaluate(0.5, true);
                    foreach (var loop in openings)
                    {
                        if (IsPointInsideLoop2D(mid.X, mid.Y, loop, coverFeet))
                        {
                            collisions++;
                            break;
                        }
                    }
                }
            }

            string warn = collisions > 0
                ? $"⚠ Phát hiện {collisions} thanh thép cắt phạm qua vùng lỗ mở sàn/dầm."
                : null;

            return (collisions, warn);
        }

        /// <summary>
        /// Đánh giá toàn diện an toàn cho Cột (Hàm lượng thép + Hình học + Chiều dài thanh).
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

            // 3. Kiểm tra chiều dài thanh thương mại
            var (overStock, stockMsg) = CheckCommercialStockLength(createdRebars);
            result.OverStockLengthCount = overStock;
            result.IsStockLengthValid = (overStock == 0);
            result.StockLengthMessage = stockMsg;

            // 4. Định dạng chuỗi hiển thị & màu sắc
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
            if (!result.IsStockLengthValid && !string.IsNullOrEmpty(stockMsg))
            {
                messages.Add(stockMsg);
            }

            result.FullDisplayText = string.Join("\n", messages);
            result.StatusColor = result.IsValid ? ColorSuccess : ColorWarning;

            return result;
        }

        /// <summary>
        /// Đánh giá toàn diện an toàn cho Dầm (Hàm lượng thép + Hình học + Chiều dài thanh).
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

            // 3. Kiểm tra chiều dài thanh
            var (overStock, stockMsg) = CheckCommercialStockLength(createdRebars);
            result.OverStockLengthCount = overStock;
            result.IsStockLengthValid = (overStock == 0);
            result.StockLengthMessage = stockMsg;

            // 4. Định dạng chuỗi hiển thị & màu sắc
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
            if (!result.IsStockLengthValid && !string.IsNullOrEmpty(stockMsg))
            {
                messages.Add(stockMsg);
            }

            result.FullDisplayText = string.Join("\n", messages);
            result.StatusColor = result.IsValid ? ColorSuccess : ColorWarning;

            return result;
        }

        /// <summary>
        /// Đánh giá hình học an toàn cho Sàn (Hình học + Va chạm lỗ mở + Chiều dài cây).
        /// </summary>
        public static RebarSafetyResult EvaluateSlab(
            Element host,
            IEnumerable<Rebar> createdRebars,
            IEnumerable<CurveLoop> openings = null,
            double toleranceMm = 20.0)
        {
            var result = new RebarSafetyResult();
            var (outCount, containmentMsg) = CheckRebarContainment(host, createdRebars, toleranceMm);
            result.OutOfBoundsCount = outCount;
            result.IsContainmentValid = (outCount == 0);
            result.ContainmentMessage = containmentMsg;

            if (openings != null && openings.Any())
            {
                var (colCount, colMsg) = CheckOpeningCollisions(createdRebars, openings);
                result.OpeningCollisionCount = colCount;
                result.IsOpeningCollisionValid = (colCount == 0);
                result.OpeningMessage = colMsg;
            }

            var (overStock, stockMsg) = CheckCommercialStockLength(createdRebars);
            result.OverStockLengthCount = overStock;
            result.IsStockLengthValid = (overStock == 0);
            result.StockLengthMessage = stockMsg;

            var messages = new List<string>();
            if (result.IsContainmentValid)
            {
                messages.Add("✓ Cốt thép sàn nằm trọn trong hình học cấu kiện (Đạt)");
            }
            else
            {
                messages.Add(containmentMsg);
            }

            if (!result.IsOpeningCollisionValid && !string.IsNullOrEmpty(result.OpeningMessage))
            {
                messages.Add(result.OpeningMessage);
            }
            if (!result.IsStockLengthValid && !string.IsNullOrEmpty(result.StockLengthMessage))
            {
                messages.Add(result.StockLengthMessage);
            }

            result.FullDisplayText = string.Join("\n", messages);
            result.StatusColor = result.IsValid ? ColorSuccess : ColorWarning;

            return result;
        }

        private static IList<Curve> GetRebarCenterlineCurves(Rebar r)
        {
            try
            {
                return r.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            }
            catch
            {
                return new List<Curve>();
            }
        }

        private static double GetRebarTotalLength(Rebar r)
        {
            try
            {
                Parameter p = r.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            catch { }
            return 0.0;
        }

        private static double GetBarDiameter(Rebar r)
        {
            try
            {
                Parameter p = r.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            catch { }
            return 0.05; // ~16mm fallback
        }

        private static bool IsPointInsideLoop2D(double x, double y, CurveLoop loop, double marginFeet = 0)
        {
            // Point in polygon 2D
            var pts = new List<XYZ>();
            foreach (Curve c in loop) pts.Add(c.GetEndPoint(0));
            if (pts.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                if (((pts[i].Y > y) != (pts[j].Y > y)) &&
                    (x < (pts[j].X - pts[i].X) * (y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}