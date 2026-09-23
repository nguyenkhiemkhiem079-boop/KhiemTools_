using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.Structural
{
    public static class ColumnSplitService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId columnId, ElementId splitLevelId)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.COLUMN_SPLIT, PrimaryElementId = columnId, TargetLevelId = splitLevelId };
            context.ElementIds.Add(columnId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Element column = doc == null ? null : doc.GetElement(columnId);
            if (!ModifyObjectElementHelpers.IsVerticalColumn(column)) { plan.Status = ModifyObjectStatus.COLUMN_GEOMETRY_UNSUPPORTED; plan.Errors.Add("Only vertical structural columns with a LocationPoint are supported."); return plan; }
            if (column.Pinned) { plan.Status = ModifyObjectStatus.PINNED; plan.Errors.Add("Pinned columns require explicit unlock before split."); }
            if (column.GroupId != null && column.GroupId != ElementId.InvalidElementId) { plan.Status = ModifyObjectStatus.GROUP_CONSTRAINED; plan.Errors.Add("Grouped columns cannot be split safely."); }
            return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan)
        {
            return ModifyObjectExecutionService.Execute(doc, plan, () => ExecuteIsolated(doc, plan));
        }
        private static ModifyObjectResult ExecuteIsolated(Document doc, ModifyObjectPlan plan)
        {
            var result = new ModifyObjectResult(); FamilyInstance source = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance; Level splitLevel = doc.GetElement(plan.Context.TargetLevelId) as Level;
            if (source == null || splitLevel == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_SPLIT_POINT, Message = "Source column or split level is unavailable." };
            Parameter baseParameter = source.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM); Parameter topParameter = source.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM); Level baseLevel = baseParameter == null ? null : doc.GetElement(baseParameter.AsElementId()) as Level; Level topLevel = topParameter == null ? null : doc.GetElement(topParameter.AsElementId()) as Level;
            if (baseLevel == null || topLevel == null || splitLevel.Elevation <= baseLevel.Elevation || splitLevel.Elevation >= topLevel.Elevation) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_SPLIT_POINT, Message = "Split level must be strictly inside the column span." };
            var created = new System.Collections.Generic.List<FamilyInstance>(); using (var tx = new Transaction(doc, "K-TOOLS Split Column")) { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ModifyObjects.ColumnSplitService"); FamilySymbol symbol = doc.GetElement(source.GetTypeId()) as FamilySymbol; LocationPoint point = source.Location as LocationPoint; FamilyInstance lower = doc.Create.NewFamilyInstance(point.Point, symbol, baseLevel, StructuralType.Column); FamilyInstance upper = doc.Create.NewFamilyInstance(point.Point, symbol, splitLevel, StructuralType.Column); SetTop(lower, splitLevel.Id); SetTop(upper, topLevel.Id); CopyParameters(source, lower, result); CopyParameters(source, upper, result); ModifyObjectElementHelpers.PreservePointRotation(doc, source, lower); ModifyObjectElementHelpers.PreservePointRotation(doc, source, upper); doc.Regenerate(); if (lower == null || upper == null) { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ModifyObjects.ColumnSplitService"); return new ModifyObjectResult { Status = ModifyObjectStatus.FAILED, Message = "Revit did not create both replacement columns." }; } doc.Delete(source.Id); KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ModifyObjects.ColumnSplitService"); created.Add(lower); created.Add(upper); }
            foreach (FamilyInstance item in created) result.CreatedElementIds.Add(item.Id); result.DeletedSourceIds.Add(source.Id); result.Status = ModifyObjectStatus.DELETED_SOURCE; result.Summary = "Column split into two replacement instances."; return result;
        }
        private static void SetTop(FamilyInstance column, ElementId levelId) { Parameter p = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM); if (p != null && !p.IsReadOnly) p.Set(levelId); }
        private static void CopyParameters(Element source, Element target, ModifyObjectResult result) { string conflict; if (!ModifyObjectElementHelpers.TryCopySafeParameters(source, target, out conflict) && !string.IsNullOrEmpty(conflict)) result.Message = "PARAMETER_CONFLICT: " + conflict; }
    }
}
