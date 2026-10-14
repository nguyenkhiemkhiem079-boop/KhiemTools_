using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public static class CircularColumnGeometryHelper
    {
        public class ColumnProfile
        {
            public double Diameter;     // feet
            public double Height;       // feet
            public XYZ BaseCenter;      // tâm đáy cột
            public XYZ TopCenter;       // tâm đỉnh cột
        }

        /// <summary>
        /// Đo tiết diện + chiều cao chuẩn 100% của cột tròn từ BoundingBox solid.
        /// </summary>
        public static ColumnProfile GetCircularProfile(FamilyInstance column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            BoundingBoxXYZ bb = column.get_BoundingBox(null);
            if (bb == null) throw new InvalidOperationException("Circular-column model bounds are unavailable.");
            CylindricalFace cylinder = GetSupportedCircularCylinder(column, bb, out double radius);
            var profile = new ColumnProfile();

            // The validated cylindrical face supplies the actual axis center; the bounds supply only its vertical extent.
            double x = cylinder.Origin.X;
            double y = cylinder.Origin.Y;

            profile.BaseCenter = new XYZ(x, y, bb.Min.Z);
            profile.TopCenter = new XYZ(x, y, bb.Max.Z);
            profile.Height = bb.Max.Z - bb.Min.Z;

            profile.Diameter = radius * 2.0;

            return profile;
        }

        private static CylindricalFace GetSupportedCircularCylinder(FamilyInstance column, BoundingBoxXYZ bounds, out double supportedRadius)
        {
            supportedRadius = 0;
            var solids = new List<Solid>();
            CollectInstanceSolids(column.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine }), solids);
            solids = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9).ToList();
            if (solids.Count != 1)
                throw new InvalidOperationException("Circular-column reinforcement requires one continuous cylindrical solid; non-circular or multi-solid families are unsupported.");

            Solid solid = solids[0];
            CylindricalFace[] cylinders = solid.Faces.Cast<Face>().OfType<CylindricalFace>().ToArray();
            PlanarFace[] planes = solid.Faces.Cast<Face>().OfType<PlanarFace>().ToArray();
            if (solid.Faces.Size != 3 || cylinders.Length != 1 || planes.Length != 2)
                throw new InvalidOperationException("Circular-column reinforcement requires one cylindrical side face and two planar end faces.");

            CylindricalFace cylinder = cylinders[0];
            XYZ axis = cylinder.Axis.Normalize();
            XYZ radiusVector = cylinder.get_Radius(0);
            double radius = radiusVector.GetLength();
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            double width = bounds.Max.X - bounds.Min.X;
            double depth = bounds.Max.Y - bounds.Min.Y;
            double height = bounds.Max.Z - bounds.Min.Z;
            bool verticalAxis = Math.Abs(axis.X) <= 1e-6 && Math.Abs(axis.Y) <= 1e-6 && Math.Abs(Math.Abs(axis.Z) - 1.0) <= 1e-6;
            bool circularBounds = Math.Abs(width - 2.0 * radius) <= tolerance && Math.Abs(depth - 2.0 * radius) <= tolerance;
            bool centered = Math.Abs(bounds.Min.X - (cylinder.Origin.X - radius)) <= tolerance &&
                Math.Abs(bounds.Max.X - (cylinder.Origin.X + radius)) <= tolerance &&
                Math.Abs(bounds.Min.Y - (cylinder.Origin.Y - radius)) <= tolerance &&
                Math.Abs(bounds.Max.Y - (cylinder.Origin.Y + radius)) <= tolerance;
            int topFaces = planes.Count(face => face.FaceNormal.Z > 1.0 - 1e-6 && Math.Abs(face.Origin.Z - bounds.Max.Z) <= tolerance);
            int bottomFaces = planes.Count(face => face.FaceNormal.Z < -1.0 + 1e-6 && Math.Abs(face.Origin.Z - bounds.Min.Z) <= tolerance);
            double expectedVolume = Math.PI * radius * radius * height;
            bool volumeMatches = Math.Abs(solid.Volume - expectedVolume) <= Math.Max(1e-8, expectedVolume * 1e-7);
            if (radius <= 0 || height <= 0 || !verticalAxis || !circularBounds || !centered || topFaces != 1 || bottomFaces != 1 || !volumeMatches)
                throw new InvalidOperationException("Circular-column reinforcement supports only a straight, constant-diameter, vertical circular solid with planar ends.");

            supportedRadius = radius;
            return cylinder;
        }

        private static void CollectInstanceSolids(GeometryElement geometry, IList<Solid> solids)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                var solid = item as Solid;
                if (solid != null) solids.Add(solid);
                else
                {
                    var instance = item as GeometryInstance;
                    if (instance != null) CollectInstanceSolids(instance.GetInstanceGeometry(), solids);
                }
            }
        }
    }
}
