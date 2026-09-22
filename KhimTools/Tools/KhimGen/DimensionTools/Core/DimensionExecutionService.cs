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
            var result = new DimensionResult { Operation = plan == null ? DimensionOperation.GENERAL : plan.Operation }; Stopwatch timer = Stopwatch.StartNew(); if (plan == null || plan.Context == null) { result.Status = DimensionStatus.INVALID_SELECTION; return result; } DimensionPreflightService.Validate(doc, plan); if (!plan.CanExecute) { result.Status = plan.Status; result.Message = string.Join("; ", plan.Errors); return result; }
            using (var group = new TransactionGroup(doc, "K-TOOLS Dimension Tools")) { group.Start(); try { using (var tx = new Transaction(doc, "Create Dimension Chain")) { tx.Start(); var refs = new ReferenceArray(); foreach (DimensionReferenceInfo info in plan.References) refs.Append(info.Reference); Dimension dimension = new DimensionApiAdapter().CreateLinearDimension(doc, plan.Context.View, plan.DimensionLine, refs, plan.DimensionTypeId); doc.Regenerate(); if (dimension == null) { tx.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = "NewDimension returned null."; group.RollBack(); return Finish(result, timer); } string verifyMessage; if (!DimensionVerificationService.Verify(doc, plan.Context.View, dimension, plan, out verifyMessage)) { tx.RollBack(); result.Status = DimensionStatus.POST_VERIFY_FAILED; result.Message = verifyMessage; group.RollBack(); return Finish(result, timer); } tx.Commit(); result.CreatedDimensionIds.Add(dimension.Id); result.ExpectedSegmentCount = plan.ExpectedSegments; result.ActualSegmentCount = dimension.NumberOfSegments; result.VerificationPassed = true; result.Status = DimensionStatus.CREATED; result.Message = "Dimension created from validated References."; } group.Assimilate(); } catch (Exception ex) { if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); result.Status = DimensionStatus.FAILED; result.Message = ex.Message; } }
            return Finish(result, timer);
        }
        private static DimensionResult Finish(DimensionResult result, Stopwatch timer) { timer.Stop(); result.Duration = timer.Elapsed; return result; }
    }
}
