using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>Read-only MEP-to-structure solid clash review. This command does not create openings.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepOpenings : IExternalCommand
    {
        private const double DefaultClearanceMm = 50;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var timer = Stopwatch.StartNew();
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;
            try
            {
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                {
                    message = "Open a model view before running MEP clash review.";
                    return Result.Failed;
                }
                var selected = uidoc.Selection.GetElementIds().OrderBy(x => x.ToLongValue())
                    .Select(doc.GetElement).OfType<MEPCurve>().ToList();
                bool selectionWasMep = selected.Count > 0;
                IList<MEPCurve> requested = selectionWasMep
                    ? selected
                    : new FilteredElementCollector(doc, view.Id)
                        .WherePasses(new ElementMulticategoryFilter(new[]
                        {
                            BuiltInCategory.OST_DuctCurves,
                            BuiltInCategory.OST_PipeCurves,
                            BuiltInCategory.OST_CableTray
                        }))
                        .WhereElementIsNotElementType().Cast<MEPCurve>().OrderBy(x => x.Id.ToLongValue()).ToList();
                if (requested.Count == 0)
                {
                    TaskDialog.Show("KhimMEP — Clash Review", LanguageManager.IsEnglish
                        ? "No ducts, pipes, or cable trays were selected or found in the active view."
                        : "Không tìm thấy ống gió, ống nước hoặc máng cáp trong vùng chọn/View hiện hành.");
                    return Result.Cancelled;
                }

                var result = MepOpeningAnalysisService.Analyze(doc, view, requested, DefaultClearanceMm);
                timer.Stop();
                MepWorkflowDiagnostics.Log(nameof(CmdMepOpenings), doc, "read-only-opening-clash-review",
                    result.Requested, result.Eligible, result.Processed, 0,
                    result.Skipped + result.HostGeometrySkipped, result.Failed + result.HostGeometryFailed, timer.Elapsed);

                var lines = result.Clashes.Take(10).Select(clash => string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "{0} {1} ↔ {2} {3}: {4:0} × {5:0} mm near ({6:0}, {7:0}, {8:0}) mm",
                    clash.MepCategory, clash.MepElementId.ToLongValue(), clash.HostCategory,
                    clash.HostElementId.ToLongValue(), clash.RecommendedWidthMm, clash.RecommendedHeightMm,
                    UnitUtils.ConvertFromInternalUnits(clash.ApproximateCenter.X, UnitTypeId.Millimeters),
                    UnitUtils.ConvertFromInternalUnits(clash.ApproximateCenter.Y, UnitTypeId.Millimeters),
                    UnitUtils.ConvertFromInternalUnits(clash.ApproximateCenter.Z, UnitTypeId.Millimeters)));
                string report = LanguageManager.IsEnglish
                        ? $"Read-only MEP clash review (50 mm clearance each side)\nMEP requested: {result.Requested}; eligible: {result.Eligible}; processed: {result.Processed}; skipped: {result.Skipped}; failed: {result.Failed}\nStructural hosts: {result.HostCount}; host geometry skipped: {result.HostGeometrySkipped}; host geometry failed: {result.HostGeometryFailed}\nExact solid clashes: {result.Clashes.Count}\n\n" +
                      string.Join("\n", lines) + (result.Clashes.Count > 10 ? "\n…" : string.Empty) +
                      "\n\nRecommended sizes are estimates for coordination only. No model elements or openings were created."
                    : $"MEP шалгалт (зөвхөн унших, тал бүрд 50 мм зай)\nMEP хүссэн: {result.Requested}; тохирох: {result.Eligible}; боловсруулсан: {result.Processed}; алгассан: {result.Skipped}; алдаа: {result.Failed}\nБүтээцийн host: {result.HostCount}; геометргүй: {result.HostGeometrySkipped}; geometry алдаа: {result.HostGeometryFailed}\nБодит solid огтлолцол: {result.Clashes.Count}\n\n" +
                      string.Join("\n", lines) + (result.Clashes.Count > 10 ? "\n…" : string.Empty) +
                      "\n\nЗөвлөсөн хэмжээ нь coordination тооцоо; моделд нүх болон элемент үүсгээгүй.";
                TaskDialog.Show("KhimMEP — Clash Review", report);
                return result.Eligible == 0 || ((result.Failed + result.HostGeometryFailed) == result.Requested && result.Requested > 0)
                    ? Result.Failed : Result.Succeeded;
            }
            catch (Exception ex)
            {
                timer.Stop();
                message = ex.Message;
                MepWorkflowDiagnostics.Log(nameof(CmdMepOpenings), doc, "read-only-opening-clash-review",
                    0, 0, 0, 0, 0, 1, timer.Elapsed, ex.GetType().FullName);
                TaskDialog.Show("KhimMEP Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
