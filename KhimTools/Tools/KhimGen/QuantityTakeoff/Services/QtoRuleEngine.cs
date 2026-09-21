using System;
using System.Linq;
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
                double adjusted = line.RawQuantity * (1.0 + line.WastePercent / 100.0);
                line.PayQuantity = Math.Round(adjusted, line.RoundingDigits, MidpointRounding.AwayFromZero);
                line.FormulaTrace = $"{line.Source} = {line.RawQuantity:0.########} {line.Unit}; " +
                                    $"Pay = Raw × (1 + {line.WastePercent:0.###}/100), " +
                                    $"làm tròn {line.RoundingDigits} số = {line.PayQuantity:0.########} {line.Unit}";
            }
        }
    }
}
