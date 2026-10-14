using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SheetCopy.Forms;
using KhimTools.SheetCopy.Models;
using KhimTools.SheetCopy.Services;

namespace KhimTools.SheetCopy.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdSheetCopy : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            using (var form = new SheetCopyForm(uidoc.Document))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Request == null) return Result.Cancelled;
                SheetCopyPlan plan = SheetCopyPlanner.BuildPlan(uidoc.Document, form.Request);
                var diagnostics = SheetCopyPreflightService.Validate(uidoc.Document, plan);
                if (diagnostics.Any(d => d.Severity == SheetCopySeverity.ERROR))
                {
                    TaskDialog.Show("Sheet Copy Preflight", string.Join(Environment.NewLine, diagnostics.Where(d => d.Severity == SheetCopySeverity.ERROR).Select(d => d.StatusCode + ": " + d.Message).Distinct()));
                    return Result.Cancelled;
                }
                SheetCopyBatchResult result = SheetCopyExecutionService.Execute(uidoc.Document, plan);
                TaskDialog.Show("Sheet Copy Result", "Requested: " + result.Requested + Environment.NewLine + "Created: " + result.Created + Environment.NewLine + "Partial: " + result.Partial + Environment.NewLine + "Blocked: " + result.Blocked + Environment.NewLine + "Failed: " + result.Failed);
            }
            return Result.Succeeded;
        }
    }
}
