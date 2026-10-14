using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    public enum SlabSectionAxis
    {
        SectionX,
        SectionY
    }

    public enum SlabSectionPrimitiveKind
    {
        CoplanarSegment,
        IntersectionMarker
    }

    /// <summary>A detached model-space point used by the Revit-free slab section solver.</summary>
    public struct SlabSectionPoint
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public SlabSectionPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public sealed class SlabSectionPrimitive
    {
        public SlabSectionPrimitiveKind Kind { get; }
        public string Role { get; }
        public double StartAlong { get; }
        public double StartElevation { get; }
        public double EndAlong { get; }
        public double EndElevation { get; }

        internal SlabSectionPrimitive(SlabSectionPrimitiveKind kind, string role,
            double startAlong, double startElevation, double endAlong, double endElevation)
        {
            Kind = kind;
            Role = role ?? string.Empty;
            StartAlong = startAlong;
            StartElevation = startElevation;
            EndAlong = endAlong;
            EndElevation = endElevation;
        }
    }

    public sealed class SlabSectionInterval
    {
        public double Start { get; }
        public double End { get; }

        internal SlabSectionInterval(double start, double end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Revit-free section-plane geometry. Coordinates use Revit internal feet; 1e-5 ft
    /// (about 0.003 mm) is the documented tolerance for coplanarity and cut intersections.
    /// Each input centerline segment is classified independently; intersections are never joined.
    /// </summary>
    public static class SlabSectionGeometry
    {
        public const double PlaneToleranceFeet = 1e-5;

        public static IReadOnlyList<SlabSectionPrimitive> IntersectPath(
            IEnumerable<SlabSectionPoint> source, SlabSectionAxis axis, double cutCoordinate,
            string role = null, double toleranceFeet = PlaneToleranceFeet)
        {
            SlabSectionPoint[] points = (source ?? Enumerable.Empty<SlabSectionPoint>()).ToArray();
            if (!IsFinite(cutCoordinate) || !IsFinite(toleranceFeet) || toleranceFeet <= 0 ||
                points.Length < 2 || points.Any(point => !IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z)))
                return new SlabSectionPrimitive[0];

            var result = new List<SlabSectionPrimitive>();
            var markers = new List<Tuple<double, double>>();
            for (int i = 0; i + 1 < points.Length; i++)
            {
                SlabSectionPoint a = points[i], b = points[i + 1];
                double offsetA = Offset(a, axis, cutCoordinate);
                double offsetB = Offset(b, axis, cutCoordinate);
                bool aOnPlane = Math.Abs(offsetA) <= toleranceFeet;
                bool bOnPlane = Math.Abs(offsetB) <= toleranceFeet;

                if (aOnPlane && bOnPlane)
                {
                    double aAlong = Along(a, axis), bAlong = Along(b, axis);
                    if (Math.Abs(aAlong - bAlong) > toleranceFeet || Math.Abs(a.Z - b.Z) > toleranceFeet)
                        result.Add(new SlabSectionPrimitive(SlabSectionPrimitiveKind.CoplanarSegment, role,
                            aAlong, a.Z, bAlong, b.Z));
                    continue;
                }

                if (!aOnPlane && !bOnPlane && Math.Sign(offsetA) == Math.Sign(offsetB)) continue;

                double t = aOnPlane ? 0 : bOnPlane ? 1 : offsetA / (offsetA - offsetB);
                if (t < 0 || t > 1) continue;
                double along = Along(a, axis) + (Along(b, axis) - Along(a, axis)) * t;
                double elevation = a.Z + (b.Z - a.Z) * t;
                if (markers.Any(marker => Math.Abs(marker.Item1 - along) <= toleranceFeet &&
                    Math.Abs(marker.Item2 - elevation) <= toleranceFeet)) continue;
                markers.Add(Tuple.Create(along, elevation));
                result.Add(new SlabSectionPrimitive(SlabSectionPrimitiveKind.IntersectionMarker, role,
                    along, elevation, along, elevation));
            }
            return result;
        }

        /// <summary>Returns actual solid intervals where the cut crosses an outer loop minus its opening loops.</summary>
        public static IReadOnlyList<SlabSectionInterval> GetConcreteIntervals(
            IReadOnlyList<SlabSectionPoint> outerLoop,
            IEnumerable<IReadOnlyList<SlabSectionPoint>> openingLoops,
            SlabSectionAxis axis, double cutCoordinate, double toleranceFeet = PlaneToleranceFeet)
        {
            if (!IsValidLoop(outerLoop) || !IsFinite(cutCoordinate) || !IsFinite(toleranceFeet) || toleranceFeet <= 0)
                return new SlabSectionInterval[0];
            IReadOnlyList<SlabSectionPoint>[] openings = (openingLoops ?? Enumerable.Empty<IReadOnlyList<SlabSectionPoint>>()).ToArray();
            if (openings.Any(opening => !IsValidLoop(opening))) return new SlabSectionInterval[0];

            List<Tuple<double, double>> solids = LoopIntervals(outerLoop, axis, cutCoordinate, toleranceFeet);
            foreach (IReadOnlyList<SlabSectionPoint> opening in openings)
                foreach (Tuple<double, double> hole in LoopIntervals(opening, axis, cutCoordinate, toleranceFeet))
                    solids = Subtract(solids, hole.Item1, hole.Item2);
            return solids.Where(interval => interval.Item2 - interval.Item1 > toleranceFeet)
                .Select(interval => new SlabSectionInterval(interval.Item1, interval.Item2)).ToArray();
        }

        private static List<Tuple<double, double>> LoopIntervals(IReadOnlyList<SlabSectionPoint> loop,
            SlabSectionAxis axis, double cut, double tolerance)
        {
            var intersections = new List<double>();
            for (int i = 0; i < loop.Count; i++)
            {
                SlabSectionPoint a = loop[i], b = loop[(i + 1) % loop.Count];
                double aOrth = Orthogonal(a, axis), bOrth = Orthogonal(b, axis);
                if (Math.Abs(aOrth - bOrth) <= tolerance) continue;
                if ((aOrth <= cut && bOrth > cut) || (bOrth <= cut && aOrth > cut))
                {
                    double along = Along(a, axis) + (cut - aOrth) * (Along(b, axis) - Along(a, axis)) / (bOrth - aOrth);
                    intersections.Add(along);
                }
            }
            intersections.Sort();
            var unique = new List<double>();
            foreach (double intersection in intersections)
                if (unique.Count == 0 || Math.Abs(unique[unique.Count - 1] - intersection) > tolerance)
                    unique.Add(intersection);
            var intervals = new List<Tuple<double, double>>();
            for (int i = 0; i + 1 < unique.Count; i += 2)
                if (unique[i + 1] - unique[i] > tolerance) intervals.Add(Tuple.Create(unique[i], unique[i + 1]));
            return intervals;
        }

        private static List<Tuple<double, double>> Subtract(List<Tuple<double, double>> source, double cutStart, double cutEnd)
        {
            var result = new List<Tuple<double, double>>();
            foreach (Tuple<double, double> interval in source)
            {
                if (cutEnd <= interval.Item1 || cutStart >= interval.Item2)
                {
                    result.Add(interval);
                    continue;
                }
                if (cutStart > interval.Item1) result.Add(Tuple.Create(interval.Item1, Math.Min(cutStart, interval.Item2)));
                if (cutEnd < interval.Item2) result.Add(Tuple.Create(Math.Max(cutEnd, interval.Item1), interval.Item2));
            }
            return result;
        }

        private static bool IsValidLoop(IReadOnlyList<SlabSectionPoint> loop) =>
            loop != null && loop.Count >= 3 && loop.All(point => IsFinite(point.X) && IsFinite(point.Y) && IsFinite(point.Z));

        private static double Offset(SlabSectionPoint point, SlabSectionAxis axis, double cut) =>
            (axis == SlabSectionAxis.SectionX ? point.Y : point.X) - cut;

        private static double Orthogonal(SlabSectionPoint point, SlabSectionAxis axis) =>
            axis == SlabSectionAxis.SectionX ? point.Y : point.X;

        private static double Along(SlabSectionPoint point, SlabSectionAxis axis) =>
            axis == SlabSectionAxis.SectionX ? point.X : point.Y;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
