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
                case DimensionOperation.QUICK: return QuickDimensionService.Create(doc, plan);
                case DimensionOperation.GENERAL: return GeneralDimensionService.Create(doc, plan);
                default: return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "Choose an automatic dimension operation for creation; edit operations require a selected Dimension." };
            }
        }
    }
}
