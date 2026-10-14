using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;

namespace KhimTools.ModifyObjects.Structural
{
    /// <summary>Capability audit only. Revit exposes no public Floor.Split API in supported targets.</summary>
    public static class SlabSplitService
    {
        public static ModifyObjectPlan CapabilityAudit(Document doc, ElementId floorId)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.SLAB_SPLIT, PrimaryElementId = floorId }; context.ElementIds.Add(floorId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Element floor = doc == null ? null : doc.GetElement(floorId);
            if (!(floor is Floor)) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Slab Split requires a Floor."); return plan; }
            plan.Status = ModifyObjectStatus.SLAB_SPLIT_CAPABILITY_UNSUPPORTED; plan.Errors.Add("No public Floor.Split API; profile reconstruction is intentionally unsupported in this MVP."); return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return new ModifyObjectResult { Operation = ModifyObjectOperation.SLAB_SPLIT.ToString(), Status = ModifyObjectStatus.SLAB_SPLIT_CAPABILITY_UNSUPPORTED, FailureKind = "VALIDATION_FAILURE", Postcondition = "Capability-only refusal; no model mutation is attempted.", PostconditionPassed = true, VerificationPassed = true, Message = "Floor.Split capability audit only; no invented API call was made." }; }
    }
}
