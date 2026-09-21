using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;
using Newtonsoft.Json;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoSnapshotService
    {
        public static string Issue(QtoResult result, QtoRuleProfile profile)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string folder = GetFolder(result.DocumentTitle);
            Directory.CreateDirectory(folder);
            var snapshot = new QtoSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString("N"), DocumentTitle = result.DocumentTitle,
                CreatedAt = DateTime.Now, ProfileId = profile?.ProfileId ?? "", ProfileVersion = profile?.Version ?? 0,
                Lines = result.Lines.Where(x => x.IsIncluded).Select(ToSnapshotLine).ToList()
            };
            string path = Path.Combine(folder, $"{snapshot.CreatedAt:yyyyMMdd-HHmmssfff}_{snapshot.SnapshotId}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
            return path;
        }

        public static QtoSnapshot LoadLatest(string documentTitle)
        {
            string folder = GetFolder(documentTitle);
            if (!Directory.Exists(folder)) return null;
            string path = Directory.GetFiles(folder, "*.json").OrderByDescending(x => x).FirstOrDefault();
            if (path == null) return null;
            try { return JsonConvert.DeserializeObject<QtoSnapshot>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public static List<QtoVarianceLine> Compare(QtoResult current, QtoSnapshot previous)
        {
            var output = new List<QtoVarianceLine>();
            if (current == null || previous == null) return output;
            var currentMap = current.Lines.Where(x => x.IsIncluded).ToDictionary(BuildKey, x => x, StringComparer.OrdinalIgnoreCase);
            var previousMap = previous.Lines.ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);
            foreach (string key in currentMap.Keys.Union(previousMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                currentMap.TryGetValue(key, out QtoLine now);
                previousMap.TryGetValue(key, out QtoSnapshotLine before);
                double oldValue = before?.Quantity ?? 0;
                double newValue = now?.PayQuantity ?? 0;
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

        public static string BuildKey(QtoLine line) => string.Join("|", line.Code, line.Material, line.TypeName, line.Level, line.Unit);

        private static QtoSnapshotLine ToSnapshotLine(QtoLine line) => new QtoSnapshotLine
        {
            Key = BuildKey(line), Code = line.Code, Description = line.Description, Unit = line.Unit,
            Quantity = line.PayQuantity, ElementUniqueIds = line.ElementUniqueIds.ToList()
        };

        private static string GetFolder(string documentTitle) => Path.Combine(QtoRuleProfileService.GetProjectFolder(documentTitle), "Snapshots");
    }
}
