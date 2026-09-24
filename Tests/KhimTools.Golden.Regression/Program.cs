using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KhimTools.Domain.Models.Architectural;
using KhimTools.Domain.Models.Mep;
using KhimTools.Domain.Models.Qs;
using KhimTools.RebarTool.Core;

static class Program
{
    private static int _checks;

    private sealed class SemanticBar
    {
        public string Type { get; set; }
        public string Host { get; set; }
        public string Orientation { get; set; }
        public int Count { get; set; }
        public double DiameterMm { get; set; }
        public double SpacingMm { get; set; }
        public double CoverMm { get; set; }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Golden regression failed: " + label);
        _checks++;
    }

    private static bool Near(double expected, double actual, double tolerance) =>
        double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance;

    private static void Main()
    {
        RunDomainGoldens();
        RunRebarCalculationGoldens();
        RunComparatorContract();
        RunEdgeCases();
        RunSyntheticStress();
        Console.WriteLine($"GOLDEN_REGRESSION_ACCEPTANCE=PASS ({_checks} assertions)");
        Console.WriteLine("REBAR_DOMAIN_GOLDEN_ACCEPTANCE=PASS (production anchorage/lap calculator)");
        Console.WriteLine("EDGE_CASE_ACCEPTANCE=PASS (QS/MEP/Architectural domain calculations)");
        Console.WriteLine("REBAR_DOMAIN_EDGE_ACCEPTANCE=PASS (anchorage/lap calculator validation)");
        Console.WriteLine("REBAR_GEOMETRY_EDGE_CASE_STATUS=HOST_REQUIRED / NOT_EXECUTED");
        Console.WriteLine("PERFORMANCE_ACCEPTANCE=PASS (pure planning/calculation only)");
        Console.WriteLine("STRESS_ACCEPTANCE=PASS (100, 1,000, 10,000 synthetic records)");
        Console.WriteLine("REBAR_HOST_GEOMETRY_GOLDEN=HOST_REQUIRED / NOT_EXECUTED");
    }

    private static void RunRebarCalculationGoldens()
    {
        double tcvnAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double tcvnLap = RebarAnchorageCalculator.CalculateLapLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double eurocodeAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2);
        double eurocodeHooked = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionHooked, DesignCode.Eurocode2);

        Check(Near(600, tcvnAnchorage, 0.000001), "production Rebar TCVN B25/CB400V d18 anchorage golden");
        Check(Near(900, tcvnLap, 0.000001), "production Rebar TCVN B25/CB400V d18 lap golden");
        Check(Near(726.4640343386, eurocodeAnchorage, 0.000001), "production Rebar Eurocode C25/30 B500 d18 anchorage golden");
        Check(Near(508.52482403702, eurocodeHooked, 0.000001), "production Rebar Eurocode C25/30 B500 d18 hooked anchorage golden");
        Check(Near(630, RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.Auto, SteelGrade.Auto, AnchorageType.TensionStraight, DesignCode.Eurocode2), 0.000001),
            "production Rebar automatic-grade fallback golden");
    }

    private static void RunDomainGoldens()
    {
        // QS quantity calculations. Units are carried by the fixture label; the shared math is unit-agnostic.
        Check(QsQuantityMath.TryCalculatePayQuantity(12.345, 5, 2, out double concrete) && Near(12.96, concrete, 0.000001), "QS concrete volume with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(10.5, 2, 1, out double finishArea) && Near(10.7, finishArea, 0.000001), "QS finish area with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(0, 0, 2, out double count) && Near(0, count, 0), "QS zero count");
        Check(QsQuantityMath.TryCalculatePayQuantity(54.32, 0, 3, out double length) && Near(54.32, length, 0), "QS lineal length");
        Check(QsQuantityMath.TryCalculatePayQuantity(2.5, 10, 2, out double material) && Near(2.75, material, 0.000001), "QS material quantity with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(1.005, 0, 2, out double midpoint) && Near(1.01, midpoint, 0.000001), "QS deterministic midpoint rounding");

        var pipe = MepMeasurementCalculator.RoundOpening(100, 25);
        Check(Near(150, pipe.WidthMm, 0) && Near(150, pipe.HeightMm, 0), "MEP circular service clearance");
        var duct = MepMeasurementCalculator.RectangularOpening(600, 300, 25);
        Check(Near(650, duct.WidthMm, 0) && Near(350, duct.HeightMm, 0), "MEP rectangular service clearance");
        var elevations = MepMeasurementCalculator.VerticalRange(5000, 600);
        Check(Near(4700, elevations.BottomMm, 0) && Near(5300, elevations.TopMm, 0), "MEP bottom/top elevation range");

        var horizontal = LintelLayout.Compute(1000, 2000, 3000, 900, 200, 1, 0);
        Check(Near(350, horizontal.StartXmm, 0) && Near(1650, horizontal.EndXmm, 0), "Architectural lintel horizontal semantic geometry");
        Check(Near(2000, horizontal.StartYmm, 0) && Near(3000, horizontal.StartZmm, 0), "Architectural lintel elevation");
        var diagonal = LintelLayout.Compute(0, 0, 0, 1000, 0, 3, 4);
        Check(Near(-300, diagonal.StartXmm, 0) && Near(-400, diagonal.StartYmm, 0) &&
              Near(300, diagonal.EndXmm, 0) && Near(400, diagonal.EndYmm, 0), "Architectural normalized diagonal orientation");
    }

    private static void RunComparatorContract()
    {
        Check(10.0 == 10.0, "exact comparator");
        Check(Near(10.0, 10.0005, 0.001), "tolerance comparator");
        Check(new[] { "A", "B", "A" }.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(new[] { "A", "A", "B" }.OrderBy(x => x, StringComparer.Ordinal)), "unordered collection comparator preserves multiplicity");
        Check(123.5 >= 120 && 123.5 <= 125, "range comparator");

        var expected = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16, SpacingMm = 0, CoverMm = 40 };
        var actual = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16.0002, SpacingMm = 0, CoverMm = 40.0001 };
        Check(SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator accepts numeric tolerance and ignores volatile ids");
        actual.Host = "BEAM";
        Check(!SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator rejects wrong host relationship");
    }

    private static bool SemanticallyEquivalent(SemanticBar a, SemanticBar b, double tolerance) =>
        a != null && b != null && a.Type == b.Type && a.Host == b.Host && a.Orientation == b.Orientation &&
        a.Count == b.Count && Near(a.DiameterMm, b.DiameterMm, tolerance) &&
        Near(a.SpacingMm, b.SpacingMm, tolerance) && Near(a.CoverMm, b.CoverMm, tolerance);

    private static void RunEdgeCases()
    {
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(0, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects zero diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(-16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects negative diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.NaN, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects NaN diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.MaxValue, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects overflowing extreme diameter result");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, double.NaN)), "Rebar rejects non-finite fallback multiplier");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, (DesignCode)999)), "Rebar rejects unknown design code");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateLapLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, percentLappedFactor: 0)), "Rebar rejects zero lap factor");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.NaN, 0, 2, out _), "QS rejects NaN input");
        Check(!QsQuantityMath.TryCalculatePayQuantity(-1, 0, 2, out _), "QS rejects negative quantity");
        Check(!QsQuantityMath.TryCalculatePayQuantity(1, -1, 2, out _), "QS rejects negative waste");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.MaxValue, double.MaxValue, 2, out _), "QS rejects overflow");
        Check(!QsQuantityMath.TryParseQuantity("1,2,3.5", false, out _), "QS rejects malformed unit quantity");
        Check(!QsQuantityMath.TryParseQuantity("NaN", false, out _), "QS rejects non-finite parsed quantity");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RoundOpening(0, 25)), "MEP rejects zero diameter");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RectangularOpening(double.PositiveInfinity, 300, 25)), "MEP rejects extreme non-finite size");
        Check(ThrowsArgument(() => MepMeasurementCalculator.VerticalRange(1000, 0)), "MEP rejects zero section height");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 0, 0, 1, 0)), "Architectural rejects zero lintel width");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 1, -1, 1, 0)), "Architectural rejects negative extension");
        Check(ThrowsArgument(() => LintelLayout.Compute(double.NaN, 0, 0, 1, 0, 1, 0)), "Architectural rejects non-finite input");
    }

    private static bool ThrowsOutOfRange(Action action)
    {
        try { action(); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }

    private static bool ThrowsArgument(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void RunSyntheticStress()
    {
        foreach (int recordCount in new[] { 100, 1000, 10000 })
        {
            var timer = Stopwatch.StartNew();
            double qsTotal = 0;
            double mepTotal = 0;
            double geometryChecksum = 0;
            for (int i = 1; i <= recordCount; i++)
            {
                if (!QsQuantityMath.TryCalculatePayQuantity(i / 10.0, 5, 2, out double pay))
                    throw new InvalidOperationException("QS stress input unexpectedly rejected.");
                qsTotal += pay;
                var measure = MepMeasurementCalculator.RectangularOpening(100 + i, 50 + (i % 300), 25);
                mepTotal += measure.WidthMm + measure.HeightMm;
                var layout = LintelLayout.Compute(i, i / 2.0, 3000, 1000, 100, 1, 0);
                geometryChecksum += layout.StartXmm + layout.EndXmm;
            }
            timer.Stop();
            Check(recordCount > 0 && qsTotal > 0 && mepTotal > 0 && geometryChecksum > 0, $"synthetic stress outputs are finite/non-empty for {recordCount} records");
            Check(timer.Elapsed < TimeSpan.FromSeconds(15), $"synthetic stress completes within 15 seconds for {recordCount} records");
            Console.WriteLine($"STRESS_RECORDS={recordCount}; ELAPSED_MS={timer.ElapsedMilliseconds}; QS_SUM={qsTotal:F2}; MEP_SUM={mepTotal:F2}");
        }
    }
}
