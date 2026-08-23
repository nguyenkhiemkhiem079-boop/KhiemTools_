using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.VisibilityTool.Services
{
    /// <summary>
    /// Service xử lý Ẩn và Hiện Category trong Active View nhanh chóng.
    /// </summary>
    public static class CategoryVisibilityService
    {
        public static bool SetCategoryVisibility(Document doc, View view, string displayName, bool isVisible, params BuiltInCategory[] categories)
        {
            if (doc == null || view == null || categories == null || categories.Length == 0)
                return false;

            var validCatIds = new List<ElementId>();
            foreach (var bic in categories)
            {
                try
                {
                    Category cat = Category.GetCategory(doc, bic);
                    if (cat != null && view.CanCategoryBeHidden(cat.Id))
                    {
                        validCatIds.Add(cat.Id);
                    }
                }
                catch { }
            }

            if (!validCatIds.Any())
            {
                TaskDialog.Show("Hiển Thị / Ẩn Đối Tượng",
                    $"Khung nhìn hiện tại ({view.Name}) không cho phép thay đổi hiển thị '{displayName}' (có thể do View Template đang khóa hoặc loại View không hỗ trợ).");
                return false;
            }

            // isVisible = true => SetCategoryHidden(id, false)
            // isVisible = false => SetCategoryHidden(id, true)
            bool targetHidden = !isVisible;

            using (var tx = new Transaction(doc, $"K-TOOLS - {(isVisible ? "Hiện" : "Ẩn")} {displayName}"))
            {
                tx.Start();
                foreach (var id in validCatIds)
                {
                    view.SetCategoryHidden(id, targetHidden);
                }
                tx.Commit();
            }

            return true;
        }

        public static bool SetTagVisibility(Document doc, View view, bool isVisible)
        {
            if (doc == null || view == null) return false;

            var tagCats = new BuiltInCategory[]
            {
                BuiltInCategory.OST_DoorTags,
                BuiltInCategory.OST_WindowTags,
                BuiltInCategory.OST_WallTags,
                BuiltInCategory.OST_FloorTags,
                BuiltInCategory.OST_StructuralFramingTags,
                BuiltInCategory.OST_StructuralColumnTags,
                BuiltInCategory.OST_StructuralFoundationTags,
                BuiltInCategory.OST_RebarTags,
                BuiltInCategory.OST_RoomTags,
                BuiltInCategory.OST_AreaTags,
                BuiltInCategory.OST_CeilingTags,
                BuiltInCategory.OST_RoofTags,
                BuiltInCategory.OST_StairsTags,
                BuiltInCategory.OST_StairsRailingTags,
                BuiltInCategory.OST_MaterialTags,
                BuiltInCategory.OST_MultiCategoryTags,
                BuiltInCategory.OST_KeynoteTags,
                BuiltInCategory.OST_GenericAnnotation
            };

            return SetCategoryVisibility(doc, view, "Tag Chú Thích", isVisible, tagCats);
        }
    }
}