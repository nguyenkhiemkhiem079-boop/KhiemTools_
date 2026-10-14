using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class CutDimensionService
    {
        public static DimensionResult Cut(Document doc, View view, ElementId dimensionId, int boundaryIndex)
        {
            Dimension original = doc == null ? null : doc.GetElement(dimensionId) as Dimension; var result = new DimensionResult { Operation = DimensionOperation.CUT }; if (original == null) { result.Status = DimensionStatus.INVALID_SELECTION; result.Message = "Dimension is unavailable."; return result; } if (original.Pinned) { result.Status = DimensionStatus.PINNED_DIMENSION; result.Message = "Pinned dimension is blocked by default."; return result; }
            var refs = DimensionReferenceService.FromDimension(doc, view, original).ToList(); if (boundaryIndex <= 0 || boundaryIndex >= refs.Count - 1) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; result.Message = "Cut would leave an invalid one-reference dimension."; return result; } var first = refs.Take(boundaryIndex + 1).ToList(); var second = refs.Skip(boundaryIndex).ToList(); return DimensionRebuildService.RebuildSplit(doc, view, original, first, second, result);
        }
    }
}
