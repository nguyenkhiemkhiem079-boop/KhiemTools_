using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class JoinDimensionService
    {
        public static DimensionResult Join(Document doc, View view, ElementId firstId, ElementId secondId)
        {
            Dimension first = doc == null ? null : doc.GetElement(firstId) as Dimension;
            Dimension second = doc == null ? null : doc.GetElement(secondId) as Dimension;
            var result = new DimensionResult { Operation = DimensionOperation.JOIN };
            if (first == null || second == null || view == null) { result.Status = DimensionStatus.INVALID_SELECTION; result.Message = "Two Dimensions in the active View are required."; return result; }
            if (first.OwnerViewId != second.OwnerViewId || first.OwnerViewId != view.Id || first.GetTypeId() != second.GetTypeId()) { result.Status = DimensionStatus.DIMENSIONS_INCOMPATIBLE; result.Message = "DIMENSIONS_INCOMPATIBLE: View or DimensionType differs."; return result; }
            if (first.Pinned || second.Pinned) { result.Status = DimensionStatus.PINNED_DIMENSION; result.Message = "PINNED_DIMENSION: both source dimensions must be unpinned."; return result; }
            Line a = first.Curve as Line; Line b = second.Curve as Line;
            if (a == null || b == null || !DimensionGeometryService.AreParallel(a.Direction, b.Direction) || DistanceBetweenParallelLines(a, b) > DimensionGeometryService.Mm(0.5)) { result.Status = DimensionStatus.DIMENSIONS_INCOMPATIBLE; result.Message = "DIMENSIONS_INCOMPATIBLE: only collinear linear dimensions can be joined."; return result; }
            var refs = DimensionReferenceService.FromDimension(doc, view, first).Concat(DimensionReferenceService.FromDimension(doc, view, second)).ToList();
            refs = DimensionReferenceService.NormalizeAndSort(view, refs, a.Direction).ToList();
            if (refs.Count < 2) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; return result; }
            return DimensionRebuildService.RebuildSingle(doc, view, first, refs, result, second.Id);
        }

        private static double DistanceBetweenParallelLines(Line first, Line second)
        {
            XYZ delta = second.GetEndPoint(0) - first.GetEndPoint(0);
            return delta.CrossProduct(first.Direction.Normalize()).GetLength();
        }
    }
}
