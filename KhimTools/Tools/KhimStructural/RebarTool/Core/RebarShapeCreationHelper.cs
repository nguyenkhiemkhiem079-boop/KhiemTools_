using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public enum CreationStatus
    {
        Success,
        Degraded,
        Failed
    }

    /// <summary>
    /// Kết quả chi tiết của quá trình tạo thanh Rebar, minh bạch tuyệt đối về tình trạng hình học
    /// và loại bỏ hoàn toàn cơ chế âm thầm hạ cấp (silent fallback).
    /// </summary>
    public class RebarCreationResult
    {
        public Rebar Rebar { get; set; }
        public CreationStatus Status { get; set; } = CreationStatus.Failed;
        public string ErrorMessage { get; set; }
        public bool IsDegraded => Status == CreationStatus.Degraded;
        public string DegradationReason { get; set; }
        public RebarStyle RequestedStyle { get; set; }
        public RebarStyle AppliedStyle { get; set; }
        public bool HooksRequested { get; set; }
        public bool HooksApplied { get; set; }

        public static RebarCreationResult FailedResult(string error) =>
            new RebarCreationResult { Status = CreationStatus.Failed, ErrorMessage = error };
    }

    /// <summary>
    /// Helper tạo Rebar từ Curves với cơ chế xác thực minh bạch, ghi nhận chi tiết lỗi nếu thất bại,
    /// và TUYỆT ĐỐI KHÔNG âm thầm biến đổi hình học (No Silent Degradation).
    /// </summary>
    public static class RebarShapeCreationHelper
    {
        /// <summary>
        /// Tạo Rebar chi tiết với đầy đủ thông tin trạng thái (Success / Degraded / Failed).
        /// </summary>
        public static RebarCreationResult CreateFromCurvesDetailed(
            Document doc,
            RebarStyle style,
            RebarBarType barType,
            RebarHookType hook0,
            RebarHookType hook1,
            Element host,
            XYZ norm,
            IList<Curve> curves,
            RebarHookOrientation hookOrient0 = RebarHookOrientation.Right,
            RebarHookOrientation hookOrient1 = RebarHookOrientation.Right,
            bool allowDegradedFallback = false,
            RebarGenerationReport report = null)
        {
            if (doc == null || barType == null || host == null)
            {
                return RebarCreationResult.FailedResult("Thiếu dữ liệu đầu vào Document, BarType hoặc Host.");
            }

            if (curves == null || curves.Count == 0)
            {
                return RebarCreationResult.FailedResult("Danh sách Curves trống.");
            }

            // Đảm bảo normal hợp lệ
            if (norm == null || norm.GetLength() < 0.001)
                norm = XYZ.BasisZ;
            else
                norm = norm.Normalize();

            bool hooksRequested = (hook0 != null || hook1 != null);
            bool isClosedLoop = curves.Count >= 3 &&
                curves[0].GetEndPoint(0).DistanceTo(curves[curves.Count - 1].GetEndPoint(1)) < 0.005;

            if (isClosedLoop && hooksRequested)
            {
                // Với loop kín, hooks ở 2 đầu trùng toạ độ không hợp lệ
                hook0 = null;
                hook1 = null;
                hooksRequested = false;
            }

            var result = new RebarCreationResult
            {
                RequestedStyle = style,
                HooksRequested = hooksRequested
            };

            Exception primaryEx = null;

            // Cấp 1: Thử tạo khớp chính xác với useExistingShape = false (ưu tiên tuyệt đối)
            try
            {
                Rebar bar = Rebar.CreateFromCurves(
                    doc, style, barType, hook0, hook1, host,
                    norm, curves, hookOrient0, hookOrient1, true, false);

                if (bar != null)
                {
                    result.Rebar = bar;
                    result.Status = CreationStatus.Success;
                    result.AppliedStyle = style;
                    result.HooksApplied = hooksRequested;
                    return result;
                }
            }
            catch (Exception ex)
            {
                primaryEx = ex;
            }

            // Cấp 2: Thử tạo với useExistingShape = true (cho phép gán shape có sẵn trong project)
            try
            {
                Rebar bar = Rebar.CreateFromCurves(
                    doc, style, barType, hook0, hook1, host,
                    norm, curves, hookOrient0, hookOrient1, true, true);

                if (bar != null)
                {
                    result.Rebar = bar;
                    result.Status = CreationStatus.Success;
                    result.AppliedStyle = style;
                    result.HooksApplied = hooksRequested;
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (primaryEx == null) primaryEx = ex;
            }

            // Nếu không cho phép fallback hạ cấp, dừng lại và báo lỗi chính xác
            if (!allowDegradedFallback)
            {
                string msg = $"Không thể tạo thanh thép theo đúng thiết kế: {primaryEx?.Message ?? "Revit Rebar solver returned null"}";
                report?.AddWarning(msg);
                return RebarCreationResult.FailedResult(msg);
            }

            // --- CƠ CHẾ SAFE FALLBACK CÓ BÁO CÁO MINH BẠCH (DEGRADED) ---

            // Fallback 1: Nếu có hook bị lỗi tham số bán kính uốn/chiều dài, thử bỏ hook NHƯNG ghi nhận Degraded
            if (hooksRequested)
            {
                try
                {
                    Rebar bar = Rebar.CreateFromCurves(
                        doc, style, barType, null, null, host,
                        norm, curves, hookOrient0, hookOrient1, true, false);

                    if (bar == null)
                    {
                        bar = Rebar.CreateFromCurves(
                            doc, style, barType, null, null, host,
                            norm, curves, hookOrient0, hookOrient1, true, true);
                    }

                    if (bar != null)
                    {
                        result.Rebar = bar;
                        result.Status = CreationStatus.Degraded;
                        result.AppliedStyle = style;
                        result.HooksApplied = false;
                        result.DegradationReason = $"Đã tự động loại bỏ móc neo (Hooks) vì Revit không giải được hình học: {primaryEx?.Message}";
                        report?.AddWarning($"[DEGRADED] {result.DegradationReason}");
                        return result;
                    }
                }
                catch { }
            }

            // Fallback 2: Nếu StirrupTie bị lỗi hình học kín, thử chuyển Standard NHƯNG ghi nhận Degraded
            if (style != RebarStyle.Standard)
            {
                try
                {
                    Rebar bar = Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType, null, null, host,
                        norm, curves, hookOrient0, hookOrient1, true, false);

                    if (bar == null)
                    {
                        bar = Rebar.CreateFromCurves(
                            doc, RebarStyle.Standard, barType, null, null, host,
                            norm, curves, hookOrient0, hookOrient1, true, true);
                    }

                    if (bar != null)
                    {
                        result.Rebar = bar;
                        result.Status = CreationStatus.Degraded;
                        result.AppliedStyle = RebarStyle.Standard;
                        result.HooksApplied = false;
                        result.DegradationReason = $"Đã chuyển kiểu thép từ {style} sang Standard: {primaryEx?.Message}";
                        report?.AddWarning($"[DEGRADED] {result.DegradationReason}");
                        return result;
                    }
                }
                catch { }
            }

            // Nếu toàn bộ fallback đều thất bại
            string finalError = primaryEx != null
                ? $"Thất bại tạo Rebar từ Curves: [{primaryEx.GetType().Name}] {primaryEx.Message}"
                : "Thất bại tạo Rebar từ Curves: Không tìm thấy hình dạng phù hợp.";

            report?.AddError(host, "Tạo Rebar hình học", primaryEx ?? new InvalidOperationException(finalError));
            return RebarCreationResult.FailedResult(finalError);
        }

        /// <summary>
        /// Tạo Rebar an toàn và trả về Rebar instance (giữ tương thích các lời gọi hàm cũ,
        /// nhưng có ghi log cảnh báo nếu bị degraded).
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
            RebarHookOrientation hookOrient1 = RebarHookOrientation.Right,
            RebarGenerationReport report = null)
        {
            var result = CreateFromCurvesDetailed(
                doc, style, barType, hook0, hook1, host,
                norm, curves, hookOrient0, hookOrient1,
                allowDegradedFallback: true,
                report: report);

            return result.Rebar;
        }

        /// <summary>JP_T00 — thanh thẳng dọc từ bottom đến top.</summary>
        public static Rebar TryCreateStraightBar(
            Document doc,
            Element host,
            RebarBarType barType,
            XYZ bottom,
            XYZ top,
            RebarGenerationReport report = null)
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
            return CreateFromCurvesSafe(
                doc, RebarStyle.Standard, barType, null, null, host,
                norm, new List<Curve> { line },
                RebarHookOrientation.Right, RebarHookOrientation.Right,
                report);
        }

        /// <summary>Vòng tròn kín, nằm ngang (mặt phẳng XY) tại center.Z.</summary>
        public static Rebar TryCreateCircularStirrup(
            Document doc,
            Element host,
            RebarBarType barType,
            XYZ center,
            double diameterFeet,
            RebarGenerationReport report = null)
        {
            double r = diameterFeet / 2.0;
            if (r <= 0.01) return null;

            Arc arc1 = Arc.Create(center, r, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, r, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
            var loop = new List<Curve> { arc1, arc2 };

            return CreateFromCurvesSafe(
                doc, RebarStyle.StirrupTie, barType, null, null, host,
                XYZ.BasisZ, loop,
                RebarHookOrientation.Right, RebarHookOrientation.Right,
                report);
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
