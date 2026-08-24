using System;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.Revit.DB;
using Color = System.Drawing.Color;

namespace KhimTools.ElementTags.Models
{
    /// <summary>
    /// Model đại diện cho một dòng cấu hình gắn Tag cho một Category.
    /// </summary>
    public class ElementTagsItem
    {
        public bool IsChecked { get; set; } = true;
        public string CategoryName { get; set; } = "";
        public BuiltInCategory HostCategory { get; set; }
        public BuiltInCategory TagCategory { get; set; }
        public Color TagColor { get; set; } = Color.Magenta; // Mặc định Hồng cánh sen
        public List<FamilySymbol> AvailableTagSymbols { get; set; } = new List<FamilySymbol>();
        public FamilySymbol SelectedTagSymbol { get; set; }

        public string TagColorHtml => ColorTranslator.ToHtml(TagColor);
    }
}
