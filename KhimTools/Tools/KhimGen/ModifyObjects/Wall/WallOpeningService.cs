using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;

namespace KhimTools.ModifyObjects.Wall
{
    public static class WallOpeningService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId wallId, XYZ openingStart, XYZ openingEnd)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.WALL_OPENING, PrimaryElementId = wallId, OpeningStart = openingStart, OpeningEnd = openingEnd }; context.ElementIds.Add(wallId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Wall wall = doc == null ? null : doc.GetElement(wallId) as Wall; if (wall == null) { plan.Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT; plan.Errors.Add("Wall opening requires a Wall host."); } if (openingStart == null || openingEnd == null || openingStart.X >= openingEnd.X || openingStart.Z >= openingEnd.Z) { plan.Status = ModifyObjectStatus.OPENING_CONFLICT; plan.Errors.Add("Opening rectangle is invalid or crosses wall bounds."); } return plan;
        }
        public static ModifyObjectResult Execute(ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(plan, () => { Wall wall = plan.Context.Document.GetElement(plan.Context.PrimaryElementId) as Wall; if (wall == null) return new ModifyObjectResult { Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT }; using (var tx = new Transaction(plan.Context.Document, "K-TOOLS Wall Opening")) { tx.Start(); Opening opening = plan.Context.Document.Create.NewOpening(wall, plan.Context.OpeningStart, plan.Context.OpeningEnd); plan.Context.Document.Regenerate(); tx.Commit(); var result = new ModifyObjectResult { Status = opening == null ? ModifyObjectStatus.FAILED : ModifyObjectStatus.CREATED, Summary = "Wall opening created through the Revit Opening API." }; if (opening != null) result.CreatedElementIds.Add(opening.Id); return result; } }); }
    }
}
