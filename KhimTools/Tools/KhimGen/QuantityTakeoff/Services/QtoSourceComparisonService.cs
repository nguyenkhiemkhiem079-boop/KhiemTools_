using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoSourceComparisonService
    {
        public static CubicostImportResult CreateCubicostBaseline(QtoResult revit)
        {
            var result = new CubicostImportResult { FilePath = "(Generated from Revit)", SheetName = "K-QS Generated" };
            if (revit == null) return result;
            int row = 1;
            foreach (QtoLine line in revit.Lines.Where(x => x.IsIncluded))
                result.Lines.Add(new CubicostQtoLine
                {
                    SourceRow = row++, CubicostCode = line.Code, RevitCode = line.Code,
                    Description = line.Description, Location = line.Level, Unit = line.Unit, Quantity = line.PayQuantity
                });
            result.Warnings.Add("Đây là baseline Cubicost nội bộ sinh từ QS Revit; hãy chỉnh mã/khối lượng ở tab QS Cubicost trước khi Compare.");
            return result;
        }

        public static List<QtoSourceComparison> Compare(QtoResult revit, CubicostImportResult cubicost)
        {
            if (revit == null || cubicost == null) return new List<QtoSourceComparison>();
            var revitMap = revit.Lines.Where(x => x.IsIncluded).GroupBy(x => Key(x.Code, x.Unit), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.PayQuantity), StringComparer.OrdinalIgnoreCase);
            var cubicostMap = cubicost.Lines.Where(x => !string.IsNullOrWhiteSpace(x.RevitCode)).GroupBy(x => Key(x.RevitCode, x.Unit), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity), StringComparer.OrdinalIgnoreCase);
            var output = new List<QtoSourceComparison>();
            foreach (string key in revitMap.Keys.Union(cubicostMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                revitMap.TryGetValue(key, out double revitQuantity);
                cubicostMap.TryGetValue(key, out double cubicostQuantity);
                string[] parts = key.Split('|');
                double difference = revitQuantity - cubicostQuantity;
                output.Add(new QtoSourceComparison
                {
                    RevitCode = parts[0], Unit = parts.Length > 1 ? parts[1] : "", RevitQuantity = revitQuantity,
                    CubicostQuantity = cubicostQuantity, Difference = difference,
                    DifferencePercent = Math.Abs(cubicostQuantity) < 1e-9 ? (double?)null : difference / cubicostQuantity * 100.0,
                    Status = revitQuantity == 0 ? "Only Cubicost" : cubicostQuantity == 0 ? "Only Revit" : Math.Abs(difference) < 1e-9 ? "Matched" : "Variance"
                });
            }
            return output;
        }

        private static string Key(string code, string unit) => (code ?? "").Trim().ToUpperInvariant() + "|" + (unit ?? "").Trim().ToLowerInvariant();
    }
}
