using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Services
{
    /// <summary>Safe replacement strategy for Cut/Join/Remove Zero: create, regenerate, verify, then delete.</summary>
    public static class DimensionRebuildService
    {
        public static DimensionResult RebuildSingle(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> refs, DimensionResult result, ElementId additionalDeleteId = null)
        {
            return RebuildSplit(doc, view, original, refs, null, result, additionalDeleteId);
        }
        public static DimensionResult RebuildSplit(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> first, IList<DimensionReferenceInfo> second, DimensionResult result)
        {
            return RebuildSplit(doc, view, original, first, second, result, null);
        }
        private static DimensionResult RebuildSplit(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> first, IList<DimensionReferenceInfo> second, DimensionResult result, ElementId additionalDeleteId)
        {
            result.SourceDimensionIds.Add(original.Id); if (additionalDeleteId != null && additionalDeleteId != ElementId.InvalidElementId) result.SourceDimensionIds.Add(additionalDeleteId); if (original.IsLocked) { result.Status = DimensionStatus.EQUALITY_CONSTRAINT_RISK; result.Message = "EQUALITY_CONSTRAINT_RISK: locked dimension is blocked."; return result; } if (!string.IsNullOrEmpty(original.ValueOverride)) { result.Status = DimensionStatus.MANUAL_OVERRIDE_RISK; result.Message = "MANUAL_OVERRIDE_RISK: value override is not silently discarded."; return result; } if (first == null || first.Count < 2 || (second != null && second.Count < 2)) { result.Status = DimensionStatus.INSUFFICIENT_REFERENCES; result.Message = "Replacement chain requires at least two references."; return result; }
            using (var group = new TransactionGroup(doc, "K-TOOLS Rebuild Dimension")) { group.Start(); try { using (var tx = new Transaction(doc, "Create Replacement Dimensions")) { tx.Start(); var adapter = new DimensionApiAdapter(); List<Dimension> replacements = new List<Dimension>(); Dimension firstReplacement = CreateReplacement(doc, view, original, first, adapter); if (firstReplacement == null) { tx.RollBack(); group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = "First replacement dimension failed."; return result; } replacements.Add(firstReplacement); if (second != null) { Dimension secondReplacement = CreateReplacement(doc, view, original, second, adapter); if (secondReplacement == null) { tx.RollBack(); group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = "Second replacement dimension failed."; return result; } replacements.Add(secondReplacement); } doc.Regenerate(); foreach (Dimension replacement in replacements) { var plan = BuildPlanForReplacement(doc, view, original, replacement, first, second); string verifyMessage; if (!DimensionVerificationService.Verify(doc, view, replacement, plan, out verifyMessage)) { tx.RollBack(); group.RollBack(); result.Status = DimensionStatus.POST_VERIFY_FAILED; result.Message = verifyMessage; return result; } } doc.Delete(original.Id); if (additionalDeleteId != null && additionalDeleteId != ElementId.InvalidElementId) doc.Delete(additionalDeleteId); foreach (Dimension replacement in replacements) result.CreatedDimensionIds.Add(replacement.Id); tx.Commit(); result.DeletedDimensionIds.Add(original.Id); if (additionalDeleteId != null && additionalDeleteId != ElementId.InvalidElementId) result.DeletedDimensionIds.Add(additionalDeleteId); result.Status = DimensionStatus.UPDATED; result.VerificationPassed = true; result.ExpectedSegmentCount = first.Count - 1; result.ActualSegmentCount = first.Count - 1; result.Message = "Replacement verified before original deletion."; } group.Assimilate(); } catch (Exception ex) { if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = ex.Message; } } return result;
        }
        private static Dimension CreateReplacement(Document doc, View view, Dimension original, IList<DimensionReferenceInfo> refs, DimensionApiAdapter adapter)
        {
            var array = new ReferenceArray(); foreach (DimensionReferenceInfo info in refs) { Reference resolved = DimensionReferenceService.ResolveStableReference(doc, info); if (resolved == null) return null; array.Append(resolved); } Line line = original.Curve as Line; if (line == null) return null; Dimension replacement = adapter.CreateLinearDimension(doc, view, line, array, original.GetTypeId()); adapter.PreserveTextProperties(original, replacement); if (replacement != null && original.AreSegmentsEqual) replacement.AreSegmentsEqual = true; return replacement;
        }
        private static DimensionPlan BuildPlanForReplacement(Document doc, View view, Dimension original, Dimension replacement, IList<DimensionReferenceInfo> first, IList<DimensionReferenceInfo> second)
        {
            IList<DimensionReferenceInfo> refs = first; if (second != null && replacement.Id != ElementId.InvalidElementId && replacement.NumberOfSegments == second.Count - 1) refs = second; var context = new DimensionContext { Document = doc, View = view, Operation = DimensionOperation.GENERAL, DimensionLine = replacement.Curve as Line, Options = new DimensionOptions { DimensionTypeId = original.GetTypeId() } }; foreach (DimensionReferenceInfo info in refs) context.References.Add(info); return DimensionPlanBuilder.Build(context);
        }
    }
}
