using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Logging;
using KhimTools.Core.Revit.Failures;
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
        private const double BbTolerance = 0.0328; // ~10mm tolerance

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
                    ExecuteChunk(doc, $"Join Elements ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})",
                        chunk, TryJoin, results, log);
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
                    ExecuteChunk(doc, $"Unjoin Elements ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})",
                        chunk, TryUnjoin, results, log);
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
                log?.Invoke($"  → Found {pairs.Count} candidate pairs");

                int batchSize = 50;
                for (int i = 0; i < pairs.Count; i += batchSize)
                {
                    var chunk = pairs.Skip(i).Take(batchSize).ToList();
                    ExecuteChunk(doc, $"Switch Join Order ({i + 1}-{Math.Min(i + batchSize, pairs.Count)})",
                        chunk, TrySwitchOrder, results, log);
                }
            }
            return results;
        }

        // ─── CANDIDATE PAIR DISCOVERY ───────────────────────────────────

        public List<Tuple<ElementId, ElementId>> FindCandidatePairs(
            Document doc, CategoryMatchRule rule, ScopeMode scope,
            ICollection<ElementId> selectedIds, LogCallback log = null)
        {
            var elemsA = CollectElements(doc, rule.CategoryA, scope, selectedIds);
            var elemsB = (rule.CategoryA == rule.CategoryB)
                ? elemsA
                : CollectElements(doc, rule.CategoryB, scope, selectedIds);

            if (log != null)
            {
                log.Invoke($"  Elements A ({CategoryMatchRule.CategoryDisplayName(rule.CategoryA)}): {elemsA.Count}");
                log.Invoke($"  Elements B ({CategoryMatchRule.CategoryDisplayName(rule.CategoryB)}): {elemsB.Count}");
            }

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
                        .Where(e => e != null && e.Category != null && (e.Category.Id.ToLongValue() == (long)category || e.Category.IsCategory(category)))
                        .ToList();

                case ScopeMode.AllModel:
                default:
                    return new FilteredElementCollector(doc).OfCategory(category).WhereElementIsNotElementType().ToList();
            }
        }

        private void ExecuteChunk(
            Document doc,
            string transactionName,
            IList<Tuple<ElementId, ElementId>> chunk,
            Func<Document, ElementId, ElementId, JoinPairResult> operation,
            List<JoinPairResult> results,
            LogCallback log)
        {
            var attempted = new List<JoinPairResult>();
            var failurePolicy = new KnownWarningFailurePreprocessor();

            using (var tx = new Transaction(doc, transactionName))
            {
                try
                {
                    KhimTools.Core.Revit.TransactionBoundary.Start(tx, "ElementJoin.Chunk");
                    var failOpts = tx.GetFailureHandlingOptions();
                    failOpts.SetFailuresPreprocessor(failurePolicy);
                    tx.SetFailureHandlingOptions(failOpts);

                    foreach (var pair in chunk)
                    {
                        attempted.Add(operation(doc, pair.Item1, pair.Item2));
                    }

                    TransactionStatus commitStatus = tx.Commit();
                    if (commitStatus == TransactionStatus.Committed)
                    {
                        results.AddRange(attempted);
                        return;
                    }

                    string reason = DescribeTransactionFailure(commitStatus, failurePolicy.Records);
                    log?.Invoke($"  → {transactionName} did not commit: {reason}");
                    AddRolledBackResults(attempted, results, reason);
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "ElementJoin.Chunk");
                    }

                    string reason = "Transaction exception: " + ex.Message;
                    KToolsLog.Current.Exception("ElementJoin.ExecuteChunk", ex, "TRANSACTION");
                    log?.Invoke($"  → {transactionName} failed: {reason}");
                    AddRolledBackResults(attempted, results, reason);

                    for (int index = attempted.Count; index < chunk.Count; index++)
                    {
                        var pair = chunk[index];
                        results.Add(new JoinPairResult(pair.Item1, pair.Item2, false, true,
                            "Not attempted because the batch transaction failed."));
                    }
                }
            }
        }

        private static void AddRolledBackResults(
            IEnumerable<JoinPairResult> attempted,
            ICollection<JoinPairResult> results,
            string reason)
        {
            foreach (JoinPairResult result in attempted)
            {
                results.Add(result.Success
                    ? new JoinPairResult(result.FloorIdA, result.FloorIdB, false, true,
                        $"{result.Message} Transaction rolled back: {reason}")
                    : result);
            }
        }

        private static string DescribeTransactionFailure(
            TransactionStatus status,
            IReadOnlyList<FailureRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return "Transaction status: " + status;
            }

            return string.Join("; ", records.Select(record =>
                $"{record.Severity} {record.DefinitionId}: {record.Description}"));
        }

        // ─── JOIN/UNJOIN/SWITCH LOGIC ───────────────────────────────────

        private JoinPairResult TryJoin(Document doc, ElementId idA, ElementId idB)
        {
            Element a = doc.GetElement(idA);
            Element b = doc.GetElement(idB);
            if (a == null || b == null) return new JoinPairResult(idA, idB, false, true, "Invalid element.");

            using (var sub = new SubTransaction(doc))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(sub, "ElementJoin.Pair");
                try
                {
                    if (JoinGeometryUtils.AreElementsJoined(doc, a, b))
                        JoinGeometryUtils.UnjoinGeometry(doc, a, b);

                    bool ok = TryJoinOrder(doc, a, b) || TryJoinOrder(doc, b, a);
                    if (ok && JoinGeometryUtils.AreElementsJoined(doc, a, b))
                    {
                        KhimTools.Core.Revit.TransactionBoundary.Commit(sub, "ElementJoin.Pair");
                        return new JoinPairResult(idA, idB, true, false, "Joined.");
                    }
                    KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementJoin.Pair");
                    return new JoinPairResult(idA, idB, false, true, "Join rejected.");
                }
                catch (Exception ex)
                {
                    try { KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementJoin.Pair"); }
                    catch (Exception rollbackEx) { KToolsLog.Current.Exception("ElementJoin.Rollback", rollbackEx, "SUBTX_ROLLBACK"); }
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
                KhimTools.Core.Revit.TransactionBoundary.Start(sub, "ElementUnjoin.Pair");
                try
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementUnjoin.Pair");
                        return new JoinPairResult(idA, idB, false, false, "Not joined.");
                    }
                    JoinGeometryUtils.UnjoinGeometry(doc, a, b);
                    if (JoinGeometryUtils.AreElementsJoined(doc, a, b))
                        throw new InvalidOperationException("Unjoin postcondition failed.");
                    KhimTools.Core.Revit.TransactionBoundary.Commit(sub, "ElementUnjoin.Pair");
                    return new JoinPairResult(idA, idB, true, false, "Unjoined.");
                }
                catch (Exception ex)
                {
                    try { KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementUnjoin.Pair"); }
                    catch (Exception rollbackEx) { KToolsLog.Current.Exception("ElementUnjoin.Rollback", rollbackEx, "SUBTX_ROLLBACK"); }
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
                KhimTools.Core.Revit.TransactionBoundary.Start(sub, "ElementSwitch.Pair");
                try
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementSwitch.Pair");
                        return new JoinPairResult(idA, idB, false, false, "Not joined — cannot switch.");
                    }
                    JoinGeometryUtils.SwitchJoinOrder(doc, a, b);
                    if (!JoinGeometryUtils.AreElementsJoined(doc, a, b))
                        throw new InvalidOperationException("Switch-order postcondition failed.");
                    KhimTools.Core.Revit.TransactionBoundary.Commit(sub, "ElementSwitch.Pair");
                    return new JoinPairResult(idA, idB, true, false, "Switched.");
                }
                catch (Exception ex)
                {
                    try { KhimTools.Core.Revit.TransactionBoundary.RollBack(sub, "ElementSwitch.Pair"); }
                    catch (Exception rollbackEx) { KToolsLog.Current.Exception("ElementSwitch.Rollback", rollbackEx, "SUBTX_ROLLBACK"); }
                    return new JoinPairResult(idA, idB, false, true, $"Error: {ex.Message}");
                }
            }
        }

        private static bool TryJoinOrder(Document doc, Element a, Element b)
        {
            try { JoinGeometryUtils.JoinGeometry(doc, a, b); return true; }
            catch (Exception ex)
            {
                KToolsLog.Current.Exception("ElementJoin.JoinOrder", ex, "JOIN_ORDER");
                return false;
            }
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
