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
