using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.General
{
    public static class Move3DService
    {
        public static ModifyObjectPlan Analyze(Document doc, IList<ElementId> ids, XYZ vectorMillimeters)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.MOVE_3D, MoveVector = new XYZ(ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.X), ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.Y), ModifyObjectElementHelpers.Millimeters(vectorMillimeters == null ? 0 : vectorMillimeters.Z)) }; if (ids != null) foreach (ElementId id in ids) context.ElementIds.Add(id); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); if (context.MoveVector.IsZeroLength()) { plan.Status = ModifyObjectStatus.NO_CHANGE; plan.Errors.Add("Move vector is zero."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Move 3D")) { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ModifyObjects.Move3DService"); ElementTransformUtils.MoveElements(doc, plan.Context.ElementIds, plan.Context.MoveVector.ToXyz()); doc.Regenerate(); KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ModifyObjects.Move3DService"); } foreach (ElementId id in plan.Context.ElementIds) result.ModifiedElementIds.Add(id); result.Status = ModifyObjectStatus.MODIFIED; result.Summary = "Selected elements moved by a unit-aware 3D vector."; return result; }); }
    }
}
