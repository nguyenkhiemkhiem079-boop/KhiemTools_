using System;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public static class BeamGeometryHelper
    {
        public class BeamProfile
        {
            public double B { get; set; }           // Width (feet)
            public double H { get; set; }           // Height (feet)
            public double Length { get; set; }      // Length (feet)
            public XYZ StartPoint { get; set; }    // Start center point of beam
            public XYZ EndPoint { get; set; }      // End center point of beam
            public XYZ Direction { get; set; }     // Unit vector along beam axis
            public XYZ RightVector { get; set; }   // Unit horizontal vector (width direction)
            public XYZ UpVector { get; set; }      // Unit vertical vector (height direction)
        }

        /// <summary>
        /// Trích xuất hình học chuẩn 100% của Dầm (Structural Framing) từ BoundingBox và LocationCurve.
        /// </summary>
        public static BeamProfile GetBeamProfile(FamilyInstance beam)
        {
            var profile = new BeamProfile();

            LocationCurve locCurve = beam.Location as LocationCurve;
            if (locCurve == null || locCurve.Curve == null) return profile;

            Curve curve = locCurve.Curve;
            XYZ pStart = curve.GetEndPoint(0);
            XYZ pEnd = curve.GetEndPoint(1);

            double len = pStart.DistanceTo(pEnd);
            if (len < 0.01) return null;

            profile.StartPoint = pStart;
            profile.EndPoint = pEnd;
            profile.Length = len;

            XYZ dir = (pEnd - pStart).Normalize();
            profile.Direction = dir;

            // Compute local axes: UpVector = BasisZ (trừ khi dầm nghiêng/đứng)
            XYZ up = XYZ.BasisZ;
            if (Math.Abs(dir.Z) > 0.95)
                up = XYZ.BasisX;

            XYZ right = dir.CrossProduct(up);
            if (right.GetLength() < 0.001)
            {
                up = XYZ.BasisY;
                right = dir.CrossProduct(up);
            }
            right = right.Normalize();
            up = right.CrossProduct(dir).Normalize();

            profile.RightVector = right;
            profile.UpVector = up;

            // Kích thước B & H từ parameter
            Parameter bParam = beam.Symbol.LookupParameter("b")
                            ?? beam.Symbol.LookupParameter("Width")
                            ?? beam.Symbol.LookupParameter("B")
                            ?? beam.LookupParameter("b")
                            ?? beam.LookupParameter("Width")
                            ?? beam.LookupParameter("B");

            Parameter hParam = beam.Symbol.LookupParameter("h")
                            ?? beam.Symbol.LookupParameter("Height")
                            ?? beam.Symbol.LookupParameter("Depth")
                            ?? beam.Symbol.LookupParameter("H")
                            ?? beam.LookupParameter("h")
                            ?? beam.LookupParameter("Height")
                            ?? beam.LookupParameter("Depth")
                            ?? beam.LookupParameter("H");

            BoundingBoxXYZ bb = beam.get_BoundingBox(null);
            double bbWidth = 0, bbHeight = 0;
            if (bb != null)
            {
                bbWidth = Math.Min(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
                bbHeight = bb.Max.Z - bb.Min.Z;
            }

            profile.B = (bParam != null && bParam.HasValue && bParam.AsDouble() > 0) ? bParam.AsDouble() : (bbWidth > 0 ? bbWidth : UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters));
            profile.H = (hParam != null && hParam.HasValue && hParam.AsDouble() > 0) ? hParam.AsDouble() : (bbHeight > 0 ? bbHeight : UnitUtils.ConvertToInternalUnits(600, UnitTypeId.Millimeters));

            // Chuẩn hóa StartPoint & EndPoint về đúng trục tâm 3D (Centroid) của dầm bê tông
            if (bb != null)
            {
                XYZ bbCenter = (bb.Min + bb.Max) / 2.0;
                XYZ locMid = (pStart + pEnd) / 2.0;

                double verticalShift = bbCenter.Z - locMid.Z;
                XYZ diff = bbCenter - locMid;
                double horizontalShift = diff.DotProduct(right);

                XYZ centerShift = verticalShift * up + horizontalShift * right;
                profile.StartPoint = pStart + centerShift;
                profile.EndPoint = pEnd + centerShift;
            }
            else
            {
                profile.StartPoint = pStart - (profile.H / 2.0) * up;
                profile.EndPoint = pEnd - (profile.H / 2.0) * up;
            }

            return profile;
        }

        /// <summary>
        /// Transform điểm từ local space dầm (x = width, y = height, z = length) sang World coordinates.
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
