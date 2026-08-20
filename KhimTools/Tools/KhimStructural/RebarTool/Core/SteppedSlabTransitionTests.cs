using System;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Bộ Test verify cho SteppedSlabTransitionPlacer (Khớp với Phần E của Prompt).
    /// </summary>
    public static class SteppedSlabTransitionTests
    {
        public static bool RunAllTests(out string report)
        {
            var sb = new System.Text.StringBuilder();
            bool allPassed = true;

            sb.AppendLine("=== BẮT ĐẦU CHẠY UNIT TEST FOR STEPPED SLAB TRANSITION PLACER ===");

            // ── TEST CASE 1: Eurocode 2 - Bậc thấp (Sàn 150mm -> 120mm, Soffit chung, DeltaH=30mm, Phi 10mm) ──
            try
            {
                sb.AppendLine("\n[TEST 1] Eurocode 2 - Sàn giật cấp 30mm (Soffit chung)...");

                var ec2Standard = new EurocodeRebarStandard();
                var placer = new SteppedSlabTransitionPlacer(ec2Standard);

                var input = new SteppedSlabInput
                {
                    Slab1ThicknessMm = 150.0,
                    Slab2ThicknessMm = 120.0,
                    Slab1TopElevationMm = 0.0,
                    Slab2TopElevationMm = -30.0,
                    CoverTop1Mm = 15.0,
                    CoverTop2Mm = 15.0,
                    CoverBot1Mm = 15.0,
                    CoverBot2Mm = 15.0,
                    BarDiameterMm = 10.0,
                    CrankRatio = 6.0,
                    MaxHorizontalTransitionLimitMm = 600.0
                };

                var result = placer.AnalyzeTransition(input);

                // Verify 1: Mandrel tối thiểu = 4 * 10 = 40mm (Bán kính = 20mm)
                bool checkMandrel = Math.Abs(result.MandrelDiameter - 40.0) < 1e-4;
                bool checkRadius = Math.Abs(result.BendRadius - 20.0) < 1e-4;
                sb.AppendLine($"  - Mandrel Diameter: {result.MandrelDiameter}mm (Kỳ vọng: 40mm) -> {(checkMandrel ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - Bend Radius: {result.BendRadius}mm (Kỳ vọng: 20mm) -> {(checkRadius ? "PASS" : "FAIL")}");

                // Verify 2: Đoạn thẳng tối thiểu sau uốn >= 5 * 10 = 50mm
                bool checkStraight = Math.Abs(result.MinStraightLengthAfterBend - 50.0) < 1e-4;
                sb.AppendLine($"  - Min Straight Length: {result.MinStraightLengthAfterBend}mm (Kỳ vọng: 50mm) -> {(checkStraight ? "PASS" : "FAIL")}");

                // Verify 3: Khoảng chuyển tiếp L = max(30 * 6, 50) = 180mm
                bool checkL = Math.Abs(result.HorizontalTransitionLengthTop - 180.0) < 1e-4;
                sb.AppendLine($"  - Horizontal Transition Length L: {result.HorizontalTransitionLengthTop}mm (Kỳ vọng: 180mm) -> {(checkL ? "PASS" : "FAIL")}");

                // Verify 4: Phân loại LowStep
                bool checkType = result.TransitionType == SteppedTransitionType.LowStep;
                sb.AppendLine($"  - Transition Type: {result.TransitionType} (Kỳ vọng: LowStep) -> {(checkType ? "PASS" : "FAIL")}");

                // Verify 5: Lớp trên bẻ uốn, lớp dưới chạy thẳng
                bool checkLayers = result.IsTopLayerCranked && !result.IsBottomLayerCranked;
                sb.AppendLine($"  - Top Layer Cranked: {result.IsTopLayerCranked}, Bottom Layer Cranked: {result.IsBottomLayerCranked} -> {(checkLayers ? "PASS" : "FAIL")}");

                if (checkMandrel && checkRadius && checkStraight && checkL && checkType && checkLayers)
                {
                    sb.AppendLine("=> TEST 1: PASS!");
                }
                else
                {
                    sb.AppendLine("=> TEST 1: FAIL!");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 1: EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            // ── TEST CASE 2: TCVN 5574:2018 (Verified - Phi 10mm có gân) ──
            try
            {
                sb.AppendLine("\n[TEST 2] TCVN 5574:2018 - Sàn giật cấp 30mm (Cốt thép có gân Phi 10mm)...");

                var tcvnStandard = new TcvnRebarStandard();
                var placer = new SteppedSlabTransitionPlacer(tcvnStandard);

                var input = new SteppedSlabInput
                {
                    Slab1ThicknessMm = 150.0,
                    Slab2ThicknessMm = 120.0,
                    Slab1TopElevationMm = 0.0,
                    Slab2TopElevationMm = -30.0,
                    CoverTop1Mm = 15.0,
                    CoverTop2Mm = 15.0,
                    CoverBot1Mm = 15.0,
                    CoverBot2Mm = 15.0,
                    BarDiameterMm = 10.0,
                    CrankRatio = 6.0,
                    MaxHorizontalTransitionLimitMm = 600.0
                };

                var result = placer.AnalyzeTransition(input);

                // Verify 1: Mandrel TCVN = 5 * 10 = 50mm (Bán kính = 25mm)
                bool checkMandrel = Math.Abs(result.MandrelDiameter - 50.0) < 1e-4;
                bool checkRadius = Math.Abs(result.BendRadius - 25.0) < 1e-4;
                sb.AppendLine($"  - Mandrel Diameter: {result.MandrelDiameter}mm (Kỳ vọng TCVN 5574 Điều 10.3.7: 50mm) -> {(checkMandrel ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - Bend Radius: {result.BendRadius}mm (Kỳ vọng: 25mm) -> {(checkRadius ? "PASS" : "FAIL")}");

                // Verify 2: Đoạn thẳng tối thiểu sau uốn >= 5 * 10 = 50mm
                bool checkStraight = Math.Abs(result.MinStraightLengthAfterBend - 50.0) < 1e-4;
                sb.AppendLine($"  - Min Straight Length: {result.MinStraightLengthAfterBend}mm (Kỳ vọng: 50mm) -> {(checkStraight ? "PASS" : "FAIL")}");

                // Verify 3: Khoảng chuyển tiếp L = max(30 * 6, 50) = 180mm
                bool checkL = Math.Abs(result.HorizontalTransitionLengthTop - 180.0) < 1e-4;
                sb.AppendLine($"  - Horizontal Transition Length L: {result.HorizontalTransitionLengthTop}mm (Kỳ vọng: 180mm) -> {(checkL ? "PASS" : "FAIL")}");

                if (checkMandrel && checkRadius && checkStraight && checkL)
                {
                    sb.AppendLine("=> TEST 2: PASS!");
                }
                else
                {
                    sb.AppendLine("=> TEST 2: FAIL!");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 2: UNEXPECTED EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            // ── TEST CASE 3: TCVN 5574:2018 (Verified - Phi 20mm có gân -> Mandrel = 8 * ds) ──
            try
            {
                sb.AppendLine("\n[TEST 3] TCVN 5574:2018 - Phi 20mm có gân (Mandrel = 8 * ds = 160mm)...");

                var tcvnStandard = new TcvnRebarStandard();
                var placer = new SteppedSlabTransitionPlacer(tcvnStandard);

                var input = new SteppedSlabInput
                {
                    Slab1ThicknessMm = 200.0,
                    Slab2ThicknessMm = 150.0,
                    Slab1TopElevationMm = 0.0,
                    Slab2TopElevationMm = -50.0,
                    BarDiameterMm = 20.0,
                    CrankRatio = 6.0,
                    MaxHorizontalTransitionLimitMm = 600.0
                };

                var result = placer.AnalyzeTransition(input);

                // Verify 1: Mandrel TCVN cho ds >= 20mm = 8 * 20 = 160mm
                bool checkMandrel = Math.Abs(result.MandrelDiameter - 160.0) < 1e-4;
                sb.AppendLine($"  - Mandrel Diameter (Phi 20mm): {result.MandrelDiameter}mm (Kỳ vọng: 160mm) -> {(checkMandrel ? "PASS" : "FAIL")}");

                if (checkMandrel)
                {
                    sb.AppendLine("=> TEST 3: PASS!");
                }
                else
                {
                    sb.AppendLine("=> TEST 3: FAIL!");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 3: UNEXPECTED EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            // ── TEST CASE 4: Top bằng nhau, Bottom lệch (Slab1: 150mm, Slab2: 120mm, Top cùng = 0.0mm) ──
            try
            {
                sb.AppendLine("\n[TEST 4] Top Elevation bằng nhau, Bottom lệch (Sàn 150mm -> 120mm, Top = 0.0mm)...");

                var ec2Standard = new EurocodeRebarStandard();
                var placer = new SteppedSlabTransitionPlacer(ec2Standard);

                var input = new SteppedSlabInput
                {
                    Slab1ThicknessMm = 150.0,
                    Slab2ThicknessMm = 120.0,
                    Slab1TopElevationMm = 0.0,
                    Slab2TopElevationMm = 0.0,
                    CoverTop1Mm = 15.0,
                    CoverTop2Mm = 15.0,
                    CoverBot1Mm = 15.0,
                    CoverBot2Mm = 15.0,
                    BarDiameterMm = 10.0,
                    CrankRatio = 6.0,
                    MaxHorizontalTransitionLimitMm = 600.0
                };

                var result = placer.AnalyzeTransition(input);

                // Verify 1: DeltaHTop = 0, DeltaHBot = 30mm
                bool checkDelta = Math.Abs(result.DeltaHTop - 0.0) < 1e-4 && Math.Abs(result.DeltaHBot - 30.0) < 1e-4;
                sb.AppendLine($"  - DeltaHTop: {result.DeltaHTop}mm (Kỳ vọng: 0mm), DeltaHBot: {result.DeltaHBot}mm (Kỳ vọng: 30mm) -> {(checkDelta ? "PASS" : "FAIL")}");

                // Verify 2: Top không crank, Bottom crank
                bool checkLayers = !result.IsTopLayerCranked && result.IsBottomLayerCranked;
                sb.AppendLine($"  - Top Layer Cranked: {result.IsTopLayerCranked} (Kỳ vọng: False), Bottom Layer Cranked: {result.IsBottomLayerCranked} (Kỳ vọng: True) -> {(checkLayers ? "PASS" : "FAIL")}");

                // Verify 3: TopLayerCrankPoints.Count == 2 (chạy thẳng fallback), BottomLayerCrankPoints.Count == 4 (uốn bẻ)
                bool checkTopCount = result.TopLayerCrankPoints.Count == 2;
                bool checkBotCount = result.BottomLayerCrankPoints.Count == 4;
                sb.AppendLine($"  - TopLayerCrankPoints Count: {result.TopLayerCrankPoints.Count} (Kỳ vọng: 2) -> {(checkTopCount ? "PASS" : "FAIL")}");
                sb.AppendLine($"  - BottomLayerCrankPoints Count: {result.BottomLayerCrankPoints.Count} (Kỳ vọng: 4) -> {(checkBotCount ? "PASS" : "FAIL")}");

                if (checkDelta && checkLayers && checkTopCount && checkBotCount)
                {
                    sb.AppendLine("=> TEST 4: PASS!");
                }
                else
                {
                    sb.AppendLine("=> TEST 4: FAIL!");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"=> TEST 4: UNEXPECTED EXCEPTION: {ex.Message}");
                allPassed = false;
            }

            sb.AppendLine("\n========================================================");
            sb.AppendLine($"KẾT QUẢ CHUNG: {(allPassed ? "TẤT CẢ TEST CASE ĐÃ PASS (100%)" : "CÓ TEST CASE THẤT BẠI!")}");

            report = sb.ToString();
            return allPassed;
        }
    }
}
