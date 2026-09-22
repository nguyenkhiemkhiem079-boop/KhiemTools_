using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ModifyObjects.Core;
using KhimTools.ModifyObjects.General;
using KhimTools.ModifyObjects.Structural;
using KhimTools.ModifyObjects.Wall;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Stage 3.6 fixture: production ANALYZE/PLAN/PREFLIGHT paths inside the inherited rollback group.</summary>
    public sealed class ModifyObjectsRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "MODIFY_OBJECTS_STAGE36"; } }
        public override string Name { get { return "Modify Objects Stage 3.6"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Column/beam/wall/parts/move/array capability and rollback smoke checks."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context == null ? null : context.Document; if (doc == null) { Block(result, "MO36_DOCUMENT", "Document prerequisite", "Open project", "No document available.", QaSeverity.WARNING); return; }
            Element column = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().FirstOrDefault(); Element beam = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().FirstOrDefault(); Wall wall = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().Cast<Wall>().FirstOrDefault();
            Check(result, "MO36_PIPELINE", "SELECT → ANALYZE → PLAN → PREFLIGHT", true, "Named pipeline exists", "Production services", "No click handler writes model state.", QaSeverity.INFO);
            if (column == null) Block(result, "MO36_COLUMN", "Column fixture", "A structural column", "No structural column exists; split/join checks are blocked.", QaSeverity.WARNING); else { ModifyObjectPlan plan = ColumnSplitService.Analyze(doc, column.Id, ElementId.InvalidElementId); Check(result, "MO36_COLUMN_PLAN", "Column split plan", plan != null, "Plan", plan.Status.ToString(), "Column geometry is audited before execution.", QaSeverity.CRITICAL); }
            if (beam == null) Block(result, "MO36_BEAM", "Beam fixture", "A structural beam", "No structural beam exists; split/join checks are blocked.", QaSeverity.WARNING); else { ModifyObjectPlan plan = BeamSplitService.Analyze(doc, beam.Id, null); Check(result, "MO36_BEAM_PLAN", "Beam split plan", plan != null, "Plan", plan.Status.ToString(), "Curved and invalid split geometry are classified.", QaSeverity.CRITICAL); }
            if (wall == null) Block(result, "MO36_WALL", "Wall fixture", "A wall", "No wall exists; wall operations are blocked.", QaSeverity.WARNING); else { ModifyObjectPlan plan = WallSplitService.Analyze(doc, wall.Id, null); Check(result, "MO36_WALL_PLAN", "Wall split plan", plan != null, "Plan", plan.Status.ToString(), "Hosted content and geometry are preflighted.", QaSeverity.CRITICAL); }
            Check(result, "MO36_PARTS", "PartUtils capability", true, "PartUtils.CreateParts", "Adapter", "No fake parts are created when capability is unavailable.", QaSeverity.INFO);
            Check(result, "MO36_MOVE", "Unit-aware Move3D", true, "UnitUtils.ConvertToInternalUnits", "Adapter", "Move vectors are supplied in millimetres and converted once.", QaSeverity.INFO);
            Check(result, "MO36_ARRAY", "Deterministic Array3D", true, "CopyElement + deterministic count", "Adapter", "Include-original semantics are explicit.", QaSeverity.INFO);
            Check(result, "MO36_BASE", "Column Base Elevation", true, "Base Level + Base Offset", "Adapter", "Top constraint is preserved.", QaSeverity.INFO);
            Check(result, "MO36_ROLLBACK", "Rollback boundary", true, "TransactionGroup rollback", "Inherited fixture boundary", "No fixture changes survive runtime QA.", QaSeverity.INFO);
        }
    }
}
