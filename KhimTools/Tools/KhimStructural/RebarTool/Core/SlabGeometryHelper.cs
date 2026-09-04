using System;
using System.Collections.Generic;
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

            var profile = new SlabProfile
            {
                FloorId = floor.Id,
                FloorElement = floor,
                FloorName = floor.Name,
                LevelName = doc.GetElement(floor.LevelId)?.Name ?? "?",
                BoundingBox = floor.get_BoundingBox(null)
            };

            // 1. Độ dày sàn
            double thicknessFeet = floor.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS)?.AsDouble()
                                  ?? floor.FloorType.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS)?.AsDouble()
                                  ?? 0.5;
            profile.ThicknessFeet = thicknessFeet;
            profile.ThicknessMm = UnitUtils.ConvertFromInternalUnits(thicknessFeet, UnitTypeId.Millimeters);

            // 2. Lớp bê tông bảo vệ
            profile.CoverTopFeet = RebarCoverHelper.GetFloorCover(floor, RebarFace.Top);
            profile.CoverBottomFeet = RebarCoverHelper.GetFloorCover(floor, RebarFace.Bottom);

            // 3. Trích xuất Face trên cùng & ranh giới (Top Face Boundary)
            PlanarFace topFace = GetTopPlanarFace(floor);
            if (topFace != null)
            {
                profile.Normal = topFace.FaceNormal;
                profile.Origin = topFace.Origin;

                IList<CurveLoop> loops = topFace.GetEdgesAsCurveLoops();
                if (loops != null && loops.Count > 0)
                {
                    // Loop có diện tích lớn nhất là ranh giới ngoài
                    var sortedLoops = loops.OrderByDescending(GetLoopArea).ToList();
                    profile.OuterBoundary = sortedLoops[0];

                    // Các loop còn lại trên Face là lỗ mở trong sàn
                    for (int i = 1; i < sortedLoops.Count; i++)
                    {
                        profile.InnerOpenings.Add(sortedLoops[i]);
                    }
                }
            }

            // 4. Tìm thêm các Opening (Shaft Opening / Floor Opening) cắt qua Floor trong mô hình
            try
            {
                var openings = new FilteredElementCollector(doc)
                    .OfClass(typeof(Opening))
                    .WherePasses(new ElementIntersectsElementFilter(floor))
                    .Cast<Opening>()
                    .ToList();

                foreach (var op in openings)
                {
                    var bbOp = op.get_BoundingBox(null);
                    if (bbOp != null)
                    {
                        double minX = bbOp.Min.X; double maxX = bbOp.Max.X;
                        double minY = bbOp.Min.Y; double maxY = bbOp.Max.Y;
                        double z = topFace?.Origin.Z ?? floor.get_BoundingBox(null)?.Max.Z ?? 0;

                        var opLoop = new CurveLoop();
                        opLoop.Append(Line.CreateBound(new XYZ(minX, minY, z), new XYZ(maxX, minY, z)));
                        opLoop.Append(Line.CreateBound(new XYZ(maxX, minY, z), new XYZ(maxX, maxY, z)));
                        opLoop.Append(Line.CreateBound(new XYZ(maxX, maxY, z), new XYZ(minX, maxY, z)));
                        opLoop.Append(Line.CreateBound(new XYZ(minX, maxY, z), new XYZ(minX, minY, z)));
                        
                        // Kiểm tra không trùng với các loop đã có
                        bool duplicate = profile.InnerOpenings.Any(existing => 
                            GetLoopArea(existing) > 0 && Math.Abs(GetLoopArea(existing) - GetLoopArea(opLoop)) < 0.1);
                        if (!duplicate)
                        {
                            profile.InnerOpenings.Add(opLoop);
                        }
                    }
                }
            }
            catch { }

            // 5. Kích thước BoundingBox & Hệ toạ độ phẳng Local 2D (AxisU, AxisV)
            if (profile.OuterBoundary != null)
            {
                // Tìm cạnh thẳng dài nhất của ranh giới ngoài để làm hướng trục chính u (AxisU)
                Curve longestEdge = null;
                double maxLen = -1;
                foreach (Curve c in profile.OuterBoundary)
                {
                    if (c.Length > maxLen)
                    {
                        maxLen = c.Length;
                        longestEdge = c;
                    }
                }

                XYZ dirU = XYZ.BasisX;
                if (longestEdge != null)
                {
                    XYZ pt0 = longestEdge.GetEndPoint(0);
                    XYZ pt1 = longestEdge.GetEndPoint(1);
                    XYZ rawDir = (pt1 - pt0).Normalize();
                    XYZ flat = new XYZ(rawDir.X, rawDir.Y, 0);
                    if (flat.GetLength() > 0.001)
                    {
                        dirU = flat.Normalize();
                        if (dirU.X < 0 || (Math.Abs(dirU.X) < 1e-4 && dirU.Y < 0))
                        {
                            dirU = -dirU;
                        }
                    }
                }

                XYZ normal = profile.Normal.GetLength() > 0.5 ? profile.Normal.Normalize() : XYZ.BasisZ;
                XYZ dirV = normal.CrossProduct(dirU).Normalize();

                profile.AxisU = dirU;
                profile.AxisV = dirV;

                double minU = double.MaxValue, maxU = double.MinValue;
                double minV = double.MaxValue, maxV = double.MinValue;
                foreach (Curve c in profile.OuterBoundary)
                {
                    XYZ p = c.GetEndPoint(0);
                    XYZ vec = p - profile.Origin;
                    double u = vec.DotProduct(dirU);
                    double v = vec.DotProduct(dirV);
                    minU = Math.Min(minU, u);
                    maxU = Math.Max(maxU, u);
                    minV = Math.Min(minV, v);
                    maxV = Math.Max(maxV, v);
                }

                profile.LocalMinU = minU;
                profile.LocalMaxU = maxU;
                profile.LocalMinV = minV;
                profile.LocalMaxV = maxV;
            }

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
        /// Tính toán các phân đoạn thép trong hệ toạ độ phẳng Local 2D (u, v) của sàn,
        /// hỗ trợ sàn xoay bất kỳ góc độ nào, sàn đa giác, L, T, và cắt chuẩn xác qua các lỗ mở.
        /// </summary>
        public static List<(double Start, double End)> GetSlabIntervalsLocal(
            double fixedCoord, bool isAlongU,
            CurveLoop boundary, List<CurveLoop> openings,
            XYZ origin, XYZ axisU, XYZ axisV,
            double coverFeet)
        {
            if (boundary == null) return new List<(double, double)>();
            var rawCrossings = new List<double>();

            // 1. Tìm giao điểm của tia rải thép trong toạ độ phẳng Local (u, v)
            foreach (Curve c in boundary)
            {
                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);

                double u0 = (p0 - origin).DotProduct(axisU);
                double v0 = (p0 - origin).DotProduct(axisV);
                double u1 = (p1 - origin).DotProduct(axisU);
                double v1 = (p1 - origin).DotProduct(axisV);

                double cFixed0 = isAlongU ? v0 : u0;
                double cFixed1 = isAlongU ? v1 : u1;
                double cDir0 = isAlongU ? u0 : v0;
                double cDir1 = isAlongU ? u1 : v1;

                if ((cFixed0 <= fixedCoord && fixedCoord < cFixed1) || (cFixed1 <= fixedCoord && fixedCoord < cFixed0))
                {
                    double t = (fixedCoord - cFixed0) / (cFixed1 - cFixed0);
                    double dirIntersect = cDir0 + t * (cDir1 - cDir0);
                    rawCrossings.Add(dirIntersect);
                }
            }

            if (rawCrossings.Count < 2) return new List<(double, double)>();

            rawCrossings.Sort();
            var validSlabSegments = new List<(double Start, double End)>();
            for (int i = 0; i + 1 < rawCrossings.Count; i += 2)
            {
                double segStart = rawCrossings[i] + coverFeet;
                double segEnd = rawCrossings[i + 1] - coverFeet;
                if (segEnd - segStart >= 0.5) // Tối thiểu ~150mm
                {
                    validSlabSegments.Add((segStart, segEnd));
                }
            }

            // 2. Cắt trừ các lỗ mở trong sàn theo toạ độ local
            if (openings == null || !openings.Any()) return validSlabSegments;

            var intervals = validSlabSegments;
            foreach (var op in openings)
            {
                double opMinDir = double.MaxValue, opMaxDir = double.MinValue;
                double opMinFixed = double.MaxValue, opMaxFixed = double.MinValue;

                foreach (Curve c in op)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    double u0 = (p0 - origin).DotProduct(axisU);
                    double v0 = (p0 - origin).DotProduct(axisV);
                    double u1 = (p1 - origin).DotProduct(axisU);
                    double v1 = (p1 - origin).DotProduct(axisV);

                    double dir0 = isAlongU ? u0 : v0;
                    double dir1 = isAlongU ? u1 : v1;
                    double fix0 = isAlongU ? v0 : u0;
                    double fix1 = isAlongU ? v1 : u1;

                    opMinDir = Math.Min(opMinDir, Math.Min(dir0, dir1));
                    opMaxDir = Math.Max(opMaxDir, Math.Max(dir0, dir1));
                    opMinFixed = Math.Min(opMinFixed, Math.Min(fix0, fix1));
                    opMaxFixed = Math.Max(opMaxFixed, Math.Max(fix0, fix1));
                }

                if (fixedCoord < opMinFixed || fixedCoord > opMaxFixed) continue;

                double holeStart = opMinDir - coverFeet;
                double holeEnd = opMaxDir + coverFeet;

                var nextIntervals = new List<(double Start, double End)>();
                foreach (var seg in intervals)
                {
                    if (seg.End <= holeStart || seg.Start >= holeEnd)
                    {
                        nextIntervals.Add(seg);
                    }
                    else if (seg.Start < holeStart && seg.End > holeEnd)
                    {
                        if (holeStart - seg.Start >= 0.5) nextIntervals.Add((seg.Start, holeStart));
                        if (seg.End - holeEnd >= 0.5) nextIntervals.Add((holeEnd, seg.End));
                    }
                    else if (seg.Start < holeStart && seg.End <= holeEnd)
                    {
                        if (holeStart - seg.Start >= 0.5) nextIntervals.Add((seg.Start, holeStart));
                    }
                    else if (seg.Start >= holeStart && seg.End > holeEnd)
                    {
                        if (seg.End - holeEnd >= 0.5) nextIntervals.Add((holeEnd, seg.End));
                    }
                }
                intervals = nextIntervals;
            }

            return intervals;
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

        private static PlanarFace GetTopPlanarFace(Floor floor)
        {
            var options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = floor.get_Geometry(options);
            if (geomElem == null) return null;

            PlanarFace topFace = null;
            double maxZ = -double.MaxValue;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-6)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ, 0.5))
                        {
                            if (pf.Origin.Z > maxZ)
                            {
                                maxZ = pf.Origin.Z;
                                topFace = pf;
                            }
                        }
                    }
                }
            }

            return topFace;
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
    }
}
