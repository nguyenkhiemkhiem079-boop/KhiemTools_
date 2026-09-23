using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;

namespace KhimTools.ModifyObjects.General
{
    public static class PartsService
    {
        public static ModifyObjectPlan Analyze(Document doc, IList<ElementId> ids)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.CREATE_PARTS }; if (ids != null) foreach (ElementId id in ids) context.ElementIds.Add(id); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); if (doc != null && context.ElementIds.Count > 0 && !PartUtils.AreElementsValidForCreateParts(doc, context.ElementIds)) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Selected elements cannot create Parts in this model context."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Create Parts")) { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ModifyObjects.PartsService"); foreach (ElementId source in plan.Context.ElementIds) { using (var sub = new SubTransaction(doc)) { KhimTools.Core.Revit.TransactionBoundary.Start(sub, "ModifyObjects.PartsService"); var one = new List<ElementId> { source }; if (!PartUtils.AreElementsValidForCreateParts(doc, one)) { KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ModifyObjects.PartsService"); continue; } PartUtils.CreateParts(doc, one); doc.Regenerate(); ICollection<ElementId> partIds = PartUtils.GetAssociatedParts(doc, source, false, false); if (partIds != null) foreach (ElementId part in partIds) result.CreatedElementIds.Add(part); KhimTools.Core.Revit.TransactionBoundary.Commit(sub, "ModifyObjects.PartsService"); } } KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ModifyObjects.PartsService"); } result.Status = result.CreatedElementIds.Count == 0 ? ModifyObjectStatus.PARTIAL : ModifyObjectStatus.CREATED; result.Summary = "Parts created through PartUtils with per-item SubTransaction isolation."; return result; }); }
    }
}
