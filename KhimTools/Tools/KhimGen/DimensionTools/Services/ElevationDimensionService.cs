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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.ELEVATION, Options = options ?? new DimensionOptions() }; context.Options.Axis = DimensionAxis.VERTICAL_IN_VIEW; IEnumerable<Level> levels = selectedLevelIds == null || selectedLevelIds.Count == 0 ? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>() : selectedLevelIds.Select(id => doc.GetElement(id) as Level).Where(x => x != null); foreach (Level level in levels) { try { Reference reference = new Reference(level); var info = new DimensionReferenceInfo { ElementId = level.Id, ElementUniqueId = level.UniqueId, Reference = reference, StableRepresentation = new DimensionApiAdapter().ConvertStableReference(doc, reference), ReferenceKind = "LEVEL", SourceRole = DimensionReferenceRole.LEVEL, WorldPoint = new XYZ(view.Origin.X, view.Origin.Y, level.Elevation), IsValid = true }; info.GeometryFingerprint = DimensionReferenceService.ComputeLiveGeometryFingerprint(doc, info, reference); context.References.Add(info); } catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] level reference: " + ex.Message); } } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult CreateLevelChain(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
        public static DimensionResult CreateSpotElevation(Document doc, View view, Reference reference, XYZ origin, XYZ bend, XYZ end, XYZ refPoint, ElementId typeId)
        {
            var result = new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION };
            if (doc == null || view == null || reference == null || origin == null || bend == null || end == null || refPoint == null) { result.Status = DimensionStatus.REFERENCE_INVALID; result.Message = "Spot Elevation requires a valid View, face Reference and points."; return result; }
            Element source = doc.GetElement(reference.ElementId); GeometryObject geometry = null; try { geometry = source == null ? null : source.GetGeometryObjectFromReference(reference); } catch (System.Exception ex) { result.Status = DimensionStatus.REFERENCE_INVALID; result.Message = "Spot Elevation face Reference is invalid: " + ex.Message; return result; } if (!(geometry is Face)) { result.Status = DimensionStatus.REFERENCE_INVALID; result.Message = "Spot Elevation requires a dimensionable face Reference."; return result; }
            using (var group = new TransactionGroup(doc, "K-TOOLS Spot Elevation")) { KhimTools.Core.Revit.TransactionBoundary.Start(group, "DimensionTools.SpotElevation"); try { using (var tx = new Transaction(doc, "Create Spot Elevation")) { KhimTools.Core.Revit.TransactionBoundary.Start(tx, "DimensionTools.SpotElevation"); SpotDimension spot = new DimensionApiAdapter().CreateSpotElevation(doc, view, reference, origin, bend, end, refPoint); if (spot == null) { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "DimensionTools.SpotElevation"); result.Status = DimensionStatus.FAILED; result.Message = "NewSpotElevation returned null."; KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "DimensionTools.SpotElevation"); return result; } if (typeId != null && typeId != ElementId.InvalidElementId) spot.ChangeTypeId(typeId); doc.Regenerate(); if (doc.GetElement(spot.Id) == null || spot.OwnerViewId != view.Id) { KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "DimensionTools.SpotElevation"); KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "DimensionTools.SpotElevation"); result.Status = DimensionStatus.POST_VERIFY_FAILED; result.Message = "Spot Elevation post verification failed."; return result; } KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "DimensionTools.SpotElevation"); result.CreatedDimensionIds.Add(spot.Id); result.Status = DimensionStatus.CREATED; result.VerificationPassed = true; result.Message = "Spot Elevation created and verified."; } KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "DimensionTools.SpotElevation"); } catch (System.Exception ex) { KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "DimensionTools.SpotElevation"); result.Status = DimensionStatus.FAILED; result.Message = ex.Message; } } return result;
        }
    }
}
