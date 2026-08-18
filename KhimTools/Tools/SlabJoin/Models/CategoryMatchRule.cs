using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Rule ghép Category: join/unjoin tất cả cặp element thuộc CategoryA × CategoryB có BB chạm nhau.
    /// </summary>
    public class CategoryMatchRule
    {
        public BuiltInCategory CategoryA { get; set; } = BuiltInCategory.OST_Floors;
        public BuiltInCategory CategoryB { get; set; } = BuiltInCategory.OST_Floors;

        public string LabelA => CategoryDisplayName(CategoryA);
        public string LabelB => CategoryDisplayName(CategoryB);

        public override string ToString() => $"{LabelA} ↔ {LabelB}";

        public static string CategoryDisplayName(BuiltInCategory cat)
        {
            switch (cat)
            {
                case BuiltInCategory.OST_Floors: return "Floors (Sàn)";
                case BuiltInCategory.OST_Walls: return "Walls (Tường)";
                case BuiltInCategory.OST_StructuralColumns: return "Columns (Cột)";
                case BuiltInCategory.OST_StructuralFraming: return "Beams (Dầm)";
                default: return cat.ToString();
            }
        }

        /// <summary>
        /// Danh sách category được hỗ trợ trong ComboBox (chỉ gồm Floor, Wall, Column, Beam).
        /// </summary>
        public static readonly BuiltInCategory[] SupportedCategories = new[]
        {
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming
        };
    }
}
