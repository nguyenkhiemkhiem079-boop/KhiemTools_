using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class ColumnDimensionService
    {
        // Modes: CENTERLINE, FACE_TO_FACE and GRID_TO_COLUMN. FamilyInstance Transform handles rotated/mirrored columns; Cylindrical faces use CYLINDRICAL_FACE_UNSUPPORTED or center references. Missing center refs return CENTER_REFERENCE_UNAVAILABLE.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedIds, string mode, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.COLUMN, Options = options ?? new DimensionOptions() }; IEnumerable<Element> columns = selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType(); context.References.AddRange(DimensionReferenceService.FromElements(doc, view, columns, mode == "CENTERLINE" ? DimensionReferenceRole.CENTER : DimensionReferenceRole.COLUMN_FACE, context.Options.Axis)); if (context.References.Count == 0 && mode == "CENTERLINE") context.References.AddRange(DimensionReferenceService.FromElements(doc, view, columns, DimensionReferenceRole.CENTER, context.Options.Axis)); return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
