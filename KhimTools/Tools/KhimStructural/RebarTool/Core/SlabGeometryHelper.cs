using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Helper service phân tích và trích xuất thông tin hình học Sàn (Floor),
    /// bao gồm ranh giới ngoài, các lỗ mở bên trong, độ dày và lớp bê tông bảo vệ.
    /// </summary>
    public static class SlabGeometryHelper
    {
        public static SlabProfile AnalyzeSlab(Document doc, Floor floor)
        {
            if (doc == null || floor == null) return null;
            if (floor.Document != doc)
                throw new InvalidOperationException("Slab host does not belong to the active document.");

            Parameter structuralParameter = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
            if (structuralParameter == null || !structuralParameter.HasValue || structuralParameter.AsInteger() != 1)
                throw new InvalidOperationException("Slab reinforcement requires a structural Floor host; non-structural floors are not supported.");

            var profile = new SlabProfile
            {
                FloorId = floor.Id,
                FloorElement = floor,
                FloorName = floor.Name,
                LevelName = doc.GetElement(floor.LevelId)?.Name ?? "?",
                BoundingBox = floor.get_BoundingBox(null)
            };
            if (profile.BoundingBox == null)
                throw new InvalidOperationException("Slab model bounds are unavailable; reinforcement cannot be safely planned.");

            // This generator places bars using the host's overall top/bottom bounds. Only accept
            // floors where those bounds describe the structural core itself, not finish layers.
            double physicalThickness = profile.BoundingBox.Max.Z - profile.BoundingBox.Min.Z;
            double structuralCoreThickness = floor.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS)?.AsDouble()
                                  ?? floor.FloorType.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS)?.AsDouble()
                                  ?? 0.0;
            double thicknessTolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            if (structuralCoreThickness <= 0 || Math.Abs(physicalThickness - structuralCoreThickness) > thicknessTolerance)
                throw new InvalidOperationException("Slab reinforcement currently requires the overall host thickness to match its structural core; floors with finish layers need a core-specific detailing workflow.");
            profile.ThicknessFeet = physicalThickness;
            profile.ThicknessMm = UnitUtils.ConvertFromInternalUnits(physicalThickness, UnitTypeId.Millimeters);

            // 2. Lớp bê tông bảo vệ
            profile.CoverTopFeet = RebarCoverHelper.GetFloorCover(floor, RebarFace.Top);
            profile.CoverBottomFeet = RebarCoverHelper.GetFloorCover(floor, RebarFace.Bottom);

            // 3. Trích xuất Face trên cùng & ranh giới (Top Face Boundary)
            PlanarFace topFace = GetTopPlanarFace(floor, profile.BoundingBox);
            if (topFace == null || !topFace.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ, 1e-6))
                throw new InvalidOperationException("Slab reinforcement currently supports horizontal planar floors only; sloped or non-planar hosts require a host-specific detailing workflow.");

            profile.Normal = topFace.FaceNormal;
            profile.Origin = topFace.Origin;
            IList<CurveLoop> loops = topFace.GetEdgesAsCurveLoops();
            if (loops == null || loops.Count == 0)
                throw new InvalidOperationException("Slab top-face boundary geometry is unavailable; reinforcement cannot be safely clipped to the host.");
            if (loops.Any(loop => !IsSupportedHorizontalLinearLoop(loop, topFace.Origin.Z)))
                throw new InvalidOperationException("Slab reinforcement currently supports straight-edged horizontal boundaries and openings only.");

            // Generation and opening clipping use global model X/Y and rectangular opening extents.
            // Reject rotated edges and non-rectangular voids before a preview/create plan is produced.
            if (loops.Any(loop => !IsAxisAlignedLoop(loop)))
                throw new InvalidOperationException("Slab reinforcement currently supports global X/Y-aligned plan boundaries only; rotated slab edges are unsupported.");

            // The largest actual top-face loop is the outer boundary. Remaining loops are the
            // actual void boundaries; never synthesize a rectangular opening from its bounding box.
            var sortedLoops = loops.OrderByDescending(GetLoopArea)
                .ThenBy(GetLoopSortKey, StringComparer.Ordinal).ToList();
            profile.OuterBoundary = sortedLoops[0];
            for (int i = 1; i < sortedLoops.Count; i++)
            {
                if (!IsAxisAlignedRectangle(sortedLoops[i]))
                    throw new InvalidOperationException("Slab mesh clipping currently supports axis-aligned rectangular openings only.");
                profile.InnerOpenings.Add(sortedLoops[i]);
            }

            // 5. Kích thước BoundingBox
            if (profile.BoundingBox != null)
            {
                double dx = profile.BoundingBox.Max.X - profile.BoundingBox.Min.X;
                double dy = profile.BoundingBox.Max.Y - profile.BoundingBox.Min.Y;
                profile.WidthMm = UnitUtils.ConvertFromInternalUnits(Math.Min(dx, dy), UnitTypeId.Millimeters);
                profile.LengthMm = UnitUtils.ConvertFromInternalUnits(Math.Max(dx, dy), UnitTypeId.Millimeters);
            }

            return profile;
        }

        /// <summary>
        /// Cắt ngắn / chia nhỏ các đoạn thép khi đi qua các lỗ mở trong sàn.
        /// Trả về danh sách các khoảng [Start, End] hợp lệ không bị đâm xuyên qua lỗ mở.
        /// </summary>
        public static List<(double Start, double End)> ClipIntervalAgainstOpenings(
            double startPos, double endPos, double fixedCoord, bool isXDirection,
            List<CurveLoop> openings, double coverFeet)
        {
            var intervals = new List<(double Start, double End)> { (Math.Min(startPos, endPos), Math.Max(startPos, endPos)) };
            if (openings == null || !openings.Any()) return intervals;

            foreach (var op in openings)
            {
                // Tính BoundingBox của lỗ mở trên mặt phẳng XY
                double opMinDir = double.MaxValue, opMaxDir = double.MinValue;
                double opMinFixed = double.MaxValue, opMaxFixed = double.MinValue;

                foreach (Curve c in op)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    double dir0 = isXDirection ? p0.X : p0.Y;
                    double dir1 = isXDirection ? p1.X : p1.Y;
                    double fix0 = isXDirection ? p0.Y : p0.X;
                    double fix1 = isXDirection ? p1.Y : p1.X;

                    opMinDir = Math.Min(opMinDir, Math.Min(dir0, dir1));
                    opMaxDir = Math.Max(opMaxDir, Math.Max(dir0, dir1));
                    opMinFixed = Math.Min(opMinFixed, Math.Min(fix0, fix1));
                    opMaxFixed = Math.Max(opMaxFixed, Math.Max(fix0, fix1));
                }

                // Nếu thanh thép nằm ngoài phạm vi bề rộng lỗ mở thì bỏ qua
                if (fixedCoord < opMinFixed || fixedCoord > opMaxFixed) continue;

                // Vùng lỗ mở cần tránh (kèm lớp bảo vệ bê tông cover)
                double holeStart = opMinDir - coverFeet;
                double holeEnd = opMaxDir + coverFeet;

                var nextIntervals = new List<(double Start, double End)>();
                foreach (var seg in intervals)
                {
                    // Trường hợp 1: Đoạn thép nằm hoàn toàn ngoài lỗ mở
                    if (seg.End <= holeStart || seg.Start >= holeEnd)
                    {
                        nextIntervals.Add(seg);
                    }
                    // Trường hợp 2: Lỗ mở cắt đôi đoạn thép ở giữa
                    else if (seg.Start < holeStart && seg.End > holeEnd)
                    {
                        if (holeStart - seg.Start >= 0.5) nextIntervals.Add((seg.Start, holeStart));
                        if (seg.End - holeEnd >= 0.5) nextIntervals.Add((holeEnd, seg.End));
                    }
                    // Trường hợp 3: Lỗ mở đè lên đầu cuối
                    else if (seg.Start < holeStart && seg.End <= holeEnd)
                    {
                        if (holeStart - seg.Start >= 0.5) nextIntervals.Add((seg.Start, holeStart));
                    }
                    // Trường hợp 4: Lỗ mở đè lên đầu bắt đầu
                    else if (seg.Start >= holeStart && seg.End > holeEnd)
                    {
                        if (seg.End - holeEnd >= 0.5) nextIntervals.Add((holeEnd, seg.End));
                    }
                    // Trường hợp 5: Đoạn thép lọt hoàn toàn trong lỗ mở -> Không thêm gì (Bỏ qua)
                }

                intervals = nextIntervals;
            }

            return intervals;
        }

        /// <summary>
        /// Tính toán các phân đoạn thép nằm chính xác bên trong ranh giới đa giác của sàn và không bị đâm qua lỗ mở.
        /// </summary>
        public static List<(double Start, double End)> GetSlabIntervalsAtCoord(
            double fixedCoord, bool isXDirection,
            CurveLoop boundary, List<CurveLoop> openings,
            double coverFeet)
        {
            var rawCrossings = new List<double>();
            if (boundary == null) return new List<(double, double)>();

            // 1. Tìm giao điểm của đường rải thép với các cạnh của ranh giới sàn (Boundary Polygon)
            foreach (Curve c in boundary)
            {
                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);

                double cFixed0 = isXDirection ? p0.Y : p0.X;
                double cFixed1 = isXDirection ? p1.Y : p1.X;
                double cDir0 = isXDirection ? p0.X : p0.Y;
                double cDir1 = isXDirection ? p1.X : p1.Y;

                // Kiểm tra xem fixedCoord có nằm trong khoảng Y (hoặc X) của đoạn thẳng không
                if ((cFixed0 <= fixedCoord && fixedCoord < cFixed1) || (cFixed1 <= fixedCoord && fixedCoord < cFixed0))
                {
                    double t = (fixedCoord - cFixed0) / (cFixed1 - cFixed0);
                    double dirIntersect = cDir0 + t * (cDir1 - cDir0);
                    rawCrossings.Add(dirIntersect);
                }
            }

            if (rawCrossings.Count < 2) return new List<(double, double)>();

            // 2. Sắp xếp các giao điểm và ghép thành các đoạn [x_in, x_out] nằm gọn trong khối bê tông sàn
            rawCrossings.Sort();
            var validSlabSegments = new List<(double Start, double End)>();
            for (int i = 0; i + 1 < rawCrossings.Count; i += 2)
            {
                // Thép phải thụt vào trong mép bê tông sàn một khoảng bảo vệ (cover = 25mm)
                // Đảm bảo không bao giờ bị đâm xiên/thừa ra ngoài không gian (out sàn)
                double segStart = rawCrossings[i] + coverFeet;
                double segEnd = rawCrossings[i + 1] - coverFeet;
                if (segEnd - segStart >= 0.5)
                {
                    validSlabSegments.Add((segStart, segEnd));
                }
            }

            // 3. Cắt trừ các lỗ mở trong sàn
            var finalIntervals = new List<(double Start, double End)>();
            foreach (var seg in validSlabSegments)
            {
                var clipped = ClipIntervalAgainstOpenings(seg.Start, seg.End, fixedCoord, isXDirection, openings, coverFeet);
                foreach (var c in clipped)
                {
                    if (c.End - c.Start >= 0.5) // Chiều dài tối thiểu 150mm
                    {
                        finalIntervals.Add(c);
                    }
                }
            }

            return finalIntervals;
        }

        [Obsolete("anchorFeet is not applied by slab interval clipping. Use the overload without anchorFeet.")]
        public static List<(double Start, double End)> GetSlabIntervalsAtCoord(
            double fixedCoord, bool isXDirection,
            CurveLoop boundary, List<CurveLoop> openings,
            double anchorFeet, double coverFeet)
        {
            return GetSlabIntervalsAtCoord(fixedCoord, isXDirection, boundary, openings, coverFeet);
        }

        /// <summary>
        /// Kiểm tra xem một tọa độ điểm (X, Y) có nằm trong khối bê tông của sàn và nằm ngoài các lỗ mở hay không.
        /// </summary>
        public static bool IsPointInsideSlab(XYZ pt, CurveLoop boundary, List<CurveLoop> openings)
        {
            if (boundary == null) return false;
            if (!IsPointInPolygon(pt, boundary)) return false;
            if (openings != null)
            {
                foreach (var op in openings)
                {
                    if (IsPointInPolygon(pt, op)) return false;
                }
            }
            return true;
        }

        private static bool IsPointInPolygon(XYZ pt, CurveLoop polygon)
        {
            if (polygon == null) return false;
            var pts = new List<XYZ>();
            foreach (Curve c in polygon) pts.Add(c.GetEndPoint(0));
            int n = pts.Count;
            if (n < 3) return false;

            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((pts[i].Y > pt.Y) != (pts[j].Y > pt.Y)) &&
                    (pt.X < (pts[j].X - pts[i].X) * (pt.Y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static PlanarFace GetTopPlanarFace(Floor floor, BoundingBoxXYZ bounds)
        {
            var options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = floor.get_Geometry(options);
            if (geomElem == null) return null;

            var positiveSolids = geomElem.OfType<Solid>().Where(solid => solid != null && solid.Volume > 1e-6).ToList();
            if (positiveSolids.Count != 1 || geomElem.OfType<GeometryInstance>().Any())
                throw new InvalidOperationException("Slab reinforcement requires one direct host solid; compound or transformed floor geometry is unsupported.");

            var upwardFaces = new List<PlanarFace>();
            var downwardFaces = new List<PlanarFace>();

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-6)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (!(face is PlanarFace pf))
                            throw new InvalidOperationException("Slab reinforcement requires planar host faces; curved or non-planar faces are unsupported.");
                        else
                        {
                            double verticalNormal = pf.FaceNormal.DotProduct(XYZ.BasisZ);
                            if (verticalNormal >= 1.0 - 1e-6) upwardFaces.Add(pf);
                            else if (verticalNormal <= -1.0 + 1e-6) downwardFaces.Add(pf);
                            else if (Math.Abs(verticalNormal) > 1e-6)
                                throw new InvalidOperationException("Slab reinforcement requires vertical side faces; tapered or sloped side geometry is unsupported.");
                        }
                    }
                }
            }

            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            if (upwardFaces.Count != 1 || downwardFaces.Count != 1 ||
                Math.Abs(upwardFaces[0].Origin.Z - bounds.Max.Z) > tolerance ||
                Math.Abs(downwardFaces[0].Origin.Z - bounds.Min.Z) > tolerance)
                throw new InvalidOperationException("Slab reinforcement currently requires one horizontal top face and one horizontal bottom face at the model bounds; stepped, tapered, or multi-face floors require host-specific detailing.");
            return upwardFaces[0];
        }

        private static bool IsAxisAlignedLoop(CurveLoop loop)
        {
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            foreach (Curve curve in loop)
            {
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                bool alongX = Math.Abs(start.Y - end.Y) <= tolerance && Math.Abs(start.X - end.X) > tolerance;
                bool alongY = Math.Abs(start.X - end.X) <= tolerance && Math.Abs(start.Y - end.Y) > tolerance;
                if (!alongX && !alongY) return false;
            }
            return true;
        }

        private static bool IsAxisAlignedRectangle(CurveLoop loop)
        {
            var points = loop.Select(curve => curve.GetEndPoint(0)).ToList();
            if (points.Count != 4 || !IsAxisAlignedLoop(loop)) return false;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            double[] xs = points.Select(point => point.X).OrderBy(value => value).ToArray();
            double[] ys = points.Select(point => point.Y).OrderBy(value => value).ToArray();
            double width = xs[3] - xs[0];
            double height = ys[3] - ys[0];
            if (width <= tolerance || height <= tolerance ||
                Math.Abs(xs[1] - xs[0]) > tolerance || Math.Abs(xs[3] - xs[2]) > tolerance ||
                Math.Abs(ys[1] - ys[0]) > tolerance || Math.Abs(ys[3] - ys[2]) > tolerance) return false;
            return Math.Abs(GetLoopArea(loop) - width * height) <= tolerance * (width + height);
        }

        /// <summary>
        /// Tính diện tích hình học chính xác của CurveLoop trên mặt phẳng XY bằng công thức Shoelace
        /// </summary>
        private static double GetLoopArea(CurveLoop loop)
        {
            if (loop == null) return 0;
            var pts = new List<XYZ>();
            foreach (Curve c in loop) pts.Add(c.GetEndPoint(0));
            if (pts.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                XYZ p1 = pts[i];
                XYZ p2 = pts[(i + 1) % pts.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return Math.Abs(area) / 2.0;
        }

        private static string GetLoopSortKey(CurveLoop loop)
        {
            return string.Join("|", loop.SelectMany(curve => new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) })
                .GroupBy(point => point.X.ToString("R", CultureInfo.InvariantCulture) + "," + point.Y.ToString("R", CultureInfo.InvariantCulture))
                .Select(group => group.Key).OrderBy(value => value, StringComparer.Ordinal));
        }

        private static bool IsSupportedHorizontalLinearLoop(CurveLoop loop, double z)
        {
            if (loop == null) return false;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            int count = 0;
            foreach (Curve curve in loop)
            {
                if (!(curve is Line)) return false;
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                if (Math.Abs(start.Z - z) > tolerance || Math.Abs(end.Z - z) > tolerance || start.DistanceTo(end) <= tolerance)
                    return false;
                count++;
            }
            return count >= 3;
        }
    }
}
