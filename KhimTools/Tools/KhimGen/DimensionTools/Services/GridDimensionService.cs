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
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.GRID, Options = options ?? new DimensionOptions() };
            IList<Grid> grids = (selectedGridIds != null && selectedGridIds.Count > 0 ? selectedGridIds.Select(id => doc.GetElement(id) as Grid).Where(x => x != null) : new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>()).ToList();
            IList<Grid> curved = grids.Where(IsCurvedGrid).ToList();
            IList<Grid> straight = grids.Where(x => x.Curve is Line).ToList();
            if (straight.Count == 0)
            {
                var blocked = DimensionPlanBuilder.CreateBasePlan(context); blocked.Status = curved.Count > 0 ? DimensionStatus.CURVED_GRID_UNSUPPORTED : DimensionStatus.INSUFFICIENT_REFERENCES;
                blocked.Errors.Add(curved.Count > 0 ? "CURVED_GRID_UNSUPPORTED" : "No straight visible Grids were found.");
                return blocked;
            }
            ViewPlane plane = ViewPlane.FromView(view);
            var groups = GroupParallel(straight, plane);
            IList<Grid> selectedGroup = SelectGroup(groups, plane, context.Options.Axis);
            XYZ gridDirection = ((Line)selectedGroup[0].Curve).Direction;
            double right = Math.Abs(gridDirection.DotProduct(plane.RightDirection));
            context.Options.Axis = right >= Math.Cos(DimensionGeometryService.AngularToleranceRadians) ? DimensionAxis.VERTICAL_IN_VIEW : DimensionAxis.HORIZONTAL_IN_VIEW;
            context.References.AddRange(DimensionReferenceService.FromGrid(doc, view, selectedGroup));
            DimensionPlan plan = DimensionPlanBuilder.Build(context);
            if (curved.Count > 0) plan.Warnings.Add("CURVED_GRID_UNSUPPORTED: " + curved.Count + " curved Grid(s) skipped.");
            if (selectedGroup.Count < straight.Count) plan.Warnings.Add("Grid orientation groups are planned independently; Preview selected the compatible " + selectedGroup.Count + "-Grid chain.");
            return plan;
        }
        public static DimensionResult Create(Document doc, DimensionPlan plan) { return DimensionExecutionService.Execute(doc, plan); }
        public static bool IsCurvedGrid(Grid grid) { return grid != null && grid.Curve is Arc; }
        public static IList<DimensionReferenceInfo> SortGridChain(View view, IEnumerable<Grid> grids, DimensionAxis axis) { return DimensionGeometryService.SortByProjectedPosition(view, DimensionReferenceService.FromGrid(null, view, grids), axis); }
        private static IList<IList<Grid>> GroupParallel(IEnumerable<Grid> grids, ViewPlane plane)
        {
            var groups = new List<IList<Grid>>();
            foreach (Grid grid in grids)
            {
                XYZ direction = ((Line)grid.Curve).Direction;
                IList<Grid> match = groups.FirstOrDefault(group => DimensionGeometryService.AreParallel(((Line)group[0].Curve).Direction, direction));
                if (match == null) { match = new List<Grid>(); groups.Add(match); }
                match.Add(grid);
            }
            return groups;
        }
        private static IList<Grid> SelectGroup(IList<IList<Grid>> groups, ViewPlane plane, DimensionAxis axis)
        {
            if (axis == DimensionAxis.HORIZONTAL_IN_VIEW) return groups.OrderByDescending(group => Math.Abs(((Line)group[0].Curve).Direction.DotProduct(plane.UpDirection))).ThenByDescending(group => group.Count).First();
            if (axis == DimensionAxis.VERTICAL_IN_VIEW) return groups.OrderByDescending(group => Math.Abs(((Line)group[0].Curve).Direction.DotProduct(plane.RightDirection))).ThenByDescending(group => group.Count).First();
            return groups.OrderByDescending(group => group.Count).First();
        }
    }
}
