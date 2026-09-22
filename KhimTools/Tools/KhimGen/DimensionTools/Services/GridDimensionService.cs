using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    public static class GridDimensionService
    {
        // Grid chains use actual Grid References and the shared NewDimension adapter; GridGeneratorService remains the legacy caller. Arc grids report CURVED_GRID_UNSUPPORTED.
        public static DimensionPlan BuildPlan(Document doc, View view, IList<ElementId> selectedGridIds, DimensionOptions options)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.GRID, Options = options ?? new DimensionOptions() }; IEnumerable<Grid> grids = selectedGridIds != null && selectedGridIds.Count > 0 ? selectedGridIds.Select(id => doc.GetElement(id) as Grid).Where(x => x != null) : new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>(); foreach (Grid grid in grids) { if (grid.Curve is Arc) continue; context.References.AddRange(DimensionReferenceService.FromGrid(doc, view, new[] { grid })); } return DimensionPlanBuilder.Build(context);
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
        public static bool IsCurvedGrid(Grid grid) { return grid != null && grid.Curve is Arc; }
        public static IList<DimensionReferenceInfo> SortGridChain(View view, IEnumerable<Grid> grids, DimensionAxis axis) { return DimensionGeometryService.SortByProjectedPosition(view, DimensionReferenceService.FromGrid(null, view, grids), axis); }
    }
}
