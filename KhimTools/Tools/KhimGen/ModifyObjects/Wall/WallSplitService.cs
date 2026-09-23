using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;
using RevitWall = Autodesk.Revit.DB.Wall;

namespace KhimTools.ModifyObjects.Wall
{
    public static class WallSplitService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId wallId, XYZ splitPoint)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.WALL_SPLIT, PrimaryElementId = wallId, SplitPoint = splitPoint }; context.ElementIds.Add(wallId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); RevitWall wall = doc == null ? null : doc.GetElement(wallId) as RevitWall; LocationCurve location = wall == null ? null : wall.Location as LocationCurve;
            if (wall == null || location == null || !(location.Curve is Line)) { plan.Status = ModifyObjectStatus.INVALID_GEOMETRY; plan.Errors.Add("Wall split requires a straight LocationCurve wall."); return plan; }
            if (splitPoint == null || location.Curve.Distance(splitPoint) > ModifyObjectElementHelpers.Millimeters(2)) { plan.Status = ModifyObjectStatus.INVALID_SPLIT_POINT; plan.Errors.Add("Split point must be on the wall centerline."); }
            if (wall != null && wall.GetDependentElements(null).Count > 0) { plan.Status = ModifyObjectStatus.HOSTED_CONTENT_RISK; plan.Errors.Add("Hosted content/dependencies require explicit migration before wall split."); }
            if (wall.Pinned) { plan.Status = ModifyObjectStatus.PINNED; plan.Errors.Add("Pinned wall requires explicit unlock."); }
            return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => ExecuteIsolated(doc, plan)); }
        private static ModifyObjectResult ExecuteIsolated(Document doc, ModifyObjectPlan plan)
        {
            RevitWall source = doc.GetElement(plan.Context.PrimaryElementId) as RevitWall; LocationCurve location = source == null ? null : source.Location as LocationCurve; Line line = location == null ? null : location.Curve as Line; Level level = source == null ? null : doc.GetElement(source.LevelId) as Level; if (line == null || level == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_GEOMETRY };
            XYZ split = plan.Context.SplitPoint == null ? line.Evaluate(0.5, true) : plan.Context.SplitPoint.ToXyz(); double height = source.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM) == null ? ModifyObjectElementHelpers.Millimeters(3000) : source.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(); double offset = source.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET) == null ? 0 : source.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble(); var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Split Wall")) { tx.Start(); RevitWall first = RevitWall.Create(doc, Line.CreateBound(line.GetEndPoint(0), split), source.WallType.Id, level.Id, height, offset, source.Flipped, false); RevitWall second = RevitWall.Create(doc, Line.CreateBound(split, line.GetEndPoint(1)), source.WallType.Id, level.Id, height, offset, source.Flipped, false); doc.Regenerate(); if (first == null || second == null) { tx.RollBack(); return new ModifyObjectResult { Status = ModifyObjectStatus.FAILED, Message = "Wall replacement creation failed." }; } string conflict; ModifyObjectElementHelpers.TryCopySafeParameters(source, first, out conflict); ModifyObjectElementHelpers.TryCopySafeParameters(source, second, out conflict); doc.Delete(source.Id); tx.Commit(); result.CreatedElementIds.Add(first.Id); result.CreatedElementIds.Add(second.Id); }
            result.DeletedSourceIds.Add(source.Id); result.Status = ModifyObjectStatus.DELETED_SOURCE; result.Summary = "Wall split with hosted-content safety preflight."; return result;
        }
    }
}
