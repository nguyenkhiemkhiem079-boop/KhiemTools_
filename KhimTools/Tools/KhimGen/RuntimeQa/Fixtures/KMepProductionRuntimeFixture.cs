using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using KhimTools.Core;
using KhimTools.MEP;
using KhimTools.MEP.Penetrations;
using KhimTools.MEP.Tags;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Exercises read-only clash analysis and elevation-note creation inside the host's rollback group.</summary>
    public sealed class KMepProductionRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "KMEP_ANALYSIS_AND_NOTES"; } }
        public override string Name { get { return "K-MEP Solid Clash Analysis and Elevation Notes"; } }
        public override string Suite { get { return "MEP"; } }
        public override string Description { get { return "Checks read-only duct/pipe/tray solid clash analysis, then creates view notes through the production service and verifies transaction-group rollback."; } }
        public override bool IsCritical { get { return true; } }

        public override bool CanRun(RuntimeQaContext context, out string reason)
        {
            if (!base.CanRun(context, out reason)) return false;
            View view = context.Document.ActiveView;
            if (view == null || view.IsTemplate || (!(view is ViewPlan) && !(view is ViewSection)))
            { reason = "Open a plan, section, or elevation model view."; return false; }
            if (!new FilteredElementCollector(context.Document).OfClass(typeof(TextNoteType)).Any())
            { reason = "The project must contain a TextNoteType."; return false; }
            if (!GetMep(context).Any())
            { reason = "Select or show at least one supported duct, pipe, or cable tray in the active view."; return false; }
            if (!new FilteredElementCollector(context.Document, view.Id)
                .WherePasses(new ElementMulticategoryFilter(new[] { BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls }))
                .WhereElementIsNotElementType().Any())
            { reason = "Show at least one structural framing, floor, or wall host in the active view."; return false; }
            reason = string.Empty;
            return true;
        }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            View view = doc.ActiveView;
            var mep = GetMep(context).ToList();
            RuntimeQaModelFingerprint beforeAnalysis = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            MepOpeningAnalysisResult analysis = MepOpeningAnalysisService.Analyze(doc, view, mep, 50);
            RuntimeQaModelFingerprint afterAnalysis = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            bool readOnly = SameModel(beforeAnalysis, afterAnalysis);
            Check(result, "KMEP_ANALYSIS_READ_ONLY", "Solid clash analysis does not change the model",
                readOnly, "Identical element/view/sheet fingerprint", "same=" + readOnly + ";clashes=" + analysis.Clashes.Count,
                "MepOpeningAnalysisService is intentionally transaction-free.", QaSeverity.CRITICAL);
            if (analysis.Clashes.Count == 0)
            {
                Block(result, "KMEP_CLASH_PREREQUISITE", "Exact solid clash fixture", "At least one duct/pipe/tray solid intersects a structural host solid",
                    "No exact MEP-host solid clash exists among selected/visible fixture elements.");
                return;
            }
            Check(result, "KMEP_CLASH_DIMENSIONS", "Exact clash exposes positive clearance recommendation",
                analysis.Clashes.All(x => x.RecommendedWidthMm > 0 && x.RecommendedHeightMm > 0),
                "Positive width and height in mm", "clashes=" + analysis.Clashes.Count,
                "Dimensions are derived from supported section parameters and 50 mm clearance each side.", QaSeverity.CRITICAL);

            MepElevationNoteResult notes = MepElevationNoteService.Create(doc, view, mep);
            foreach (ElementId id in notes.CreatedElementIds) context.TrackCreated(id);
            bool resolved = notes.CreatedElementIds.Count == notes.Created &&
                notes.CreatedElementIds.All(id => doc.GetElement(id) is TextNote note && note.OwnerViewId == view.Id);
            Check(result, "KMEP_ELEVATION_NOTES", "Production service creates view-owned BOP/invert annotations",
                notes.Created > 0 && resolved, "One or more resolvable TextNotes owned by active view",
                "created=" + notes.Created + ";skipped=" + notes.Skipped + ";resolved=" + resolved,
                "MepElevationNoteService verifies note text and view ownership before committing.", QaSeverity.CRITICAL);
            Check(result, "KMEP_OUTER_ROLLBACK", "Harness tracks annotation elements for cleanup",
                notes.CreatedElementIds.All(id => context.CreatedElementIds.Contains(id)),
                "All created note IDs tracked", "tracked=" + context.CreatedElementIds.Count,
                "RuntimeQaFixtureBase rolls back the outer TransactionGroup and verifies the model fingerprint.", QaSeverity.INFO);
        }

        private static System.Collections.Generic.IEnumerable<MEPCurve> GetMep(RuntimeQaContext context)
        {
            Document doc = context.Document;
            var selected = context.UiDocument.Selection.GetElementIds().Select(doc.GetElement).OfType<MEPCurve>()
                .Where(MepCurveMeasurementService.IsSupported);
            if (selected.Any()) return selected;
            return new FilteredElementCollector(doc, doc.ActiveView.Id)
                .WherePasses(new ElementMulticategoryFilter(new[]
                {
                    BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_CableTray
                })).WhereElementIsNotElementType().Cast<MEPCurve>().Where(MepCurveMeasurementService.IsSupported);
        }

        private static bool SameModel(RuntimeQaModelFingerprint left, RuntimeQaModelFingerprint right) =>
            left != null && right != null && left.ElementCount == right.ElementCount && left.ViewCount == right.ViewCount &&
            left.SheetCount == right.SheetCount && left.ElementIds.Select(x => x.ToLongValue()).OrderBy(x => x)
                .SequenceEqual(right.ElementIds.Select(x => x.ToLongValue()).OrderBy(x => x));
    }
}
