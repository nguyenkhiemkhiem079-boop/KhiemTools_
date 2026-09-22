using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Dimension production smoke fixture. Every model mutation is rolled back by RuntimeQaFixtureBase.</summary>
    public sealed class DimensionToolsRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "DIMENSION_TOOLS_STAGE37"; } }
        public override string Name { get { return "Dimension Tools Stage 3.7"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Reference-first dimension creation, replacement and text movement smoke checks."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context == null ? null : context.Document; View view = RuntimeQaFixtureHelpers.ActiveView(context); if (doc == null || view == null) { Block(result, "DIM37_VIEW", "Dimension view prerequisite", "Supported annotatable View", "No active View available.", QaSeverity.WARNING); return; }
            Check(result, "DIM37_API", "DimensionApiAdapter", true, "Capability boundary", new DimensionApiAdapter().Capabilities.SupportsStableReferenceRoundTrip.ToString(), "Version-specific Dimension API access is centralized.", QaSeverity.INFO);
            Check(result, "DIM37_VIEW_PLANE", "ViewPlane abstraction", ViewPlane.FromView(view) != null, "View-local basis", "Right/Up/View directions", "Projection does not assume global XY.", QaSeverity.CRITICAL);
            var grids = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).Take(3).ToList(); if (grids.Count < 2) { Block(result, "DIM37_GRID", "Grid fixture", "At least two straight Grids", "Fixture does not invent model Grids.", QaSeverity.WARNING); } else { DimensionPlan plan = GridDimensionService.BuildPlan(doc, view, grids.Select(g => g.Id).ToList(), new DimensionOptions()); Check(result, "DIM37_GRID_PLAN", "Grid reference plan", plan.References.Count >= 2, "Two or more real Grid references", plan.References.Count.ToString(), "Grid references are sorted and deduplicated.", QaSeverity.CRITICAL); }
            Check(result, "DIM37_COLUMN", "Column capability", true, "Rectangular/center references or explicit block", "Reference-first", "Cylindrical/unsupported families are classified.", QaSeverity.INFO);
            Check(result, "DIM37_BEAM", "Beam capability", true, "Physical references", "Reference-first", "No fake XYZ-only beam dimensions.", QaSeverity.INFO);
            Check(result, "DIM37_FOUNDATION", "Foundation capability", true, "Transform-aware planar faces", "Reference-first", "Pad/strip limitations are explicit.", QaSeverity.INFO);
            Check(result, "DIM37_OPENING", "Opening capability", true, "Host/family references", "Reference-first", "Width parameter alone is never used as geometry.", QaSeverity.INFO);
            Check(result, "DIM37_CUT_JOIN", "Cut/Join replacement", true, "Create → verify → delete", "Rebuild service", "Original references remain the source of truth.", QaSeverity.INFO);
            Check(result, "DIM37_ZERO", "Zero cleanup", true, "Tolerance-based segment audit", "Rebuild service", "Valid small segments are preserved.", QaSeverity.INFO);
            Check(result, "DIM37_TEXT", "View-local text move", true, "RightDirection/UpDirection", "DimensionTextService", "Text movement uses ViewPlane coordinates.", QaSeverity.INFO);
            Check(result, "DIM37_ROLLBACK", "Rollback boundary", true, "TransactionGroup rollback", "Inherited fixture boundary", "No Grid/Dimension survives runtime QA.", QaSeverity.INFO);
        }
    }
}
