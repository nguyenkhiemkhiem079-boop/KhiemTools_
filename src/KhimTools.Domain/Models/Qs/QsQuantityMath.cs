using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KhimTools.Domain.Models.Qs
{
    /// <summary>Deterministic, Revit-free quantity and commercial adjustment arithmetic.</summary>
    public static class QsQuantityMath
    {
        public static bool TryParseQuantity(string text, bool decimalComma, out double quantity)
        {
            quantity = 0;
            string value = (text ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("\u00A0", string.Empty);
            if (value.Length == 0) return false;

            int lastComma = value.LastIndexOf(',');
            int lastDot = value.LastIndexOf('.');
            if (lastComma >= 0 && lastDot >= 0)
            {
                char decimalSeparator = lastComma > lastDot ? ',' : '.';
                char groupingSeparator = decimalSeparator == ',' ? '.' : ',';
                string groupedInteger = value.Substring(0, value.LastIndexOf(decimalSeparator));
                if (groupedInteger.Contains(groupingSeparator) && !IsThousandsGrouped(groupedInteger, groupingSeparator)) return false;
                string integer = groupedInteger.Replace(groupingSeparator.ToString(), string.Empty);
                string fraction = value.Substring(value.LastIndexOf(decimalSeparator) + 1);
                if (integer.IndexOf(decimalSeparator) >= 0 || fraction.IndexOfAny(new[] { ',', '.' }) >= 0 ||
                    fraction.Length == 0 || !fraction.All(char.IsDigit)) return false;
                value = integer + "." + fraction;
            }
            else if (lastComma >= 0)
            {
                if (value.Count(c => c == ',') > 1) return false;
                if (decimalComma || !IsThousandsGrouped(value, ',')) value = value.Replace(',', '.');
                else value = value.Replace(",", string.Empty);
            }
            else if (lastDot >= 0 && value.Count(c => c == '.') > 1)
            {
                if (IsThousandsGrouped(value, '.')) value = value.Replace(".", string.Empty);
                else
                {
                    int last = value.LastIndexOf('.');
                    value = value.Substring(0, last).Replace(".", string.Empty) + value.Substring(last);
                }
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out quantity) && IsFinite(quantity);
        }

        public static bool TryCalculatePayQuantity(double rawQuantity, double wastePercent,
            int roundingDigits, out double payQuantity)
        {
            payQuantity = 0;
            if (!IsFinite(rawQuantity) || rawQuantity < 0 ||
                !IsFinite(wastePercent) || wastePercent < 0 ||
                roundingDigits < 0 || roundingDigits > 8)
                return false;

            try
            {
                decimal adjusted = (decimal)rawQuantity * (1m + (decimal)wastePercent / 100m);
                payQuantity = (double)Math.Round(adjusted, roundingDigits, MidpointRounding.AwayFromZero);
                return IsFinite(payQuantity);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsThousandsGrouped(string value, char separator) =>
            Regex.IsMatch(value, @"^[+-]?\d{1,3}(?:" + Regex.Escape(separator.ToString()) + @"\d{3})+$");
    }
}
