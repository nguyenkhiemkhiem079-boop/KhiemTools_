using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public sealed class NamingPlanException : InvalidOperationException
    {
        public ExportPreflightCode Code { get; private set; }
        public NamingPlanException(ExportPreflightCode code, string message) : base(message) { Code = code; }
    }

    public static class NamingPlanService
    {
        public static readonly HashSet<string> SupportedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProjectCode", "Project", "SheetNumber", "SheetName", "Revision", "RevisionDate", "Date", "PaperSize", "Orientation"
        };

        public static string Expand(SheetExportItem item, NamingTemplate template, ExportJobOptions options)
        {
            if (item == null) throw new ArgumentNullException("item");
            string expression = template == null || string.IsNullOrWhiteSpace(template.Expression)
                ? "{SheetNumber} - {SheetName}" : template.Expression;
            string expanded = Regex.Replace(expression, "\\{([^{}]+)\\}", match =>
            {
                string token = match.Groups[1].Value.Trim();
                if (!SupportedTokens.Contains(token))
                    throw new NamingPlanException(ExportPreflightCode.UNKNOWN_NAMING_TOKEN, "UNKNOWN_NAMING_TOKEN: " + token);
                return TokenValue(token, item, options);
            });
            // Validate trailing dot/space before sanitation trims display whitespace.
            // Windows rejects these names, so surface the actionable preflight code.
            if (expanded.EndsWith(".", StringComparison.Ordinal) || expanded.EndsWith(" ", StringComparison.Ordinal))
                throw new NamingPlanException(ExportPreflightCode.INVALID_FILE_NAME, "INVALID_FILE_NAME: trailing dot or space.");
            string safe = Sanitize(expanded);
            if (string.IsNullOrWhiteSpace(safe)) throw new NamingPlanException(ExportPreflightCode.INVALID_FILE_NAME, "INVALID_OUTPUT_NAME");
            if (IsReservedWindowsName(safe)) throw new NamingPlanException(ExportPreflightCode.INVALID_FILE_NAME, "INVALID_FILE_NAME: reserved Windows name.");
            if (template != null && !string.IsNullOrWhiteSpace(template.RegexPattern))
            {
                try
                {
                    if (!Regex.IsMatch(safe, template.RegexPattern))
                        throw new NamingPlanException(ExportPreflightCode.INVALID_NAMING_REGEX, "Tên file không khớp Regex.");
                }
                catch (NamingPlanException) { throw; }
                catch (Exception ex) { throw new NamingPlanException(ExportPreflightCode.INVALID_NAMING_REGEX, ex.Message); }
            }
            return safe;
        }

        public static List<string> FindDuplicates(IEnumerable<SheetExportItem> items)
        {
            return (items ?? Enumerable.Empty<SheetExportItem>()).Where(item => item != null && !string.IsNullOrWhiteSpace(item.ComputedFileName))
                .GroupBy(item => item.ComputedFileName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string Sanitize(string value)
        {
            if (value == null) return "";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim();
        }

        public static bool IsReservedWindowsName(string value)
        {
            string stem = Path.GetFileNameWithoutExtension((value ?? "").Trim().TrimEnd('.')).ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return true;
            if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && char.IsDigit(stem[3])) return true;
            return false;
        }

        private static string TokenValue(string token, SheetExportItem item, ExportJobOptions options)
        {
            switch (token.ToUpperInvariant())
            {
                case "PROJECTCODE": return options?.ProjectCode ?? "";
                case "PROJECT": return options?.ProjectCode ?? "";
                case "SHEETNUMBER": return item.SheetNumber ?? "";
                case "SHEETNAME": return item.SheetName ?? "";
                case "REVISION": return item.CurrentRevisionNumber ?? "";
                case "REVISIONDATE": return item.CurrentRevisionDate ?? "";
                case "DATE": return (options?.IssueDate ?? DateTime.Today).ToString("yyyyMMdd");
                case "PAPERSIZE": return string.IsNullOrWhiteSpace(item.PaperSize) ? "Unknown" : item.PaperSize;
                case "ORIENTATION": return string.IsNullOrWhiteSpace(item.Orientation) ? "Unknown" : item.Orientation;
                default: return "";
            }
        }
    }
}
