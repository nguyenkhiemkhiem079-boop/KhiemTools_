using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    /// <summary>Safe replacement strategy for Cut/Join/Remove Zero: snapshot, create, regenerate, verify, delete, regenerate, verify.</summary>
    public static class DimensionRebuildService
    {
        public static DimensionResult RebuildSingle(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> refs, DimensionResult result, ElementId additionalDeleteId = null)
        {
            return Rebuild(doc, view, original, new[] { refs }, result, additionalDeleteId);
        }

        public static DimensionResult RebuildSplit(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> first, IList<DimensionReferenceInfo> second, DimensionResult result)
        {
            return Rebuild(doc, view, original, new[] { first, second }, result, null);
        }

        private static DimensionResult Rebuild(Document doc, View view, Dimension original, IList<DimensionReferenceInfo>[] chains, DimensionResult result, ElementId additionalDeleteId)
        {
            if (doc == null || view == null || original == null) { result.Status = DimensionStatus.INVALID_SELECTION; return result; }
            result.SourceDimensionIds.Add(original.Id);
            Dimension additional = additionalDeleteId == null || additionalDeleteId == ElementId.InvalidElementId ? null : doc.GetElement(additionalDeleteId) as Dimension;
            if (additional != null) result.SourceDimensionIds.Add(additional.Id);

            DimensionResult safety = ValidateDestructiveSource(doc, view, original, result);
            if (safety != null) return safety;
            if (additional != null)
            {
                safety = ValidateDestructiveSource(doc, view, additional, result);
                if (safety != null) return safety;
            }
            foreach (IList<DimensionReferenceInfo> chain in chains) if (chain == null || chain.Count < 2) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; result.Message = "Replacement chain requires at least two references."; return result; }

            using (var group = new TransactionGroup(doc, "K-TOOLS Rebuild Dimension"))
            {
                group.Start();
                try
                {
                    using (var tx = new Transaction(doc, "Create and Verify Replacement Dimensions"))
                    {
                        tx.Start();
                        var replacements = new List<Dimension>();
                        for (int i = 0; i < chains.Length; i++)
                        {
                            Dimension replacement = CreateReplacement(doc, view, original, chains[i]);
                            if (replacement == null) return Rollback(tx, group, result, DimensionStatus.FAILED, "Replacement dimension " + (i + 1) + " could not be created.");
                            replacements.Add(replacement);
                        }

                        doc.Regenerate();
                        for (int i = 0; i < replacements.Count; i++)
                        {
                            DimensionPlan plan = BuildPlanForReplacement(doc, view, original, replacements[i], chains[i]);
                            string verifyMessage;
                            if (!DimensionVerificationService.Verify(doc, view, replacements[i], plan, out verifyMessage)) return Rollback(tx, group, result, DimensionStatus.POST_VERIFY_FAILED, verifyMessage);
                        }

                        doc.Delete(original.Id);
                        if (additional != null) doc.Delete(additional.Id);
                        doc.Regenerate();
                        if (doc.GetElement(original.Id) != null || (additional != null && doc.GetElement(additional.Id) != null)) return Rollback(tx, group, result, DimensionStatus.POST_VERIFY_FAILED, "Source dimension deletion could not be verified.");
                        foreach (Dimension replacement in replacements) if (doc.GetElement(replacement.Id) == null) return Rollback(tx, group, result, DimensionStatus.POST_VERIFY_FAILED, "A verified replacement disappeared after source deletion.");

                        foreach (Dimension replacement in replacements) result.CreatedDimensionIds.Add(replacement.Id);
                        result.DeletedDimensionIds.Add(original.Id);
                        if (additional != null) result.DeletedDimensionIds.Add(additional.Id);
                        result.ExpectedSegmentCount = SegmentCount(chains[0]);
                        result.ActualSegmentCount = replacements[0].NumberOfSegments;
                        result.VerificationPassed = true;
                        result.Status = DimensionStatus.REPLACED;
                        result.Message = "Replacement(s) verified before and after source deletion.";
                        tx.Commit();
                    }
                    group.Assimilate();
                }
                catch (Exception ex)
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    result.Status = DimensionStatus.FAILED;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        private static DimensionResult ValidateDestructiveSource(Document doc, View view, Dimension dimension, DimensionResult result)
        {
            DimensionSnapshot snapshot = DimensionReferenceService.CaptureSnapshot(doc, view, dimension);
            if (snapshot.Pinned) { result.Status = DimensionStatus.PINNED_DIMENSION; result.Message = "PINNED_DIMENSION: destructive reconstruction is blocked."; return result; }
            if (snapshot.Locked) { result.Status = DimensionStatus.CONSTRAINT_DIMENSION_UNSUPPORTED; result.Message = "CONSTRAINT_DIMENSION_UNSUPPORTED: locked dimensions are not reconstructed."; return result; }
            if (snapshot.HasEqualityConstraint) { result.Status = DimensionStatus.EQUALITY_CONSTRAINT_RISK; result.Message = "EQUALITY_CONSTRAINT_RISK: equality constraints cannot be guaranteed."; return result; }
            if (snapshot.HasManualOverride) { result.Status = DimensionStatus.MANUAL_OVERRIDE_RISK; result.Message = "MANUAL_OVERRIDE_RISK: drafting overrides would not be preserved exactly."; return result; }
            if (snapshot.Curve == null) { result.Status = DimensionStatus.DIMENSIONS_INCOMPATIBLE; result.Message = "Only linear dimensions can be reconstructed."; return result; }
            return null;
        }

        private static Dimension CreateReplacement(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> refs)
        {
            var array = new ReferenceArray();
            foreach (DimensionReferenceInfo info in refs)
            {
                Reference resolved = DimensionReferenceService.ResolveStableReference(doc, info);
                if (resolved == null) return null;
                array.Append(resolved);
            }
            Line line = original.Curve as Line;
            if (line == null) return null;
            Dimension replacement = new DimensionApiAdapter().CreateLinearDimension(doc, view, line, array, original.GetTypeId());
            if (replacement != null) new DimensionApiAdapter().PreserveTextProperties(original, replacement);
            return replacement;
        }

        private static DimensionPlan BuildPlanForReplacement(Document doc, View view, Dimension original, Dimension replacement, IList<DimensionReferenceInfo> refs)
        {
            var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.GENERAL, DimensionLine = replacement.Curve as Line, Options = new DimensionOptions { DimensionTypeId = original.GetTypeId() } };
            foreach (DimensionReferenceInfo info in refs) context.References.Add(info);
            return DimensionPlanBuilder.Build(context);
        }

        private static DimensionResult Rollback(Transaction tx, TransactionGroup group, DimensionResult result, DimensionStatus status, string message)
        {
            if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
            if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            result.Status = status; result.Message = message; return result;
        }

        private static int SegmentCount(IList<DimensionReferenceInfo> refs) { return refs != null && refs.Count > 2 ? refs.Count - 1 : 0; }
    }
}
