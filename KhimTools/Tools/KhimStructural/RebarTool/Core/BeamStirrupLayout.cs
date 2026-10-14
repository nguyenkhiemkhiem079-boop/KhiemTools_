using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Deterministic, Revit-independent Beam stirrup station planning. Inputs and
    /// outputs use one consistent length unit (production passes Revit internal feet).
    /// </summary>
    public static class BeamStirrupLayout
    {
        private const int MaximumStations = 10000;

        public static List<double> CreateZoneStations(double totalLength, double zoneA1Length,
            double spacingA1, double spacingA2, double endClearance, double duplicateTolerance)
        {
            RequirePositiveFinite(totalLength, nameof(totalLength));
            RequirePositiveFinite(spacingA1, nameof(spacingA1));
            RequirePositiveFinite(spacingA2, nameof(spacingA2));
            RequirePositiveFinite(endClearance, nameof(endClearance));
            RequirePositiveFinite(duplicateTolerance, nameof(duplicateTolerance));
            if (!IsFinite(zoneA1Length) || zoneA1Length < 0)
                throw new ArgumentOutOfRangeException(nameof(zoneA1Length), "End-zone length must be finite and non-negative.");
            if (totalLength <= 2 * endClearance)
                throw new ArgumentOutOfRangeException(nameof(totalLength), "Beam is too short for the requested stirrup end clearances.");

            double endZone = zoneA1Length > 0 ? zoneA1Length : totalLength / 4.0;
            double leftEnd = Math.Min(endZone, totalLength / 2.0);
            double rightStart = Math.Max(totalLength - endZone, totalLength / 2.0);
            var stations = new List<double>();

            AddGrid(stations, endClearance, leftEnd, spacingA1, duplicateTolerance);
            double lastLeft = stations.Count == 0 ? 0 : stations[stations.Count - 1];
            AddGrid(stations, lastLeft + spacingA2, rightStart - duplicateTolerance,
                spacingA2, duplicateTolerance);
            AddGrid(stations, rightStart, totalLength - endClearance,
                spacingA1, duplicateTolerance);

            return MergeAndValidate(stations, duplicateTolerance, minimumSeparation: 0);
        }

        public static List<double> CreateHangerStations(IEnumerable<double> intersectionStations,
            int quantity, double spacing, double startClearance, double endClearance)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (quantity == 0) return new List<double>();
            RequirePositiveFinite(spacing, nameof(spacing));
            RequirePositiveFinite(startClearance, nameof(startClearance));
            RequirePositiveFinite(endClearance, nameof(endClearance));
            var stations = new List<double>();
            foreach (double center in intersectionStations ?? Enumerable.Empty<double>())
            {
                if (!IsFinite(center))
                    throw new ArgumentOutOfRangeException(nameof(intersectionStations), "Intersection station must be finite.");
                for (int index = 0; index < quantity; index++)
                {
                    double station = center + (index - (quantity - 1) / 2.0) * spacing;
                    if (station > startClearance && station < endClearance)
                        AddStation(stations, station);
                }
            }
            return stations;
        }

        public static List<double> MergeAndValidate(IEnumerable<double> stations,
            double duplicateTolerance, double minimumSeparation)
        {
            RequirePositiveFinite(duplicateTolerance, nameof(duplicateTolerance));
            if (!IsFinite(minimumSeparation) || minimumSeparation < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSeparation));
            var merged = new List<double>();
            foreach (double station in (stations ?? Enumerable.Empty<double>()).OrderBy(value => value))
            {
                if (!IsFinite(station))
                    throw new ArgumentOutOfRangeException(nameof(stations), "Stirrup station must be finite.");
                if (merged.Count == 0 || station - merged[merged.Count - 1] > duplicateTolerance)
                    AddStation(merged, station);
            }
            for (int index = 1; index < merged.Count; index++)
            {
                if (minimumSeparation > 0 && merged[index] - merged[index - 1] < minimumSeparation - duplicateTolerance)
                    throw new InvalidOperationException("Planned Beam stirrup centerlines overlap or are closer than the selected bar diameter.");
            }
            return merged;
        }

        private static void AddGrid(List<double> stations, double start, double end,
            double spacing, double duplicateTolerance)
        {
            if (start > end) return;
            int count = 0;
            for (double station = start; station <= end + duplicateTolerance; station += spacing)
            {
                AddStation(stations, station);
                if (++count > MaximumStations)
                    throw new InvalidOperationException("Beam stirrup layout exceeds the supported station limit; increase the spacing.");
            }
        }

        private static void AddStation(ICollection<double> stations, double station)
        {
            if (stations.Count >= MaximumStations)
                throw new InvalidOperationException("Beam stirrup layout exceeds the supported station limit; increase the spacing.");
            stations.Add(station);
        }

        private static void RequirePositiveFinite(double value, string name)
        {
            if (!IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, "Value must be finite and positive.");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
