using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using KhimTools.Core.Workflow;
using KhimTools.QuantityTakeoff.Models;
using Newtonsoft.Json;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoSnapshotService
    {
        private const long MaximumSnapshotBytes = 20L * 1024L * 1024L;
        public static string Issue(QtoResult result, QtoRuleProfile profile)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string folder = GetFolder(result.DocumentTitle);
            Directory.CreateDirectory(folder);
            var snapshot = new QtoSnapshot
            {
                SchemaVersion = 2,
                SnapshotId = Guid.NewGuid().ToString("N"), DocumentTitle = result.DocumentTitle,
                CreatedAt = DateTime.Now, ProfileId = profile?.ProfileId ?? "", ProfileVersion = profile?.Version ?? 0,
                Lines = result.Lines.Where(x => x.IsIncluded).Select(ToSnapshotLine).ToList()
            };
            string path = Path.Combine(folder, $"{snapshot.CreatedAt:yyyyMMdd-HHmmssfff}_{snapshot.SnapshotId}.json");
            string temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            return path;
        }

        public static QtoSnapshot LoadLatest(string documentTitle)
        {
            string folder = GetFolder(documentTitle);
            if (!Directory.Exists(folder)) return null;
            foreach (string path in Directory.GetFiles(folder, "*.json").OrderByDescending(x => x))
            {
                try
                {
                    var file = new FileInfo(path);
                    if (file.Length <= 0 || file.Length > MaximumSnapshotBytes) continue;
                    QtoSnapshot snapshot = JsonConvert.DeserializeObject<QtoSnapshot>(File.ReadAllText(path),
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });
                    if (IsValidSnapshot(snapshot)) return snapshot;
                }
                catch { /* Immutable snapshots: skip a damaged record and try the preceding one. */ }
            }
            return null;
        }

        private static bool IsValidSnapshot(QtoSnapshot snapshot)
        {
            if (snapshot == null || (snapshot.SchemaVersion != 1 && snapshot.SchemaVersion != 2) ||
                string.IsNullOrWhiteSpace(snapshot.SnapshotId) || snapshot.SnapshotId.Length > 100 ||
                string.IsNullOrWhiteSpace(snapshot.DocumentTitle) || snapshot.CreatedAt == DateTime.MinValue ||
                snapshot.ProfileVersion < 0 || snapshot.Lines == null || snapshot.Lines.Count > 100000) return false;
            return snapshot.Lines.All(line => line != null && !double.IsNaN(line.Quantity) &&
                !double.IsInfinity(line.Quantity) && line.Quantity >= 0 && line.ElementUniqueIds != null &&
                line.ElementUniqueIds.Count <= 100000);
        }

        public static List<QtoVarianceLine> Compare(QtoResult current, QtoSnapshot previous)
        {
            var output = new List<QtoVarianceLine>();
            if (current == null || previous == null) return output;
            bool hasDetailedKeys = previous.SchemaVersion >= 2;
            Dictionary<string, QtoCompareValue> currentMap = hasDetailedKeys
                ? current.Lines.Where(x => x.IsIncluded).ToDictionary(BuildKey, QtoCompareValue.From, StringComparer.OrdinalIgnoreCase)
                : current.Lines.Where(x => x.IsIncluded).GroupBy(BuildLegacyKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => QtoCompareValue.From(x.First(), x.Sum(y => y.PayQuantity)), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, QtoSnapshotLine> previousMap = previous.Lines
                .GroupBy(x => x.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => new QtoSnapshotLine
                {
                    Key = x.Key, Code = x.First().Code, Description = x.First().Description,
                    Unit = x.First().Unit, Quantity = x.Sum(y => y.Quantity)
                }, StringComparer.OrdinalIgnoreCase);
            foreach (string key in currentMap.Keys.Union(previousMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                currentMap.TryGetValue(key, out QtoCompareValue now);
                previousMap.TryGetValue(key, out QtoSnapshotLine before);
                double oldValue = before?.Quantity ?? 0;
                double newValue = now?.Quantity ?? 0;
                double difference = newValue - oldValue;
                output.Add(new QtoVarianceLine
                {
                    Key = key, Code = now?.Code ?? before?.Code ?? "", Description = now?.Description ?? before?.Description ?? "",
                    Unit = now?.Unit ?? before?.Unit ?? "", PreviousQuantity = oldValue, CurrentQuantity = newValue,
                    Difference = difference, DifferencePercent = Math.Abs(oldValue) < 1e-9 ? (double?)null : difference / oldValue * 100.0,
                    Status = before == null ? "Added" : now == null ? "Removed" : Math.Abs(difference) < 1e-9 ? "Unchanged" : "Changed"
                });
            }
            return output;
        }

        public static string BuildKey(QtoLine line) => WorkflowFingerprint.Compute(
            line.Code, line.CategoryId.ToString(CultureInfo.InvariantCulture), line.Material,
            line.FamilyName, line.TypeUniqueId, line.TypeName, line.LevelUniqueId, line.Level, line.Unit);

        private static string BuildLegacyKey(QtoLine line) => string.Join("|", line.Code, line.Material, line.TypeName, line.Level, line.Unit);

        private static QtoSnapshotLine ToSnapshotLine(QtoLine line) => new QtoSnapshotLine
        {
            Key = BuildKey(line), Code = line.Code, Description = line.Description,
            Category = line.Category, CategoryId = line.CategoryId, Material = line.Material,
            FamilyName = line.FamilyName, TypeUniqueId = line.TypeUniqueId,
            Level = line.Level, LevelUniqueId = line.LevelUniqueId, Unit = line.Unit,
            Quantity = line.PayQuantity, ElementUniqueIds = line.ElementUniqueIds.ToList()
        };

        private sealed class QtoCompareValue
        {
            public string Code { get; private set; }
            public string Description { get; private set; }
            public string Unit { get; private set; }
            public double Quantity { get; private set; }
            public static QtoCompareValue From(QtoLine line) => From(line, line.PayQuantity);
            public static QtoCompareValue From(QtoLine line, double quantity) => new QtoCompareValue
            {
                Code = line.Code, Description = line.Description, Unit = line.Unit, Quantity = quantity
            };
        }

        private static string GetFolder(string documentTitle) => Path.Combine(QtoRuleProfileService.GetProjectFolder(documentTitle), "Snapshots");
    }
}
