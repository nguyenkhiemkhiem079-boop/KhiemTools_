using KhimTools.Domain.Models.Mep;

static void Near(double expected, double actual, string label)
{
    if (Math.Abs(expected - actual) > 1e-8) throw new InvalidOperationException($"{label}: expected {expected}; got {actual}.");
}

void Reject(Action action, string label)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException($"Invalid MEP input accepted: {label}.");
}

var pipe = MepMeasurementCalculator.RoundOpening(100, 25);
Near(150, pipe.WidthMm, "pipe clearance width");
Near(150, pipe.HeightMm, "pipe clearance height");
var duct = MepMeasurementCalculator.RectangularOpening(600, 300, 25);
Near(650, duct.WidthMm, "duct clearance width");
Near(350, duct.HeightMm, "duct clearance height");
var tray = MepMeasurementCalculator.RectangularOpening(450, 100, 10);
Near(470, tray.WidthMm, "tray width");
Near(120, tray.HeightMm, "tray height");
var elevations = MepMeasurementCalculator.VerticalRange(5000, 600);
Near(4700, elevations.BottomMm, "BOP");
Near(5300, elevations.TopMm, "TOP");
Reject(() => MepMeasurementCalculator.RoundOpening(0, 25), "zero diameter");
Reject(() => MepMeasurementCalculator.RoundOpening(100, -1), "negative clearance");
Reject(() => MepMeasurementCalculator.RectangularOpening(double.NaN, 50, 5), "non-finite size");
Reject(() => MepMeasurementCalculator.VerticalRange(1, 0), "zero section height");

Console.WriteLine("K-MEP domain QA: PASS (12 assertions)");
