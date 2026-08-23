using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SectionCutTool.Models;

namespace KhimTools.SectionCutTool.Core
{
    /// <summary>
    /// Bộ xử lý đặt tên tự động cho Section View với hỗ trợ dynamic tokens ({Mark}, {Type}, {Index}, {Level}, {Category}, {Pos})
    /// và thuật toán tránh trùng tên view trong Revit.
    /// </summary>
    public static class SectionNamingHelper
    {
        public static string FormatSectionName(
            string pattern,
            ElementCutItem item,
            int index,
            string positionLabel,
            bool isLongitudinal)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                pattern = isLongitudinal ? "MC-D-{Mark}" : "MC-N-{Mark}-{Index}";
            }

            string mark = !string.IsNullOrWhiteSpace(item?.Mark) ? item.Mark : (item?.Id.ToLongValue().ToString() ?? "0");
            string type = !string.IsNullOrWhiteSpace(item?.TypeName) ? item.TypeName : "Type";
            string category = !string.IsNullOrWhiteSpace(item?.CategoryName) ? item.CategoryName : "Element";
            string level = !string.IsNullOrWhiteSpace(item?.LevelName) ? item.LevelName : "Level";
            string idxStr = index.ToString("D2"); // 01, 02, 03...

            string name = pattern
                .Replace("{Mark}", mark)
                .Replace("{mark}", mark)
                .Replace("{Type}", type)
                .Replace("{type}", type)
                .Replace("{Category}", category)
                .Replace("{category}", category)
                .Replace("{Level}", level)
                .Replace("{level}", level)
                .Replace("{Index}", idxStr)
                .Replace("{index}", idxStr)
                .Replace("{Pos}", positionLabel)
                .Replace("{pos}", positionLabel);

            // Loại bỏ các ký tự cấm trong tên View của Revit: \ / : { } [ ] | ; < > ? ` ~ " *
            char[] invalidChars = { '\\', '/', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', '"', '*' };
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        /// <summary>
        /// Tạo tên View không trùng lặp bằng cách kiểm tra trên tập HashSet tên view đã tồn tại (Tối ưu hiệu năng O(1), không truy vấn lại DB Revit).
        /// </summary>
        public static string GetUniqueViewName(ISet<string> existingNames, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return "Section_01";
            if (existingNames == null) return baseName;

            if (!existingNames.Contains(baseName))
            {
                existingNames.Add(baseName);
                return baseName;
            }

            int counter = 1;
            string candidate;
            do
            {
                candidate = $"{baseName}_{counter:D2}";
                counter++;
            } while (existingNames.Contains(candidate));

            existingNames.Add(candidate);
            return candidate;
        }

        /// <summary>
        /// Tạo tên View không trùng lặp trong Document bằng cách thêm hậu tố số tăng dần nếu tên đã tồn tại.
        /// </summary>
        public static string GetUniqueViewName(Document doc, string baseName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(baseName)) return baseName;

            var existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate)
                    .Select(v => v.Name),
                StringComparer.OrdinalIgnoreCase);

            return GetUniqueViewName(existingNames, baseName);
        }
    }
}
