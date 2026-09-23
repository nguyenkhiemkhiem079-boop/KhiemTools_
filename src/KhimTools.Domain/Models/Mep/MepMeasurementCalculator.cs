using System;

namespace KhimTools.Domain.Models.Mep
{
    public static class MepMeasurementCalculator
    {
        public static MepOpeningDimensions RoundOpening(double outsideDiameterMm, double clearanceEachSideMm)
        {
            RequirePositive(outsideDiameterMm, nameof(outsideDiameterMm));
            RequireNonNegative(clearanceEachSideMm, nameof(clearanceEachSideMm));
            double size = outsideDiameterMm + 2 * clearanceEachSideMm;
            return new MepOpeningDimensions(size, size);
        }

        public static MepOpeningDimensions RectangularOpening(double widthMm, double heightMm, double clearanceEachSideMm)
        {
            RequirePositive(widthMm, nameof(widthMm));
            RequirePositive(heightMm, nameof(heightMm));
            RequireNonNegative(clearanceEachSideMm, nameof(clearanceEachSideMm));
            return new MepOpeningDimensions(widthMm + 2 * clearanceEachSideMm,
                heightMm + 2 * clearanceEachSideMm);
        }

        public static MepVerticalRange VerticalRange(double centerElevationMm, double sectionHeightMm)
        {
            RequireFinite(centerElevationMm, nameof(centerElevationMm));
            RequirePositive(sectionHeightMm, nameof(sectionHeightMm));
            double halfHeight = sectionHeightMm / 2;
            return new MepVerticalRange(centerElevationMm - halfHeight, centerElevationMm + halfHeight);
        }

        private static void RequirePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0) throw new ArgumentOutOfRangeException(name, "Value must be greater than zero.");
        }

        private static void RequireNonNegative(double value, string name)
        {
            RequireFinite(value, name);
            if (value < 0) throw new ArgumentOutOfRangeException(name, "Value cannot be negative.");
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class MepOpeningDimensions
    {
        public double WidthMm { get; }
        public double HeightMm { get; }
        internal MepOpeningDimensions(double widthMm, double heightMm) { WidthMm = widthMm; HeightMm = heightMm; }
    }

    public sealed class MepVerticalRange
    {
        public double BottomMm { get; }
        public double TopMm { get; }
        internal MepVerticalRange(double bottomMm, double topMm) { BottomMm = bottomMm; TopMm = topMm; }
    }
}
