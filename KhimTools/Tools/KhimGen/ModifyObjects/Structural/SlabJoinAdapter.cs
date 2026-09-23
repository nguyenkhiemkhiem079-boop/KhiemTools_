using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Services;

namespace KhimTools.ModifyObjects.Structural
{
    /// <summary>Adapter over the existing SlabJoin service and ElementJoinService rules; Modify Objects never duplicates JoinGeometryUtils rules.</summary>
    public static class SlabJoinAdapter
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId firstId, ElementId secondId)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.SLAB_JOIN, PrimaryElementId = firstId, SecondaryElementId = secondId }; context.ElementIds.Add(firstId); context.ElementIds.Add(secondId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Element first = doc == null ? null : doc.GetElement(firstId); Element second = doc == null ? null : doc.GetElement(secondId);
            if (!(first is Floor) || !(second is Floor)) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Slab Join requires two Floor elements."); }
            return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan)
        {
            return ModifyObjectExecutionService.Execute(doc, plan, () =>
            {
                var result = new ModifyObjectResult(); var pairs = new List<SlabPair> { new SlabPair(plan.Context.PrimaryElementId, plan.Context.SecondaryElementId) };
                using (var tx = new Transaction(doc, "K-TOOLS Slab Join")) { tx.Start(); IList<JoinPairResult> joined = new SlabJoinService().JoinSlabs(doc, pairs); TransactionStatus status = tx.Commit(); if (status != TransactionStatus.Committed || joined.Count == 0 || !joined[0].Success) { result.Status = ModifyObjectStatus.FAILED; result.Message = status != TransactionStatus.Committed ? "Transaction did not commit: " + status : joined.Count == 0 ? "No join result." : joined[0].Message; return result; } }
                result.Status = ModifyObjectStatus.MODIFIED; result.ModifiedElementIds.Add(plan.Context.PrimaryElementId); result.ModifiedElementIds.Add(plan.Context.SecondaryElementId); result.Summary = "Existing SlabJoinService joined the floor pair."; return result;
            });
        }
    }
}
