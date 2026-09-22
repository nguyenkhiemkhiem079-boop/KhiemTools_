using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.ParameterManager.Forms;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterManager.Services;

namespace KhimTools.ParameterManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdParameterManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData == null || commandData.Application == null ? null : commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null) return Result.Cancelled;
            IList<ElementId> selected = uidoc.Selection.GetElementIds().ToList(); ElementId activeViewId = uidoc.ActiveView == null ? ElementId.InvalidElementId : uidoc.ActiveView.Id;
            using (var form = new ParameterManagerForm(uidoc.Document, activeViewId, selected))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Request == null) return Result.Cancelled;
                ParameterManagerPlan plan = form.Plan ?? ParameterManagerPlanner.BuildPlan(uidoc.Document, form.Request);
                ParameterManagerPreflightResult preflight = ParameterManagerPreflightService.Validate(uidoc.Document, plan);
                if (!preflight.IsValid && form.Request.Options.AllOrNothing) { TaskDialog.Show("Parameter Manager Preflight", string.Join(Environment.NewLine, preflight.Errors)); return Result.Cancelled; }
                ParameterManagerResult result = ParameterManagerExecutionService.Execute(uidoc.Document, plan);
                TaskDialog.Show("Parameter Manager 2.0", result.Summary + Environment.NewLine + "Verification: " + (result.VerificationPassed ? "PASS" : "DEFERRED/FAIL"));
            }
            return Result.Succeeded;
        }
    }
}
