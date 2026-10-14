using System;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionVerificationService
    {
        public static bool Verify(Document doc, View view, Dimension dimension, DimensionPlan plan, out string message)
        {
            message = string.Empty; if (doc == null || view == null || dimension == null || plan == null) { message = "Dimension or plan is unavailable."; return false; }
            if (dimension.OwnerViewId != view.Id) { message = "Dimension belongs to another View."; return false; }
            var adapter = new DimensionApiAdapter(); var actualReferences = adapter.ReadReferences(dimension); if (actualReferences.Count != plan.References.Count) { message = "Reference count differs from plan."; return false; }
            for (int i = 0; i < actualReferences.Count; i++)
            {
                string actual = adapter.ConvertStableReference(doc, actualReferences[i]);
                string expected = plan.References[i].StableRepresentation;
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(actual, expected, StringComparison.Ordinal)) { message = "Reference order/content differs from plan."; return false; }
            }
            if (adapter.ReadSegments(dimension) != plan.ExpectedSegments) { message = "Segment count differs from plan."; return false; }
            if (plan.DimensionTypeId != null && plan.DimensionTypeId != ElementId.InvalidElementId && dimension.GetTypeId() != plan.DimensionTypeId) { message = "DimensionType differs from plan."; return false; }
            if (adapter.GetDimensionCurve(dimension) == null) { message = "Dimension curve is invalid."; return false; }
            foreach (DimensionSegment segment in dimension.Segments) if (!segment.Value.HasValue || double.IsNaN(segment.Value.Value) || double.IsInfinity(segment.Value.Value)) { message = "Dimension contains an invalid segment value."; return false; }
            return true;
        }
    }
}
