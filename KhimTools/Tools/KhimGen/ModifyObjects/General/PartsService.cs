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
        public static ModifyObjectResult Execute(ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(plan, () => { var result = new ModifyObjectResult(); using (var tx = new Transaction(plan.Context.Document, "K-TOOLS Create Parts")) { tx.Start(); foreach (ElementId source in plan.Context.ElementIds) { using (var sub = new SubTransaction(plan.Context.Document)) { sub.Start(); var one = new List<ElementId> { source }; if (!PartUtils.AreElementsValidForCreateParts(plan.Context.Document, one)) { sub.RollBack(); continue; } PartUtils.CreateParts(plan.Context.Document, one); plan.Context.Document.Regenerate(); ICollection<ElementId> partIds = PartUtils.GetAssociatedParts(plan.Context.Document, source, false, false); if (partIds != null) foreach (ElementId part in partIds) result.CreatedElementIds.Add(part); sub.Commit(); } } tx.Commit(); } result.Status = result.CreatedElementIds.Count == 0 ? ModifyObjectStatus.PARTIAL : ModifyObjectStatus.CREATED; result.Summary = "Parts created through PartUtils with per-item SubTransaction isolation."; return result; }); }
    }
}
