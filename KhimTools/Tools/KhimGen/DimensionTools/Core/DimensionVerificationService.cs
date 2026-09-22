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
            var adapter = new DimensionApiAdapter(); if (adapter.ReadReferences(dimension).Count != plan.References.Count) { message = "Reference count differs from plan."; return false; }
            if (adapter.ReadSegments(dimension) != plan.ExpectedSegments) { message = "Segment count differs from plan."; return false; }
            if (plan.DimensionTypeId != null && plan.DimensionTypeId != ElementId.InvalidElementId && dimension.GetTypeId() != plan.DimensionTypeId) { message = "DimensionType differs from plan."; return false; }
            if (adapter.GetDimensionCurve(dimension) == null) { message = "Dimension curve is invalid."; return false; }
            return true;
        }
    }
}
