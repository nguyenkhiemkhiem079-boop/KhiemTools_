using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using KhimTools.Domain.Models.Architectural;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.Architectural.Lintels
{
    internal sealed class LintelResult
    {
        internal int Created { get; set; }
        internal int SkippedExisting { get; set; }
        internal int Failed { get; set; }
        internal List<ElementId> CreatedIds { get; } = new List<ElementId>();
        internal TimeSpan Duration { get; set; }
    }

    internal static class LintelService
    {
        private const string CommentPrefix = "K-TOOLS LINTEL | Opening=";

        internal static LintelResult Create(Document doc, IList<FamilyInstance> openings,
            FamilySymbol symbol, double extensionMm, double offsetMm, bool skipExisting)
        {
            if (doc == null || openings == null || symbol == null) throw new ArgumentNullException("Document, openings, and symbol are required.");
            if (symbol.Document != doc || symbol.Category == null || symbol.Category.Id != new ElementId(BuiltInCategory.OST_StructuralFraming))
                throw new ArgumentException("Selected lintel type must be a Structural Framing type from this document.", nameof(symbol));
            if (double.IsNaN(extensionMm) || double.IsInfinity(extensionMm) || extensionMm < 0 ||
                double.IsNaN(offsetMm) || double.IsInfinity(offsetMm))
                throw new ArgumentOutOfRangeException(nameof(extensionMm), "Extension and offset must be finite; extension cannot be negative.");
            var timer = Stopwatch.StartNew();
            var result = new LintelResult();
            var existing = GetExistingSourceIds(doc);

            using var transaction = new Transaction(doc, "K-TOOLS: Create lintels");
            KhimTools.Core.Revit.TransactionBoundary.Start(transaction, "Architectural.Lintels");
            try
            {
                if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }

                foreach (FamilyInstance opening in openings)
                {
                    if (opening == null || opening.Document != doc || !IsDoorOrWindow(opening))
                    { result.Failed++; continue; }
                    if (skipExisting && existing.Contains(opening.UniqueId))
                    {
                        result.SkippedExisting++;
                        continue;
                    }

                    using var sub = new SubTransaction(doc);
                    KhimTools.Core.Revit.TransactionBoundary.Start(sub, "Architectural.Lintels");
                    try
                    {
                        if (!(opening.Host is Wall wall) || !(wall.Location is LocationCurve wallLocation))
                            throw new InvalidOperationException("Opening is not hosted by a wall with a location curve.");

                        BoundingBoxXYZ box = opening.get_BoundingBox(null);
                        if (box == null) throw new InvalidOperationException("Opening has no model bounding box.");
                        var bounds = GetWorldBounds(box);

                        XYZ center = (bounds.Min + bounds.Max) * 0.5;
                        XYZ direction = GetHorizontalTangent(wallLocation.Curve, center);
                        double width = GetOpeningWidth(opening, bounds, direction);
                        if (width <= 1e-6 || double.IsNaN(width) || double.IsInfinity(width))
                            throw new InvalidOperationException("Opening width is invalid.");
                        double elevationMm = UnitUtils.ConvertFromInternalUnits(bounds.Max.Z, UnitTypeId.Millimeters) + offsetMm;
                        var layout = LintelLayout.Compute(
                            UnitUtils.ConvertFromInternalUnits(center.X, UnitTypeId.Millimeters),
                            UnitUtils.ConvertFromInternalUnits(center.Y, UnitTypeId.Millimeters),
                            elevationMm,
                            UnitUtils.ConvertFromInternalUnits(width, UnitTypeId.Millimeters),
                            extensionMm, direction.X, direction.Y);
                        XYZ start = new XYZ(
                            UnitUtils.ConvertToInternalUnits(layout.StartXmm, UnitTypeId.Millimeters),
                            UnitUtils.ConvertToInternalUnits(layout.StartYmm, UnitTypeId.Millimeters),
                            UnitUtils.ConvertToInternalUnits(layout.StartZmm, UnitTypeId.Millimeters));
                        XYZ end = new XYZ(
                            UnitUtils.ConvertToInternalUnits(layout.EndXmm, UnitTypeId.Millimeters),
                            UnitUtils.ConvertToInternalUnits(layout.EndYmm, UnitTypeId.Millimeters),
                            UnitUtils.ConvertToInternalUnits(layout.EndZmm, UnitTypeId.Millimeters));
                        Level level = doc.GetElement(opening.LevelId) as Level ?? doc.GetElement(wall.LevelId) as Level;
                        if (level == null) throw new InvalidOperationException("No valid level is available for the lintel.");

                        FamilyInstance lintel = doc.Create.NewFamilyInstance(Line.CreateBound(start, end), symbol, level, StructuralType.Beam);
                        if (lintel == null || doc.GetElement(lintel.Id) is not FamilyInstance persisted ||
                            persisted.Category?.Id != new ElementId(BuiltInCategory.OST_StructuralFraming) ||
                            !(persisted.Location is LocationCurve generatedCurve) || generatedCurve.Curve.Length <= 1e-6)
                            throw new InvalidOperationException("Created lintel failed its instance/category/location postcondition.");
                        Parameter comments = lintel.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comments == null || comments.IsReadOnly)
                            throw new InvalidOperationException("Lintel source marker is unavailable; duplicate prevention cannot be guaranteed.");
                        string marker = CommentPrefix + opening.UniqueId;
                        comments.Set(marker);
                        if (!string.Equals(comments.AsString(), marker, StringComparison.Ordinal))
                            throw new InvalidOperationException("Lintel source marker failed its postcondition.");
                        KhimTools.Core.Revit.TransactionBoundary.Commit(sub, "Architectural.Lintels");
                        result.Created++;
                        result.CreatedIds.Add(lintel.Id);
                    }
                    catch
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "Architectural.Lintels");
                        result.Failed++;
                    }
                }

                if (result.CreatedIds.Any(id => !(doc.GetElement(id) is FamilyInstance)))
                    throw new InvalidOperationException("One or more lintel instances could not be resolved before commit.");
                if (result.Created == 0) KhimTools.Core.Revit.TransactionBoundary.RollBack(transaction, "Architectural.Lintels");
                else KhimTools.Core.Revit.TransactionBoundary.Commit(transaction, "Architectural.Lintels");
            }
            catch
            {
                KhimTools.Core.Revit.TransactionBoundary.RollBack(transaction, "Architectural.Lintels");
                result.Created = 0;
                result.CreatedIds.Clear();
                result.Failed = openings.Count;
                throw;
            }
            timer.Stop();
            result.Duration = timer.Elapsed;
            return result;
        }

        private static bool IsDoorOrWindow(FamilyInstance item) =>
            item.Category?.Id == new ElementId(BuiltInCategory.OST_Doors) ||
            item.Category?.Id == new ElementId(BuiltInCategory.OST_Windows);

        private static HashSet<string> GetExistingSourceIds(Document doc)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FamilyInstance item in new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType().OfType<FamilyInstance>())
            {
                string comment = item.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                if (comment?.StartsWith(CommentPrefix, StringComparison.OrdinalIgnoreCase) == true)
                    ids.Add(comment.Substring(CommentPrefix.Length));
            }
            return ids;
        }

        private static XYZ GetHorizontalTangent(Curve curve, XYZ point)
        {
            IntersectionResult projected = curve.Project(point);
            double parameter = projected?.Parameter ?? (curve.GetEndParameter(0) + curve.GetEndParameter(1)) * 0.5;
            XYZ tangent = curve.ComputeDerivatives(parameter, false).BasisX;
            XYZ horizontal = new XYZ(tangent.X, tangent.Y, 0);
            return horizontal.GetLength() > 1e-9 ? horizontal.Normalize() : XYZ.BasisX;
        }

        private sealed class WorldBounds
        {
            internal XYZ Min { get; set; }
            internal XYZ Max { get; set; }
        }

        private static WorldBounds GetWorldBounds(BoundingBoxXYZ box)
        {
            Transform transform = box.Transform ?? Transform.Identity;
            var points = new List<XYZ>();
            foreach (double x in new[] { box.Min.X, box.Max.X })
                foreach (double y in new[] { box.Min.Y, box.Max.Y })
                    foreach (double z in new[] { box.Min.Z, box.Max.Z })
                        points.Add(transform.OfPoint(new XYZ(x, y, z)));
            return new WorldBounds
            {
                Min = new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
                Max = new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z))
            };
        }

        private static double GetOpeningWidth(FamilyInstance opening, WorldBounds box, XYZ direction)
        {
            Parameter parameter = opening.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                ?? opening.get_Parameter(BuiltInParameter.WINDOW_WIDTH)
                ?? opening.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                ?? opening.Symbol?.get_Parameter(BuiltInParameter.WINDOW_WIDTH);
            if (parameter != null && parameter.StorageType == StorageType.Double && parameter.AsDouble() > 1e-6)
                return parameter.AsDouble();

            XYZ size = box.Max - box.Min;
            return Math.Abs(direction.X) * size.X + Math.Abs(direction.Y) * size.Y;
        }
    }
}
