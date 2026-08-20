using System;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    public static class FoundationRebarTests
    {
        public static bool RunAllTests(out string report)
        {
            var sb = new System.Text.StringBuilder();
            bool allPassed = true;

            sb.AppendLine("=== BẮT ĐẦU CHẠY UNIT TEST FOR FOUNDATION REBAR ENGINE ===");

            // TEST 1: TCVN 5574:2018 - Anchorage & Lap length for Footing Dowels
            try
            {
                sb.AppendLine("\n[TEST 1] TCVN 5574:2018 - Chiều dài neo Ld & Thép chờ cột (CB400-V, B25, Phi 18mm)...");

                double barDiaMm = 18.0;
                double ld = RebarAnchorageCalculator.CalculateAnchorageLength(
                    barDiaMm, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);

                double lap = RebarAnchorageCalculator.CalculateLapLength(
                    barDiaMm, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);

                bool checkLd = ld > 300.0 && ld < 800.0;
                bool checkLap = lap >= ld;

                sb.AppendLine($"  - Ld (TCVN B25 / CB400-V / d18): {ld:F1}mm -> {(checkLd ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - L0 Lap (TCVN B25 / CB400-V / d18): {lap:F1}mm -> {(checkLap ? "PASS" : "FAIL")}");

                if (checkLd && checkLap) sb.AppendLine("=> TEST 1: PASS!");
                else { sb.AppendLine("=> TEST 1: FAIL!"); allPassed = false; }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 1: EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            // TEST 2: Eurocode 2 / Eurocode 7 - Anchorage & Mandrel Diameter
            try
            {
                sb.AppendLine("\n[TEST 2] Eurocode 2/7 - Chiều dài neo Ld & Mandrel (B500, C25/30, Phi 18mm)...");

                double barDiaMm = 18.0;
                var ec2 = new EurocodeRebarStandard();

                double mandrel = ec2.GetMinMandrelDiameter(barDiaMm);
                double straightAfterBend = ec2.GetMinStraightLengthAfterBend(barDiaMm);

                double ld = RebarAnchorageCalculator.CalculateAnchorageLength(
                    barDiaMm, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2);

                bool checkMandrel = Math.Abs(mandrel - 7.0 * 18.0) < 1e-4; // 126mm
                bool checkStraight = Math.Abs(straightAfterBend - 5.0 * 18.0) < 1e-4; // 90mm
                bool checkLd = ld > 300.0 && ld < 900.0;

                sb.AppendLine($"  - Mandrel Diameter (EC2 Phi 18mm): {mandrel:F1}mm (Kỳ vọng: 126.0mm) -> {(checkMandrel ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - Straight After Bend: {straightAfterBend:F1}mm (Kỳ vọng: 90.0mm) -> {(checkStraight ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - Ld (EC2 C25/30 / B500 / d18): {ld:F1}mm -> {(checkLd ? "PASS" : "FAIL")}");

                if (checkMandrel && checkStraight && checkLd) sb.AppendLine("=> TEST 2: PASS!");
                else { sb.AppendLine("=> TEST 2: FAIL!"); allPassed = false; }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 2: EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            sb.AppendLine("\n========================================================");
            sb.AppendLine($"KẾT QUẢ CHUNG: {(allPassed ? "TẤT CẢ TEST CASE ĐÃ PASS (100%)" : "CÓ TEST CASE THẤT BẠI!")}");

            report = sb.ToString();
            return allPassed;
        }
    }
}
