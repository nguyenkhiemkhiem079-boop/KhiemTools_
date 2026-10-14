using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Workflow;
using KhimTools.DimensionTools.Models;
namespace KhimTools.DimensionTools.Core
{
    public static class DimensionPlanBuilder
    {
        public static DimensionPlan Build(DimensionContext context)
        {
            DimensionPlan plan = CreateBasePlan(context);
            if (context == null || context.Document == null || context.View == null) { plan.Status = DimensionStatus.INVALID_SELECTION; plan.Errors.Add("Document and View are required."); return plan; }
            if (!ViewPlane.IsSupportedView(context.View)) { plan.Status = DimensionStatus.UNSUPPORTED_VIEW_TYPE; plan.Errors.Add("UNSUPPORTED_VIEW_TYPE: dimension view is not annotatable."); return plan; }
            DimensionAxis axis = context.Options == null ? DimensionAxis.AUTO : context.Options.Axis;
            XYZ sortAxis = context.DimensionLine == null ? DimensionGeometryService.ResolveDirection(context.View, context.References, axis) : context.DimensionLine.Direction;
            IList<DimensionReferenceInfo> normalized = DimensionReferenceService.NormalizeAndSort(context.View, context.References, sortAxis);
            foreach (DimensionReferenceInfo info in normalized) { DimensionReferenceSnapshot snapshot = DimensionPlanSnapshotAdapter.Capture(info); if (snapshot != null) plan.References.Add(snapshot); }
            Line line = context.DimensionLine ?? DimensionGeometryService.BuildDimensionLine(context.View, normalized, context.Options);
            plan.DimensionLine = DimensionPlanSnapshotAdapter.Capture(line);
            plan.ExpectedSegments = plan.References.Count > 2 ? plan.References.Count - 1 : 0;
            plan.Fingerprint = BuildFingerprint(plan);
            if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("At least two valid References are required."); }
            else if (plan.References.Any(r => string.IsNullOrWhiteSpace(r.StableRepresentation))) { plan.Status = DimensionStatus.REFERENCE_NOT_FOUND; plan.Errors.Add("A reference cannot be represented stably for execution."); }
            else if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("Dimension line cannot be constructed in the view plane."); }
            else if (!DimensionStyleService.IsValid(context.Document, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_MISSING; plan.Errors.Add("No existing DimensionType is available."); }
            return plan;
        }

        public static DimensionPlan CreateBasePlan(DimensionContext context)
        {
            DimensionPlanContext snapshot = DimensionPlanContext.From(context);
            Document doc = context == null ? null : context.Document;
            var plan = new DimensionPlan { Context = snapshot, Operation = snapshot.Operation, ViewId = snapshot.ViewId, ViewUniqueId = snapshot.ViewUniqueId ?? string.Empty, DocumentIdentityKey = DocumentIdentity.From(doc).StableKey, DimensionTypeId = context == null ? ElementId.InvalidElementId : DimensionStyleService.ResolveId(doc, context.Options) };
            plan.SourceStateFingerprint = ComputeEditSourceFingerprint(doc, snapshot);
            return plan;
        }

        public static string BuildFingerprint(DimensionPlan plan)
        {
            if (plan == null) return string.Empty;
            var tokens = new List<string> { plan.DocumentIdentityKey ?? string.Empty, IdValue(plan.ViewId), plan.ViewUniqueId ?? string.Empty, plan.Operation.ToString(), IdValue(plan.DimensionTypeId), DimensionPlanSnapshotAdapter.LineToken(plan.DimensionLine), plan.SourceStateFingerprint ?? string.Empty };
            tokens.AddRange(plan.References.Select(r => (r.StableRepresentation ?? r.ElementUniqueId ?? string.Empty) + "@" + (r.GeometryFingerprint ?? string.Empty)));
            return WorkflowFingerprint.Compute(tokens);
        }

        public static string BuildLiveFingerprint(Document doc, DimensionPlan plan)
        {
            if (doc == null || plan == null) return string.Empty;
            var tokens = new List<string> { DocumentIdentity.From(doc).StableKey, IdValue(plan.ViewId), plan.ViewUniqueId ?? string.Empty, plan.Operation.ToString(), IdValue(plan.DimensionTypeId), DimensionPlanSnapshotAdapter.LineToken(plan.DimensionLine), ComputeEditSourceFingerprint(doc, plan.Context) };
            foreach (DimensionReferenceSnapshot info in plan.References)
            {
                Reference reference = DimensionReferenceService.ResolveStableReference(doc, info);
                if (reference == null) return string.Empty;
                tokens.Add((info.StableRepresentation ?? info.ElementUniqueId ?? string.Empty) + "@" + DimensionReferenceService.ComputeLiveGeometryFingerprint(doc, info, reference));
            }
            return WorkflowFingerprint.Compute(tokens);
        }

        public static string BuildFingerprint(Document doc, View view, DimensionOperation operation, IEnumerable<DimensionReferenceInfo> refs, Line line, ElementId typeId)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = operation, DimensionLine = line };
            if (refs != null) foreach (DimensionReferenceInfo reference in refs) context.References.Add(reference);
            context.Options.DimensionTypeId = typeId;
            return BuildFingerprint(Build(context));
        }
        private static string IdValue(ElementId id) { return id == null ? string.Empty : id.ToLongValue().ToString(); }

        public static string ComputeEditSourceFingerprint(Document doc, DimensionPlanContext context)
        {
            if (doc == null || context == null || context.DimensionIds.Count == 0) return WorkflowFingerprint.Compute(new string[0]);
            var tokens = new List<string>();
            foreach (ElementId id in context.DimensionIds.OrderBy(value => value.ToLongValue()))
            {
                Dimension dimension = doc.GetElement(id) as Dimension;
                if (dimension == null) { tokens.Add("missing:" + IdValue(id)); continue; }
                var view = doc.GetElement(dimension.OwnerViewId) as View;
                string curve = DimensionPlanSnapshotAdapter.LineToken(DimensionPlanSnapshotAdapter.Capture(dimension.Curve as Line));
                var references = DimensionReferenceService.FromDimension(doc, view, dimension)
                    .Select(reference => reference.StableRepresentation ?? string.Empty)
                    .OrderBy(value => value, System.StringComparer.Ordinal);
                string segments = string.Join(",", dimension.Segments.Cast<DimensionSegment>().Select(segment => segment.Value.HasValue ? segment.Value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : string.Empty));
                tokens.Add(string.Join("|", dimension.UniqueId, IdValue(dimension.OwnerViewId), IdValue(dimension.GetTypeId()), curve, string.Join(",", references), segments, dimension.ValueOverride ?? string.Empty, dimension.Prefix ?? string.Empty, dimension.Suffix ?? string.Empty));
            }
            return WorkflowFingerprint.Compute(tokens);
        }
    }
}
