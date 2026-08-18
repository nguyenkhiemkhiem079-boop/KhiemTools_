using System;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public static class RectangularColumnGeometryHelper
    {
        public class ColumnProfile
        {
            public double B;            // feet, chiều rộng (cạnh X local khi chưa xoay)
            public double H;            // feet, chiều sâu (cạnh Y local khi chưa xoay)
            public double Height;       // feet, chiều cao cột
            public XYZ BaseCenter;      // tâm đáy cột (tính từ BoundingBox solid thực tế)
            public XYZ TopCenter;       // tâm đỉnh cột
            public double RotationRad;  // góc xoay cột quanh trục Z (từ LocationPoint.Rotation)
        }

        /// <summary>
        /// Đo tiết diện & vị trí chuẩn 100% của cột vuông/chữ nhật từ BoundingBox solid.
        /// Đảm bảo thép KHÔNG BAO GIỜ bị văng ra ngoài host.
        /// </summary>
        public static ColumnProfile GetRectangularProfile(FamilyInstance column)
        {
            var profile = new ColumnProfile();

            LocationPoint loc = column.Location as LocationPoint;
            profile.RotationRad = loc?.Rotation ?? 0.0;

            BoundingBoxXYZ bb = column.get_BoundingBox(null);
            if (bb == null) return profile;

            // Tâm cột BẮT BUỘC lấy theo trung điểm BoundingBox Solid thực tế của host
            // KHÔNG dùng loc.Point vì origin của FamilyInstance có thể bị lệch ở góc family!
            double x = (bb.Min.X + bb.Max.X) / 2.0;
            double y = (bb.Min.Y + bb.Max.Y) / 2.0;
            profile.BaseCenter = new XYZ(x, y, bb.Min.Z);
            profile.TopCenter = new XYZ(x, y, bb.Max.Z);
            profile.Height = bb.Max.Z - bb.Min.Z;

            // Kích thước hình học vật lý của BoundingBox
            double dx = bb.Max.X - bb.Min.X;
            double dy = bb.Max.Y - bb.Min.Y;

            double cos = Math.Abs(Math.Cos(profile.RotationRad));
            double phyB = (cos > 0.7) ? dx : dy;
            double phyH = (cos > 0.7) ? dy : dx;

            // Tìm parameter B & H
            Parameter bParam = column.Symbol.LookupParameter("b")
                            ?? column.Symbol.LookupParameter("Width")
                            ?? column.Symbol.LookupParameter("B")
                            ?? column.LookupParameter("b")
                            ?? column.LookupParameter("Width")
                            ?? column.LookupParameter("B");

            Parameter hParam = column.Symbol.LookupParameter("h")
                            ?? column.Symbol.LookupParameter("Height")
                            ?? column.Symbol.LookupParameter("Depth")
                            ?? column.Symbol.LookupParameter("H")
                            ?? column.LookupParameter("h")
                            ?? column.LookupParameter("Height")
                            ?? column.LookupParameter("Depth")
                            ?? column.LookupParameter("H");

            if (bParam != null && bParam.HasValue && bParam.AsDouble() > 0 &&
                hParam != null && hParam.HasValue && hParam.AsDouble() > 0)
            {
                double paramB = bParam.AsDouble();
                double paramH = hParam.AsDouble();

                // Đảm bảo B & H không bao giờ vượt quá BoundingBox vật lý của cột
                profile.B = (phyB > 0) ? Math.Min(paramB, phyB) : paramB;
                profile.H = (phyH > 0) ? Math.Min(paramH, phyH) : paramH;
            }
            else
            {
                profile.B = phyB;
                profile.H = phyH;
            }

            return profile;
        }

        /// <summary>
        /// Transform điểm từ local space (tâm 0,0) sang world space theo Transform chuẩn của FamilyInstance.
        /// Tự động xử lý cả cột bị xoay (Rotation) lẫn cột bị xoay lật (Mirrored).
        /// </summary>
        public static XYZ TransformLocalToWorld(FamilyInstance column, double lx, double ly, double lz, XYZ center, double rotationRad)
        {
            if (column != null)
            {
                try
                {
                    Transform tf = column.GetTransform();
                    XYZ vx = tf.BasisX.Normalize();
                    XYZ vy = tf.BasisY.Normalize();
                    XYZ vz = tf.BasisZ.Normalize();
                    return center + lx * vx + ly * vy + lz * vz;
                }
                catch { }
            }

            double cos = Math.Cos(rotationRad);
            double sin = Math.Sin(rotationRad);
            double rx = lx * cos - ly * sin;
            double ry = lx * sin + ly * cos;
            return new XYZ(center.X + rx, center.Y + ry, center.Z + lz);
        }
    }
}
