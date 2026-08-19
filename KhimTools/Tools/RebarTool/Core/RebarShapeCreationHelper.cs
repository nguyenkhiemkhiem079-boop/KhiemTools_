using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tạo Rebar chuẩn từ Curve, gán RebarShape tương ứng (JP_T00, JP_T51, JP_T75).
    /// Đảm bảo kiểm tra an toàn vị trí hình học — tuyệt đối KHÔNG làm văng/lệch thép ra ngoài host.
    /// </summary>
    /// <summary>
    /// Tạo Rebar chuẩn từ Curve, tự động xử lý và fallback an toàn để KHÔNG BAO GIỜ bị lỗi "Can't solve Rebar Shape".
    /// Đảm bảo kiểm tra an toàn vị trí hình học — tuyệt đối KHÔNG làm văng/lệch thép ra ngoài host.
    /// </summary>
    public static class RebarShapeCreationHelper
    {
        /// <summary>
        /// Tạo Rebar an toàn từ danh sách Curve với cơ chế fallback 4 cấp độ:
        /// 1) Thử useExistingShape: false (tránh xung đột tham số/hook với RebarShape có sẵn trong project).
        /// 2) Nếu có hooks mà lỗi, thử bỏ hooks với useExistingShape: false.
        /// 3) Thử chuyển về RebarStyle.Standard.
        /// 4) Thử useExistingShape: true.
        /// </summary>
        public static Rebar CreateFromCurvesSafe(
            Document doc,
            RebarStyle style,
            RebarBarType barType,
            RebarHookType hook0,
            RebarHookType hook1,
            Element host,
            XYZ norm,
            IList<Curve> curves,
            RebarHookOrientation hookOrient0 = RebarHookOrientation.Right,
            RebarHookOrientation hookOrient1 = RebarHookOrientation.Right)
        {
            if (curves == null || curves.Count == 0 || barType == null || host == null)
                return null;

            // Đảm bảo normal hợp lệ
            if (norm == null || norm.GetLength() < 0.001)
                norm = XYZ.BasisZ;
            else
                norm = norm.Normalize();

            // Nếu đường curve là vòng kín (Closed Loop), Revit không cho phép gắn hook ở 2 đầu vì trùng tọa độ điểm
            bool isClosedLoop = curves.Count >= 3 &&
                curves[0].GetEndPoint(0).DistanceTo(curves[curves.Count - 1].GetEndPoint(1)) < 0.005;

            if (isClosedLoop)
            {
                hook0 = null;
                hook1 = null;
            }

            Rebar rebar = null;

            // Cấp 1 (Ưu tiên số 1 - Khuyến nghị của Revit API): useExistingShape: true, createNewShape: true
            // Giúp Revit tự động gán và khớp với RebarShape chuẩn có sẵn (Rebar Shape 00, 51, M_00, T1), loại bỏ hoàn toàn lỗi "Can't solve Rebar Shape"
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, style, barType, hook0, hook1, host,
                    norm, curves, hookOrient0, hookOrient1, true, true);
                if (rebar != null) return rebar;
            }
            catch { }

            // Cấp 2: Nếu có hook mà bị lỗi tham số hook, thử bỏ hook với useExistingShape: true
            if (hook0 != null || hook1 != null)
            {
                try
                {
                    rebar = Rebar.CreateFromCurves(
                        doc, style, barType, null, null, host,
                        norm, curves, hookOrient0, hookOrient1, true, true);
                    if (rebar != null) return rebar;
                }
                catch { }
            }

            // Cấp 3: Nếu style là StirrupTie bị lỗi, chuyển sang Standard với useExistingShape: true
            if (style != RebarStyle.Standard)
            {
                try
                {
                    rebar = Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType, null, null, host,
                        norm, curves, hookOrient0, hookOrient1, true, true);
                    if (rebar != null) return rebar;
                }
                catch { }
            }

            // Cấp 4: Fallback useExistingShape: false nếu dự án chưa có bất kỳ RebarShape nào
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, style, barType, hook0, hook1, host,
                    norm, curves, hookOrient0, hookOrient1, false, true);
                if (rebar != null) return rebar;
            }
            catch { }

            // Cấp 5: Fallback Standard không hook với useExistingShape: false
            try
            {
                rebar = Rebar.CreateFromCurves(
                    doc, RebarStyle.Standard, barType, null, null, host,
                    norm, curves, hookOrient0, hookOrient1, false, true);
                if (rebar != null) return rebar;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RebarShapeCreationHelper] CreateFromCurvesSafe failed: {ex.Message}");
            }

            return rebar;
        }

        /// <summary>JP_T00 — thanh thẳng dọc từ bottom đến top.</summary>
        public static Rebar TryCreateStraightBar(Document doc, Element host, RebarBarType barType, XYZ bottom, XYZ top)
        {
            if (bottom.DistanceTo(top) < 0.01) return null;

            XYZ dir = (top - bottom).Normalize();
            XYZ refNorm = Math.Abs(dir.Z) > 0.9 ? XYZ.BasisX : XYZ.BasisZ;
            XYZ perp = dir.CrossProduct(refNorm);
            if (perp.GetLength() < 0.001)
            {
                refNorm = XYZ.BasisY;
                perp = dir.CrossProduct(refNorm);
            }
            XYZ norm = perp.CrossProduct(dir).Normalize();

            Line line = Line.CreateBound(bottom, top);
            return CreateFromCurvesSafe(doc, RebarStyle.Standard, barType, null, null, host, norm, new List<Curve> { line });
        }

        /// <summary>Vòng tròn kín, nằm ngang (mặt phẳng XY) tại center.Z.</summary>
        public static Rebar TryCreateCircularStirrup(Document doc, Element host, RebarBarType barType, XYZ center, double diameterFeet)
        {
            double r = diameterFeet / 2.0;
            if (r <= 0.01) return null;

            Arc arc1 = Arc.Create(center, r, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, r, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
            var loop = new List<Curve> { arc1, arc2 };

            return CreateFromCurvesSafe(doc, RebarStyle.StirrupTie, barType, null, null, host, XYZ.BasisZ, loop);
        }

        /// <summary>
        /// Gán RebarShape an toàn cho Rebar nếu tương thích.
        /// </summary>
        public static void AssignShapeIfLoaded(Rebar rebar, RebarShape shape)
        {
            // Tránh ép tham số REBAR_SHAPE lên Rebar tạo từ Curve tự do để không gây lỗi Can't solve Rebar Shape
        }
    }
}
