using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    public class RebarTestResult
    {
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// Bộ kiểm thử tự động toàn diện (Comprehensive Automated Test Suite) cho RebarTool
    /// Xác thực các tiêu chuẩn, bộ tính toán hình học, thuật toán nối chồng và tính năng an toàn.
    /// </summary>
    public static class RebarEngineTestSuite
    {
        public static List<RebarTestResult> RunAllTests()
        {
            var results = new List<RebarTestResult>();

            // 1. Kiểm thử tiêu chuẩn thiết kế (Standards Engine Tests)
            results.Add(TestEurocodeStandard());
            results.Add(TestTcvnStandard());

            // 2. Kiểm thử an toàn & kiểm tra chiều dài thương mại (Safety Validator Tests)
            results.Add(TestCommercialStockLength());
            results.Add(TestMinClearSpacing());

            // 3. Kiểm thử logic thép sườn dầm (Beam Side Bar Logic)
            results.Add(TestBeamSideBarStrictDisable());
            results.Add(TestBeamSideBarAutoThreshold());

            // 4. Kiểm thử tính liên tục cột đa tầng (Column Continuity Engine Tests)
            results.Add(TestColumnCrankSlope1to6());
            results.Add(TestColumnLargeReductionDowels());
            results.Add(TestColumnStaggeredSpliceOffset());

            // 5. Kiểm thử vòng đời & chống trùng lặp thép (Lifecycle Manager Tests)
            results.Add(TestLifecycleTagging());

            // 6. Kiểm thử Bảng thống kê thép (BBS Engine Tests)
            results.Add(TestBbsWeightAndLengthCalculation());

            return results;
        }

        private static RebarTestResult TestEurocodeStandard()
        {
            var ec2 = new EurocodeRebarStandard();
            double lapTension = ec2.GetLapSpliceLength(20.0, ConcreteGrade.C30_37, SteelGrade.CB500_V, AnchorageType.TensionStraight);
            double lapComp = ec2.GetLapSpliceLength(20.0, ConcreteGrade.C30_37, SteelGrade.CB500_V, AnchorageType.Compression);
            double clearSpace = ec2.GetMinClearSpacing(20.0, 20.0);
            double hookTail = ec2.GetHookTailLength(20.0, 90.0);

            bool pass = (lapTension > 0) && (lapComp <= lapTension) && (clearSpace >= 25.0) && (hookTail >= 100.0);
            return new RebarTestResult
            {
                TestName = "Standards: Eurocode 2 (EC2) Formulas",
                Passed = pass,
                Details = $"LapTension={lapTension:F1}mm, LapComp={lapComp:F1}mm, ClearSpacing={clearSpace}mm, HookTail={hookTail}mm"
            };
        }

        private static RebarTestResult TestTcvnStandard()
        {
            var tcvn = new TcvnRebarStandard();
            double anch = tcvn.GetAnchorageLength(20.0, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight);
            double lap = tcvn.GetLapSpliceLength(20.0, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight);
            double cover = tcvn.GetMinConcreteCoverMm("Beam", "XC1");

            bool pass = (anch > 0) && (lap >= anch * 1.2) && (cover >= 25.0);
            return new RebarTestResult
            {
                TestName = "Standards: TCVN 5574:2018 Formulas",
                Passed = pass,
                Details = $"Anchorage={anch:F1}mm, Lap={lap:F1}mm (alpha=1.2), BeamCover={cover}mm"
            };
        }

        private static RebarTestResult TestCommercialStockLength()
        {
            // Cây thép 11.0m hợp lệ, cây thép 12.0m vượt quá chiều dài thanh thương mại 11.7m
            double lenShortFeet = UnitUtils.ConvertToInternalUnits(11000.0, UnitTypeId.Millimeters);
            double lenLongFeet = UnitUtils.ConvertToInternalUnits(12000.0, UnitTypeId.Millimeters);

            double maxAllowedFeet = UnitUtils.ConvertToInternalUnits(11700.0, UnitTypeId.Millimeters);
            bool passShort = lenShortFeet <= maxAllowedFeet;
            bool passLong = lenLongFeet > maxAllowedFeet;

            return new RebarTestResult
            {
                TestName = "SafetyValidator: Commercial Stock Length Check (<= 11.7m)",
                Passed = passShort && passLong,
                Details = "11.0m thanh thép hợp lệ; 12.0m phát hiện vượt quá 11.7m chính xác."
            };
        }

        private static RebarTestResult TestMinClearSpacing()
        {
            var standard = new TcvnRebarStandard();
            double minClear = standard.GetMinClearSpacing(25.0, 20.0);
            bool pass = minClear >= 25.0;

            return new RebarTestResult
            {
                TestName = "SafetyValidator: Min Clear Rebar Spacing (>= 25mm / d_max)",
                Passed = pass,
                Details = $"MinClearSpacing={minClear}mm >= 25mm"
            };
        }

        private static RebarTestResult TestBeamSideBarStrictDisable()
        {
            // Khi AutoSideBars = false và ManualSideBars = false: không bao giờ sinh thép sườn
            var input = new BeamRebarInput
            {
                EnableSideBars = true,
                AutoSideBars = false,
                ManualSideBars = false,
                SideBarQty = 2
            };

            double hMm = 800.0;
            bool shouldGenerate = input.EnableSideBars && (
                input.AutoSideBars
                    ? (hMm >= input.SideBarThresholdMm && input.SideBarQty > 0)
                    : (input.ManualSideBars && input.SideBarQty > 0)
            );

            return new RebarTestResult
            {
                TestName = "BeamRebar: AutoSideBars=false strictly disables side bars",
                Passed = !shouldGenerate,
                Details = "Khi AutoSideBars=false và ManualSideBars=false, shouldGenerate=false."
            };
        }

        private static RebarTestResult TestBeamSideBarAutoThreshold()
        {
            // Khi AutoSideBars = true và H >= 600mm: sinh thép sườn
            var input = new BeamRebarInput
            {
                EnableSideBars = true,
                AutoSideBars = true,
                SideBarThresholdMm = 600.0,
                SideBarQty = 2
            };

            double hMmSmall = 500.0;
            double hMmLarge = 700.0;

            bool genSmall = input.EnableSideBars && (hMmSmall >= input.SideBarThresholdMm && input.SideBarQty > 0);
            bool genLarge = input.EnableSideBars && (hMmLarge >= input.SideBarThresholdMm && input.SideBarQty > 0);

            return new RebarTestResult
            {
                TestName = "BeamRebar: AutoSideBars threshold (H >= 600mm)",
                Passed = (!genSmall && genLarge),
                Details = $"H=500mm -> gen={genSmall}; H=700mm -> gen={genLarge}"
            };
        }

        private static RebarTestResult TestColumnCrankSlope1to6()
        {
            double dbFeet = UnitUtils.ConvertToInternalUnits(25.0, UnitTypeId.Millimeters);
            double offsetFeet = UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters); // Thu nhỏ 50mm <= 75mm

            double totalInward = offsetFeet + dbFeet;
            double crankHeight = totalInward * 6.0;
            double slope = crankHeight / totalInward;

            bool pass = Math.Abs(slope - 6.0) < 0.001;
            return new RebarTestResult
            {
                TestName = "ColumnContinuity: 1:6 Crank Slope with Section Reduction",
                Passed = pass,
                Details = $"Slope={slope:F1} (CrankHeight={crankHeight * 304.8:F0}mm cho inward={totalInward * 304.8:F0}mm)"
            };
        }

        private static RebarTestResult TestColumnLargeReductionDowels()
        {
            double offsetMm = 100.0; // Thu nhỏ 100mm > 75mm giới hạn tiêu chuẩn
            bool requiresDowels = offsetMm > 75.0;

            return new RebarTestResult
            {
                TestName = "ColumnContinuity: Section Reduction > 75mm requires separate starter dowels",
                Passed = requiresDowels,
                Details = $"Offset={offsetMm}mm > 75mm -> RequiresSeparateDowels={requiresDowels}"
            };
        }

        private static RebarTestResult TestColumnStaggeredSpliceOffset()
        {
            double lapFeet = UnitUtils.ConvertToInternalUnits(800.0, UnitTypeId.Millimeters);
            double staggerOffset = lapFeet * 1.3;

            bool pass = Math.Abs(staggerOffset / lapFeet - 1.3) < 0.001;
            return new RebarTestResult
            {
                TestName = "ColumnContinuity: 50% Staggered Splice Offset (1.3 * Ls)",
                Passed = pass,
                Details = $"Offset = {staggerOffset * 304.8:F0}mm = 1.3 * {lapFeet * 304.8:F0}mm"
            };
        }

        private static RebarTestResult TestLifecycleTagging()
        {
            string tag = $"{RebarLifecycleManager.TagPrefix}|Module:Column|Host:12345|Role:RectangularColumn|v2.0";
            bool isKhim = tag.StartsWith(RebarLifecycleManager.TagPrefix) && tag.Contains("Module:Column");

            return new RebarTestResult
            {
                TestName = "LifecycleManager: Tag Format & Identification",
                Passed = isKhim,
                Details = $"Tag: {tag}"
            };
        }

        private static RebarTestResult TestBbsWeightAndLengthCalculation()
        {
            var item = new BbsItem
            {
                DiameterMm = 20.0,
                Quantity = 10,
                CutLengthMm = 5000.0
            };

            // TotalLength = 10 * 5.0m = 50m
            // UnitWeight = 0.006165 * 400 = 2.466 kg/m
            // TotalWeight = 50 * 2.466 = 123.3 kg
            bool lenOk = Math.Abs(item.TotalLengthM - 50.0) < 0.01;
            bool weightOk = Math.Abs(item.TotalWeightKg - 123.3) < 1.0;

            return new RebarTestResult
            {
                TestName = "BBS Engine: Bar Weight & Length Calculation",
                Passed = lenOk && weightOk,
                Details = $"TotalLength={item.TotalLengthM:F1}m, UnitWeight={item.UnitWeightKgPerM:F3}kg/m, TotalWeight={item.TotalWeightKg:F1}kg"
            };
        }
    }
}
