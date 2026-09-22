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
            if (view == null || refs == null || refs.Count < 2) return null; ViewPlane plane = ViewPlane.FromView(view); DimensionAxis axis = options == null ? DimensionAxis.AUTO : options.Axis; XYZ direction = axis == DimensionAxis.VERTICAL_IN_VIEW ? plane.UpDirection : plane.RightDirection; XYZ first = refs.First().WorldPoint ?? XYZ.Zero; XYZ last = refs.Last().WorldPoint ?? XYZ.Zero; XYZ offset = plane.UpDirection * Mm(options == null ? 0 : options.OffsetMillimeters); if (axis == DimensionAxis.VERTICAL_IN_VIEW) direction = plane.UpDirection; if (axis == DimensionAxis.PICK_LINE && options != null) direction = plane.RightDirection; XYZ p1 = first + offset; XYZ p2 = last + offset; if ((p2 - p1).DotProduct(direction) < 0) { XYZ temp = p1; p1 = p2; p2 = temp; } if (p1.DistanceTo(p2) < Mm(0.1)) return null; return Line.CreateBound(p1, p2);
        }
        public static IList<DimensionReferenceInfo> SortByProjectedPosition(View view, IEnumerable<DimensionReferenceInfo> refs, DimensionAxis axis)
        { ViewPlane plane = ViewPlane.FromView(view); XYZ direction = axis == DimensionAxis.VERTICAL_IN_VIEW ? plane.UpDirection : plane.RightDirection; return DimensionReferenceService.NormalizeAndSort(view, refs, direction); }
        public static bool IsOnViewPlane(View view, XYZ point, double tolerance) { if (view == null || point == null) return false; ViewPlane plane = ViewPlane.FromView(view); return Math.Abs(plane.ToView(point).Z) <= tolerance; }
    }
}
