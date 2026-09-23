using System;

namespace KhimTools.Domain.Models.Architectural
{
    /// <summary>Pure architectural geometry for a lintel centered over an opening; all distances are millimetres.</summary>
    public static class LintelLayout
    {
        public static LintelLine Compute(double centerXmm, double centerYmm, double elevationMm,
            double openingWidthMm, double extensionEachEndMm, double directionX, double directionY)
        {
            RequireFinite(centerXmm, nameof(centerXmm));
            RequireFinite(centerYmm, nameof(centerYmm));
            RequireFinite(elevationMm, nameof(elevationMm));
            RequireFinite(openingWidthMm, nameof(openingWidthMm));
            RequireFinite(extensionEachEndMm, nameof(extensionEachEndMm));
            RequireFinite(directionX, nameof(directionX));
            RequireFinite(directionY, nameof(directionY));
            if (openingWidthMm <= 0) throw new ArgumentOutOfRangeException(nameof(openingWidthMm));
            if (extensionEachEndMm < 0) throw new ArgumentOutOfRangeException(nameof(extensionEachEndMm));

            double length = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (length <= 1e-12) throw new ArgumentException("Opening direction must have a non-zero plan component.");
            double halfLength = openingWidthMm / 2 + extensionEachEndMm;
            double unitX = directionX / length;
            double unitY = directionY / length;
            return new LintelLine(centerXmm - unitX * halfLength, centerYmm - unitY * halfLength,
                elevationMm, centerXmm + unitX * halfLength, centerYmm + unitY * halfLength, elevationMm);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class LintelLine
    {
        public double StartXmm { get; }
        public double StartYmm { get; }
        public double StartZmm { get; }
        public double EndXmm { get; }
        public double EndYmm { get; }
        public double EndZmm { get; }

        internal LintelLine(double startXmm, double startYmm, double startZmm,
            double endXmm, double endYmm, double endZmm)
        {
            StartXmm = startXmm;
            StartYmm = startYmm;
            StartZmm = startZmm;
            EndXmm = endXmm;
            EndYmm = endYmm;
            EndZmm = endZmm;
        }
    }
}
