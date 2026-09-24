using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public static class BeamGeometryHelper
    {
        public class BeamProfile
        {
            public double B { get; set; }           // Width (feet)
            public double H { get; set; }           // Height (feet)
            public double Length { get; set; }      // Length of the straight location axis (feet)
            public XYZ StartPoint { get; set; }    // Start section center point of beam
            public XYZ EndPoint { get; set; }      // End section center point of beam
            public XYZ Direction { get; set; }     // Unit vector along beam axis
            public XYZ RightVector { get; set; }   // Unit horizontal/local width vector
            public XYZ UpVector { get; set; }      // Unit local height vector
        }

        /// <summary>
        /// Analyzes physical framing geometry. The current reinforcement generator
        /// creates rectangular cages, so it only accepts one straight rectangular prism.
        /// Family parameters and axis-aligned bounding boxes are not proof of that shape.
        /// </summary>
        public static BeamProfile GetBeamProfile(FamilyInstance beam)
        {
            if (beam == null) throw new ArgumentNullException(nameof(beam));
            LocationCurve location = beam.Location as LocationCurve;
            Line line = location?.Curve as Line;
            if (line == null)
                throw new InvalidOperationException("Beam reinforcement supports straight framing location lines only.");

            XYZ pStart = line.GetEndPoint(0);
            XYZ pEnd = line.GetEndPoint(1);
            double length = line.Length;
            if (length < 0.01)
                throw new InvalidOperationException("Beam reinforcement requires a non-zero straight location line.");

            XYZ direction = (pEnd - pStart).Normalize();
            XYZ up = Math.Abs(direction.Z) > 0.95 ? XYZ.BasisX : XYZ.BasisZ;
            XYZ right = direction.CrossProduct(up);
            if (right.GetLength() < 0.001)
            {
                up = XYZ.BasisY;
                right = direction.CrossProduct(up);
            }
            right = right.Normalize();
            up = right.CrossProduct(direction).Normalize();

            (double width, double height, double centerRight, double centerUp) =
                AnalyzeRectangularPrism(beam, direction, right, up);

            double locationRight = ((pStart + pEnd) / 2.0).DotProduct(right);
            double locationUp = ((pStart + pEnd) / 2.0).DotProduct(up);
            XYZ centerShift = (centerRight - locationRight) * right + (centerUp - locationUp) * up;

            return new BeamProfile
            {
                B = width,
                H = height,
                Length = length,
                StartPoint = pStart + centerShift,
                EndPoint = pEnd + centerShift,
                Direction = direction,
                RightVector = right,
                UpVector = up
            };
        }

        private static (double Width, double Height, double CenterRight, double CenterUp) AnalyzeRectangularPrism(
            FamilyInstance beam, XYZ direction, XYZ right, XYZ up)
        {
            var solids = new List<Solid>();
            CollectInstanceSolids(beam.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine }), solids);
            solids = solids.Where(solid => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9).ToList();
            if (solids.Count != 1)
                throw new InvalidOperationException("Beam reinforcement requires one continuous rectangular concrete solid; multi-solid framing families are unsupported.");

            Solid solid = solids[0];
            PlanarFace[] planes = solid.Faces.Cast<Face>().OfType<PlanarFace>().ToArray();
            if (solid.Faces.Size != 6 || planes.Length != 6)
                throw new InvalidOperationException("Beam reinforcement requires a six-face rectangular prism; tapered, cutback, curved and non-prismatic geometry is unsupported.");

            var vertices = new List<XYZ>();
            foreach (Edge edge in solid.Edges)
            {
                Curve curve = edge.AsCurve();
                vertices.Add(curve.GetEndPoint(0));
                vertices.Add(curve.GetEndPoint(1));
            }
            if (vertices.Count < 8)
                throw new InvalidOperationException("Beam solid does not expose enough edge vertices to verify its section.");

            double minRight = vertices.Min(point => point.DotProduct(right));
            double maxRight = vertices.Max(point => point.DotProduct(right));
            double minUp = vertices.Min(point => point.DotProduct(up));
            double maxUp = vertices.Max(point => point.DotProduct(up));
            double minAxis = vertices.Min(point => point.DotProduct(direction));
            double maxAxis = vertices.Max(point => point.DotProduct(direction));
            double width = maxRight - minRight;
            double height = maxUp - minUp;
            double solidLength = maxAxis - minAxis;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            if (width <= tolerance || height <= tolerance || solidLength <= tolerance)
                throw new InvalidOperationException("Beam solid has a zero or invalid physical dimension.");

            var faceCounts = new int[6];
            foreach (PlanarFace face in planes)
            {
                XYZ normal = face.FaceNormal.Normalize();
                double dotRight = normal.DotProduct(right);
                double dotUp = normal.DotProduct(up);
                double dotAxis = normal.DotProduct(direction);
                double absRight = Math.Abs(dotRight);
                double absUp = Math.Abs(dotUp);
                double absAxis = Math.Abs(dotAxis);
                if (Math.Max(absRight, Math.Max(absUp, absAxis)) < 1.0 - 1e-6)
                    throw new InvalidOperationException("Beam prism faces must be parallel to the straight location axis and rectangular section axes.");

                int faceIndex;
                double faceCoordinate;
                double expectedCoordinate;
                if (absRight >= absUp && absRight >= absAxis)
                {
                    faceIndex = dotRight >= 0 ? 0 : 1;
                    faceCoordinate = face.Origin.DotProduct(right);
                    expectedCoordinate = dotRight >= 0 ? maxRight : minRight;
                }
                else if (absUp >= absAxis)
                {
                    faceIndex = dotUp >= 0 ? 2 : 3;
                    faceCoordinate = face.Origin.DotProduct(up);
                    expectedCoordinate = dotUp >= 0 ? maxUp : minUp;
                }
                else
                {
                    faceIndex = dotAxis >= 0 ? 4 : 5;
                    faceCoordinate = face.Origin.DotProduct(direction);
                    expectedCoordinate = dotAxis >= 0 ? maxAxis : minAxis;
                }

                if (Math.Abs(faceCoordinate - expectedCoordinate) > tolerance)
                    throw new InvalidOperationException("Beam prism faces do not bound one constant rectangular section.");
                faceCounts[faceIndex]++;
            }
            if (faceCounts.Any(count => count != 1))
                throw new InvalidOperationException("Beam prism must have exactly one planar face on each of its six sides.");

            var cornerKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (XYZ point in vertices)
            {
                int rightSide = GetExtremeSide(point.DotProduct(right), minRight, maxRight, tolerance);
                int upSide = GetExtremeSide(point.DotProduct(up), minUp, maxUp, tolerance);
                int axisSide = GetExtremeSide(point.DotProduct(direction), minAxis, maxAxis, tolerance);
                if (rightSide < 0 || upSide < 0 || axisSide < 0)
                    throw new InvalidOperationException("Beam solid contains non-corner or stepped edge vertices.");
                cornerKeys.Add(rightSide.ToString() + upSide.ToString() + axisSide.ToString());
            }
            if (cornerKeys.Count != 8)
                throw new InvalidOperationException("Beam solid does not contain all eight rectangular-prism corners.");

            double expectedVolume = width * height * solidLength;
            if (Math.Abs(solid.Volume - expectedVolume) > Math.Max(1e-8, expectedVolume * 1e-7))
                throw new InvalidOperationException("Beam solid volume does not match its measured rectangular-prism envelope.");

            return (width, height, (minRight + maxRight) / 2.0, (minUp + maxUp) / 2.0);
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
        /// Transform point from local beam coordinates (x = width, y = height, z = length) to world coordinates.
        /// </summary>
        public static XYZ TransformLocalToWorld(BeamProfile profile, double offsetX, double offsetY, double offsetZ)
        {
            return profile.StartPoint
                 + offsetZ * profile.Direction
                 + offsetX * profile.RightVector
                 + offsetY * profile.UpVector;
        }
    }
}
