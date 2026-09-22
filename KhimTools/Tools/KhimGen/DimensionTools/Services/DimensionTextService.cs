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
            var result = new DimensionResult { Operation = DimensionOperation.MOVE_TEXT }; Dimension dimension = doc == null ? null : doc.GetElement(dimensionId) as Dimension; if (dimension == null) { result.Status = DimensionStatus.INVALID_SELECTION; result.Message = "Dimension is unavailable."; return result; } if (dimension.Pinned) { result.Status = DimensionStatus.PINNED; result.Message = "Pinned dimension is blocked by default."; return result; } ViewPlane plane = ViewPlane.FromView(view); XYZ delta = plane.RightDirection * DimensionGeometryService.Mm(rightMillimeters) + plane.UpDirection * DimensionGeometryService.Mm(upMillimeters); using (var tx = new Transaction(doc, "K-TOOLS Move Dimension Text")) { tx.Start(); string error; if (dimension.NumberOfSegments > 0) { foreach (DimensionSegment segment in dimension.Segments) { XYZ position = segment.TextPosition; if (position != null && !new DimensionApiAdapter().TrySetSegmentTextPosition(segment, position + delta, out error)) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = error; return result; } } } else { XYZ position = dimension.TextPosition; if (position != null && !new DimensionApiAdapter().TrySetTextPosition(dimension, position + delta, out error)) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = error; return result; } } doc.Regenerate(); tx.Commit(); } bool verified = dimension.NumberOfSegments > 0 ? dimension.Segments.Cast<DimensionSegment>().All(segment => segment.TextPosition != null) : dimension.TextPosition != null; result.Status = verified ? DimensionStatus.UPDATED : DimensionStatus.POST_VERIFY_FAILED; result.VerificationPassed = verified; result.SourceDimensionIds.Add(dimension.Id); result.Message = verified ? "Text moved in ViewPlane coordinates." : "Post verification could not read actual TextPosition."; return result;
        }
    }
}
