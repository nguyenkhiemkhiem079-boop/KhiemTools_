using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Pure section-plane layout and geometric non-overlap checks for Beam longitudinal bars.</summary>
    public static class BeamLongitudinalLayout
    {
        public sealed class SectionBar
        {
            public SectionBar(double x, double y, double diameter)
            {
                X = x;
                Y = y;
                Diameter = diameter;
            }

            public double X { get; }
            public double Y { get; }
            public double Diameter { get; }
        }

        public static List<SectionBar> CreateSymmetricRow(double width, double height,
            double cover, double stirrupDiameter, double barDiameter, int quantity, bool top,
            double tolerance = 1e-9)
        {
            RequirePositiveFinite(width, nameof(width));
            RequirePositiveFinite(height, nameof(height));
            RequireNonNegativeFinite(cover, nameof(cover));
            RequirePositiveFinite(stirrupDiameter, nameof(stirrupDiameter));
            RequirePositiveFinite(barDiameter, nameof(barDiameter));
            RequirePositiveFinite(tolerance, nameof(tolerance));
            if (quantity < 2) throw new ArgumentOutOfRangeException(nameof(quantity), "A symmetric longitudinal row requires at least two bars.");

            double halfWidth = width / 2.0 - cover - stirrupDiameter - barDiameter / 2.0;
            double y = top
                ? height / 2.0 - cover - stirrupDiameter - barDiameter / 2.0
                : -height / 2.0 + cover + stirrupDiameter + barDiameter / 2.0;
            if (halfWidth <= 0 || Math.Abs(y) <= tolerance)
                throw new InvalidOperationException("Beam section is too small for the selected cover, stirrup and longitudinal bar diameter.");

            double centerSpacing = (2.0 * halfWidth) / (quantity - 1);
            if (centerSpacing + tolerance < barDiameter)
                throw new InvalidOperationException("Selected Beam bar quantity and diameter overlap in the section; increase the width or reduce the quantity/diameter.");

            var result = new List<SectionBar>(quantity);
            for (int index = 0; index < quantity; index++)
            {
                double fraction = (double)index / (quantity - 1);
                result.Add(new SectionBar(-halfWidth + fraction * 2.0 * halfWidth, y, barDiameter));
            }
            return result;
        }

        public static void RequireNoOverlap(IEnumerable<SectionBar> bars, double tolerance = 1e-9)
        {
            RequirePositiveFinite(tolerance, nameof(tolerance));
            SectionBar[] items = (bars ?? Enumerable.Empty<SectionBar>()).ToArray();
            for (int leftIndex = 0; leftIndex < items.Length; leftIndex++)
            {
                SectionBar left = items[leftIndex];
                RequirePositiveFinite(left?.Diameter ?? double.NaN, nameof(bars));
                RequireFinite(left.X, nameof(bars));
                RequireFinite(left.Y, nameof(bars));
                for (int rightIndex = leftIndex + 1; rightIndex < items.Length; rightIndex++)
                {
                    SectionBar right = items[rightIndex];
                    RequirePositiveFinite(right?.Diameter ?? double.NaN, nameof(bars));
                    RequireFinite(right.X, nameof(bars));
                    RequireFinite(right.Y, nameof(bars));
                    double requiredCenterDistance = (left.Diameter + right.Diameter) / 2.0;
                    double actualCenterDistance = Math.Sqrt(
                        Square(left.X - right.X) + Square(left.Y - right.Y));
                    if (actualCenterDistance + tolerance < requiredCenterDistance)
                        throw new InvalidOperationException("Beam longitudinal bar centerlines overlap in the section; adjust count, diameter or layer geometry.");
                }
            }
        }

        private static double Square(double value) => value * value;

        private static void RequirePositiveFinite(double value, string name)
        {
            if (!IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name, "Value must be finite and positive.");
        }

        private static void RequireNonNegativeFinite(double value, string name)
        {
            if (!IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name, "Value must be finite and non-negative.");
        }

        private static void RequireFinite(double value, string name)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Coordinate must be finite.");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
