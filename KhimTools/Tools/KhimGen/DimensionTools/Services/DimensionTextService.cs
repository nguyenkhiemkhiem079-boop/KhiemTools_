using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class DimensionTextService
    {
        // MoveText uses view-local RightDirection/UpDirection vectors; it never assumes global XY.
        public static DimensionResult MoveText(Document doc, View view, ElementId dimensionId, double rightMillimeters, double upMillimeters)
        {
            var result = new DimensionResult { Operation = DimensionOperation.MOVE_TEXT };
            Dimension dimension = doc == null ? null : doc.GetElement(dimensionId) as Dimension;
            if (dimension == null || view == null || dimension.OwnerViewId != view.Id) { result.Status = DimensionStatus.INVALID_SELECTION; result.Message = "Dimension is unavailable in the active View."; return result; }
            if (dimension.Pinned) { result.Status = DimensionStatus.PINNED_DIMENSION; result.Message = "Pinned dimension is blocked by default."; return result; }
            ViewPlane plane = ViewPlane.FromView(view);
            XYZ delta = plane.RightDirection * DimensionGeometryService.Mm(rightMillimeters) + plane.UpDirection * DimensionGeometryService.Mm(upMillimeters);
            var before = new List<XYZ>();
            var adapter = new DimensionApiAdapter();
            if (dimension.NumberOfSegments > 0) before.AddRange(dimension.Segments.Cast<DimensionSegment>().Select(x => x.TextPosition)); else before.Add(dimension.TextPosition);
            if (before.Any(x => x == null)) { result.Status = DimensionStatus.REFERENCE_INVALID; result.Message = "TextPosition is unavailable for this dimension style."; return result; }
            using (var tx = new Transaction(doc, "K-TOOLS Move Dimension Text"))
            {
                tx.Start(); string error;
                if (dimension.NumberOfSegments > 0)
                {
                    int index = 0;
                    foreach (DimensionSegment segment in dimension.Segments) { if (!adapter.TrySetSegmentTextPosition(segment, before[index++] + delta, out error)) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = error; return result; } }
                }
                else if (!adapter.TrySetTextPosition(dimension, before[0] + delta, out error)) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = error; return result; }
                doc.Regenerate();
                var after = dimension.NumberOfSegments > 0 ? dimension.Segments.Cast<DimensionSegment>().Select(x => x.TextPosition).ToList() : new List<XYZ> { dimension.TextPosition };
                bool verified = after.Count == before.Count && after.Select((position, index) => position != null && position.IsAlmostEqualTo(before[index] + delta)).All(x => x);
                if (!verified) { tx.RollBack(); result.Status = DimensionStatus.POST_VERIFY_FAILED; result.Message = "Actual TextPosition does not match the requested View-local displacement."; return result; }
                tx.Commit();
            }
            result.Status = DimensionStatus.UPDATED; result.VerificationPassed = true; result.SourceDimensionIds.Add(dimension.Id); result.Message = "Text moved and verified in ViewPlane coordinates."; return result;
        }
    }
}
