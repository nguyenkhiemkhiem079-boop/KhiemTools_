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
            if (!ViewPlane.IsSupportedView(plan.Context.View)) { plan.Status = DimensionStatus.UNSUPPORTED_VIEW_TYPE; plan.Errors.Add("UNSUPPORTED_VIEW_TYPE"); return plan; }
            if (plan.ViewId != plan.Context.View.Id || doc.GetElement(plan.ViewId) == null) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN: View changed or no longer exists."); return plan; }
            string liveFingerprint = DimensionPlanBuilder.BuildLiveFingerprint(doc, plan);
            if (string.IsNullOrEmpty(liveFingerprint)) { plan.Status = DimensionStatus.STALE_REFERENCE; plan.Errors.Add("STALE_REFERENCE: one or more stable references cannot be resolved."); return plan; }
            if (!string.Equals(plan.Fingerprint, liveFingerprint, StringComparison.Ordinal)) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN: source geometry changed after Preview."); return plan; }
            foreach (DimensionReferenceInfo info in plan.References)
            {
                Reference resolved = DimensionReferenceService.ResolveStableReference(doc, info);
                if (resolved == null) { plan.Status = DimensionStatus.STALE_REFERENCE; plan.Errors.Add("STALE_REFERENCE: " + info.StableRepresentation); }
                else DimensionReferenceService.RefreshResolvedGeometry(doc, plan.Context.View, info, resolved);
            }
            if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("INSUFFICIENT_REFERENCES"); }
            if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("INVALID_DIMENSION_LINE"); }
            if (!DimensionStyleService.IsValid(doc, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_INVALID; plan.Errors.Add("DIMENSION_TYPE_INVALID"); }
            if (plan.DimensionLine != null && Math.Abs(plan.DimensionLine.Direction.DotProduct(plan.Context.View.ViewDirection)) > 1e-6) { plan.Status = DimensionStatus.INVALID_DIMENSION_AXIS; plan.Errors.Add("INVALID_DIMENSION_AXIS: line is not in the View plane."); }
            if (plan.DimensionLine != null)
            {
                foreach (DimensionReferenceInfo info in plan.References)
                {
                    if (info.ReferenceDirection == null || info.ReferenceDirection.IsZeroLength()) continue;
                    bool datum = info.SourceRole == DimensionReferenceRole.GRID || info.SourceRole == DimensionReferenceRole.LEVEL || (!string.IsNullOrEmpty(info.ReferenceKind) && info.ReferenceKind.IndexOf("LINEAR", StringComparison.OrdinalIgnoreCase) >= 0);
                    bool compatible = datum
                        ? Math.Abs(info.ReferenceDirection.Normalize().DotProduct(plan.DimensionLine.Direction)) <= Math.Sin(DimensionGeometryService.AngularToleranceRadians)
                        : DimensionGeometryService.AreParallel(info.ReferenceDirection, plan.DimensionLine.Direction);
                    if (!compatible) { plan.Status = DimensionStatus.REFERENCE_NOT_PARALLEL; plan.Errors.Add("REFERENCE_NOT_PARALLEL: " + info.StableRepresentation); break; }
                }
            }
            return plan;
        }
    }
}
