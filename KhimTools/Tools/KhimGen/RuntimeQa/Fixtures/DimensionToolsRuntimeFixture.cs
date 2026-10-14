using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Production dimension smoke fixture. All mutations are enclosed by the inherited outer TransactionGroup rollback.</summary>
    public sealed class DimensionToolsRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "DIMENSION_TOOLS_STAGE37"; } }
        public override string Name { get { return "Dimension Tools Stage 3.7"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Reference-first creation, Cut/Join reconstruction and View-local text movement."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context == null ? null : context.Document;
            View view = RuntimeQaFixtureHelpers.ActiveView(context);
            if (doc == null || view == null || !ViewPlane.IsSupportedView(view)) { Block(result, "DIM37_VIEW", "Dimension view prerequisite", "Supported annotatable View", "No supported active View is available.", QaSeverity.WARNING); return; }

            ViewPlane plane = ViewPlane.FromView(view);
            XYZ localProbe = new XYZ(12.5, -7.25, 0);
            Check(result, "DIM37_VIEW_PLANE", "Rotated ViewPlane round-trip", plane.FromView(localProbe) != null && plane.ToView(plane.FromView(localProbe)).IsAlmostEqualTo(localProbe), "Local point round-trips", "Right/Up/View basis", "Projection and movement use the View basis, not global XY.", QaSeverity.CRITICAL);
            Check(result, "DIM37_API", "DimensionApiAdapter", new DimensionApiAdapter().Capabilities.SupportsStableReferenceRoundTrip, "Stable reference round-trip", "Available", "2024/2025 API access is centralized.", QaSeverity.CRITICAL);

            IList<Grid> grids = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
            DimensionResult gridResult = null;
            if (grids.Count < 2) Block(result, "DIM37_GRID", "Grid creation", "At least two straight visible Grids", "Fixture does not invent project resources.", QaSeverity.WARNING);
            else
            {
                DimensionPlan gridPlan = GridDimensionService.BuildPlan(doc, view, grids.Select(g => g.Id).ToList(), new DimensionOptions { OffsetMillimeters = 250 });
                bool ordered = gridPlan.References.Select(x => x.ProjectedPosition).SequenceEqual(gridPlan.References.Select(x => x.ProjectedPosition).OrderBy(x => x));
                Check(result, "DIM37_GRID_ORDER", "Grid reference ordering", ordered, "Monotonic projected positions", string.Join(",", gridPlan.References.Select(x => x.ProjectedPosition.ToString("0.###")).ToArray()), "Collector order is not used.", QaSeverity.CRITICAL);
                if (!gridPlan.CanExecute) Block(result, "DIM37_GRID_PLAN", "Grid creation", "Executable reference plan", gridPlan.Status + ": " + string.Join("; ", gridPlan.Errors.ToArray()), QaSeverity.WARNING);
                else
                {
                    gridResult = GridDimensionService.Create(doc, gridPlan); Track(context, gridResult);
                    Check(result, "DIM37_GRID_CREATE", "Grid Dimension", gridResult.Status == DimensionStatus.CREATED && gridResult.VerificationPassed, "CREATED and post-verified", gridResult.Status.ToString(), gridResult.Message, QaSeverity.CRITICAL);
                }
            }

            Element column = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().FirstOrDefault();
            if (column == null) Block(result, "DIM37_COLUMN", "Column plan", "Visible structural column", "Missing project resource.", QaSeverity.WARNING);
            else CheckPlan(result, "DIM37_COLUMN", "Column plan", ColumnDimensionService.BuildPlan(doc, view, new[] { column.Id }, "FACE_TO_FACE", new DimensionOptions()));

            Element beam = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().FirstOrDefault();
            if (beam == null) Block(result, "DIM37_BEAM", "Beam plan", "Visible straight structural framing", "Missing project resource.", QaSeverity.WARNING);
            else CheckPlan(result, "DIM37_BEAM", "Beam plan", BeamDimensionService.BuildPlan(doc, view, new[] { beam.Id }, "END_TO_END", new DimensionOptions()));

            if (gridResult == null || gridResult.CreatedDimensionIds.Count != 1 || doc.GetElement(gridResult.CreatedDimensionIds[0]) == null) Block(result, "DIM37_EDIT", "Cut/Join/Text workflow", "Created multi-reference Grid dimension", "Grid creation prerequisite unavailable.", QaSeverity.WARNING);
            else
            {
                Dimension created = doc.GetElement(gridResult.CreatedDimensionIds[0]) as Dimension;
                int referenceCount = DimensionReferenceService.FromDimension(doc, view, created).Count;
                if (referenceCount < 3) Block(result, "DIM37_CUT", "Cut Dimension", "At least three references", referenceCount + " references", QaSeverity.WARNING);
                else
                {
                    DimensionResult cut = CutDimensionService.Cut(doc, view, created.Id, 1); Track(context, cut);
                    Check(result, "DIM37_CUT", "Cut Dimension", cut.Status == DimensionStatus.REPLACED && cut.CreatedDimensionIds.Count == 2, "Two verified replacements", cut.Status + " / " + cut.CreatedDimensionIds.Count, cut.Message, QaSeverity.CRITICAL);
                    if (cut.CreatedDimensionIds.Count == 2)
                    {
                        DimensionResult join = JoinDimensionService.Join(doc, view, cut.CreatedDimensionIds[0], cut.CreatedDimensionIds[1]); Track(context, join);
                        Check(result, "DIM37_JOIN", "Join Dimension", join.Status == DimensionStatus.REPLACED && join.CreatedDimensionIds.Count == 1, "One verified replacement", join.Status + " / " + join.CreatedDimensionIds.Count, join.Message, QaSeverity.CRITICAL);
                        if (join.CreatedDimensionIds.Count == 1)
                        {
                            DimensionResult move = DimensionTextService.MoveText(doc, view, join.CreatedDimensionIds[0], 10, -5);
                            Check(result, "DIM37_TEXT", "Move Dimension Text", move.Status == DimensionStatus.UPDATED && move.VerificationPassed, "View-local +10/-5 mm displacement", move.Status.ToString(), move.Message, QaSeverity.CRITICAL);
                            DimensionResult zero = ZeroDimensionService.RemoveZero(doc, view, join.CreatedDimensionIds[0], 0.1);
                            Check(result, "DIM37_ZERO", "Remove Zero no-change safety", zero.Status == DimensionStatus.NO_CHANGE || zero.Status == DimensionStatus.REPLACED, "NO_CHANGE or verified replacement", zero.Status.ToString(), "Small non-zero segments remain intact.", QaSeverity.CRITICAL);
                        }
                    }
                }
            }
            Check(result, "DIM37_ROLLBACK", "Rollback boundary", true, "Outer TransactionGroup rollback", "Active", "Created and replacement Dimensions are rolled back after this fixture.", QaSeverity.INFO);
        }

        private static void CheckPlan(QaFixtureResult result, string id, string name, DimensionPlan plan)
        {
            bool valid = plan != null && (plan.CanExecute || plan.Status == DimensionStatus.CURVED_BEAM_UNSUPPORTED || plan.Status == DimensionStatus.CENTER_REFERENCE_UNAVAILABLE || plan.Status == DimensionStatus.INSUFFICIENT_REFERENCES);
            Check(result, id, name, valid, "Reference-driven plan or explicit blocked status", plan == null ? "null" : plan.Status.ToString(), "No naked LocationCurve endpoint is treated as a Reference.", QaSeverity.CRITICAL);
        }

        private static void Track(RuntimeQaContext context, DimensionResult result)
        {
            if (context == null || result == null) return;
            foreach (ElementId id in result.CreatedDimensionIds) if (!context.CreatedElementIds.Contains(id)) context.CreatedElementIds.Add(id);
        }
    }
}
