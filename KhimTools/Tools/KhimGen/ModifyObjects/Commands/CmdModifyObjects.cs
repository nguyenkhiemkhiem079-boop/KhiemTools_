using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.Forms;
using KhimTools.ModifyObjects.General;
using KhimTools.ModifyObjects.Structural;
using KhimTools.ModifyObjects.Wall;

namespace KhimTools.ModifyObjects.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdModifyObjects : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData == null || commandData.Application == null ? null : commandData.Application.ActiveUIDocument; if (uidoc == null || uidoc.Document == null) return Result.Cancelled; IList<ElementId> selected = uidoc.Selection.GetElementIds().ToList();
            using (var form = new ModifyObjectsForm(uidoc.Document, selected))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Plan == null) return Result.Cancelled;
                if (form.Plan.Context.PreviewOnly) return Result.Cancelled; // Preview never writes model state.
                ModifyObjectResult result = ExecutePlan(uidoc.Document, form.Plan); TaskDialog.Show("Modify Objects 2.0", result.Summary ?? result.Status.ToString() + Environment.NewLine + (result.Message ?? string.Empty));
            }
            return Result.Succeeded;
        }
        private static ModifyObjectResult ExecutePlan(Document doc, ModifyObjectPlan plan)
        {
            ModifyObjectPreflightResult preflight = ModifyObjectPreflightService.Validate(doc, plan);
            if (!preflight.IsValid) return new ModifyObjectResult { Status = preflight.Statuses.Count == 0 ? ModifyObjectStatus.FAILED : preflight.Statuses[0], Message = string.Join(Environment.NewLine, preflight.Errors) };
            switch (plan.Context.Operation)
            {
                case ModifyObjectOperation.MOVE_3D: return Move3DService.Execute(doc, plan);
                case ModifyObjectOperation.ARRAY_3D: return Array3DService.Execute(doc, plan);
                case ModifyObjectOperation.CREATE_PARTS: return PartsService.Execute(doc, plan);
                case ModifyObjectOperation.COLUMN_SPLIT: return ColumnSplitService.Execute(doc, plan);
                case ModifyObjectOperation.COLUMN_JOIN: return ColumnJoinService.Execute(doc, plan);
                case ModifyObjectOperation.BEAM_SPLIT: return BeamSplitService.Execute(doc, plan);
                case ModifyObjectOperation.BEAM_JOIN: return BeamJoinService.Execute(doc, plan);
                case ModifyObjectOperation.SLAB_JOIN: return SlabJoinAdapter.Execute(doc, plan);
                case ModifyObjectOperation.SLAB_SPLIT: return SlabSplitService.Execute(doc, plan);
                case ModifyObjectOperation.WALL_SPLIT: return WallSplitService.Execute(doc, plan);
                case ModifyObjectOperation.WALL_TRIM: return WallTrimService.Execute(doc, plan);
                case ModifyObjectOperation.WALL_OPENING: return WallOpeningService.Execute(doc, plan);
                case ModifyObjectOperation.COLUMN_BASE_ELEVATION: return ColumnBaseElevationService.Execute(doc, plan);
                default: return new ModifyObjectResult { Status = ModifyObjectStatus.UNSUPPORTED_ELEMENT, Message = "Operation is not implemented." };
            }
        }
    }
}
