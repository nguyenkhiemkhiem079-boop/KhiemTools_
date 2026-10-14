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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.COLUMN, Options = options ?? new DimensionOptions() };
            IList<Element> columns = (selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType()).ToList();
            DimensionReferenceRole role = string.Equals(mode, "CENTERLINE", System.StringComparison.OrdinalIgnoreCase) ? DimensionReferenceRole.COLUMN_CENTER : DimensionReferenceRole.COLUMN_FACE;
            context.References.AddRange(DimensionReferenceService.FromElements(doc, view, columns, role, context.Options.Axis));
            DimensionPlan plan = DimensionPlanBuilder.Build(context);
            if (plan.References.Count == 0 && role == DimensionReferenceRole.COLUMN_CENTER) { plan.Status = DimensionStatus.CENTER_REFERENCE_UNAVAILABLE; plan.Errors.Add("CENTER_REFERENCE_UNAVAILABLE"); }
            return plan;
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
