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
    /// Bộ quy chuẩn và tính toán uốn xiên cổ chai (Crank Engine - Section 43 & Eurocode 2 / ACI 318):
    /// Độ dốc tối đa 1:6, giới hạn độ lệch <= 75mm. Nếu > 75mm bắt buộc dùng thép chờ rời (Separate Starter Dowels).
    /// </summary>
    public static class CrankRuleEngine
    {
        public const double MaxCrankOffsetMm = 75.0; // Eurocode 2 / ACI 318 threshold
        public const double MaxCrankSlope = 6.0;     // 1:6 slope (height >= 6 * offset)

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
