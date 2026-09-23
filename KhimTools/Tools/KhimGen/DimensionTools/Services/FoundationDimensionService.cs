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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.FOUNDATION, Options = options ?? new DimensionOptions() };
            IList<Element> foundations = (selectedIds != null && selectedIds.Count > 0 ? selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null) : new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_StructuralFoundation).WhereElementIsNotElementType()).ToList();
            int unsupported = 0;
            foreach (Element foundation in foundations)
            {
                FamilyInstance instance = foundation as FamilyInstance;
                if (instance == null) { unsupported++; continue; }
                context.References.AddRange(DimensionReferenceService.FromElements(doc, view, new[] { instance }, DimensionReferenceRole.FOUNDATION_FACE, context.Options.Axis));
            }
            if (context.References.Count == 0 && unsupported > 0) { var blocked = DimensionPlanBuilder.CreateBasePlan(context); blocked.Status = DimensionStatus.FOUNDATION_TYPE_UNSUPPORTED; blocked.Errors.Add("FOUNDATION_TYPE_UNSUPPORTED: strip/wall foundations are not reconstructed as pad dimensions."); return blocked; }
            DimensionPlan plan = DimensionPlanBuilder.Build(context); if (unsupported > 0) plan.Warnings.Add("FOUNDATION_TYPE_UNSUPPORTED: " + unsupported + " non-pad foundation(s) skipped."); return plan;
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
    }
}
