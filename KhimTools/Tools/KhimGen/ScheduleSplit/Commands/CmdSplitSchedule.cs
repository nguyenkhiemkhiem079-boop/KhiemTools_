using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.ScheduleSplit.Forms;
using KhimTools.ScheduleSplit.Models;
using KhimTools.ScheduleSplit.Services;

namespace KhimTools.ScheduleSplit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdSplitSchedule : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument; if (uidoc == null) return Result.Cancelled;
            using (var form = new ScheduleSplitForm(uidoc.Document))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Request == null) return Result.Cancelled;
                ScheduleSplitPlan plan = ScheduleSplitPlanner.BuildPlan(uidoc.Document, form.Request);
                var diagnostics = ScheduleSplitPreflightService.Validate(uidoc.Document, plan, form.Request.Options);
                if (diagnostics.Any(d => d.Severity == ScheduleSplitSeverity.ERROR)) { TaskDialog.Show("Split Schedule Preflight", string.Join(Environment.NewLine, diagnostics.Where(d => d.Severity == ScheduleSplitSeverity.ERROR).Select(d => d.Status + ": " + d.Message).Distinct())); return Result.Cancelled; }
                ScheduleSplitExecutionResult result = ScheduleSplitExecutionService.Execute(uidoc.Document, plan, form.Request.Options);
                TaskDialog.Show("Split Schedule Result", "Status: " + result.Status + Environment.NewLine + "Segments: " + result.SegmentResults.Count + Environment.NewLine + string.Join(Environment.NewLine, result.Messages));
            }
            return Result.Succeeded;
        }
    }
}
