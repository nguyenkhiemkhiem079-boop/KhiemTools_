using System;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    public static class ModifyObjectExecutionService
    {
        // Pipeline contract: SELECT -> ANALYZE -> PLAN -> PREVIEW -> PREFLIGHT -> EXECUTE -> REGENERATE -> VERIFY -> RESULT.
        // Per-operation Transaction/SubTransaction isolation is required; operation adapters may isolate individual items.
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan, Func<ModifyObjectResult> operation)
        {
            var result = new ModifyObjectResult { PreviewOnly = plan == null || plan.Context == null || plan.Context.PreviewOnly };
            if (doc == null || plan == null || plan.Context == null) { result.Status = ModifyObjectStatus.INVALID_SELECTION; return result; }
            if (result.PreviewOnly) { result.Status = ModifyObjectStatus.PREVIEW_ONLY; result.Summary = "Preview only; no model writes occurred."; return result; }
            ModifyObjectPreflightResult preflight = ModifyObjectPreflight.Validate(doc, plan);
            if (!preflight.IsValid) { result.Status = preflight.Statuses.Count == 0 ? ModifyObjectStatus.FAILED : preflight.Statuses[0]; result.Message = string.Join("; ", preflight.Errors); return result; }
            using (var group = new TransactionGroup(doc, "K-TOOLS Modify Objects"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "ModifyObjects.Batch");
                try
                {
                    result = operation == null ? new ModifyObjectResult { Status = ModifyObjectStatus.FAILED, Message = "No operation delegate." } : operation();
                    if (result == null) result = new ModifyObjectResult { Status = ModifyObjectStatus.FAILED };
                    doc.Regenerate();
                    result.VerificationPassed = Verify(doc, plan);
                    if (!result.VerificationPassed) result.Status = ModifyObjectStatus.POST_VERIFY_FAILED;
                    if (result.Status == ModifyObjectStatus.FAILED || result.Status == ModifyObjectStatus.POST_VERIFY_FAILED) KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects.Batch"); else KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ModifyObjects.Batch");
                }
                catch (Exception ex)
                {
                    if (group.GetStatus() == TransactionStatus.Started) KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects.Batch");
                    result.Status = ModifyObjectStatus.FAILED; result.Message = ex.Message;
                }
            }
            return result;
        }
        public static bool Verify(Document doc, ModifyObjectPlan plan)
        {
            if (doc == null || plan == null || plan.Context == null) return false;
            foreach (ModifyObjectSourceSnapshot source in plan.Sources) if (doc.GetElement(source.ElementId) == null && plan.Context.Operation != ModifyObjectOperation.COLUMN_SPLIT && plan.Context.Operation != ModifyObjectOperation.BEAM_SPLIT && plan.Context.Operation != ModifyObjectOperation.WALL_SPLIT) return false;
            return true;
        }
    }
}
