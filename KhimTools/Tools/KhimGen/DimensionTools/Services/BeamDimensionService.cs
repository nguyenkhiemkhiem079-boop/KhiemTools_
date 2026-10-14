using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class BeamDimensionService
    {
        // Modes: LENGTH, END_TO_END, GRID_TO_GRID and FACE_CHAIN. Straight beams only; sloped plan results are PROJECTED_VIEW_DIMENSION and curved beams are CURVED_BEAM_UNSUPPORTED.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedIds, string mode, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.BEAM, Options = options ?? new DimensionOptions() };
            IList<Element> beams = (selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType()).ToList();
            int curved = 0;
            foreach (Element beam in beams)
            {
                LocationCurve curve = beam.Location as LocationCurve;
                if (curve == null || !(curve.Curve is Line)) { curved++; continue; }
                DimensionReferenceRole role = string.Equals(mode, "END_TO_END", System.StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "LENGTH", System.StringComparison.OrdinalIgnoreCase) ? DimensionReferenceRole.BEAM_END : DimensionReferenceRole.BEAM_FACE;
                context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { beam }, role, context.Options.Axis));
            }
            if (context.References.Count == 0 && curved > 0) { var blocked = DimensionPlanBuilder.CreateBasePlan(context); blocked.Status = DimensionStatus.CURVED_BEAM_UNSUPPORTED; blocked.Errors.Add("CURVED_BEAM_UNSUPPORTED"); return blocked; }
            DimensionPlan plan = DimensionPlanBuilder.Build(context); if (curved > 0) plan.Warnings.Add("CURVED_BEAM_UNSUPPORTED: " + curved + " curved beam(s) skipped."); return plan;
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
