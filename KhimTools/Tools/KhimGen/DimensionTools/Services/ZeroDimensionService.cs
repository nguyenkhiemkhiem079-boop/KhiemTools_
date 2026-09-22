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
            Dimension dimension = doc == null ? null : doc.GetElement(dimensionId) as Dimension; var result = new DimensionResult { Operation = DimensionOperation.REMOVE_ZERO }; if (dimension == null) { result.Status = DimensionStatus.INVALID_SELECTION; return result; } if (dimension.Pinned) { result.Status = DimensionStatus.PINNED; result.Message = "Pinned dimension is blocked by default."; return result; } double tolerance = DimensionGeometryService.Mm(Math.Max(0.1, toleranceMillimeters)); var infos = DimensionReferenceService.FromDimension(doc, view, dimension).ToList(); var segments = new List<DimensionSegment>(); foreach (DimensionSegment segment in dimension.Segments) segments.Add(segment); var zeroIndices = segments.Select((s, i) => new { s, i }).Where(x => x.s.Value.HasValue && Math.Abs(x.s.Value.Value) <= tolerance).Select(x => x.i).ToList(); if (zeroIndices.Count == 0 && infos.Count == segments.Count + 1) { result.Status = DimensionStatus.NO_CHANGE; result.Message = "No zero segment within tolerance."; return result; } var clean = DimensionReferenceService.NormalizeAndSort(view, infos, view.RightDirection); if (clean.Count < 2) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; result.Message = "Clean chain would have fewer than two references."; return result; } return DimensionRebuildService.RebuildSingle(doc, view, dimension, clean, result);
        }
    }
}
