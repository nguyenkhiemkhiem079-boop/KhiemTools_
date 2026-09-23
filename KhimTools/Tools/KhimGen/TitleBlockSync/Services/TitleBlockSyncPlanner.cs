using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockSyncPlanner
    {
        public static TitleBlockSyncPlan BuildPlan(Document doc, TitleBlockSyncRequest request)
        {
            var plan = new TitleBlockSyncPlan { Options = request == null || request.Options == null ? new TitleBlockSyncOptions() : request.Options };
            if (doc == null || request == null) { plan.Status = TitleBlockSyncStatusCode.SOURCE_SHEET_MISSING; plan.BlockedConditions.Add("Document or request is missing."); return plan; }
            TitleBlockSyncStatusCode sourceStatus;
            TitleBlockInfo source = TitleBlockCollector.FindExplicit(doc, request.SourceSheetId, request.SourceTitleBlockId, out sourceStatus);
            if (source == null && (request.SourceSheetId == null || request.SourceSheetId == ElementId.InvalidElementId) && (request.SourceTitleBlockId == null || request.SourceTitleBlockId == ElementId.InvalidElementId)) source = TitleBlockCollector.FindActiveIfUnambiguous(doc);
            if (source == null)
            {
                plan.Status = sourceStatus == TitleBlockSyncStatusCode.READY ? TitleBlockSyncStatusCode.SOURCE_TITLEBLOCK_MISSING : sourceStatus; plan.BlockedConditions.Add(plan.Status.ToString()); return plan;
            }
            plan.SourceSheetId = source.Sheet.Id; plan.SourceSheetUniqueId = source.Sheet.UniqueId; plan.SourceRevisionFingerprint = TitleBlockCollector.RevisionFingerprint(source.Sheet); plan.SourceTitleBlockId = source.TitleBlock.Id; plan.SourceTitleBlockTypeId = source.TitleBlock.GetTypeId();
            var sourceInstance = TitleBlockParameterService.DiscoverTitleBlockParameters(source.TitleBlock);
            var sourceSheet = TitleBlockParameterService.DiscoverSheetParameters(source.Sheet);
            var selected = new HashSet<ParameterKey>(request.SelectedParameterKeys ?? new List<ParameterKey>());
            if (plan.Options.SyncInstanceParameters) AddItems(plan, sourceInstance, TitleBlockSyncScope.TITLE_BLOCK_INSTANCE, selected, request.SelectedParameterKeys == null || request.SelectedParameterKeys.Count == 0);
            if (plan.Options.SyncSheetParameters) AddItems(plan, sourceSheet, TitleBlockSyncScope.SHEET, selected, request.SelectedParameterKeys == null || request.SelectedParameterKeys.Count == 0);
            var targetInfos = new List<TitleBlockInfo>();
            IEnumerable<TitleBlockSyncTarget> targets = request.Targets == null ? Enumerable.Empty<TitleBlockSyncTarget>() : request.Targets.Where(t => t != null && t.IsSelected);
            foreach (TitleBlockSyncTarget requested in targets.OrderBy(t => t.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.SheetId == null ? 0 : t.SheetId.IntegerValue))
            {
                ViewSheet sheet = doc.GetElement(requested.SheetId) as ViewSheet;
                if (sheet == null || sheet.IsPlaceholder) { plan.Warnings.Add("TARGET_SHEET_MISSING:" + requested.SheetId); continue; }
                var blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet);
                var targetPlan = new TitleBlockTargetPlan { TargetSheetId = sheet.Id, TargetSheetUniqueId = sheet.UniqueId, TargetSheetNumber = sheet.SheetNumber ?? string.Empty };
                if (blocks.Count == 0) { targetPlan.Status = TitleBlockSyncStatusCode.TARGET_TITLEBLOCK_MISSING; targetPlan.Message = "Target has no title block."; plan.Targets.Add(targetPlan); continue; }
                if (blocks.Count > 1) { targetPlan.Status = TitleBlockSyncStatusCode.TARGET_MULTIPLE_TITLEBLOCKS; targetPlan.Message = "Target has multiple title blocks."; plan.Targets.Add(targetPlan); continue; }
                FamilyInstance targetBlock = blocks[0];
                var targetInfo = TitleBlockCollector.Analyze(doc, sheet, targetBlock); targetInfos.Add(targetInfo);
                targetPlan.TargetTitleBlockId = targetBlock.Id; targetPlan.OldTypeId = targetBlock.GetTypeId(); targetPlan.NewTypeId = plan.SourceTitleBlockTypeId;
                targetPlan.TypeChange = plan.Options.SyncTitleBlockType && targetPlan.OldTypeId != targetPlan.NewTypeId;
                BuildTargetParameters(plan, targetPlan, targetBlock, sheet);
                plan.Targets.Add(targetPlan);
            }
            plan.Fingerprint = ComputeFingerprint(doc, plan, source, targetInfos);
            plan.Status = plan.BlockedConditions.Count > 0 ? TitleBlockSyncStatusCode.SOURCE_TITLEBLOCK_MISSING : TitleBlockSyncStatusCode.READY;
            if (plan.Targets.Count == 0) { plan.Status = TitleBlockSyncStatusCode.TARGET_SHEET_MISSING; plan.Message = "No selected target sheets."; }
            return plan;
        }

        private static void AddItems(TitleBlockSyncPlan plan, IList<ParameterTransferPlan> source, TitleBlockSyncScope scope, HashSet<ParameterKey> selected, bool selectDefaults)
        {
            foreach (ParameterTransferPlan transfer in source)
            {
                bool isSelected = selectDefaults ? transfer.CanSync && !transfer.Protected : selected.Contains(transfer.Key);
                if (!isSelected && !selected.Contains(transfer.Key)) continue;
                plan.Parameters.Add(new ParameterSyncItem { Scope = scope, TransferPlan = transfer, Selected = isSelected, Status = transfer.CanSync ? "READY" : transfer.Reason });
            }
        }

        private static void BuildTargetParameters(TitleBlockSyncPlan plan, TitleBlockTargetPlan target, FamilyInstance targetBlock, ViewSheet targetSheet)
        {
            foreach (ParameterSyncItem item in plan.Parameters.Where(p => p.Selected))
            {
                Element targetElement = item.Scope == TitleBlockSyncScope.SHEET ? (Element)targetSheet : targetBlock;
                Parameter targetParameter = ParameterTransferService.FindMatchingParameter(targetElement, item.TransferPlan.Key);
                var result = new ParameterSyncTargetPlan { Key = item.TransferPlan.Key, CanExecute = false, Status = TitleBlockSyncStatusCode.PARAMETER_MISSING };
                if (targetParameter != null)
                {
                    result.OldValue = ParameterTransferService.Snapshot(targetParameter); result.NewValue = item.TransferPlan.SourceValue;
                    result.Status = targetParameter.IsReadOnly ? TitleBlockSyncStatusCode.PARAMETER_READ_ONLY : (targetParameter.StorageType == StorageType.ElementId ? TitleBlockSyncStatusCode.UNSAFE_ELEMENT_REFERENCE : (targetParameter.StorageType != item.TransferPlan.SourceValue.StorageType ? TitleBlockSyncStatusCode.PARAMETER_TYPE_MISMATCH : TitleBlockSyncStatusCode.READY));
                    result.CanExecute = result.Status == TitleBlockSyncStatusCode.READY;
                    result.Message = result.Status.ToString();
                }
                if (item.Scope == TitleBlockSyncScope.SHEET) target.SheetParameters.Add(result); else target.InstanceParameters.Add(result);
                item.TargetTotal++; if (result.Status == TitleBlockSyncStatusCode.PARAMETER_MISSING) item.Missing++; else if (result.Status == TitleBlockSyncStatusCode.PARAMETER_READ_ONLY) item.ReadOnly++; else if (result.OldValue != null && result.NewValue != null && result.OldValue.DisplayValue != result.NewValue.DisplayValue) item.Different++; else item.Matched++;
            }
        }

        public static string ComputeFingerprint(Document doc, TitleBlockSyncPlan plan, TitleBlockInfo source, IEnumerable<TitleBlockInfo> targets)
        {
            var text = new StringBuilder(); text.Append(plan.SourceSheetUniqueId).Append('|').Append(plan.SourceTitleBlockId).Append('|').Append(plan.SourceTitleBlockTypeId);
            text.Append('|').Append(TitleBlockCollector.RevisionFingerprint(source.Sheet));
            foreach (TitleBlockTargetPlan target in plan.Targets.OrderBy(t => t.TargetSheetUniqueId, StringComparer.Ordinal))
            {
                text.Append('|').Append(target.TargetSheetUniqueId).Append('|').Append(target.TargetTitleBlockId).Append('|').Append(target.OldTypeId);
                foreach (ParameterSyncTargetPlan value in target.InstanceParameters) text.Append("|I:").Append(value.Key).Append('=').Append(value.OldValue == null ? string.Empty : value.OldValue.DisplayValue);
                foreach (ParameterSyncTargetPlan value in target.SheetParameters) text.Append("|S:").Append(value.Key).Append('=').Append(value.OldValue == null ? string.Empty : value.OldValue.DisplayValue);
            }
            foreach (ParameterSyncItem item in plan.Parameters.Where(p => p.Selected).OrderBy(p => p.TransferPlan.Key.ToString(), StringComparer.Ordinal)) text.Append('|').Append(item.TransferPlan.Key).Append('=').Append(item.TransferPlan.SourceValue == null ? string.Empty : item.TransferPlan.SourceValue.DisplayValue);
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        public static string ComputeCurrentFingerprint(Document doc, TitleBlockSyncPlan plan)
        {
            TitleBlockSyncStatusCode sourceStatus; TitleBlockInfo source = TitleBlockCollector.FindExplicit(doc, plan.SourceSheetId, plan.SourceTitleBlockId, out sourceStatus);
            if (source == null) return string.Empty;
            var current = new TitleBlockSyncPlan { SourceSheetUniqueId = plan.SourceSheetUniqueId, SourceTitleBlockId = plan.SourceTitleBlockId, SourceTitleBlockTypeId = source.TitleBlock.GetTypeId(), SourceRevisionFingerprint = TitleBlockCollector.RevisionFingerprint(source.Sheet) };
            foreach (TitleBlockTargetPlan original in plan.Targets)
            {
                var copy = new TitleBlockTargetPlan { TargetSheetUniqueId = original.TargetSheetUniqueId, TargetTitleBlockId = original.TargetTitleBlockId, OldTypeId = original.OldTypeId };
                ViewSheet sheet = doc.GetElement(original.TargetSheetId) as ViewSheet; var blocks = TitleBlockCollector.CollectTitleBlocks(doc, sheet); if (blocks.Count == 1)
                {
                    copy.TargetTitleBlockId = blocks[0].Id; copy.OldTypeId = blocks[0].GetTypeId();
                    foreach (ParameterSyncTargetPlan old in original.InstanceParameters) { Parameter p = ParameterTransferService.FindMatchingParameter(blocks[0], old.Key); copy.InstanceParameters.Add(new ParameterSyncTargetPlan { Key = old.Key, OldValue = p == null ? null : ParameterTransferService.Snapshot(p) }); }
                    foreach (ParameterSyncTargetPlan old in original.SheetParameters) { Parameter p = ParameterTransferService.FindMatchingParameter(sheet, old.Key); copy.SheetParameters.Add(new ParameterSyncTargetPlan { Key = old.Key, OldValue = p == null ? null : ParameterTransferService.Snapshot(p) }); }
                }
                current.Targets.Add(copy);
            }
            foreach (ParameterSyncItem item in plan.Parameters)
            {
                Element sourceElement = item.Scope == TitleBlockSyncScope.SHEET ? (Element)source.Sheet : source.TitleBlock;
                Parameter sourceParameter = ParameterTransferService.FindMatchingParameter(sourceElement, item.TransferPlan.Key);
                var transfer = new ParameterTransferPlan { Key = item.TransferPlan.Key, ParameterName = item.TransferPlan.ParameterName, SourceElementId = sourceElement.Id, SourceValue = sourceParameter == null ? null : ParameterTransferService.Snapshot(sourceParameter), Protected = item.TransferPlan.Protected, Selected = item.Selected, CanSync = item.TransferPlan.CanSync };
                current.Parameters.Add(new ParameterSyncItem { Scope = item.Scope, Selected = item.Selected, TransferPlan = transfer });
            }
            return ComputeFingerprint(doc, current, source, null);
        }
    }
}
