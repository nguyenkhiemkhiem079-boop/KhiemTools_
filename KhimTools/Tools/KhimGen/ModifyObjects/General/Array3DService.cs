using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.General
{
    public static class Array3DService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId sourceId, int count, XYZ vectorMillimeters, bool includeOriginal)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.ARRAY_3D, PrimaryElementId = sourceId, ArrayCount = count, IncludeOriginal = includeOriginal, ArrayVector = new XYZ(ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.X), ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.Y), ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.Z)) }; context.ElementIds.Add(sourceId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); if (count < 1) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("Array count must be positive."); } if (context.ArrayVector.IsZeroLength() && count > 1) { plan.Status = ModifyObjectStatus.NO_CHANGE; plan.Errors.Add("Array vector is zero."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Array 3D")) { tx.Start(); XYZ vector = plan.Context.ArrayVector.ToXyz(); for (int index = 1; index < plan.Context.ArrayCount; index++) { ICollection<ElementId> copies = ElementTransformUtils.CopyElement(doc, plan.Context.PrimaryElementId, new XYZ(vector.X * index, vector.Y * index, vector.Z * index)); foreach (ElementId copy in copies) result.CreatedElementIds.Add(copy); } doc.Regenerate(); tx.Commit(); } if (plan.Context.IncludeOriginal) result.ModifiedElementIds.Add(plan.Context.PrimaryElementId); result.Status = ModifyObjectStatus.CREATED; result.Summary = "Deterministic 3D array created; include original = " + plan.Context.IncludeOriginal; return result; }); }
    }
}
