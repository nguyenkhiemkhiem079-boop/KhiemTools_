using System;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionPreflightService
    {
        public static DimensionPlan Validate(Document doc, DimensionPlan plan)
        {
            if (plan == null || plan.Context == null || doc == null) return plan;
            if (!string.Equals(plan.DocumentIdentityKey, KhimTools.Core.Workflow.DocumentIdentity.From(doc).StableKey, StringComparison.Ordinal)) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN: document identity changed."); return plan; }
            View view = doc.GetElement(plan.ViewId) as View;
            if (!ViewPlane.IsSupportedView(view)) { plan.Status = DimensionStatus.UNSUPPORTED_VIEW_TYPE; plan.Errors.Add("UNSUPPORTED_VIEW_TYPE"); return plan; }
            if ((!string.IsNullOrEmpty(plan.ViewUniqueId) && !string.Equals(view.UniqueId, plan.ViewUniqueId, StringComparison.Ordinal)) || doc.GetElement(plan.ViewId) == null) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN: View changed or no longer exists."); return plan; }
            string liveFingerprint = DimensionPlanBuilder.BuildLiveFingerprint(doc, plan);
            if (string.IsNullOrEmpty(liveFingerprint)) { plan.Status = DimensionStatus.STALE_REFERENCE; plan.Errors.Add("STALE_REFERENCE: one or more stable references cannot be resolved."); return plan; }
            if (!string.Equals(plan.Fingerprint, liveFingerprint, StringComparison.Ordinal)) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN: source geometry changed after Preview."); return plan; }
            foreach (DimensionReferenceSnapshot snapshot in plan.References)
            {
                Reference resolved = DimensionReferenceService.ResolveStableReference(doc, snapshot);
                if (resolved == null) { plan.Status = DimensionStatus.STALE_REFERENCE; plan.Errors.Add("STALE_REFERENCE: " + snapshot.StableRepresentation); }
            }
            if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("INSUFFICIENT_REFERENCES"); }
            if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("INVALID_DIMENSION_LINE"); }
            if (!DimensionStyleService.IsValid(doc, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_INVALID; plan.Errors.Add("DIMENSION_TYPE_INVALID"); }
            Line dimensionLine = DimensionPlanSnapshotAdapter.ToLine(plan.DimensionLine);
            if (dimensionLine != null && Math.Abs(dimensionLine.Direction.DotProduct(view.ViewDirection)) > 1e-6) { plan.Status = DimensionStatus.INVALID_DIMENSION_AXIS; plan.Errors.Add("INVALID_DIMENSION_AXIS: line is not in the View plane."); }
            if (dimensionLine != null)
            {
                foreach (DimensionReferenceSnapshot snapshot in plan.References)
                {
                    DimensionReferenceInfo info = DimensionReferenceService.ToTransientInfo(snapshot);
                    if (info.ReferenceDirection == null || info.ReferenceDirection.IsZeroLength()) continue;
                    bool datum = info.SourceRole == DimensionReferenceRole.GRID || info.SourceRole == DimensionReferenceRole.LEVEL || (!string.IsNullOrEmpty(info.ReferenceKind) && info.ReferenceKind.IndexOf("LINEAR", StringComparison.OrdinalIgnoreCase) >= 0);
                    bool compatible = datum
                        ? Math.Abs(info.ReferenceDirection.Normalize().DotProduct(dimensionLine.Direction)) <= Math.Sin(DimensionGeometryService.AngularToleranceRadians)
                        : DimensionGeometryService.AreParallel(info.ReferenceDirection, dimensionLine.Direction);
                    if (!compatible) { plan.Status = DimensionStatus.REFERENCE_NOT_PARALLEL; plan.Errors.Add("REFERENCE_NOT_PARALLEL: " + info.StableRepresentation); break; }
                }
            }
            return plan;
        }
    }
}
