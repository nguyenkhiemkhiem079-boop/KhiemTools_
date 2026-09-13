using System;
using System.Collections.Generic;
using System.IO;
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
        public static List<PreflightWarning> RunPreflightChecks(List<SheetExportItem> selectedSheets, string outputDirectory = "", bool combinePdf = false)
        {
            var warnings = new List<PreflightWarning>();
            if (selectedSheets == null || !selectedSheets.Any()) return warnings;

            // 1. Check paper size consistency
            var sizeGroups = selectedSheets.GroupBy(s => s.PaperSize).ToList();
            if (combinePdf && sizeGroups.Count > 1)
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
            if (combinePdf && orientGroups.Count > 1)
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

            var duplicateNames = selectedSheets
                .Where(s => !string.IsNullOrWhiteSpace(s.ComputedFileName))
                .GroupBy(s => s.ComputedFileName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateNames.Any())
            {
                warnings.Add(new PreflightWarning
                {
                    Title = "Tên file bị trùng",
                    Details = string.Join(", ", duplicateNames.Take(5).Select(g => g.Key)),
                    IsCritical = true
                });
            }

            var unknownPaper = selectedSheets.Where(s => string.IsNullOrWhiteSpace(s.PaperSize) || s.PaperSize.IndexOf("Unknown", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (unknownPaper.Any())
            {
                warnings.Add(new PreflightWarning
                {
                    Title = "Không xác định được khổ giấy",
                    Details = $"Kiểm tra title block của: {string.Join(", ", unknownPaper.Take(8).Select(s => s.SheetNumber))}.",
                    IsCritical = false
                });
            }

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                var longPaths = selectedSheets.Where(s => Path.Combine(outputDirectory, (s.ComputedFileName ?? "Sheet") + ".pdf").Length >= 240).ToList();
                if (longPaths.Any())
                {
                    warnings.Add(new PreflightWarning
                    {
                        Title = "Đường dẫn đầu ra quá dài",
                        Details = $"Rút ngắn thư mục hoặc mẫu tên cho: {string.Join(", ", longPaths.Take(8).Select(s => s.SheetNumber))}.",
                        IsCritical = true
                    });
                }
            }

            return warnings;
        }
    }
}
