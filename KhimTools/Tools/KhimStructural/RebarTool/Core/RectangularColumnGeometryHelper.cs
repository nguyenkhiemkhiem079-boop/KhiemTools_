using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public static class RectangularColumnGeometryHelper
    {
        public class ColumnProfile
        {
            public double B;            // feet, width along family local X
            public double H;            // feet, depth along family local Y
            public double Height;       // feet, vertical height
            public XYZ BaseCenter;      // geometric center of the lower section
            public XYZ TopCenter;       // geometric center of the upper section
            public double RotationRad;  // angle of the family local X axis about Z
        }

        /// <summary>
        /// Analyzes physical family geometry and accepts only one straight rectangular
        /// prism aligned to the family transform. Bounding boxes and family parameters
        /// alone are not sufficient proof of a rectangular concrete host.
        /// </summary>
        public static ColumnProfile GetRectangularProfile(FamilyInstance column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            Transform transform = column.GetTransform();
            XYZ axisX = transform.BasisX.Normalize();
            XYZ axisY = transform.BasisY.Normalize();
            XYZ axisZ = transform.BasisZ.Normalize();
            if (Math.Abs(axisX.Z) > 1e-6 || Math.Abs(axisY.Z) > 1e-6 ||
                Math.Abs(axisZ.X) > 1e-6 || Math.Abs(axisZ.Y) > 1e-6 || Math.Abs(axisZ.Z - 1.0) > 1e-6 ||
                Math.Abs(axisX.DotProduct(axisY)) > 1e-6 || Math.Abs(axisX.DotProduct(axisZ)) > 1e-6 || Math.Abs(axisY.DotProduct(axisZ)) > 1e-6)
                throw new InvalidOperationException("Rectangular-column reinforcement supports plan-rotated vertical columns only.");

            var solids = new List<Solid>();
            CollectInstanceSolids(column.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine }), solids);
            solids = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9).ToList();
            if (solids.Count != 1)
                throw new InvalidOperationException("Rectangular-column reinforcement requires one continuous rectangular concrete solid; non-prismatic or multi-solid families are unsupported.");

            Solid solid = solids[0];
            PlanarFace[] planes = solid.Faces.Cast<Face>().OfType<PlanarFace>().ToArray();
            if (solid.Faces.Size != 6 || planes.Length != 6)
                throw new InvalidOperationException("Rectangular-column reinforcement requires a six-face rectangular prism.");

            var vertices = new List<XYZ>();
            foreach (Edge edge in solid.Edges)
            {
                Curve curve = edge.AsCurve();
                vertices.Add(curve.GetEndPoint(0));
                vertices.Add(curve.GetEndPoint(1));
            }
            if (vertices.Count < 8)
                throw new InvalidOperationException("Rectangular-column solid does not expose enough edge vertices to verify its section.");

            double minX = vertices.Min(point => point.DotProduct(axisX));
            double maxX = vertices.Max(point => point.DotProduct(axisX));
            double minY = vertices.Min(point => point.DotProduct(axisY));
            double maxY = vertices.Max(point => point.DotProduct(axisY));
            double minZ = vertices.Min(point => point.Z);
            double maxZ = vertices.Max(point => point.Z);
            double b = maxX - minX;
            double h = maxY - minY;
            double height = maxZ - minZ;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            if (b <= tolerance || h <= tolerance || height <= tolerance)
                throw new InvalidOperationException("Rectangular-column solid has a zero or invalid physical dimension.");

            var faceCounts = new int[6];
            foreach (PlanarFace face in planes)
            {
                XYZ normal = face.FaceNormal.Normalize();
                double dotX = normal.DotProduct(axisX);
                double dotY = normal.DotProduct(axisY);
                double dotZ = normal.DotProduct(axisZ);
                double absX = Math.Abs(dotX);
                double absY = Math.Abs(dotY);
                double absZ = Math.Abs(dotZ);
                if (Math.Max(absX, Math.Max(absY, absZ)) < 1.0 - 1e-6)
                    throw new InvalidOperationException("Rectangular-column side faces must be parallel to the family axes.");

                int faceIndex;
                double faceCoordinate;
                double expectedCoordinate;
                if (absX >= absY && absX >= absZ)
                {
                    faceIndex = dotX >= 0 ? 0 : 1;
                    faceCoordinate = face.Origin.DotProduct(axisX);
                    expectedCoordinate = dotX >= 0 ? maxX : minX;
                }
                else if (absY >= absZ)
                {
                    faceIndex = dotY >= 0 ? 2 : 3;
                    faceCoordinate = face.Origin.DotProduct(axisY);
                    expectedCoordinate = dotY >= 0 ? maxY : minY;
                }
                else
                {
                    faceIndex = dotZ >= 0 ? 4 : 5;
                    faceCoordinate = face.Origin.Z;
                    expectedCoordinate = dotZ >= 0 ? maxZ : minZ;
                }

                if (Math.Abs(faceCoordinate - expectedCoordinate) > tolerance)
                    throw new InvalidOperationException("Rectangular-column planar faces do not bound one constant-section prism.");
                faceCounts[faceIndex]++;
            }
            if (faceCounts.Any(count => count != 1))
                throw new InvalidOperationException("Rectangular-column prism must have exactly one planar face on each of its six sides.");

            var cornerKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (XYZ point in vertices)
            {
                int xSide = GetExtremeSide(point.DotProduct(axisX), minX, maxX, tolerance);
                int ySide = GetExtremeSide(point.DotProduct(axisY), minY, maxY, tolerance);
                int zSide = GetExtremeSide(point.Z, minZ, maxZ, tolerance);
                if (xSide < 0 || ySide < 0 || zSide < 0)
                    throw new InvalidOperationException("Rectangular-column solid contains non-corner or stepped edge vertices.");
                cornerKeys.Add(xSide.ToString() + ySide.ToString() + zSide.ToString());
            }
            if (cornerKeys.Count != 8)
                throw new InvalidOperationException("Rectangular-column solid does not contain all eight rectangular-prism corners.");

            double expectedVolume = b * h * height;
            if (Math.Abs(solid.Volume - expectedVolume) > Math.Max(1e-8, expectedVolume * 1e-7))
                throw new InvalidOperationException("Rectangular-column solid volume does not match its measured rectangular-prism envelope.");

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            XYZ sectionCenter = axisX * centerX + axisY * centerY;
            double rotation = Math.Atan2(axisX.Y, axisX.X);
            return new ColumnProfile
            {
                B = b,
                H = h,
                Height = height,
                BaseCenter = new XYZ(sectionCenter.X, sectionCenter.Y, minZ),
                TopCenter = new XYZ(sectionCenter.X, sectionCenter.Y, maxZ),
                RotationRad = rotation
            };
        }

        private static int GetExtremeSide(double value, double min, double max, double tolerance)
        {
            if (Math.Abs(value - min) <= tolerance) return 0;
            if (Math.Abs(value - max) <= tolerance) return 1;
            return -1;
        }

        private static void CollectInstanceSolids(GeometryElement geometry, IList<Solid> solids)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                Solid solid = item as Solid;
                if (solid != null) solids.Add(solid);
                else
                {
                    GeometryInstance instance = item as GeometryInstance;
                    if (instance != null) CollectInstanceSolids(instance.GetInstanceGeometry(), solids);
                }
            }
        }

        /// <summary>
        /// Transform point from local section coordinates to world space using the family transform.
        /// </summary>
        public static XYZ TransformLocalToWorld(FamilyInstance column, double lx, double ly, double lz, XYZ center, double rotationRad)
        {
            Transform transform = column.GetTransform();
            XYZ axisX = transform.BasisX.Normalize();
            XYZ axisY = transform.BasisY.Normalize();
            XYZ axisZ = transform.BasisZ.Normalize();
            return center + lx * axisX + ly * axisY + lz * axisZ;
        }
    }
}
