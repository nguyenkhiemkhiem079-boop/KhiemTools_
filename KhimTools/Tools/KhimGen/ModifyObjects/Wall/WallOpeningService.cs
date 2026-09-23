using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using RevitWall = Autodesk.Revit.DB.Wall;

namespace KhimTools.ModifyObjects.Wall
{
    public static class WallOpeningService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId wallId, XYZ openingStart, XYZ openingEnd)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.WALL_OPENING, PrimaryElementId = wallId, OpeningStart = openingStart, OpeningEnd = openingEnd }; context.ElementIds.Add(wallId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); RevitWall wall = doc == null ? null : doc.GetElement(wallId) as RevitWall; if (wall == null) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Wall opening requires a Wall host."); } if (openingStart == null || openingEnd == null || openingStart.X >= openingEnd.X || openingStart.Z >= openingEnd.Z) { plan.Status = ModifyObjectStatus.OPENING_CONFLICT; plan.Errors.Add("Opening rectangle is invalid or crosses wall bounds."); } return plan;
        }
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(doc, plan, () => { RevitWall wall = doc.GetElement(plan.Context.PrimaryElementId) as RevitWall; if (wall == null) return new ModifyObjectResult { Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT }; using (var tx = new Transaction(doc, "K-TOOLS Wall Opening")) { tx.Start(); Opening opening = doc.Create.NewOpening(wall, plan.Context.OpeningStart.ToXyz(), plan.Context.OpeningEnd.ToXyz()); doc.Regenerate(); tx.Commit(); var result = new ModifyObjectResult { Status = opening == null ? ModifyObjectStatus.FAILED : ModifyObjectStatus.CREATED, Summary = "Wall opening created through the Revit Opening API." }; if (opening != null) result.CreatedElementIds.Add(opening.Id); return result; } }); }
    }
}
