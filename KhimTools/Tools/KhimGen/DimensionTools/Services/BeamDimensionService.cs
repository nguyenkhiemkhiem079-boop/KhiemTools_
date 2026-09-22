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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.BEAM, Options = options ?? new DimensionOptions() }; IEnumerable<Element> beams = selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType(); foreach (Element beam in beams) { LocationCurve curve = beam.Location as LocationCurve; if (curve != null && curve.Curve is Arc) continue; context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { beam }, DimensionReferenceRole.BEAM_FACE, context.Options.Axis)); } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
