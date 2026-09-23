using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionExecutionService
    {
        // TransactionGroup with per-chain Transaction/SubTransaction isolation; no generic warning swallowing.
        public static DimensionResult Execute(Document doc, DimensionPlan plan)
        {
            var result = new DimensionResult { Operation = plan == null ? DimensionOperation.GENERAL : plan.Operation }; Stopwatch timer = Stopwatch.StartNew(); if (doc == null || plan == null || plan.Context == null) { result.Status = DimensionStatus.INVALID_SELECTION; return result; } DimensionPreflightService.Validate(doc, plan); if (!plan.CanExecute) { result.Status = plan.Status; result.Message = string.Join("; ", plan.Errors); return result; }
            View view = doc.GetElement(plan.ViewId) as View;
            using (var group = new TransactionGroup(doc, "K-TOOLS Dimension Tools")) { group.Start(); try { using (var tx = new Transaction(doc, "Create Dimension Chain")) { tx.Start(); var refs = new ReferenceArray(); foreach (DimensionReferenceSnapshot info in plan.References) { Reference reference = DimensionReferenceService.ResolveStableReference(doc, info); if (reference == null) throw new InvalidOperationException("STALE_REFERENCE: " + info.StableRepresentation); refs.Append(reference); } Line dimensionLine = DimensionPlanSnapshotAdapter.ToLine(plan.DimensionLine); Dimension dimension = new DimensionApiAdapter().CreateLinearDimension(doc, view, dimensionLine, refs, plan.DimensionTypeId); doc.Regenerate(); if (dimension == null) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = "NewDimension returned null."; group.RollBack(); return Finish(result, timer); } string verifyMessage; if (!DimensionVerificationService.Verify(doc, view, dimension, plan, out verifyMessage)) { tx.RollBack(); result.Status = DimensionStatus.POST_VERIFY_FAILED; result.Message = verifyMessage; group.RollBack(); return Finish(result, timer); } TransactionStatus commitStatus = tx.Commit(); if (commitStatus != TransactionStatus.Committed) { result.Status = DimensionStatus.FAILED; result.Message = "Dimension transaction did not commit: " + commitStatus; if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); return Finish(result, timer); } result.CreatedDimensionIds.Add(dimension.Id); result.ExpectedSegmentCount = plan.ExpectedSegments; result.ActualSegmentCount = dimension.NumberOfSegments; result.VerificationPassed = true; result.Status = DimensionStatus.CREATED; result.Message = "Dimension created from validated References."; } if (group.GetStatus() == TransactionStatus.Started) group.Assimilate(); } catch (Exception ex) { if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = ex.Message; } }
            return Finish(result, timer);
        }
        private static DimensionResult Finish(DimensionResult result, Stopwatch timer) { timer.Stop(); result.Duration = timer.Elapsed; return result; }
    }
}
