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
            Dimension first = doc == null ? null : doc.GetElement(firstId) as Dimension; Dimension second = doc == null ? null : doc.GetElement(secondId) as Dimension; var result = new DimensionResult { Operation = DimensionOperation.JOIN }; if (first == null || second == null) { result.Status = DimensionStatus.INVALID_SELECTION; result.Message = "Two Dimensions are required."; return result; } if (first.OwnerViewId != second.OwnerViewId || first.GetTypeId() != second.GetTypeId()) { result.Status = DimensionStatus.DIMENSIONS_INCOMPATIBLE; result.Message = "DIMENSIONS_INCOMPATIBLE: View or DimensionType differs."; return result; } Line a = first.Curve as Line; Line b = second.Curve as Line; if (a == null || b == null || !DimensionGeometryService.AreParallel(a.Direction, b.Direction)) { result.Status = DimensionStatus.DIMENSIONS_INCOMPATIBLE; result.Message = "DIMENSIONS_INCOMPATIBLE: dimension angles differ."; return result; } var refs = DimensionReferenceService.FromDimension(doc, view, first).Concat(DimensionReferenceService.FromDimension(doc, view, second)).ToList(); return DimensionRebuildService.RebuildSingle(doc, view, first, refs, result, second.Id);
        }
    }
}
