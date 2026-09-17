using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsUnitConversionService
    {
        public static bool TryConvert(double value, string from, string to, out decimal converted)
        {
            converted = 0; string a = (from ?? "").Trim().ToLowerInvariant(), b = (to ?? "").Trim().ToLowerInvariant();
            if (a == b || (a == "ea" && b == "each") || (a == "each" && b == "ea")) { converted = (decimal)value; return true; }
            if (a == "kg" && b == "t") { converted = (decimal)value / 1000m; return true; }
            if (a == "t" && b == "kg") { converted = (decimal)value * 1000m; return true; }
            if (a == "mm" && b == "m") { converted = (decimal)value / 1000m; return true; }
            if (a == "m" && b == "mm") { converted = (decimal)value * 1000m; return true; }
            return false;
        }
    }
    public static class QsCostEngine
    {
        public static QsEstimate Price(IEnumerable<QtoLine> quantities, IEnumerable<QsUnitRate> rates, IEnumerable<QsCostAdjustment> adjustments, string currency = "VND")
        {
            var estimate = new QsEstimate { Currency = currency }; var index = (rates ?? Enumerable.Empty<QsUnitRate>()).Where(x => x.IsActive).GroupBy(x => x.BoqCode, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var q in quantities ?? Enumerable.Empty<QtoLine>()) { var item = new QsCostItem { BoqCode = q.Code, Description = q.Description, Quantity = q.PayQuantity, QuantityUnit = q.Unit, Level = q.Level, PricingStatus = QsPricingStatus.UNPRICED }; if (q.PayQuantity < 0) item.PricingStatus = QsPricingStatus.INVALID_QUANTITY; else if (!index.TryGetValue(q.Code, out var matches) || matches.Count == 0) item.PricingStatus = QsPricingStatus.RATE_MISSING; else if (matches.Count > 1) item.PricingStatus = QsPricingStatus.AMBIGUOUS_RATE; else if (!QsUnitConversionService.TryConvert(q.PayQuantity, q.Unit, matches[0].Unit, out var normalized)) item.PricingStatus = QsPricingStatus.UNIT_MISMATCH; else { var rate = matches[0]; item.RateId = rate.RateId; item.UnitRate = rate.BaseRate; item.RateUnit = rate.Unit; item.Currency = rate.Currency; item.NormalizedQuantity = normalized; item.DirectCost = normalized * rate.BaseRate; item.PricingStatus = QsPricingStatus.PRICED; } estimate.Items.Add(item); }
            ApplyAdjustments(estimate, adjustments); return estimate;
        }
        public static void ApplyAdjustments(QsEstimate estimate, IEnumerable<QsCostAdjustment> adjustments)
        {
            estimate.DirectCost = estimate.Items.Where(x => x.PricingStatus == QsPricingStatus.PRICED).Sum(x => x.DirectCost); decimal total = estimate.DirectCost;
            foreach (var a in (adjustments ?? Enumerable.Empty<QsCostAdjustment>()).Where(x => x.Enabled).OrderBy(x => x.Priority)) { decimal amount = a.Method == QsAdjustmentMethod.Percent ? total * a.Value / 100m : a.Value; if (a.Type == QsAdjustmentType.Discount) amount = -Math.Abs(amount); total += amount; foreach (var item in estimate.Items.Where(x => x.PricingStatus == QsPricingStatus.PRICED)) item.AdjustmentCost += amount * (item.DirectCost / Math.Max(1m, estimate.DirectCost)); }
            estimate.Adjustments = total - estimate.DirectCost; estimate.FinalCost = total;
        }
        public static QsCostDelta Decompose(string boq, decimal q1, decimal r1, decimal q2, decimal r2) => new QsCostDelta { BoqCode = boq, QuantityEffect = (q2 - q1) * r1, RateEffect = q1 * (r2 - r1), InteractionEffect = (q2 - q1) * (r2 - r1) };
    }
}
