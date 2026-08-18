using System;
using System.Text.RegularExpressions;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class NamingTemplateManager
    {
        public static string ComputeFileName(SheetExportItem item, NamingTemplate template, string projectCode)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.Expression))
            {
                return SanitizeFileName($"{item.SheetNumber} - {item.SheetName}");
            }

            string result = template.Expression;
            string rev = string.IsNullOrWhiteSpace(item.CurrentRevisionNumber) ? "0" : item.CurrentRevisionNumber;
            string dateStr = DateTime.Now.ToString("yyyyMMdd");

            result = result.Replace("{ProjectCode}", projectCode ?? "PROJ")
                           .Replace("{SheetNumber}", item.SheetNumber ?? "")
                           .Replace("{SheetName}", item.SheetName ?? "")
                           .Replace("{Revision}", rev)
                           .Replace("{RevisionDate}", item.CurrentRevisionDate ?? "")
                           .Replace("{Date}", dateStr)
                           .Replace("{PaperSize}", item.PaperSize ?? "")
                           .Replace("{Orientation}", item.Orientation ?? "");

            return SanitizeFileName(result);
        }

        public static bool ValidateFileNameRegex(string fileName, NamingTemplate template, out string errorMsg)
        {
            errorMsg = "";
            if (template == null || string.IsNullOrWhiteSpace(template.RegexPattern)) return true;

            try
            {
                bool isMatch = Regex.IsMatch(fileName, template.RegexPattern);
                if (!isMatch)
                {
                    errorMsg = $"Tên file '{fileName}' không khớp pattern Regex: {template.RegexPattern}";
                }
                return isMatch;
            }
            catch (Exception ex)
            {
                errorMsg = $"Regex pattern không hợp lệ: {ex.Message}";
                return false;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Sheet";
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
