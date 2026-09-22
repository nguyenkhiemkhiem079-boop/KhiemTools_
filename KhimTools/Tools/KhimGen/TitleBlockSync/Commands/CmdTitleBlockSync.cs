using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.TitleBlockSync.Forms;
using KhimTools.TitleBlockSync.Models;
using KhimTools.TitleBlockSync.Services;

namespace KhimTools.TitleBlockSync.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdTitleBlockSync : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument; if (uidoc == null) return Result.Cancelled;
            using (var form = new TitleBlockSyncForm(uidoc.Document))
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Request == null) return Result.Cancelled;
                TitleBlockSyncPlan plan = TitleBlockSyncPlanner.BuildPlan(uidoc.Document, form.Request); var checks = TitleBlockSyncPreflightService.Validate(uidoc.Document, plan); var errors = checks.Where(c => c.Severity == TitleBlockSyncSeverity.ERROR).ToList(); if (errors.Count > 0 && !form.Request.Options.AllowPartialTarget) { TaskDialog.Show("Title Block Sync Preflight", string.Join(Environment.NewLine, errors.Select(x => x.Status + ": " + x.Message).Distinct())); return Result.Cancelled; }
                TitleBlockSyncBatchResult result = TitleBlockSyncExecutionService.Execute(uidoc.Document, plan); TaskDialog.Show("Title Block Sync Result", "Requested: " + result.Requested + Environment.NewLine + "Synced: " + result.Synced + Environment.NewLine + "No change: " + result.NoChange + Environment.NewLine + "Blocked: " + result.Blocked + Environment.NewLine + "Failed: " + result.Failed);
            }
            return Result.Succeeded;
        }
    }
}
