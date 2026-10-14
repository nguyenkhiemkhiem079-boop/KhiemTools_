using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionGeometryService
    {
        public const double AngularToleranceRadians = 0.0174532925199433;
        public static double Mm(double value) { return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters); }
        public static bool AreParallel(XYZ first, XYZ second) { if (first == null || second == null || first.IsZeroLength() || second.IsZeroLength()) return false; return first.Normalize().CrossProduct(second.Normalize()).GetLength() <= Math.Sin(AngularToleranceRadians); }
        public static Line BuildDimensionLine(View view, IList<DimensionReferenceInfo> refs, DimensionOptions options)
        {
            if (view == null || refs == null || refs.Count < 2) return null;
            ViewPlane plane = ViewPlane.FromView(view);
            if (plane == null) return null;
            DimensionAxis axis = ResolveAxis(view, refs, options == null ? DimensionAxis.AUTO : options.Axis);
            XYZ direction = ResolveDirection(view, refs, options == null ? DimensionAxis.AUTO : options.Axis);
            XYZ perpendicular = plane.ViewDirection.CrossProduct(direction).Normalize();
            IList<DimensionReferenceInfo> sorted = NormalizeForAxis(view, refs, direction);
            if (sorted.Count < 2) return null;
            double offset = Mm(options == null ? 0 : options.OffsetMillimeters);
            double start = plane.ProjectAlong(sorted.First().WorldPoint ?? XYZ.Zero, direction);
            double end = plane.ProjectAlong(sorted.Last().WorldPoint ?? XYZ.Zero, direction);
            double fixedCoordinate = sorted.Average(x => plane.ProjectAlong(x.WorldPoint ?? XYZ.Zero, perpendicular)) + offset;
            XYZ p1 = plane.Origin + direction * start + perpendicular * fixedCoordinate;
            XYZ p2 = plane.Origin + direction * end + perpendicular * fixedCoordinate;
            if (p1.DistanceTo(p2) < Mm(0.1) || !AreParallel(p2 - p1, direction) || Math.Abs((p2 - p1).DotProduct(perpendicular)) > Mm(0.01)) return null;
            return Line.CreateBound(p1, p2);
        }
        public static IList<DimensionReferenceInfo> SortByProjectedPosition(View view, IEnumerable<DimensionReferenceInfo> refs, DimensionAxis axis)
        { ViewPlane plane = ViewPlane.FromView(view); DimensionAxis resolved = ResolveAxis(view, refs == null ? null : refs.ToList(), axis); XYZ direction = resolved == DimensionAxis.VERTICAL_IN_VIEW ? plane.UpDirection : plane.RightDirection; return NormalizeForAxis(view, refs, direction); }
        public static bool IsOnViewPlane(View view, XYZ point, double tolerance) { if (view == null || point == null) return false; ViewPlane plane = ViewPlane.FromView(view); return Math.Abs(plane.ToView(point).Z) <= tolerance; }
        public static DimensionAxis ResolveAxis(View view, IList<DimensionReferenceInfo> refs, DimensionAxis requested)
        {
            if (requested == DimensionAxis.HORIZONTAL_IN_VIEW || requested == DimensionAxis.VERTICAL_IN_VIEW) return requested;
            ViewPlane plane = ViewPlane.FromView(view);
            if (plane == null || refs == null || refs.Count < 2) return DimensionAxis.HORIZONTAL_IN_VIEW;
            IList<XYZ> local = refs.Where(x => x != null && x.WorldPoint != null).Select(x => plane.ToView(x.WorldPoint)).ToList();
            if (local.Count < 2) return DimensionAxis.HORIZONTAL_IN_VIEW;
            double spanU = local.Max(x => x.X) - local.Min(x => x.X);
            double spanV = local.Max(x => x.Y) - local.Min(x => x.Y);
            return spanV > spanU ? DimensionAxis.VERTICAL_IN_VIEW : DimensionAxis.HORIZONTAL_IN_VIEW;
        }
        public static XYZ ResolveDirection(View view, IList<DimensionReferenceInfo> refs, DimensionAxis requested)
        {
            ViewPlane plane = ViewPlane.FromView(view);
            if (requested == DimensionAxis.HORIZONTAL_IN_VIEW) return plane.RightDirection;
            if (requested == DimensionAxis.VERTICAL_IN_VIEW) return plane.UpDirection;
            DimensionReferenceInfo directional = refs == null ? null : refs.FirstOrDefault(x => x.ReferenceDirection != null && !x.ReferenceDirection.IsZeroLength() && Math.Abs(x.ReferenceDirection.Normalize().DotProduct(plane.ViewDirection)) < 0.999);
            if (directional != null)
            {
                XYZ projected = directional.ReferenceDirection - plane.ViewDirection * directional.ReferenceDirection.DotProduct(plane.ViewDirection);
                if (!projected.IsZeroLength()) return projected.Normalize();
            }
            return ResolveAxis(view, refs, requested) == DimensionAxis.VERTICAL_IN_VIEW ? plane.UpDirection : plane.RightDirection;
        }
        private static IList<DimensionReferenceInfo> NormalizeForAxis(View view, IEnumerable<DimensionReferenceInfo> refs, XYZ worldDirection)
        {
            return DimensionReferenceService.NormalizeAndSort(view, refs, worldDirection);
        }
    }
}
