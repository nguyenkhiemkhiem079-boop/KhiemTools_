using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;
using RevitWall = Autodesk.Revit.DB.Wall;

namespace KhimTools.ModifyObjects.Wall
{
    public static class WallTrimService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId wallId, XYZ trimPoint)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.WALL_TRIM, PrimaryElementId = wallId, SplitPoint = trimPoint }; context.ElementIds.Add(wallId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); RevitWall wall = doc == null ? null : doc.GetElement(wallId) as RevitWall; if (wall == null || !ModifyObjectElementHelpers.IsLine(wall)) { plan.Status = ModifyObjectStatus.INVALID_GEOMETRY; plan.Errors.Add("Wall trim requires a line LocationCurve."); } if (wall != null && wall.Pinned) { plan.Status = ModifyObjectStatus.PINNED; plan.Errors.Add("Pinned wall cannot be trimmed automatically."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { RevitWall wall = doc.GetElement(plan.Context.PrimaryElementId) as RevitWall; LocationCurve location = wall == null ? null : wall.Location as LocationCurve; Line line = location == null ? null : location.Curve as Line; XYZ trim = plan.Context.SplitPoint == null ? null : plan.Context.SplitPoint.ToXyz(); if (line == null || trim == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_SPLIT_POINT }; XYZ projected = line.Project(trim).XYZPoint; using (var tx = new Transaction(doc, "K-TOOLS Trim Wall")) { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ModifyObjects.WallTrimService"); WallUtils.DisallowWallJoinAtEnd(wall, 0); WallUtils.DisallowWallJoinAtEnd(wall, 1); if (projected.DistanceTo(line.GetEndPoint(0)) < projected.DistanceTo(line.GetEndPoint(1))) location.Curve = Line.CreateBound(projected, line.GetEndPoint(1)); else location.Curve = Line.CreateBound(line.GetEndPoint(0), projected); doc.Regenerate(); KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "ModifyObjects.WallTrimService"); } var result = new ModifyObjectResult { Status = ModifyObjectStatus.MODIFIED, Summary = "Wall trimmed to geometric intersection." }; result.ModifiedElementIds.Add(wall.Id); return result; }); }
    }
}
