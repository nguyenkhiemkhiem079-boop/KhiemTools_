using System;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionPreflightService
    {
        public static DimensionPlan Validate(Document doc, DimensionPlan plan)
        {
            if (plan == null || plan.Context == null) return plan;
            if (!ViewPlane.IsSupportedView(plan.Context.View)) { plan.Status = DimensionStatus.UNSUPPORTED_VIEW_TYPE; return plan; }
            foreach (DimensionReferenceInfo info in plan.References) { Reference resolved = DimensionReferenceService.ResolveStableReference(doc, info); if (resolved == null) { plan.Status = DimensionStatus.STALE_REFERENCE; plan.Errors.Add("STALE_REFERENCE: " + info.StableRepresentation); } else info.Reference = resolved; }
            if (plan.References.Count < 2) { plan.Status = DimensionStatus.INSUFFICIENT_REFERENCES; plan.Errors.Add("INSUFFICIENT_REFERENCES"); }
            if (plan.DimensionLine == null) { plan.Status = DimensionStatus.INVALID_DIMENSION_LINE; plan.Errors.Add("INVALID_DIMENSION_LINE"); }
            if (!DimensionStyleService.IsValid(doc, plan.DimensionTypeId)) { plan.Status = DimensionStatus.DIMENSION_TYPE_INVALID; plan.Errors.Add("DIMENSION_TYPE_INVALID"); }
            if (!string.Equals(plan.Fingerprint, DimensionPlanBuilder.BuildFingerprint(plan), StringComparison.Ordinal)) { plan.Status = DimensionStatus.STALE_DIMENSION_PLAN; plan.Errors.Add("STALE_DIMENSION_PLAN"); }
            return plan;
        }
    }
}
