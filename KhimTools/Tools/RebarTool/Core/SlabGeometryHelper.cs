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
                    // Loop lớn nhất có diện tích/chu vi lớn nhất là ranh giới ngoài
                    var sortedLoops = loops.OrderByDescending(GetLoopArea).ToList();
                    profile.OuterBoundary = sortedLoops[0];

                    // Các loop còn lại là lỗ mở trong sàn
                    for (int i = 1; i < sortedLoops.Count; i++)
                    {
                        profile.InnerOpenings.Add(sortedLoops[i]);
                    }
                }
            }

            // 4. Kích thước BoundingBox
            if (profile.BoundingBox != null)
            {
                double dx = profile.BoundingBox.Max.X - profile.BoundingBox.Min.X;
                double dy = profile.BoundingBox.Max.Y - profile.BoundingBox.Min.Y;
                profile.WidthMm = UnitUtils.ConvertFromInternalUnits(Math.Min(dx, dy), UnitTypeId.Millimeters);
                profile.LengthMm = UnitUtils.ConvertFromInternalUnits(Math.Max(dx, dy), UnitTypeId.Millimeters);
            }

            return profile;
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

        private static double GetLoopArea(CurveLoop loop)
        {
            if (loop == null) return 0;
            double length = 0;
            foreach (Curve c in loop) length += c.Length;
            return length;
        }
    }
}
