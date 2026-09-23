using System;
using System.Linq;
using KhimTools.Domain.Models.Qs;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoRuleEngine
    {
        public static void Apply(QtoResult result, QtoRuleProfile profile)
        {
            if (result == null || profile == null) return;
            foreach (QtoLine line in result.Lines)
            {
                QtoMeasurementRule rule = profile.Rules.FirstOrDefault(x =>
                    string.Equals(x.Code, line.Code, StringComparison.OrdinalIgnoreCase));
                line.RuleId = rule?.Code ?? line.Code;
                line.IsIncluded = rule?.Enabled ?? true;
                line.WastePercent = rule?.WastePercent ?? 0;
                line.RoundingDigits = Math.Max(0, Math.Min(6, rule?.RoundingDigits ?? 3));
                if (!QsQuantityMath.TryCalculatePayQuantity(line.RawQuantity, line.WastePercent,
                    line.RoundingDigits, out double payQuantity))
                {
                    line.IsIncluded = false;
                    line.PayQuantity = 0;
                    result.Findings.Add(new QtoFinding
                    {
                        Severity = "Warning",
                        Check = "Invalid quantity rule or measurement",
                        Message = $"QS line {line.Code} has a non-finite/negative raw quantity or invalid waste/rounding rule; excluded from payable quantities.",
                        Count = line.ElementCount,
                    });
                    result.Findings[result.Findings.Count - 1].ElementIds.AddRange(line.ElementIds);
                    continue;
                }
                line.PayQuantity = payQuantity;
                line.FormulaTrace = $"{line.Source} = {line.RawQuantity:0.########} {line.Unit}; " +
                                    $"Pay = Raw × (1 + {line.WastePercent:0.###}/100), " +
                                    $"làm tròn {line.RoundingDigits} số = {line.PayQuantity:0.########} {line.Unit}";
            }
        }
    }
}
