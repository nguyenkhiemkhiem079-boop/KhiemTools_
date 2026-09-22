using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class ElevationDimensionService
    {
        public static DimensionPlan BuildLevelPlan(Document doc, View view, IList<ElementId> selectedLevelIds, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.ELEVATION, Options = options ?? new DimensionOptions() }; IEnumerable<Level> levels = selectedLevelIds == null || selectedLevelIds.Count == 0 ? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>() : selectedLevelIds.Select(id => doc.GetElement(id) as Level).Where(x => x != null); foreach (Level level in levels) { try { context.References.Add(new DimensionReferenceInfo { ElementId = level.Id, ElementUniqueId = level.UniqueId, Reference = new Reference(level), ReferenceKind = "LEVEL", SourceRole = DimensionReferenceRole.LEVEL, WorldPoint = new XYZ(0, 0, level.Elevation), IsValid = true }); } catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] level reference: " + ex.Message); } } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult CreateLevelChain(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
        public static DimensionResult CreateSpotElevation(Document doc, View view, Reference reference, XYZ origin, XYZ bend, XYZ end, XYZ refPoint, ElementId typeId)
        {
            var result = new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION }; using (var group = new TransactionGroup(doc, "K-TOOLS Spot Elevation")) { group.Start(); try { using (var tx = new Transaction(doc, "Create Spot Elevation")) { tx.Start(); SpotDimension spot = new DimensionApiAdapter().CreateSpotElevation(doc, view, reference, origin, bend, end, refPoint); if (spot == null) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = "NewSpotElevation returned null."; group.RollBack(); return result; } if (typeId != null && typeId != ElementId.InvalidElementId) spot.ChangeTypeId(typeId); tx.Commit(); result.CreatedDimensionIds.Add(spot.Id); result.Status = DimensionStatus.CREATED; result.VerificationPassed = true; } group.Assimilate(); } catch (System.Exception ex) { group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = ex.Message; } } return result;
        }
    }
}
