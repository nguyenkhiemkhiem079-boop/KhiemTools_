using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Forms;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;

namespace KhimTools.DimensionTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdDimensionTools : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData == null || commandData.Application == null ? null : commandData.Application.ActiveUIDocument; if (uidoc == null || uidoc.Document == null) return Result.Cancelled; View view = uidoc.ActiveView; IList<ElementId> selection = uidoc.Selection.GetElementIds().ToList(); if (!ViewPlane.IsSupportedView(view)) { TaskDialog.Show("Dimension Tools", "UNSUPPORTED_VIEW_TYPE"); return Result.Cancelled; }
            using (var form = new DimensionToolsForm(uidoc.Document, view, selection)) { if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Plan == null) return Result.Cancelled; DimensionResult result = ExecutePlan(uidoc.Document, form.Plan); TaskDialog.Show("Dimension Tools 2.0", result.Status + Environment.NewLine + (result.Message ?? string.Empty)); }
            return Result.Succeeded;
        }
        private static DimensionResult ExecutePlan(Document doc, DimensionPlan plan)
        {
            switch (plan.Operation)
            {
                case DimensionOperation.GRID: return GridDimensionService.Create(doc, plan);
                case DimensionOperation.COLUMN: return ColumnDimensionService.Create(doc, plan);
                case DimensionOperation.BEAM: return BeamDimensionService.Create(doc, plan);
                case DimensionOperation.FOUNDATION: return FoundationDimensionService.Create(doc, plan);
                case DimensionOperation.OPENING: return OpeningDimensionService.Create(doc, plan);
                case DimensionOperation.ELEVATION: return ElevationDimensionService.CreateLevelChain(doc, plan);
                case DimensionOperation.SPOT_ELEVATION: return CreateSpot(doc, plan);
                case DimensionOperation.QUICK: return QuickDimensionService.Create(doc, plan);
                case DimensionOperation.GENERAL: return GeneralDimensionService.Create(doc, plan);
                case DimensionOperation.CUT: return plan.Context.DimensionIds.Count == 1 ? CutDimensionService.Cut(doc, plan.Context.View, plan.Context.DimensionIds[0], plan.Context.Options.BoundaryIndex) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "CUT requires one selected Dimension." };
                case DimensionOperation.JOIN: return plan.Context.DimensionIds.Count == 2 ? JoinDimensionService.Join(doc, plan.Context.View, plan.Context.DimensionIds[0], plan.Context.DimensionIds[1]) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "JOIN requires two selected Dimensions." };
                case DimensionOperation.REMOVE_ZERO: return plan.Context.DimensionIds.Count == 1 ? ZeroDimensionService.RemoveZero(doc, plan.Context.View, plan.Context.DimensionIds[0], plan.Context.Options.ToleranceMillimeters) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "REMOVE_ZERO requires one selected Dimension." };
                case DimensionOperation.MOVE_TEXT: if (plan.Context.DimensionIds.Count != 1) return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "MOVE_TEXT requires one selected Dimension." }; double horizontal = plan.Context.Options.Axis == DimensionAxis.VERTICAL_IN_VIEW ? 0 : plan.Context.Options.OffsetMillimeters; double vertical = plan.Context.Options.Axis == DimensionAxis.VERTICAL_IN_VIEW ? plan.Context.Options.OffsetMillimeters : 0; return DimensionTextService.MoveText(doc, plan.Context.View, plan.Context.DimensionIds[0], horizontal, vertical);
                default: return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "Select a supported operation and valid references." };
            }
        }
        private static DimensionResult CreateSpot(Document doc, DimensionPlan plan)
        {
            if (plan.Context.References.Count != 1) return new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION, Status = DimensionStatus.REFERENCE_NOT_FOUND, Message = "Spot Elevation requires one valid face Reference." };
            DimensionReferenceInfo info = plan.Context.References[0]; ViewPlane plane = ViewPlane.FromView(plan.Context.View); XYZ target = info.WorldPoint ?? XYZ.Zero; double offset = DimensionGeometryService.Mm(Math.Abs(plan.Context.Options.OffsetMillimeters) < 0.1 ? 100 : plan.Context.Options.OffsetMillimeters); XYZ origin = target + plane.RightDirection * offset; XYZ bend = origin + plane.UpDirection * DimensionGeometryService.Mm(20); XYZ end = bend + plane.RightDirection * DimensionGeometryService.Mm(20); return ElevationDimensionService.CreateSpotElevation(doc, plan.Context.View, info.Reference, origin, bend, end, target, ElementId.InvalidElementId);
        }
    }
}
