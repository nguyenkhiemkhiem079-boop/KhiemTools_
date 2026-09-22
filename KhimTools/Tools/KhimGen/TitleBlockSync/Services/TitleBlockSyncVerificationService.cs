using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockSyncVerificationService
    {
        public static bool VerifyTarget(Document doc, TitleBlockTargetPlan plan, TitleBlockSyncPlan batch, out string message)
        {
            message = string.Empty;
            if (doc == null || plan == null) { message = "Missing verification inputs."; return false; }
            ViewSheet sheet = doc.GetElement(plan.TargetSheetId) as ViewSheet;
            if (sheet == null) { message = "Target sheet missing."; return false; }
            var blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet);
            if (blocks.Count != 1) { message = "Target title block count is " + blocks.Count + "."; return false; }
            FamilyInstance block = blocks[0];
            if (batch.Options.SyncTitleBlockType && block.GetTypeId() != batch.SourceTitleBlockTypeId) { message = "Target title block type did not match source."; return false; }
            foreach (ParameterSyncTargetPlan item in plan.InstanceParameters)
            {
                if (!item.CanExecute) continue;
                Parameter p = ParameterTransferService.FindMatchingParameter(block, item.Key);
                if (p == null) { message = "Target instance parameter missing."; return false; }
                if (item.NewValue != null && ParameterTransferService.Snapshot(p).DisplayValue != item.NewValue.DisplayValue) { message = "Target instance parameter value mismatch."; return false; }
            }
            foreach (ParameterSyncTargetPlan item in plan.SheetParameters)
            {
                if (!item.CanExecute) continue;
                Parameter p = ParameterTransferService.FindMatchingParameter(sheet, item.Key);
                if (p == null) { message = "Target sheet parameter missing."; return false; }
                if (item.NewValue != null && ParameterTransferService.Snapshot(p).DisplayValue != item.NewValue.DisplayValue) { message = "Target sheet parameter value mismatch."; return false; }
            }
            message = "Target verified."; return true;
        }

        public static bool VerifySourceUnchanged(Document doc, TitleBlockSyncPlan plan, out string message)
        {
            message = string.Empty; TitleBlockSyncStatusCode sourceStatus; TitleBlockInfo source = TitleBlockCollector.FindExplicit(doc, plan.SourceSheetId, plan.SourceTitleBlockId, out sourceStatus);
            if (source == null) { message = "Source missing after execution."; return false; }
            if (source.TitleBlock.GetTypeId() != plan.SourceTitleBlockTypeId) { message = "Source type changed."; return false; }
            if (!string.Equals(source.Sheet.UniqueId, plan.SourceSheetUniqueId, StringComparison.Ordinal)) { message = "Source identity changed."; return false; }
            message = "Source identity and type preserved."; return true;
        }
    }
}
