using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Services;

namespace KhimTools.ModifyObjects.Structural
{
    public static class BeamSplitService
    {
        public static ModifyObjectPlan Analyze(Document doc, ElementId beamId, XYZ splitPoint)
        {
            var context = new ModifyObjectContext { Document = doc, Operation = ModifyObjectOperation.BEAM_SPLIT, PrimaryElementId = beamId, SplitPoint = splitPoint }; context.ElementIds.Add(beamId); ModifyObjectPlan plan = ModifyObjectPlanBuilder.Build(context); Element beam = doc == null ? null : doc.GetElement(beamId); LocationCurve location = beam == null ? null : beam.Location as LocationCurve;
            if (!ModifyObjectElementHelpers.IsStraightBeam(beam)) { plan.Status = ModifyObjectStatus.BEAM_GEOMETRY_UNSUPPORTED; plan.Errors.Add("Only straight line structural framing is supported; curved beams are unsupported."); return plan; }
            if (splitPoint == null || location.Curve.Distance(splitPoint) > ModifyObjectElementHelpers.Millimeters(2)) { plan.Status = ModifyObjectStatus.INVALID_SPLIT_POINT; plan.Errors.Add("Split point must lie on the beam centerline."); }
            return plan;
        }
        public static ModifyObjectResult Execute(ModifyObjectPlan plan) { return ModifyObjectExecutionService.Execute(plan, () => ExecuteIsolated(plan)); }
        private static ModifyObjectResult ExecuteIsolated(ModifyObjectPlan plan)
        {
            Document doc = plan.Context.Document; FamilyInstance source = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance; LocationCurve location = source == null ? null : source.Location as LocationCurve; if (source == null || location == null || !(location.Curve is Line)) return new ModifyObjectResult { Status = ModifyObjectStatus.BEAM_GEOMETRY_UNSUPPORTED };
            Line line = location.Curve as Line; XYZ split = plan.Context.SplitPoint; if (split == null) split = line.Evaluate(0.5, true); if (split.DistanceTo(line.GetEndPoint(0)) < ModifyObjectElementHelpers.Millimeters(1) || split.DistanceTo(line.GetEndPoint(1)) < ModifyObjectElementHelpers.Millimeters(1)) return new ModifyObjectResult { Status = ModifyObjectStatus.INVALID_SPLIT_POINT };
            Level level = doc.GetElement(source.LevelId) as Level; FamilySymbol symbol = doc.GetElement(source.GetTypeId()) as FamilySymbol; var result = new ModifyObjectResult(); using (var tx = new Transaction(doc, "K-TOOLS Split Beam")) { tx.Start(); FamilyInstance first = doc.Create.NewFamilyInstance(Line.CreateBound(line.GetEndPoint(0), split), symbol, level, StructuralType.Beam); FamilyInstance second = doc.Create.NewFamilyInstance(Line.CreateBound(split, line.GetEndPoint(1)), symbol, level, StructuralType.Beam); string conflict; ModifyObjectElementHelpers.TryCopySafeParameters(source, first, out conflict); ModifyObjectElementHelpers.TryCopySafeParameters(source, second, out conflict); doc.Regenerate(); if (first == null || second == null) { tx.RollBack(); return new ModifyObjectResult { Status = ModifyObjectStatus.FAILED }; } doc.Delete(source.Id); tx.Commit(); result.CreatedElementIds.Add(first.Id); result.CreatedElementIds.Add(second.Id); }
            result.DeletedSourceIds.Add(source.Id); result.Status = ModifyObjectStatus.DELETED_SOURCE; result.Summary = "Beam split into two straight framing instances."; return result;
        }
    }
}
