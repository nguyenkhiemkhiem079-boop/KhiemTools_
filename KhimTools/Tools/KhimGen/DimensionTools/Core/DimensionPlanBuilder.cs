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
            ViewPlane plane = ViewPlane.FromView(context.View);
            DimensionAxis resolvedAxis = DimensionGeometryService.ResolveAxis(context.View, context.References, context.Options == null ? DimensionAxis.AUTO : context.Options.Axis);
            XYZ sortAxis = context.DimensionLine != null ? context.DimensionLine.Direction : DimensionGeometryService.ResolveDirection(context.View, context.References, context.Options == null ? DimensionAxis.AUTO : context.Options.Axis);
            IList<DimensionReferenceInfo> normalized = DimensionReferenceService.NormalizeAndSort(context.View, context.References, sortAxis);
            foreach (DimensionReferenceInfo item in normalized) plan.References.Add(item);
            plan.DimensionLine = context.DimensionLine ?? DimensionGeometryService.BuildDimensionLine(context.View, plan.References, context.Options);
            plan.ExpectedSegments = plan.References.Count > 2 ? plan.References.Count - 1 : 0;
            plan.Fingerprint = BuildFingerprint(plan);
            if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("At least two valid References are required."); }
            else if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("Dimension line cannot be constructed in the view plane."); }
            else if (!DimensionStyleService.IsValid(context.Document, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_MISSING; plan.Errors.Add("No existing DimensionType is available."); }
            return plan;
        }
        public static string BuildFingerprint(DimensionPlan plan)
        { if (plan == null) return string.Empty; return string.Join("|", new[] { IdValue(plan.ViewId), plan.Operation.ToString(), IdValue(plan.DimensionTypeId), plan.DimensionLine == null ? "" : plan.DimensionLine.GetEndPoint(0) + ":" + plan.DimensionLine.GetEndPoint(1) }.Concat(plan.References.Select(r => (r.StableRepresentation ?? r.ElementUniqueId) + "@" + (r.GeometryFingerprint ?? string.Empty))).ToArray()); }
        public static string BuildLiveFingerprint(Document doc, DimensionPlan plan)
        {
            if (doc == null || plan == null) return string.Empty;
            var tokens = new List<string> { IdValue(plan.ViewId), plan.Operation.ToString(), IdValue(plan.DimensionTypeId), plan.DimensionLine == null ? "" : plan.DimensionLine.GetEndPoint(0) + ":" + plan.DimensionLine.GetEndPoint(1) };
            foreach (DimensionReferenceInfo info in plan.References)
            {
                Reference resolved = DimensionReferenceService.ResolveStableReference(doc, info);
                if (resolved == null) return string.Empty;
                tokens.Add((info.StableRepresentation ?? info.ElementUniqueId) + "@" + DimensionReferenceService.ComputeLiveGeometryFingerprint(doc, info, resolved));
            }
            return string.Join("|", tokens.ToArray());
        }
        public static string BuildFingerprint(Document doc, View view, DimensionOperation operation, IEnumerable<DimensionReferenceInfo> refs, Line line, ElementId typeId) { var context = new DimensionContext { Document = doc, View = view, Operation = operation, DimensionLine = line }; if (refs != null) foreach (DimensionReferenceInfo reference in refs) context.References.Add(reference); context.Options.DimensionTypeId = typeId; return BuildFingerprint(Build(context)); }
        private static string IdValue(ElementId id) { return id == null ? string.Empty : id.Value.ToString(); }
    }
}
