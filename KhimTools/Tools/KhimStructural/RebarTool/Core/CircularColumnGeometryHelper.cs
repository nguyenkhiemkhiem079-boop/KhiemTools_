using System;
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
            var profile = new ColumnProfile();

            BoundingBoxXYZ bb = column.get_BoundingBox(null);
            if (bb == null) return profile;

            // Tâm cột BẮT BUỘC lấy theo trung điểm BoundingBox Solid thực tế của host
            double x = (bb.Min.X + bb.Max.X) / 2.0;
            double y = (bb.Min.Y + bb.Max.Y) / 2.0;

            profile.BaseCenter = new XYZ(x, y, bb.Min.Z);
            profile.TopCenter = new XYZ(x, y, bb.Max.Z);
            profile.Height = bb.Max.Z - bb.Min.Z;

            double phyDia = Math.Min(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);

            Parameter diaParam = column.Symbol.LookupParameter("Diameter")
                                  ?? column.Symbol.LookupParameter("b")
                                  ?? column.LookupParameter("Diameter");

            if (diaParam != null && diaParam.HasValue && diaParam.AsDouble() > 0)
            {
                double paramDia = diaParam.AsDouble();
                profile.Diameter = (phyDia > 0) ? Math.Min(paramDia, phyDia) : paramDia;
            }
            else
            {
                profile.Diameter = phyDia;
            }

            return profile;
        }
    }
}
