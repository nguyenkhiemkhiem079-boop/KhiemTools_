using System;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.Structural
{
    public static class ColumnJoinService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId lowerId, ElementId upperId, ModifyObjectConflictPolicy policy)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.COLUMN_JOIN, PrimaryElementId = lowerId, SecondaryElementId = upperId, ConflictPolicy = policy }; context.ElementIds.Add(lowerId); context.ElementIds.Add(upperId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); FamilyInstance lower = doc == null ? null : doc.GetElement(lowerId) as FamilyInstance; FamilyInstance upper = doc == null ? null : doc.GetElement(upperId) as FamilyInstance;
            if (!ModifyObjectElementHelpers.IsVerticalColumn(lower) || !ModifyObjectElementHelpers.IsVerticalColumn(upper)) { plan.Status = ModifyObjectStatus.COLUMN_GEOMETRY_UNSUPPORTED; plan.Errors.Add("Both columns must be vertical structural columns."); return plan; }
            if (policy == ModifyObjectConflictPolicy.BLOCK || policy == ModifyObjectConflictPolicy.MANUAL) { plan.Status = ModifyObjectStatus.PARAMETER_CONFLICT; plan.Errors.Add("Join conflict policy requires KEEP_LOWER or KEEP_UPPER."); return plan; }
            LocationPoint a = lower.Location as LocationPoint; LocationPoint b = upper.Location as LocationPoint; if (!ModifyObjectElementHelpers.IsAlmost(a.Point, b.Point)) { plan.Status = ModifyObjectStatus.INVALID_JOIN_PAIR; plan.Errors.Add("Columns must share the same XY location."); }
            if (lower.GetTypeId() != upper.GetTypeId() || System.Math.Abs(a.Rotation - b.Rotation) > 0.001) { plan.Status = ModifyObjectStatus.INVALID_JOIN_PAIR; plan.Errors.Add("Columns must share type and rotation."); }
            return plan;
        }
        public static ModifyObjectResult Execute(ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(plan, () => ExecuteIsolated(plan)); }
        private static ModifyObjectResult ExecuteIsolated(ModifyObjectPlan plan)
        {
            Document doc = plan.Context.Document; FamilyInstance first = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance; FamilyInstance second = doc.GetElement(plan.Context.SecondaryElementId) as FamilyInstance; if (first == null || second == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_JOIN_PAIR };
            FamilyInstance survivor = plan.Context.ConflictPolicy == ModifyObjectConflictPolicy.KEEP_UPPER ? second : first; FamilyInstance removed = survivor.Id == first.Id ? second : first; Parameter top = survivor.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM); Parameter removedTop = removed.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM); if (top == null || removedTop == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_JOIN_PAIR, Message = "Column level constraints are unavailable." };
            using (var tx = new Transaction(doc, "K-TOOLS Join Columns")) { tx.Start(); if (!top.IsReadOnly) top.Set(removedTop.AsElementId()); doc.Regenerate(); doc.Delete(removed.Id); tx.Commit(); }
            return new ModifyObjectResult { Status = ModifyObjectStatus.DELETED_SOURCE, Summary = "Column pair joined with explicit conflict policy." };
        }
    }
}
