using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Profile phân tích hình học cho đối tượng Móng (Structural Foundation / Footing / Pile Cap).
    /// </summary>
    public class FoundationProfile
    {
        public FamilyInstance FoundationElement { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }
        public XYZ Center { get; set; }
        public double LengthFeet { get; set; }
        public double WidthFeet { get; set; }
        public double ThicknessFeet { get; set; }

        public double CoverBottomFeet { get; set; } = 50.0 / 304.8;
        public double CoverTopFeet { get; set; } = 50.0 / 304.8;
        public double CoverSideFeet { get; set; } = 50.0 / 304.8;
    }

    /// <summary>
    /// Helper trích xuất số liệu hình học 3D của móng đơn, móng băng, đài móng.
    /// </summary>
    public static class FoundationGeometryHelper
    {
        public static FoundationProfile AnalyzeFoundation(Document doc, FamilyInstance foundation)
        {
            if (doc == null || foundation == null) return null;

            var profile = new FoundationProfile
            {
                FoundationElement = foundation
            };

            BoundingBoxXYZ bb = foundation.get_BoundingBox(null);
            if (bb == null) return null;

            if (!HasAxisAlignedPlanBasis(foundation.GetTransform()))
                throw new InvalidOperationException(
                    "Foundation reinforcement currently supports axis-aligned plan hosts only. Rotate the footing to a world-aligned orientation or use an approved host-specific detailing workflow.");

            if (!HasSingleAxisAlignedRectangularSolid(foundation, bb))
                throw new InvalidOperationException(
                    "Foundation reinforcement currently supports one axis-aligned rectangular solid only. Irregular, tapered, stepped, or multi-solid foundation families require an approved host-specific detailing workflow.");

            profile.BoundingBox = bb;
            profile.LengthFeet = Math.Abs(bb.Max.X - bb.Min.X);
            profile.WidthFeet = Math.Abs(bb.Max.Y - bb.Min.Y);
            profile.ThicknessFeet = Math.Abs(bb.Max.Z - bb.Min.Z);
            profile.Center = (bb.Min + bb.Max) / 2.0;

            // Lấy Lớp bảo vệ (Cover)
            double botCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Bottom);
            double topCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Top);
            double sideCover = RebarCoverHelper.GetColumnCover(foundation, RebarFace.Exterior);

            if (botCover > 0) profile.CoverBottomFeet = botCover;
            if (topCover > 0) profile.CoverTopFeet = topCover;
            if (sideCover > 0) profile.CoverSideFeet = sideCover;

            return profile;
        }

        private static bool HasAxisAlignedPlanBasis(Transform transform)
        {
            if (transform == null) return false;
            const double tolerance = 1e-6;
            XYZ x = transform.BasisX;
            XYZ y = transform.BasisY;
            XYZ z = transform.BasisZ;
            bool zAxisAligned = Math.Abs(z.X) <= tolerance && Math.Abs(z.Y) <= tolerance &&
                Math.Abs(Math.Abs(z.Z) - 1.0) <= tolerance;
            bool xyAligned =
                (Math.Abs(Math.Abs(x.X) - 1.0) <= tolerance && Math.Abs(x.Y) <= tolerance && Math.Abs(x.Z) <= tolerance &&
                 Math.Abs(Math.Abs(y.Y) - 1.0) <= tolerance && Math.Abs(y.X) <= tolerance && Math.Abs(y.Z) <= tolerance) ||
                (Math.Abs(Math.Abs(x.Y) - 1.0) <= tolerance && Math.Abs(x.X) <= tolerance && Math.Abs(x.Z) <= tolerance &&
                 Math.Abs(Math.Abs(y.X) - 1.0) <= tolerance && Math.Abs(y.Y) <= tolerance && Math.Abs(y.Z) <= tolerance);
            return zAxisAligned && xyAligned;
        }

        private static bool HasSingleAxisAlignedRectangularSolid(FamilyInstance foundation, BoundingBoxXYZ bounds)
        {
            var options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };
            var solids = new List<Solid>();
            CollectInstanceSolids(foundation.get_Geometry(options), solids);
            solids = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9).ToList();
            if (solids.Count != 1) return false;

            Solid candidate = solids[0];
            double minX = bounds.Min.X, minY = bounds.Min.Y, minZ = bounds.Min.Z;
            double maxX = bounds.Max.X, maxY = bounds.Max.Y, maxZ = bounds.Max.Z;
            double scale = Math.Max(1.0, Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)));
            double tolerance = Math.Max(1e-7, scale * 1e-7);
            double boxVolume = (maxX - minX) * (maxY - minY) * (maxZ - minZ);
            if (boxVolume <= 1e-9 || Math.Abs(candidate.Volume - boxVolume) > Math.Max(1e-8, boxVolume * 1e-7))
                return false;

            var corners = new HashSet<string>(StringComparer.Ordinal);
            var axisFaceCounts = new int[3];
            foreach (Face face in candidate.Faces)
            {
                var planar = face as PlanarFace;
                if (planar == null) return false;
                XYZ normal = planar.FaceNormal;
                int axis = Math.Abs(normal.X) > 1.0 - 1e-7 && Math.Abs(normal.Y) < 1e-7 && Math.Abs(normal.Z) < 1e-7 ? 0 :
                    Math.Abs(normal.Y) > 1.0 - 1e-7 && Math.Abs(normal.X) < 1e-7 && Math.Abs(normal.Z) < 1e-7 ? 1 :
                    Math.Abs(normal.Z) > 1.0 - 1e-7 && Math.Abs(normal.X) < 1e-7 && Math.Abs(normal.Y) < 1e-7 ? 2 : -1;
                if (axis < 0) return false;
                axisFaceCounts[axis]++;

                foreach (CurveLoop loop in planar.GetEdgesAsCurveLoops())
                foreach (Curve edge in loop)
                {
                    var line = edge as Line;
                    if (line == null) return false;
                    XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1);
                    if (!IsBoxCorner(a, minX, minY, minZ, maxX, maxY, maxZ, tolerance) ||
                        !IsBoxCorner(b, minX, minY, minZ, maxX, maxY, maxZ, tolerance)) return false;
                    corners.Add(CornerKey(a, minX, minY, minZ, maxX, maxY, maxZ, tolerance));
                    corners.Add(CornerKey(b, minX, minY, minZ, maxX, maxY, maxZ, tolerance));
                    XYZ direction = (b - a).Normalize();
                    int edgeAxis = Math.Abs(direction.X) > 1.0 - 1e-7 ? 0 : Math.Abs(direction.Y) > 1.0 - 1e-7 ? 1 : Math.Abs(direction.Z) > 1.0 - 1e-7 ? 2 : -1;
                    if (edgeAxis < 0 || edgeAxis == axis) return false;
                }
            }

            return axisFaceCounts[0] == 2 && axisFaceCounts[1] == 2 && axisFaceCounts[2] == 2 && corners.Count == 8;
        }

        private static void CollectInstanceSolids(GeometryElement geometry, IList<Solid> solids)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                var solid = item as Solid;
                if (solid != null)
                {
                    solids.Add(solid);
                    continue;
                }
                var instance = item as GeometryInstance;
                if (instance != null) CollectInstanceSolids(instance.GetInstanceGeometry(), solids);
            }
        }

        private static bool IsBoxCorner(XYZ point, double minX, double minY, double minZ, double maxX, double maxY, double maxZ, double tolerance)
        {
            return IsEnd(point.X, minX, maxX, tolerance) && IsEnd(point.Y, minY, maxY, tolerance) && IsEnd(point.Z, minZ, maxZ, tolerance);
        }

        private static string CornerKey(XYZ point, double minX, double minY, double minZ, double maxX, double maxY, double maxZ, double tolerance)
        {
            return (Math.Abs(point.X - minX) <= tolerance ? "0" : "1") +
                (Math.Abs(point.Y - minY) <= tolerance ? "0" : "1") +
                (Math.Abs(point.Z - minZ) <= tolerance ? "0" : "1");
        }

        private static bool IsEnd(double value, double min, double max, double tolerance)
        {
            return Math.Abs(value - min) <= tolerance || Math.Abs(value - max) <= tolerance;
        }
    }
}
