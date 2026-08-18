using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Utilities;

namespace KhimTools.SlabJoin.Services
{
    /// <summary>
    /// Service tổng quát cho Join/Unjoin/Switch giữa bất kỳ cặp Category nào.
    /// Mở rộng từ SlabJoinService hiện tại, hỗ trợ cross-category join.
    /// </summary>
    public class ElementJoinService
    {
        private const double BbTolerance = 0.003; // ~1mm

        public delegate void LogCallback(string message);

        // ─── JOIN ────────────────────────────────────────────────────────

        public List<JoinPairResult> JoinByRules(
            Document doc, List<CategoryMatchRule> rules, ScopeMode scope,
            ICollection<ElementId> selectedIds, LogCallback log)
        {
            var results = new List<JoinPairResult>();
            if (rules == null || !rules.Any()) return results;

            foreach (var rule in rules)
            {
                log?.Invoke($"[JOIN] Processing: {rule}");
                var pairs = FindCandidatePairs(doc, rule, scope, selectedIds, log);
                log?.Invoke($"  → Found {pairs.Count} candidate pairs");

                int batchSize = 50;
                for (int i = 0; i < pairs.Count; i += batchSize)
                {
                    var chunk = pairs.Skip(i).Take(batchSize).ToList();
                    using (var tx = new Transaction(doc, $"Join Elements ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})"))
                    {
                        tx.Start();
                        var failOpts = tx.GetFailureHandlingOptions();
                        failOpts.SetFailuresPreprocessor(new SwallowWarningsPreprocessor());
                        tx.SetFailureHandlingOptions(failOpts);

                        foreach (var pair in chunk)
                        {
                            var r = TryJoin(doc, pair.Item1, pair.Item2);
                            results.Add(r);
                        }
                        tx.Commit();
                    }
                }
                int ok = results.Count(r => r.Success);
                log?.Invoke($"  → Joined: {ok}, Skipped/Failed: {results.Count - ok}");
            }

            return results;
        }

        // ─── UNJOIN ──────────────────────────────────────────────────────

        public List<JoinPairResult> UnjoinByRules(
            Document doc, List<CategoryMatchRule> rules, ScopeMode scope,
            ICollection<ElementId> selectedIds, LogCallback log)
        {
            var results = new List<JoinPairResult>();
            if (rules == null || !rules.Any()) return results;

            foreach (var rule in rules)
            {
                log?.Invoke($"[UNJOIN] Processing: {rule}");
                var pairs = FindCandidatePairs(doc, rule, scope, selectedIds, log);
                log?.Invoke($"  → Found {pairs.Count} candidate pairs");

                int batchSize = 50;
                for (int i = 0; i < pairs.Count; i += batchSize)
                {
                    var chunk = pairs.Skip(i).Take(batchSize).ToList();
                    using (var tx = new Transaction(doc, $"Unjoin Elements ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})"))
                    {
                        tx.Start();
                        var failOpts = tx.GetFailureHandlingOptions();
                        failOpts.SetFailuresPreprocessor(new SwallowWarningsPreprocessor());
                        tx.SetFailureHandlingOptions(failOpts);

                        foreach (var pair in chunk)
                        {
                            var r = TryUnjoin(doc, pair.Item1, pair.Item2);
                            results.Add(r);
                        }
                        tx.Commit();
                    }
                }
            }
            return results;
        }

        // ─── SWITCH ─────────────────────────────────────────────────────

        public List<JoinPairResult> SwitchByRules(
            Document doc, List<CategoryMatchRule> rules, ScopeMode scope,
            ICollection<ElementId> selectedIds, LogCallback log)
        {
            var results = new List<JoinPairResult>();
            if (rules == null || !rules.Any()) return results;

            foreach (var rule in rules)
            {
                log?.Invoke($"[SWITCH] Processing: {rule}");
                var pairs = FindCandidatePairs(doc, rule, scope, selectedIds, log);

                int batchSize = 50;
                for (int i = 0; i < pairs.Count; i += batchSize)
                {
                    var chunk = pairs.Skip(i).Take(batchSize).ToList();
                    using (var tx = new Transaction(doc, $"Switch Join Order ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})"))
                    {
                        tx.Start();
                        var failOpts = tx.GetFailureHandlingOptions();
                        failOpts.SetFailuresPreprocessor(new SwallowWarningsPreprocessor());
                        tx.SetFailureHandlingOptions(failOpts);

                        foreach (var pair in chunk)
                        {
                            var r = TrySwitchOrder(doc, pair.Item1, pair.Item2);
                            results.Add(r);
                        }
                        tx.Commit();
                    }
                }
            }
            return results;
        }

        // ─── CANDIDATE PAIR FINDER ──────────────────────────────────────

        private List<Tuple<ElementId, ElementId>> FindCandidatePairs(
            Document doc, CategoryMatchRule rule, ScopeMode scope,
            ICollection<ElementId> selectedIds, LogCallback log)
        {
            var elemsA = CollectElements(doc, rule.CategoryA, scope, selectedIds);
            var elemsB = (rule.CategoryA == rule.CategoryB)
                ? elemsA
                : CollectElements(doc, rule.CategoryB, scope, selectedIds);

            log?.Invoke($"  Elements A ({CategoryMatchRule.CategoryDisplayName(rule.CategoryA)}): {elemsA.Count}");
            if (rule.CategoryA != rule.CategoryB)
                log?.Invoke($"  Elements B ({CategoryMatchRule.CategoryDisplayName(rule.CategoryB)}): {elemsB.Count}");

            var seen = new HashSet<string>();
            var pairs = new List<Tuple<ElementId, ElementId>>();

            foreach (var a in elemsA)
            {
                BoundingBoxXYZ bbA = a.get_BoundingBox(null);
                if (bbA == null) continue;

                foreach (var b in elemsB)
                {
                    if (a.Id == b.Id) continue;
                    string key = MakeKey(a.Id, b.Id);
                    if (seen.Contains(key)) continue;

                    BoundingBoxXYZ bbB = b.get_BoundingBox(null);
                    if (bbB == null) continue;

                    if (BoundingBoxesOverlap(bbA, bbB))
                    {
                        seen.Add(key);
                        pairs.Add(Tuple.Create(a.Id, b.Id));
                    }
                }
            }

            return pairs;
        }

        private List<Element> CollectElements(Document doc, BuiltInCategory category, ScopeMode scope, ICollection<ElementId> selectedIds)
        {
            switch (scope)
            {
                case ScopeMode.CurrentView:
                    var activeView = doc.ActiveView;
                    if (activeView == null)
                        return new FilteredElementCollector(doc).OfCategory(category).WhereElementIsNotElementType().ToList();
                    return new FilteredElementCollector(doc, activeView.Id).OfCategory(category).WhereElementIsNotElementType().ToList();

                case ScopeMode.Selection:
                    if (selectedIds == null || !selectedIds.Any())
                        return new List<Element>();
                    return selectedIds
                        .Select(id => doc.GetElement(id))
                        .Where(e => e != null && e.Category.IsCategory(category))
                        .ToList();

                case ScopeMode.AllModel:
                default:
                    return new FilteredElementCollector(doc).OfCategory(category).WhereElementIsNotElementType().ToList();
            }
        }

        // ─── JOIN/UNJOIN/SWITCH LOGIC ───────────────────────────────────

        private JoinPairResult TryJoin(Document doc, ElementId idA, ElementId idB)
        {
            Element a = doc.GetElement(idA);
            Element b = doc.GetElement(idB);
            if (a == null || b == null) return new JoinPairResult(idA, idB, false, true, "Invalid element.");

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    if (JoinGeometryUtils.AreElementsJoined(doc, a, b))
                        JoinGeometryUtils.UnjoinGeometry(doc, a, b);

                    bool ok = TryJoinOrder(doc, a, b) || TryJoinOrder(doc, b, a);
                    if (ok) { sub.Commit(); return new JoinPairResult(idA, idB, true, false, "Joined."); }
                    sub.RollBack();
                    return new JoinPairResult(idA, idB, false, true, "Join rejected.");
                }
                catch (Exception ex)
                {
                    try { sub.RollBack(); } catch { }
                    return new JoinPairResult(idA, idB, false, true, $"Error: {ex.Message}");
                }
            }
        }

        private JoinPairResult TryUnjoin(Document doc, ElementId idA, ElementId idB)
        {
            Element a = doc.GetElement(idA);
            Element b = doc.GetElement(idB);
            if (a == null || b == null) return new JoinPairResult(idA, idB, false, true, "Invalid element.");

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
                    {
                        sub.RollBack();
                        return new JoinPairResult(idA, idB, false, false, "Not joined.");
                    }
                    JoinGeometryUtils.UnjoinGeometry(doc, a, b);
                    sub.Commit();
                    return new JoinPairResult(idA, idB, true, false, "Unjoined.");
                }
                catch (Exception ex)
                {
                    try { sub.RollBack(); } catch { }
                    return new JoinPairResult(idA, idB, false, true, $"Error: {ex.Message}");
                }
            }
        }

        private JoinPairResult TrySwitchOrder(Document doc, ElementId idA, ElementId idB)
        {
            Element a = doc.GetElement(idA);
            Element b = doc.GetElement(idB);
            if (a == null || b == null) return new JoinPairResult(idA, idB, false, true, "Invalid element.");

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
                    {
                        sub.RollBack();
                        return new JoinPairResult(idA, idB, false, false, "Not joined — cannot switch.");
                    }
                    JoinGeometryUtils.SwitchJoinOrder(doc, a, b);
                    sub.Commit();
                    return new JoinPairResult(idA, idB, true, false, "Switched.");
                }
                catch (Exception ex)
                {
                    try { sub.RollBack(); } catch { }
                    return new JoinPairResult(idA, idB, false, true, $"Error: {ex.Message}");
                }
            }
        }

        private static bool TryJoinOrder(Document doc, Element a, Element b)
        {
            try { JoinGeometryUtils.JoinGeometry(doc, a, b); return true; }
            catch { return false; }
        }

        // ─── HELPERS ────────────────────────────────────────────────────

        private static bool BoundingBoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            return (a.Max.X + BbTolerance >= b.Min.X && a.Min.X - BbTolerance <= b.Max.X) &&
                   (a.Max.Y + BbTolerance >= b.Min.Y && a.Min.Y - BbTolerance <= b.Max.Y) &&
                   (a.Max.Z + BbTolerance >= b.Min.Z && a.Min.Z - BbTolerance <= b.Max.Z);
        }

        private static string MakeKey(ElementId a, ElementId b)
        {
            long la = a.ToLongValue(), lb = b.ToLongValue();
            return la < lb ? $"{la}_{lb}" : $"{lb}_{la}";
        }
    }
}
