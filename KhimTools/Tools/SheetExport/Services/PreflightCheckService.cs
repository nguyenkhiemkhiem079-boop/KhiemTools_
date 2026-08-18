using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public class PreflightWarning
    {
        public string Title { get; set; }
        public string Details { get; set; }
        public bool IsCritical { get; set; }
    }

    public static class PreflightCheckService
    {
        public static List<PreflightWarning> RunPreflightChecks(List<SheetExportItem> selectedSheets)
        {
            var warnings = new List<PreflightWarning>();
            if (selectedSheets == null || !selectedSheets.Any()) return warnings;

            // 1. Check paper size consistency
            var sizeGroups = selectedSheets.GroupBy(s => s.PaperSize).ToList();
            if (sizeGroups.Count > 1)
            {
                var mainGroup = sizeGroups.OrderByDescending(g => g.Count()).First();
                var anomalyCount = selectedSheets.Count - mainGroup.Count();
                warnings.Add(new PreflightWarning
                {
                    Title = "Phát hiện khác biệt kích thước khổ giấy",
                    Details = $"Đa số sheet là khổ '{mainGroup.Key}' ({mainGroup.Count()} sheet), " +
                              $"nhưng có {anomalyCount} sheet khác khổ ({string.Join(", ", sizeGroups.Where(g => g.Key != mainGroup.Key).Select(g => g.Key))}).",
                    IsCritical = false
                });
            }

            // 2. Check orientation consistency
            var orientGroups = selectedSheets.GroupBy(s => s.Orientation).ToList();
            if (orientGroups.Count > 1)
            {
                warnings.Add(new PreflightWarning
                {
                    Title = "Phát hiện khác biệt hướng xoay trang (Orientation)",
                    Details = $"Có {orientGroups.FirstOrDefault(g => g.Key == "Landscape")?.Count() ?? 0} sheet Landscape và " +
                              $"{orientGroups.FirstOrDefault(g => g.Key == "Portrait")?.Count() ?? 0} sheet Portrait.",
                    IsCritical = false
                });
            }

            // 3. Check Regex invalid file names
            var invalidNames = selectedSheets.Where(s => !s.IsRegexValid).ToList();
            if (invalidNames.Any())
            {
                warnings.Add(new PreflightWarning
                {
                    Title = "Tên file không đúng chuẩn Naming Convention (Regex)",
                    Details = $"Có {invalidNames.Count} sheet ({string.Join(", ", invalidNames.Take(3).Select(s => s.SheetNumber))}) không khớp quy tắc Regex.",
                    IsCritical = true
                });
            }

            return warnings;
        }
    }
}
