using System;
using System.Text.RegularExpressions;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class NamingTemplateManager
    {
        public static string ComputeFileName(SheetExportItem item, NamingTemplate template, string projectCode)
        {
            return NamingPlanService.Expand(item, template, ExportJobOptions.Normalize(new ExportOptions { ProjectCode = projectCode }));
        }

        public static bool ValidateFileNameRegex(string fileName, NamingTemplate template, out string errorMsg)
        {
            errorMsg = "";
            if (template == null || string.IsNullOrWhiteSpace(template.RegexPattern)) return true;

            try { return Regex.IsMatch(fileName, template.RegexPattern); }
            catch (Exception ex) { errorMsg = $"Regex pattern không hợp lệ: {ex.Message}"; return false; }
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
