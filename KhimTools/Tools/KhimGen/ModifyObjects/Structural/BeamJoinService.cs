using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.Structural
{
    public static class BeamJoinService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId firstId, ElementId secondId)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.BEAM_JOIN, PrimaryElementId = firstId, SecondaryElementId = secondId }; context.ElementIds.Add(firstId); context.ElementIds.Add(secondId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Element first = doc == null ? null : doc.GetElement(firstId); Element second = doc == null ? null : doc.GetElement(secondId); if (!ModifyObjectElementHelpers.IsStraightBeam(first) || !ModifyObjectElementHelpers.IsStraightBeam(second)) { plan.Status = ModifyObjectStatus.BEAM_GEOMETRY_UNSUPPORTED; plan.Errors.Add("Only straight beams can be joined."); return plan; } Line a = ((LocationCurve)first.Location).Curve as Line; Line b = ((LocationCurve)second.Location).Curve as Line; if (a == null || b == null || a.Direction.CrossProduct(b.Direction).GetLength() > 0.001) { plan.Status = ModifyObjectStatus.INVALID_JOIN_PAIR; plan.Errors.Add("Beams must be collinear."); } return plan;
        }
        public static ModifyObjectResult Execute(ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(plan, () => ExecuteIsolated(plan)); }
        private static ModifyObjectResult ExecuteIsolated(ModifyObjectPlan plan)
        {
            Document doc = plan.Context.Document; FamilyInstance first = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance; FamilyInstance second = doc.GetElement(plan.Context.SecondaryElementId) as FamilyInstance; LocationCurve a = first == null ? null : first.Location as LocationCurve; LocationCurve b = second == null ? null : second.Location as LocationCurve; if (a == null || b == null) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_JOIN_PAIR };
            Line l1 = a.Curve as Line; Line l2 = b.Curve as Line; if (l1 == null || l2 == null || (l1.GetEndPoint(0).DistanceTo(l2.GetEndPoint(0)) > ModifyObjectElementHelpers.Millimeters(2) && l1.GetEndPoint(0).DistanceTo(l2.GetEndPoint(1)) > ModifyObjectElementHelpers.Millimeters(2) && l1.GetEndPoint(1).DistanceTo(l2.GetEndPoint(0)) > ModifyObjectElementHelpers.Millimeters(2) && l1.GetEndPoint(1).DistanceTo(l2.GetEndPoint(1)) > ModifyObjectElementHelpers.Millimeters(2))) return new ModifyObjectResult { Status = ModifyObjectStatus.NO_INTERSECTION, Message = "Beams must touch at an endpoint." };
            XYZ[] points = { l1.GetEndPoint(0), l1.GetEndPoint(1), l2.GetEndPoint(0), l2.GetEndPoint(1) }; XYZ start = points[0], end = points[0]; foreach (XYZ point in points) { if (point.X + point.Y + point.Z < start.X + start.Y + start.Z) start = point; if (point.X + point.Y + point.Z > end.X + end.Y + end.Z) end = point; }
            using (var tx = new Transaction(doc, "K-TOOLS Join Beams")) { tx.Start(); a.Curve = Line.CreateBound(start, end); doc.Regenerate(); doc.Delete(second.Id); tx.Commit(); }
            var result = new ModifyObjectResult { Status = ModifyObjectStatus.DELETED_SOURCE, Summary = "Collinear beams joined into the first instance." }; result.ModifiedElementIds.Add(first.Id); result.DeletedSourceIds.Add(second.Id); return result;
        }
    }
}
