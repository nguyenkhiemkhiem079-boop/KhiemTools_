using System;
using Autodesk.Revit.DB;
using KhimTools.Core;

namespace KhimTools.SectionCutTool.Models
{
    /// <summary>
    /// Item đại diện cho cấu kiện được chọn hiển thị trên DataGridView của SectionCutForm.
    /// </summary>
    public class ElementCutItem
    {
        public Element Element { get; set; }
        public ElementId Id => Element?.Id;
        public bool IsSelected { get; set; } = true;

        public string Mark { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public double LengthMm { get; set; } = 0;

        public ElementCutItem(Element elem)
        {
            Element = elem;
            if (elem == null) return;

            // 1. Mark
            Parameter markParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
                               ?? elem.LookupParameter("Mark")
                               ?? elem.LookupParameter("Comments");
            Mark = (markParam != null && markParam.HasValue && !string.IsNullOrWhiteSpace(markParam.AsString()))
                ? markParam.AsString().Trim()
                : elem.Id.ToLongValue().ToString();

            // 2. Type Name
            if (elem is FamilyInstance fi && fi.Symbol != null)
            {
                TypeName = fi.Symbol.Name;
            }
            else
            {
                var typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId && elem.Document != null)
                {
                    var typeElem = elem.Document.GetElement(typeId);
                    TypeName = typeElem?.Name ?? elem.Name;
                }
                else
                {
                    TypeName = elem.Name;
                }
            }

            // 3. Category Name
            if (elem.Category != null)
            {
                CategoryName = elem.Category.Name;
            }

            // 4. Level Name
            ElementId levelId = elem.LevelId;
            if (levelId == null || levelId == ElementId.InvalidElementId)
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)
                           ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)
                           ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (p != null && p.HasValue) levelId = p.AsElementId();
            }

            if (levelId != null && levelId != ElementId.InvalidElementId && elem.Document != null)
            {
                var levelElem = elem.Document.GetElement(levelId);
                LevelName = levelElem?.Name ?? "";
            }

            // 5. Length calculation
            if (elem.Location is LocationCurve locCurve && locCurve.Curve != null)
            {
                LengthMm = UnitUtils.ConvertFromInternalUnits(locCurve.Curve.Length, UnitTypeId.Millimeters);
            }
            else
            {
                BoundingBoxXYZ bb = elem.get_BoundingBox(null);
                if (bb != null)
                {
                    double dx = bb.Max.X - bb.Min.X;
                    double dy = bb.Max.Y - bb.Min.Y;
                    double dz = bb.Max.Z - bb.Min.Z;
                    double maxDim = Math.Max(dx, Math.Max(dy, dz));
                    LengthMm = UnitUtils.ConvertFromInternalUnits(maxDim, UnitTypeId.Millimeters);
                }
            }
        }
    }
}
