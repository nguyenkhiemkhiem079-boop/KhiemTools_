using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tạo đai tròn cho cột. Ưu tiên dùng family JP_T75 (RebarShapeConfig.CircularStirrup)
    /// đã nạp qua RebarShapeLibrary — đúng yêu cầu dùng family thép riêng thay vì shape mặc
    /// định của Revit. Nếu vì lý do gì đó không nạp được family (thiếu file .rfa cạnh DLL...),
    /// fallback về cách ghép 2 Arc để lệnh vẫn chạy được thay vì crash.
    /// </summary>
    public static class CircularStirrupHelper
    {
        public static Rebar CreateHoop(Document doc, Element hostColumn, RebarBarType barType,
            XYZ center, double radius, XYZ normal)
        {
            Rebar viaShape = RebarShapeCreationHelper.TryCreateCircularStirrup(
                doc, hostColumn, barType, center, radius * 2.0);
            if (viaShape != null) return viaShape;

            return CreateHoopFromArcsFallback(doc, hostColumn, barType, center, radius, normal);
        }

        private static Rebar CreateHoopFromArcsFallback(Document doc, Element hostColumn,
            RebarBarType barType, XYZ center, double radius, XYZ normal)
        {
            Arc arc1 = Arc.Create(center, radius, 0, System.Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, radius, System.Math.PI, 2 * System.Math.PI, XYZ.BasisX, XYZ.BasisY);
            var loop = new List<Curve> { arc1, arc2 };

            return RebarShapeCreationHelper.CreateFromCurvesSafe(
                doc,
                RebarStyle.StirrupTie,
                barType,
                null,
                null,
                hostColumn,
                normal ?? XYZ.BasisZ,
                loop,
                RebarHookOrientation.Right,
                RebarHookOrientation.Right);
        }
    }
}
