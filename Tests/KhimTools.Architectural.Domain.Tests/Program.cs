using KhimTools.Domain.Models.Architectural;

static void Near(double expected, double actual, string field)
{
    if (Math.Abs(expected - actual) > 1e-8) throw new InvalidOperationException($"{field}: expected {expected}, got {actual}");
}

var horizontal = LintelLayout.Compute(1000, 2000, 3000, 900, 200, 1, 0);
Near(350, horizontal.StartXmm, "horizontal start");
Near(1650, horizontal.EndXmm, "horizontal end");
Near(2000, horizontal.StartYmm, "horizontal y");
Near(3000, horizontal.StartZmm, "horizontal z");

var diagonal = LintelLayout.Compute(0, 0, 0, 1000, 0, 3, 4);
Near(-300, diagonal.StartXmm, "normalized direction x");
Near(-400, diagonal.StartYmm, "normalized direction y");
Near(300, diagonal.EndXmm, "normalized end x");
Near(400, diagonal.EndYmm, "normalized end y");

var verticalPlan = LintelLayout.Compute(0, 0, 0, 600, 50, 0, -2);
Near(350, verticalPlan.StartYmm, "negative plan direction start");
Near(-350, verticalPlan.EndYmm, "negative plan direction end");

void Reject(Action action, string label)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException($"Expected invalid input rejection: {label}");
}

Reject(() => LintelLayout.Compute(0, 0, 0, 0, 0, 1, 0), "zero width");
Reject(() => LintelLayout.Compute(0, 0, 0, 1, -1, 1, 0), "negative extension");
Reject(() => LintelLayout.Compute(0, 0, 0, 1, 0, 0, 0), "zero direction");
Reject(() => LintelLayout.Compute(double.NaN, 0, 0, 1, 0, 1, 0), "non-finite center");

Console.WriteLine("K-Architectural domain QA: PASS (14 assertions)");
