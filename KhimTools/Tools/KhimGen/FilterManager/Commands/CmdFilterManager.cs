using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.FilterManager.Forms;
using KhimTools.FilterManager.Models;
using KhimTools.FilterManager.Services;

namespace KhimTools.FilterManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdFilterManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData == null || commandData.Application == null ? null : commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null) return Result.Cancelled;
            ElementId activeId = uidoc.ActiveView == null ? ElementId.InvalidElementId : uidoc.ActiveView.Id;
            using (var form = new FilterManagerForm(uidoc.Document, activeId))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Request == null) return Result.Cancelled;
                FilterCopyPlan plan = FilterCopyPlanner.BuildPlan(uidoc.Document, form.Request); var checks = FilterPreflightService.Validate(uidoc.Document, plan); var errors = checks.Where(x => x.IsError).ToList();
                if (errors.Count > 0 && !form.Request.Options.AllowPartialTargets) { TaskDialog.Show("Filter Manager Preflight", string.Join(Environment.NewLine, errors.Select(x => x.Code + ": " + x.Message))); return Result.Cancelled; }
                FilterManagerResult result = FilterExecutionService.Execute(uidoc.Document, plan); TaskDialog.Show("Filter Manager Result", "Requested: " + result.RequestedTargets + Environment.NewLine + "Applied: " + result.AppliedTargets + Environment.NewLine + "No change: " + result.NoChangeTargets + Environment.NewLine + "Failed: " + result.FailedTargets);
            }
            return Result.Succeeded;
        }
    }
}
