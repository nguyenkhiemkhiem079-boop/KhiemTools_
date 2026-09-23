using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;

namespace KhimTools.ModifyObjects.General
{
    public static class ColumnBaseElevationService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId columnId, ElementId baseLevelId, double baseOffsetMillimeters)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.COLUMN_BASE_ELEVATION, PrimaryElementId = columnId, TargetLevelId = baseLevelId, TargetBaseOffset = UnitUtils.ConvertToInternalUnits(baseOffsetMillimeters, UnitTypeId.Millimeters) }; context.ElementIds.Add(columnId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); FamilyInstance column = doc == null ? null : doc.GetElement(columnId) as FamilyInstance; if (column == null || column.Category == null || column.Category.Id.IntegerValue != (int)BuiltInCategory.OST_StructuralColumns) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Column base elevation requires a structural column."); } if (doc == null || doc.GetElement(baseLevelId) as Level == null) { plan.Status = ModifyObjectStatus.BASE_LEVEL_MISSING; plan.Errors.Add("Target base Level is unavailable."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { FamilyInstance column = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance; var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Column Base Elevation")) { tx.Start(); Parameter level = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM); Parameter offset = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM); if (level == null || offset == null || level.IsReadOnly || offset.IsReadOnly) { tx.RollBack(); return new ModifyObjectResult { Status = ModifyObjectStatus.READ_ONLY, Message = "Base Level/Base Offset parameters are unavailable or read-only." }; } level.Set(plan.Context.TargetLevelId); offset.Set(plan.Context.TargetBaseOffset); doc.Regenerate(); tx.Commit(); } result.Status = ModifyObjectStatus.MODIFIED; result.ModifiedElementIds.Add(column.Id); result.Summary = "Base Level and Base Offset updated while preserving top constraint."; return result; }); }
    }
}
