using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Revit;
using KhimTools.Structural.QuickStructure.Models;

namespace KhimTools.Structural.QuickStructure.Services
{
    /// <summary>Plans grid nodes and atomically creates selected structural framing and footings.</summary>
    public static class QuickStructureService
    {
        public static List<Grid> GetAllGrids(Document doc)
        {
            if (doc == null) return new List<Grid>();
            return new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().ToList();
        }

        public static List<XYZ> CalculateGridIntersections(List<Grid> grids)
        {
            var points2D = new List<Point2D>();
            if (grids == null || grids.Count < 2) return new List<XYZ>();

            for (int i = 0; i < grids.Count; i++)
            {
                Curve c1 = grids[i]?.Curve;
                if (!(c1 is Line)) continue;
                Point2D p1 = new Point2D(c1.GetEndPoint(0).X, c1.GetEndPoint(0).Y);
                Point2D p2 = new Point2D(c1.GetEndPoint(1).X, c1.GetEndPoint(1).Y);
                for (int j = i + 1; j < grids.Count; j++)
                {
                    Curve c2 = grids[j]?.Curve;
                    if (!(c2 is Line)) continue;
                    Point2D p3 = new Point2D(c2.GetEndPoint(0).X, c2.GetEndPoint(0).Y);
                    Point2D p4 = new Point2D(c2.GetEndPoint(1).X, c2.GetEndPoint(1).Y);
                    Point2D intersection = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, true);
                    if (intersection != null) points2D.Add(intersection);
                }
            }

            return GridIntersectionHelper.DeduplicatePoints(points2D, 1e-4)
                .Select(point => new XYZ(point.X, point.Y, 0)).ToList();
        }

        /// <summary>Runs all selected creation steps under one rollback-capable transaction group.</summary>
        public static QuickStructureGenerationResult Generate(Document doc, QuickStructureGenerationRequest request)
        {
            ValidateRequest(doc, request);
            return TransactionBoundary.ExecuteGroup(doc, "K-TOOLS — Quick Structure Batch", () =>
            {
                var columns = request.CreateColumns
                    ? CreateColumnsCore(doc, request.Intersections.ToList(), request.ColumnSymbol,
                        request.ColumnBaseLevel, request.ColumnTopLevel ?? request.ColumnBaseLevel,
                        request.ColumnBaseOffsetMm, request.ColumnTopOffsetMm)
                    : new List<FamilyInstance>();
                var beams = request.CreateBeams
                    ? CreateBeamsCore(doc, request.Grids.ToList(), request.Intersections.ToList(),
                        request.BeamSymbol, request.BeamLevel, request.BeamZOffsetMm)
                    : new List<FamilyInstance>();
                var footings = request.CreateFootings
                    ? CreateFootingsCore(doc, columns, request.FootingSymbol,
                        request.FootingLevel, request.FootingOffsetMm)
                    : new List<FamilyInstance>();

                VerifyCreatedAfterCommit(doc, columns, "columns");
                VerifyCreatedAfterCommit(doc, beams, "beams");
                VerifyCreatedAfterCommit(doc, footings, "footings");
                return new QuickStructureGenerationResult(columns, beams, footings);
            });
        }

        // Kept for callers that request one operation directly; each remains atomic.
        public static List<FamilyInstance> CreateColumnsAtPoints(Document doc, List<XYZ> points,
            FamilySymbol columnSymbol, Level baseLevel, Level topLevel, double baseOffsetMm, double topOffsetMm)
        {
            ValidateSingle(doc, points, columnSymbol, baseLevel, "columns");
            if (points.Count == 0) return new List<FamilyInstance>();
            return TransactionBoundary.ExecuteGroup(doc, "K-TOOLS — Quick Structure Columns", () =>
            {
                List<FamilyInstance> result = CreateColumnsCore(doc, points, columnSymbol, baseLevel,
                    topLevel ?? baseLevel, baseOffsetMm, topOffsetMm);
                VerifyCreatedAfterCommit(doc, result, "columns");
                return result;
            });
        }

        public static List<FamilyInstance> CreateBeamsAlongGridSpans(Document doc, List<Grid> grids,
            List<XYZ> nodePoints, FamilySymbol beamSymbol, Level level, double zOffsetMm)
        {
            ValidateSingle(doc, nodePoints, beamSymbol, level, "beams");
            if (grids == null) throw new ArgumentNullException(nameof(grids));
            if (nodePoints.Count == 0 || grids.Count == 0) return new List<FamilyInstance>();
            return TransactionBoundary.ExecuteGroup(doc, "K-TOOLS — Quick Structure Beams", () =>
            {
                List<FamilyInstance> result = CreateBeamsCore(doc, grids, nodePoints, beamSymbol, level, zOffsetMm);
                VerifyCreatedAfterCommit(doc, result, "beams");
                return result;
            });
        }

        public static List<FamilyInstance> CreateFootingsUnderColumns(Document doc, List<FamilyInstance> columns,
            FamilySymbol footingSymbol, Level level, double offsetMm)
        {
            ValidateSingle(doc, columns, footingSymbol, level, "footings");
            if (columns.Count == 0) return new List<FamilyInstance>();
            return TransactionBoundary.ExecuteGroup(doc, "K-TOOLS — Quick Structure Footings", () =>
            {
                List<FamilyInstance> result = CreateFootingsCore(doc, columns, footingSymbol, level, offsetMm);
                VerifyCreatedAfterCommit(doc, result, "footings");
                return result;
            });
        }

        private static List<FamilyInstance> CreateColumnsCore(Document doc, IList<XYZ> points,
            FamilySymbol symbol, Level baseLevel, Level topLevel, double baseOffsetMm, double topOffsetMm)
        {
            double baseOffset = RevitUnitService.MillimetresToFeet(baseOffsetMm);
            double topOffset = RevitUnitService.MillimetresToFeet(topOffsetMm);
            return ExecuteCreation(doc, "Create Quick Structure Columns", symbol, points.Count, () =>
            {
                var created = new List<FamilyInstance>();
                foreach (XYZ point in points)
                {
                    if (point == null) throw new InvalidOperationException("A column node is null.");
                    var column = doc.Create.NewFamilyInstance(
                        new XYZ(point.X, point.Y, baseLevel.Elevation), symbol, baseLevel, StructuralType.Column);
                    if (column == null) throw new InvalidOperationException("Revit returned no column instance.");
                    if (topLevel != null && !topLevel.Id.Equals(baseLevel.Id))
                        SetRequiredParameter(column, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, topLevel.Id,
                            "column top level");
                    if (Math.Abs(baseOffset) > 1e-5)
                        SetRequiredParameter(column, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, baseOffset,
                            "column base offset");
                    if (Math.Abs(topOffset) > 1e-5)
                        SetRequiredParameter(column, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, topOffset,
                            "column top offset");
                    created.Add(column);
                }
                return created;
            });
        }

        private static List<FamilyInstance> CreateBeamsCore(Document doc, IList<Grid> grids,
            IList<XYZ> nodePoints, FamilySymbol symbol, Level level, double zOffsetMm)
        {
            double zOffset = RevitUnitService.MillimetresToFeet(zOffsetMm);
            return ExecuteCreation(doc, "Create Quick Structure Beams", symbol, null, () =>
            {
                var created = new List<FamilyInstance>();
                foreach (Grid grid in grids)
                {
                    Curve curve = grid?.Curve;
                    if (!(curve is Line)) continue;
                    var pointsOnGrid = nodePoints
                        .Where(point => point != null && curve.Distance(new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z)) < 0.05)
                        .Select(point => new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z))
                        .OrderBy(point => curve.Project(point).Parameter)
                        .ToList();

                    for (int index = 0; index < pointsOnGrid.Count - 1; index++)
                    {
                        XYZ start = pointsOnGrid[index];
                        XYZ end = pointsOnGrid[index + 1];
                        if (start.DistanceTo(end) < 0.5) continue;
                        Line beamLine = Line.CreateBound(
                            new XYZ(start.X, start.Y, level.Elevation + zOffset),
                            new XYZ(end.X, end.Y, level.Elevation + zOffset));
                        FamilyInstance beam = doc.Create.NewFamilyInstance(beamLine, symbol, level, StructuralType.Beam);
                        if (beam == null) throw new InvalidOperationException("Revit returned no beam instance.");
                        created.Add(beam);
                    }
                }
                if (created.Count == 0)
                    throw new InvalidOperationException("The selected grid nodes contain no eligible beam spans.");
                return created;
            });
        }

        private static List<FamilyInstance> CreateFootingsCore(Document doc, IList<FamilyInstance> columns,
            FamilySymbol symbol, Level level, double offsetMm)
        {
            double offset = RevitUnitService.MillimetresToFeet(offsetMm);
            return ExecuteCreation(doc, "Create Quick Structure Footings", symbol, columns.Count, () =>
            {
                var created = new List<FamilyInstance>();
                foreach (FamilyInstance column in columns)
                {
                    LocationPoint location = column?.Location as LocationPoint;
                    if (location == null) throw new InvalidOperationException("A selected column has no point location.");
                    XYZ point = new XYZ(location.Point.X, location.Point.Y, level.Elevation + offset);
                    FamilyInstance footing = doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.Footing);
                    if (footing == null) throw new InvalidOperationException("Revit returned no footing instance.");
                    created.Add(footing);
                }
                return created;
            });
        }

        private static List<FamilyInstance> ExecuteCreation(Document doc, string operation, FamilySymbol symbol,
            int? expectedCount, Func<List<FamilyInstance>> create)
        {
            return TransactionBoundary.Execute(doc, operation, () =>
            {
                if (!symbol.IsActive) symbol.Activate();
                doc.Regenerate();
                List<FamilyInstance> instances = create();
                doc.Regenerate();
                if (instances == null || instances.Count == 0 ||
                    (expectedCount.HasValue && instances.Count != expectedCount.Value))
                    throw new InvalidOperationException(operation + " failed its created-instance count postcondition.");
                if (instances.Any(instance => instance == null || !instance.IsValidObject ||
                    doc.GetElement(instance.Id) == null))
                    throw new InvalidOperationException(operation + " failed its in-transaction element postcondition.");
                return instances;
            });
        }

        private static void VerifyCreatedAfterCommit(Document doc, IEnumerable<FamilyInstance> instances, string kind)
        {
            foreach (FamilyInstance instance in instances)
            {
                if (instance == null || !instance.IsValidObject || doc.GetElement(instance.Id) == null)
                    throw new InvalidOperationException("Quick Structure committed but the " + kind +
                        " postcondition failed; the enclosing group will roll back.");
            }
        }

        private static void SetRequiredParameter(FamilyInstance instance, BuiltInParameter parameterId,
            ElementId value, string description)
        {
            Parameter parameter = instance.get_Parameter(parameterId);
            if (parameter == null || parameter.IsReadOnly || !parameter.Set(value))
                throw new InvalidOperationException("Unable to set required " + description + ".");
        }

        private static void SetRequiredParameter(FamilyInstance instance, BuiltInParameter parameterId,
            double value, string description)
        {
            Parameter parameter = instance.get_Parameter(parameterId);
            if (parameter == null || parameter.IsReadOnly || !parameter.Set(value))
                throw new InvalidOperationException("Unable to set required " + description + ".");
        }

        private static void ValidateRequest(Document doc, QuickStructureGenerationRequest request)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.CreateColumns && !request.CreateBeams && !request.CreateFootings)
                throw new ArgumentException("Select at least one Quick Structure operation.", nameof(request));
            if (request.Intersections == null || request.Intersections.Count == 0)
                throw new ArgumentException("No valid grid intersections were supplied.", nameof(request));
            if (request.CreateColumns) ValidateSingle(doc, request.Intersections, request.ColumnSymbol,
                request.ColumnBaseLevel, "columns");
            if (request.CreateColumns && request.ColumnTopLevel != null &&
                (!request.ColumnTopLevel.IsValidObject || !request.ColumnTopLevel.Document.Equals(doc)))
                throw new ArgumentException("The selected column top level is invalid or belongs to another document.", nameof(request));
            if (request.CreateBeams)
            {
                ValidateSingle(doc, request.Intersections, request.BeamSymbol, request.BeamLevel, "beams");
                if (request.Grids == null || request.Grids.Count == 0)
                    throw new ArgumentException("No grids were supplied for beam creation.", nameof(request));
            }
            if (request.CreateFootings)
            {
                if (!request.CreateColumns)
                    throw new ArgumentException("Footings require columns in the same atomic Quick Structure request.", nameof(request));
                ValidateSingle(doc, request.Intersections, request.FootingSymbol, request.FootingLevel, "footings");
            }
            foreach (double offset in new[] { request.ColumnBaseOffsetMm, request.ColumnTopOffsetMm,
                request.BeamZOffsetMm, request.FootingOffsetMm })
                if (double.IsNaN(offset) || double.IsInfinity(offset))
                    throw new ArgumentOutOfRangeException(nameof(request), "Quick Structure offsets must be finite millimetre values.");
        }

        private static void ValidateSingle<T>(Document doc, IList<T> inputs, FamilySymbol symbol, Level level, string operation)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (symbol == null) throw new ArgumentException("A family type is required for " + operation + ".", nameof(symbol));
            if (!symbol.IsValidObject || !symbol.Document.Equals(doc))
                throw new ArgumentException("The selected family type is invalid or belongs to another document.", nameof(symbol));
            if (level == null || !level.IsValidObject || !level.Document.Equals(doc))
                throw new ArgumentException("A valid level in the active document is required for " + operation + ".", nameof(level));
        }
    }
}
