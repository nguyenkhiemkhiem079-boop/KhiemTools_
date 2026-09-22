using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.TitleBlockSync.Models;
using KhimTools.TitleBlockSync.Services;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class TitleBlockSyncRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "TITLE_BLOCK_SYNC_STAGE33"; } }
        public override string Name { get { return "Title Block Sync Stage 3.3"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Create temporary source/target Sheets, execute production Title Block Sync, verify and rollback."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document; FamilySymbol symbol = null; foreach (FamilySymbol candidate in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>()) { symbol = candidate; break; }
            if (symbol == null) { Block(result, "TB33_PREREQ", "Title block family", "At least one loaded title block type", "No loaded title block FamilySymbol exists.", QaSeverity.WARNING); return; }
            ViewSheet source = null, target = null; string suffix = DateTime.Now.ToString("HHmmss");
            using (var tx = new Transaction(doc, "Title Block Sync fixture setup"))
            {
                tx.Start(); source = ViewSheet.Create(doc, symbol.Id); target = ViewSheet.Create(doc, symbol.Id); if (source == null || target == null) { tx.RollBack(); Block(result, "TB33_CREATE", "Temporary sheets", "Two temporary sheets", "Revit did not create temporary sheets."); return; }
                source.SheetNumber = "KTB-SRC-" + suffix; source.Name = "KTOOLS TB Source " + suffix; target.SheetNumber = "KTB-TGT-" + suffix; target.Name = "KTOOLS TB Target " + suffix;
                Parameter writable = source.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY); if (writable == null || writable.IsReadOnly || writable.StorageType != StorageType.String) writable = source.get_Parameter(BuiltInParameter.SHEET_CHECKED_BY);
                if (writable == null || writable.IsReadOnly || writable.StorageType != StorageType.String) { tx.RollBack(); Block(result, "TB33_PARAM", "Safe writable sheet parameter", "SHEET_DRAWN_BY or SHEET_CHECKED_BY", "No safe writable built-in sheet parameter is available."); return; }
                writable.Set("KTOOLS-QA"); tx.Commit();
                var blocks = TitleBlockCollector.CollectTitleBlocks(doc, source); if (blocks.Count != 1) { Block(result, "TB33_SOURCE", "Source title block", "Exactly one", "Source title block was not resolved."); return; }
                var targetBlocks = TitleBlockCollector.CollectTitleBlocks(doc, target); if (targetBlocks.Count != 1) { Block(result, "TB33_TARGET", "Target title block", "Exactly one", "Target title block was not resolved."); return; }
                var request = new TitleBlockSyncRequest { Document = doc, SourceSheetId = source.Id, SourceTitleBlockId = blocks[0].Id, Options = new TitleBlockSyncOptions { SyncTitleBlockType = true, SyncInstanceParameters = false, SyncSheetParameters = true } }; request.Targets.Add(new TitleBlockSyncTarget { SheetId = target.Id, SheetUniqueId = target.UniqueId, SheetNumber = target.SheetNumber, SheetName = target.Name }); request.SelectedParameterKeys.Add(ParameterTransferService.CreateKey(writable));
                TitleBlockSyncPlan plan = TitleBlockSyncPlanner.BuildPlan(doc, request); Check(result, "TB33_PLAN", "Production plan", plan.Status == TitleBlockSyncStatusCode.READY, "READY", plan.Status.ToString(), "Fixture uses production planner.", QaSeverity.CRITICAL);
                var execution = TitleBlockSyncExecutionService.Execute(doc, plan); Check(result, "TB33_EXECUTE", "Production execution", execution.Synced == 1 || execution.NoChange == 1, "Synced or NO_CHANGE", execution.Synced + "/" + execution.NoChange, "Fixture uses production execution and per-target transaction.", QaSeverity.CRITICAL);
                Parameter targetValue = target.get_Parameter((BuiltInParameter)writable.Id.IntegerValue); Check(result, "TB33_VERIFY", "Target parameter verification", targetValue != null && targetValue.AsString() == "KTOOLS-QA", "KTOOLS-QA", targetValue == null ? "missing" : targetValue.AsString(), "Target value is verified before fixture rollback.", QaSeverity.CRITICAL);
                Check(result, "TB33_SOURCE", "Source unchanged", source.SheetNumber.StartsWith("KTB-SRC-", StringComparison.Ordinal), "Source identity retained", source.SheetNumber, "Source sheet identity remains stable.", QaSeverity.CRITICAL);
            }
        }
    }
}
