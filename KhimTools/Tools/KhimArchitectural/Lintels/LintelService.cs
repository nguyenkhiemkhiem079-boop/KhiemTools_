using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.Architectural.Lintels
{
    internal sealed class LintelResult
    {
        internal int Created { get; set; }
        internal int SkippedExisting { get; set; }
        internal int Failed { get; set; }
    }

    internal static class LintelService
    {
        private const string CommentPrefix = "K-TOOLS LINTEL | Opening=";

        internal static LintelResult Create(Document doc, IList<FamilyInstance> openings,
            FamilySymbol symbol, double extensionMm, double offsetMm, bool skipExisting)
        {
            var result = new LintelResult();
            var existing = GetExistingSourceIds(doc);
            double extension = UnitUtils.ConvertToInternalUnits(extensionMm, UnitTypeId.Millimeters);
            double offset = UnitUtils.ConvertToInternalUnits(offsetMm, UnitTypeId.Millimeters);

            using var transaction = new Transaction(doc, "K-TOOLS: Create lintels");
            transaction.Start();
            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }

            foreach (FamilyInstance opening in openings)
            {
                if (skipExisting && existing.Contains(opening.UniqueId))
                {
                    result.SkippedExisting++;
                    continue;
                }

                try
                {
                    if (!(opening.Host is Wall wall) || !(wall.Location is LocationCurve wallLocation))
                    {
                        result.Failed++;
                        continue;
                    }

                    BoundingBoxXYZ box = opening.get_BoundingBox(null);
                    if (box == null) { result.Failed++; continue; }

                    XYZ center = (box.Min + box.Max) * 0.5;
                    XYZ direction = GetHorizontalTangent(wallLocation.Curve, center);
                    double width = GetOpeningWidth(opening, box, direction);
                    double z = box.Max.Z + offset;
                    XYZ start = new XYZ(center.X, center.Y, z) - direction * (width * 0.5 + extension);
                    XYZ end = new XYZ(center.X, center.Y, z) + direction * (width * 0.5 + extension);
                    Level level = doc.GetElement(opening.LevelId) as Level ?? doc.GetElement(wall.LevelId) as Level;
                    if (level == null) { result.Failed++; continue; }

                    FamilyInstance lintel = doc.Create.NewFamilyInstance(Line.CreateBound(start, end), symbol, level, StructuralType.Beam);
                    Parameter comments = lintel.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comments != null && !comments.IsReadOnly) comments.Set(CommentPrefix + opening.UniqueId);
                    result.Created++;
                }
                catch { result.Failed++; }
            }

            transaction.Commit();
            return result;
        }

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

        private static double GetOpeningWidth(FamilyInstance opening, BoundingBoxXYZ box, XYZ direction)
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
