using System;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public class CrankEvaluationResult
    {
        public bool CanCrank { get; set; } = true;
        public bool RequiresSeparateDowels { get; set; } = false;
        public double OffsetMm { get; set; }
        public double CrankSlope { get; set; } = 6.0; // 1:6
        public double RequiredCrankHeightMm { get; set; }
        public double AvailableHeightMm { get; set; }
        public double BendRadiusMm { get; set; }
        public string DiagnosticMessage { get; set; } = "";
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// Bộ quy chuẩn và tính toán uốn xiên cổ chai (Crank Engine - Section 43):
    /// - PHÂN LOẠI QUY CHUẨN:
    ///   1. Độ dốc tối đa 1:6: Quy chuẩn cấu tạo dự án / Thực hành kỹ thuật kết cấu (ACI 318-19 §25.7.1.4 / BS 8666 / IStructE Manual).
    ///      (Lưu ý: EN 1992-1-1 không quy định trực tiếp tỷ lệ 1:6 mà quản lý qua đường kính uốn Cl. 8.3 và lực kéo tách ngang Cl. 8.4.1/8.7.4.1).
    ///   2. Giới hạn độ lệch offset <= 75mm: Quy chuẩn cấu tạo dự án (ACI 318-19 §25.7.1.3 / IStructE Manual). Nếu > 75mm bắt buộc dùng thép chờ rời (Separate Starter Dowels).
    ///   3. Bán kính uốn tại điểm bẻ: Tuân thủ nghiêm ngặt Eurocode 2 EN 1992-1-1 Cl. 8.3 Table 8.1N (Mandrel diameter 4d hoặc 7d).
    /// </summary>
    public static class CrankRuleEngine
    {
        /// <summary>Giới hạn độ lệch offset tối đa cho phép uốn xiên (75mm theo ACI 318-19 §25.7.1.3 / IStructE Detailing Practice)</summary>
        public const double MaxCrankOffsetMm = 75.0; // Project Detailing Practice Rule
        /// <summary>Độ dốc uốn xiên tối đa (1:6 theo ACI 318-19 §25.7.1.4 / BS 8666 / IStructE Detailing Practice)</summary>
        public const double MaxCrankSlope = 6.0;     // 1:6 slope (height >= 6 * offset) - Project Detailing Practice Rule

        /// <summary>
        /// Thẩm định khả năng uốn xiên cổ chai theo hình học tiết diện và tiêu chuẩn
        /// </summary>
        public static CrankEvaluationResult EvaluateCrank(
            double offsetMm,
            double barDiameterMm,
            double availableHeightMm,
            double nominalCoverMm = 30.0)
        {
            var res = new CrankEvaluationResult
            {
                OffsetMm = Math.Abs(offsetMm),
                AvailableHeightMm = availableHeightMm
            };

            // 1. Nếu độ lệch không đáng kể (<= 3mm): Thanh chạy thẳng
            if (res.OffsetMm <= 3.0)
            {
                res.CanCrank = false;
                res.RequiresSeparateDowels = false;
                res.RequiredCrankHeightMm = 0;
                res.DiagnosticMessage = "Coaxial alignment: Straight bar continuation without crank.";
                return res;
            }

            // 2. Nếu độ lệch vượt quá ngưỡng 75mm: Cấm uốn xiên, bắt buộc dùng thép chờ rời
            if (res.OffsetMm > MaxCrankOffsetMm)
            {
                res.CanCrank = false;
                res.RequiresSeparateDowels = true;
                res.IsValid = false;
                res.DiagnosticMessage = $"Offset {res.OffsetMm:F1}mm exceeds maximum crank limit ({MaxCrankOffsetMm}mm). Separate starter dowels strictly required (NEED DESIGN INPUT).";
                return res;
            }

            // 3. Tính toán chiều cao đoạn uốn xiên theo độ dốc 1:6
            double totalInward = res.OffsetMm + barDiameterMm;
            res.RequiredCrankHeightMm = totalInward * MaxCrankSlope;
            res.CrankSlope = MaxCrankSlope;

            // Bán kính uốn tối thiểu theo Eurocode 2 (Table 8.1N)
            res.BendRadiusMm = (barDiameterMm <= 16.0) ? (2.0 * barDiameterMm) : (3.5 * barDiameterMm);

            // 4. Kiểm tra chiều cao khả dụng trong vùng nối / nút dầm sàn
            if (availableHeightMm > 0 && res.RequiredCrankHeightMm > availableHeightMm)
            {
                res.CanCrank = false;
                res.RequiresSeparateDowels = true;
                res.IsValid = false;
                res.DiagnosticMessage = $"Insufficient height for 1:6 crank slope: Required {res.RequiredCrankHeightMm:F0}mm > Available {availableHeightMm:F0}mm. Separate starter dowels required.";
                return res;
            }

            res.CanCrank = true;
            res.RequiresSeparateDowels = false;
            res.DiagnosticMessage = $"Standard 1:6 crank permitted (Required Height = {res.RequiredCrankHeightMm:F0}mm for offset = {res.OffsetMm:F1}mm).";
            return res;
        }
    }
}
