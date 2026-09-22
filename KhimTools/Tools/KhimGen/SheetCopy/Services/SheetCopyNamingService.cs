using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyNamingService
    {
        public static string ApplyPattern(string pattern, SheetCopyItem source, int index, string prefix = "", string suffix = "")
        {
            string value = string.IsNullOrWhiteSpace(pattern) ? "{SheetNumber}-COPY" : pattern;
            value = value.Replace("{SheetNumber}", source == null ? string.Empty : source.SourceSheetNumber ?? string.Empty)
                .Replace("{SheetName}", source == null ? string.Empty : source.SourceSheetName ?? string.Empty)
                .Replace("{Index}", index.ToString());
            return (prefix ?? string.Empty) + value + (suffix ?? string.Empty);
        }

        public static string SuggestTargetNumber(Document doc, ViewSheet source, ISet<string> pendingNumbers = null)
        {
            string baseNumber = (source == null ? string.Empty : source.SheetNumber ?? string.Empty) + "-COPY";
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc != null)
                foreach (ViewSheet sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
                    used.Add(sheet.SheetNumber ?? string.Empty);
            if (pendingNumbers != null) foreach (string value in pendingNumbers) used.Add(value ?? string.Empty);
            string candidate = baseNumber;
            int index = 1;
            while (used.Contains(candidate)) candidate = baseNumber + "." + index++;
            return candidate;
        }

        public static string SuggestViewName(Document doc, string sourceName, ISet<string> pendingNames = null)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? "View" : sourceName.Trim() + " - Copy";
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc != null)
                foreach (View view in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
                    if (!view.IsTemplate) used.Add(view.Name ?? string.Empty);
            if (pendingNames != null) foreach (string value in pendingNames) used.Add(value ?? string.Empty);
            string candidate = baseName;
            int index = 2;
            while (used.Contains(candidate)) candidate = baseName + " " + index++;
            return candidate;
        }

        public static bool IsValidTargetNumber(string value, out string message)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) { message = "Target sheet number is required."; return false; }
            if (normalized.Length > 255 || normalized.Any(char.IsControl)) { message = "Target sheet number contains unsupported characters."; return false; }
            message = string.Empty;
            return true;
        }

        public static bool HasDuplicateNumbers(IEnumerable<SheetCopyItem> items, out string duplicate)
        {
            duplicate = string.Empty;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SheetCopyItem item in items ?? Enumerable.Empty<SheetCopyItem>())
            {
                string number = (item == null ? string.Empty : item.TargetSheetNumber ?? string.Empty).Trim();
                if (!seen.Add(number)) { duplicate = number; return true; }
            }
            return false;
        }
    }
}
