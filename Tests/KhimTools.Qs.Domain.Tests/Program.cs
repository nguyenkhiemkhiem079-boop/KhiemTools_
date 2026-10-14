using KhimTools.Domain.Models.Qs;

var checks = 0;
void Near(double expected, double actual, string label)
{
    if (Math.Abs(expected - actual) > 1e-8) throw new InvalidOperationException($"{label}: expected {expected}; got {actual}.");
    checks++;
}
void Reject(double raw, double waste, int digits, string label)
{
    if (QsQuantityMath.TryCalculatePayQuantity(raw, waste, digits, out _))
        throw new InvalidOperationException($"Invalid QS quantity accepted: {label}.");
    checks++;
}
void Parse(double expected, string text, bool decimalComma, string label)
{
    if (!QsQuantityMath.TryParseQuantity(text, decimalComma, out var value))
        throw new InvalidOperationException($"{label}: valid quantity text rejected.");
    Near(expected, value, label);
}

if (!QsQuantityMath.TryCalculatePayQuantity(12.345, 5, 2, out var pay))
    throw new InvalidOperationException("Valid QS quantity rejected.");
Near(12.96, pay, "waste and rounding");
if (!QsQuantityMath.TryCalculatePayQuantity(1.005, 0, 2, out pay))
    throw new InvalidOperationException("Valid midpoint quantity rejected.");
Near(1.01, pay, "away-from-zero midpoint rounding");
if (!QsQuantityMath.TryCalculatePayQuantity(0, 0, 3, out pay))
    throw new InvalidOperationException("Zero quantity rejected.");
Near(0, pay, "zero quantity");
Reject(double.NaN, 0, 2, "NaN raw");
Reject(double.PositiveInfinity, 0, 2, "infinite raw");
Reject(-1, 0, 2, "negative raw");
Reject(1, -0.01, 2, "negative waste");
Reject(double.MaxValue, double.MaxValue, 2, "overflow");
Reject(1, double.NaN, 2, "NaN waste");
Reject(1, 0, 9, "excessive rounding digits");
Parse(1.25, "1,25", true, "CSV decimal comma");
Parse(1234, "1,234", false, "grouped thousands comma");
Parse(1234.56, "1.234,56", true, "European grouped decimal");
Parse(1234.56, "1,234.56", false, "invariant grouped decimal");
if (QsQuantityMath.TryParseQuantity("1,2,3.5", false, out _))
    throw new InvalidOperationException("Malformed grouped numeric text accepted.");
checks++;
if (QsQuantityMath.TryParseQuantity("NaN", false, out _))
    throw new InvalidOperationException("Non-finite numeric text accepted.");
checks++;

Console.WriteLine($"K-QS domain QA: PASS ({checks} assertions)");
