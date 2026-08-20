using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.CopyLink.Models;

namespace KhimTools.CopyLink.Services
{
    public static class LinkElementCopyService
    {
        /// <summary>
        /// Lấy tất cả các Revit Link Instances đang được tải trong Host Document.
        /// </summary>
        public static List<LinkInstanceInfo> GetLinkInstances(Document hostDoc)
        {
            var result = new List<LinkInstanceInfo>();
            if (hostDoc == null) return result;

            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            foreach (var inst in linkInstances)
            {
                Document linkDoc = inst.GetLinkDocument();
                if (linkDoc == null) continue; // File link bị Unload

                string docTitle = linkDoc.Title;
                string instName = inst.Name;
                string display = $"{docTitle} ({instName})";

                result.Add(new LinkInstanceInfo
                {
                    Instance = inst,
                    LinkDocument = linkDoc,
                    DisplayName = display
                });
            }

            return result.OrderBy(l => l.DisplayName).ToList();
        }

        /// <summary>
        /// Quét tất cả các Category có chứa đối tượng trong file Link và đếm số lượng.
        /// </summary>
        public static List<LinkCategoryItem> GetCategoriesWithElements(Document linkDoc)
        {
            var result = new List<LinkCategoryItem>();
            if (linkDoc == null) return result;

            var allElements = new FilteredElementCollector(linkDoc)
                .WhereElementIsNotElementType()
                .ToElements();

            var categoryDict = new Dictionary<string, LinkCategoryItem>();

            foreach (Element elem in allElements)
            {
                // Bỏ qua các đối tượng nội bộ không phải mô hình cần copy
                if (elem is View || elem is RevitLinkInstance || elem is RevitLinkType) continue;
                if (elem is Sketch || elem is Dimension || elem is TextNote) continue;

                Category cat = elem.Category;
                if (cat == null || string.IsNullOrWhiteSpace(cat.Name)) continue;
                if (cat.CategoryType != CategoryType.Model) continue;

                string catKey = cat.Name;

                if (!categoryDict.TryGetValue(catKey, out var catItem))
                {
                    catItem = new LinkCategoryItem
                    {
                        CategoryId = cat.Id,
                        CategoryName = cat.Name,
                        ElementIds = new List<ElementId>()
                    };
                    categoryDict[catKey] = catItem;
                }

                catItem.ElementIds.Add(elem.Id);
            }

            // Cũng thêm Grids và Levels nếu có
            AddDatumCategoryIfAny(linkDoc, BuiltInCategory.OST_Grids, "Grids", categoryDict);
            AddDatumCategoryIfAny(linkDoc, BuiltInCategory.OST_Levels, "Levels", categoryDict);

            return categoryDict.Values
                .Where(c => c.ElementCount > 0)
                .OrderBy(c => c.CategoryName)
                .ToList();
        }

        private static void AddDatumCategoryIfAny(Document linkDoc, BuiltInCategory bic, string defaultName, Dictionary<string, LinkCategoryItem> dict)
        {
            if (dict.ContainsKey(defaultName)) return;

            var elems = new FilteredElementCollector(linkDoc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .ToList();

            if (elems.Any())
            {
                dict[defaultName] = new LinkCategoryItem
                {
                    CategoryId = new ElementId(bic),
                    CategoryName = defaultName,
                    ElementIds = elems
                };
            }
        }

        /// <summary>
        /// Thực hiện sao chép các Element từ Link Document sang Host Document theo đúng Transform.
        /// </summary>
        public static (int copiedCount, List<string> errors) CopyElements(
            Document hostDoc,
            Document linkDoc,
            Transform transform,
            ICollection<ElementId> elementIdsToCopy)
        {
            var errors = new List<string>();
            if (hostDoc == null || linkDoc == null || elementIdsToCopy == null || !elementIdsToCopy.Any())
            {
                return (0, errors);
            }

            var copyOptions = new CopyPasteOptions();
            copyOptions.SetDuplicateTypeNamesHandler(new CustomDuplicateTypeHandler());

            ICollection<ElementId> copiedIds = new List<ElementId>();

            try
            {
                copiedIds = ElementTransformUtils.CopyElements(
                    linkDoc,
                    elementIdsToCopy,
                    hostDoc,
                    transform,
                    copyOptions);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            return (copiedIds?.Count ?? 0, errors);
        }

        private class CustomDuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                // Sử dụng type đã có sẵn trong host doc để tránh trùng lặp rác
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
