using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.MEP.Tags
{
    /// <summary>Create idempotent, view-scoped BOP/invert and top elevation text annotations for visible MEP curves.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepElevationTags : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var timer = Stopwatch.StartNew();
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;
            try
            {
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate || (!(view is ViewPlan) && !(view is ViewSection)))
                {
                    message = "Open a plan, section, or elevation view to create MEP elevation notes.";
                    return Result.Failed;
                }
                var selected = uidoc.Selection.GetElementIds().OrderBy(x => x.ToLongValue())
                    .Select(doc.GetElement).OfType<MEPCurve>().ToList();
                var requested = selected.Count > 0
                    ? selected
                    : new FilteredElementCollector(doc, view.Id)
                        .WherePasses(new ElementMulticategoryFilter(new[]
                        {
                            BuiltInCategory.OST_DuctCurves,
                            BuiltInCategory.OST_PipeCurves,
                            BuiltInCategory.OST_CableTray
                        }))
                        .WhereElementIsNotElementType().Cast<MEPCurve>().OrderBy(x => x.Id.ToLongValue()).ToList();
                if (requested.Count == 0) return Result.Cancelled;

                MepElevationNoteResult result = MepElevationNoteService.Create(doc, view, requested);
                timer.Stop();
                MepWorkflowDiagnostics.Log(nameof(CmdMepElevationTags), doc, "create-mep-elevation-notes",
                    result.Requested, result.Created + result.Skipped + result.Failed, result.Created,
                    result.Created, result.Skipped, result.Failed, timer.Elapsed,
                    transactionState: result.Created > 0 ? "COMMITTED" : "NOT_STARTED_OR_ROLLED_BACK",
                    postcondition: result.Created > 0 ? "PASSED" : "NO_CHANGE");
                string report = LanguageManager.IsEnglish
                    ? $"MEP elevation notes\nRequested: {result.Requested}; created: {result.Created}; already present/unsupported: {result.Skipped}; failed: {result.Failed}.\nNotes show BOP/invert, top, and section size."
                    : $"Түвшний annotation\nХүссэн: {result.Requested}; үүсгэсэн: {result.Created}; өмнө байсан/дэмжигдээгүй: {result.Skipped}; алдаа: {result.Failed}.\nAnnotation-д BOP/invert, дээд түвшин болон огтлолын хэмжээ орно.";
                TaskDialog.Show("KhimMEP — Elevation Notes", report);
                if (result.Created == 0 && result.Skipped == 0 && result.Failed > 0) return Result.Failed;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                timer.Stop();
                message = ex.Message;
                MepWorkflowDiagnostics.Log(nameof(CmdMepElevationTags), doc, "create-mep-elevation-notes",
                    0, 0, 0, 0, 0, 1, timer.Elapsed, ex.GetType().FullName,
                    "UNKNOWN", "NOT_RUN");
                TaskDialog.Show("KhimMEP Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
