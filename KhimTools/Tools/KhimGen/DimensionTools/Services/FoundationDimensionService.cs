using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class FoundationDimensionService
    {
        // Modes: WIDTH, LENGTH, FACE_CHAIN and GRID_OFFSET. FamilyInstance Transform is respected; strip/wall foundations report FOUNDATION_TYPE_UNSUPPORTED.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedIds, string mode, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.FOUNDATION, Options = options ?? new DimensionOptions() }; IEnumerable<Element> foundations = selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralFoundation).WhereElementIsNotElementType(); foreach (Element foundation in foundations) { FamilyInstance instance = foundation as FamilyInstance; if (instance == null) continue; context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { foundation }, DimensionReferenceRole.FOUNDATION_FACE, context.Options.Axis)); } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
