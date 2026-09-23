using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class ZeroDimensionService
    {
        public static DimensionResult RemoveZero(Document doc, View view, ElementId dimensionId, double toleranceMillimeters)
        {
            Dimension dimension = doc == null ? null : doc.GetElement(dimensionId) as Dimension;
            var result = new DimensionResult { Operation = DimensionOperation.REMOVE_ZERO };
            if (dimension == null || view == null) { result.Status = DimensionStatus.INVALID_SELECTION; return result; }
            if (dimension.Pinned) { result.Status = DimensionStatus.PINNED_DIMENSION; result.Message = "Pinned dimension is blocked by default."; return result; }
            double tolerance = DimensionGeometryService.Mm(Math.Max(0.001, toleranceMillimeters));
            var infos = DimensionReferenceService.FromDimension(doc, view, dimension).ToList();
            if (infos.Count < 2) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; return result; }
            var zeroIndices = new List<int>();
            if (dimension.NumberOfSegments == 0)
            {
                if (dimension.Value.HasValue && Math.Abs(dimension.Value.Value) <= tolerance) zeroIndices.Add(0);
            }
            else
            {
                int index = 0;
                foreach (DimensionSegment segment in dimension.Segments) { if (segment.Value.HasValue && Math.Abs(segment.Value.Value) <= tolerance) zeroIndices.Add(index); index++; }
            }
            if (zeroIndices.Count == 0) { result.Status = DimensionStatus.NO_CHANGE; result.Message = "No zero segment within tolerance."; return result; }
            var remove = new HashSet<int>(zeroIndices.Select(i => Math.Min(i + 1, infos.Count - 1)));
            var clean = infos.Where((info, index) => !remove.Contains(index)).ToList();
            Line line = dimension.Curve as Line;
            if (line != null) clean = DimensionReferenceService.NormalizeAndSort(view, clean, line.Direction).ToList();
            if (clean.Count < 2) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; result.Message = "Clean chain would have fewer than two references."; return result; }
            return DimensionRebuildService.RebuildSingle(doc, view, dimension, clean, result);
        }
    }
}
