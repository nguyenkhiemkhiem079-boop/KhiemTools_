using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionPlanBuilder
    {
        public static DimensionPlan Build(DimensionContext context)
        {
            var plan = new DimensionPlan { Context = context, Operation = context == null ? DimensionOperation.GENERAL : context.Operation, ViewId = context == null || context.View == null ? ElementId.InvalidElementId : context.View.Id, DimensionTypeId = context == null ? ElementId.InvalidElementId : DimensionStyleService.ResolveId(context.Document, context.Options) };
            if (context == null || context.Document == null || context.View == null) { plan.Status = DimensionStatus.INVALID_SELECTION; plan.Errors.Add("Document and View are required."); return plan; }
            if (!ViewPlane.IsSupportedView(context.View)) { plan.Status = DimensionStatus.UNSUPPORTED_VIEW_TYPE; plan.Errors.Add("UNSUPPORTED_VIEW_TYPE: dimension view is not annotatable."); return plan; }
            IList<DimensionReferenceInfo> normalized = DimensionReferenceService.NormalizeAndSort(context.View, context.References, ViewPlane.FromView(context.View).RightDirection); foreach (DimensionReferenceInfo item in normalized) plan.References.Add(item); plan.DimensionLine = context.DimensionLine ?? DimensionGeometryService.BuildDimensionLine(context.View, plan.References, context.Options); plan.ExpectedSegments = Math.Max(0, plan.References.Count - 1); plan.Fingerprint = BuildFingerprint(plan); if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("At least two valid References are required."); } else if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("Dimension line cannot be constructed in the view plane."); } else if (!DimensionStyleService.IsValid(context.Document, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_MISSING; plan.Errors.Add("No existing DimensionType is available."); } return plan;
        }
        public static string BuildFingerprint(DimensionPlan plan)
        { if (plan == null) return string.Empty; return string.Join("|", new[] { plan.ViewId == null ? "" : plan.ViewId.IntegerValue.ToString(), plan.Operation.ToString(), plan.DimensionTypeId == null ? "" : plan.DimensionTypeId.IntegerValue.ToString(), plan.DimensionLine == null ? "" : plan.DimensionLine.GetEndPoint(0) + ":" + plan.DimensionLine.GetEndPoint(1) }.Concat(plan.References.Select(r => r.StableRepresentation ?? r.ElementUniqueId)).ToArray()); }
        public static string BuildFingerprint(Document doc, View view, DimensionOperation operation, IEnumerable<DimensionReferenceInfo> refs, Line line, ElementId typeId) { var context = new DimensionContext { Document = doc, View = view, Operation = operation, DimensionLine = line }; if (refs != null) foreach (DimensionReferenceInfo reference in refs) context.References.Add(reference); context.Options.DimensionTypeId = typeId; return BuildFingerprint(Build(context)); }
    }
}
