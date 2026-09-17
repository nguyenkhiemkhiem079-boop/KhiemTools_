using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsRevisionCompareService
    {
        public static List<QsCompareRow> Compare(QsQuantitySnapshot baseline, QsQuantitySnapshot current, double tolerance = 1e-6)
        {
            var oldMap = (baseline?.Quantities ?? new List<QsSnapshotQuantity>()).GroupBy(Key).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity), StringComparer.OrdinalIgnoreCase);
            var newMap = (current?.Quantities ?? new List<QsSnapshotQuantity>()).GroupBy(Key).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity), StringComparer.OrdinalIgnoreCase);
            var rows = new List<QsCompareRow>();
            foreach (var key in oldMap.Keys.Union(newMap.Keys, StringComparer.OrdinalIgnoreCase)) { oldMap.TryGetValue(key, out var oldQ); newMap.TryGetValue(key, out var newQ); var delta = newQ - oldQ; rows.Add(new QsCompareRow { UniqueId = key.Split('|')[0], Measurement = key.Split('|')[1], Unit = key.Split('|').Last(), BaselineQuantity = oldQ, CurrentQuantity = newQ, Delta = delta, DeltaPercent = Math.Abs(oldQ) < tolerance ? (double?)null : delta / oldQ * 100, Status = !oldMap.ContainsKey(key) ? "ADDED" : !newMap.ContainsKey(key) ? "REMOVED" : Math.Abs(delta) <= tolerance ? "UNCHANGED" : "MODIFIED" }); }
            return rows;
        }
        private static string Key(QsSnapshotQuantity q) => string.Join("|", q.ElementUniqueId, q.MeasurementCode, q.BoqCode, q.Unit, q.Material);
    }
}
