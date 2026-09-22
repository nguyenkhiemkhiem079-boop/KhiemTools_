using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.SheetCopy.Models;
using KhimTools.SheetCopy.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Rollback-isolated Sheet Copy smoke fixture. Production SheetCopy never depends on RuntimeQa.</summary>
    public sealed class SheetCopyRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SHEET_COPY_STAGE31"; } }
        public override string Name { get { return "Sheet Copy Stage 3.1"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Create a temporary source Sheet, copy it with SheetCopy services, verify source and target, then rollback."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            var symbols = new FilteredElementCollector(context.Document).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>().ToList();
            if (symbols.Count == 0) { Block(result, "SC31_PREREQ", "Title block", "A loaded title block type", "No title block type is available."); return; }
            string token = "KTOOLS-QA-SC31-" + context.RunId.Substring(0, Math.Min(8, context.RunId.Length));
            ViewSheet source;
            using (var transaction = new Transaction(context.Document, "K-TOOLS Sheet Copy QA source"))
            {
                transaction.Start();
                source = ViewSheet.Create(context.Document, symbols[0].Id);
                source.SheetNumber = token;
                source.Name = "K-TOOLS Sheet Copy QA Source";
                transaction.Commit();
            }
            context.TrackCreated(source.Id);
            SheetCopyItem sourceItem = SheetCopyCollector.AnalyzeSheet(context.Document, source);
            string before = sourceItem.SourceFingerprint;
            sourceItem.TargetSheetNumber = token + "-COPY";
            sourceItem.TargetSheetName = source.Name;
            var request = new SheetCopyRequest { Document = context.Document, Options = new SheetCopyOptions { ViewPolicy = ViewCopyPolicy.SHEET_ONLY } };
            request.Items.Add(sourceItem);
            SheetCopyPlan plan = SheetCopyPlanner.BuildPlan(context.Document, request);
            SheetCopyBatchResult execution = SheetCopyExecutionService.Execute(context.Document, plan);
            SheetCopyExecutionResult copied = execution.Results.FirstOrDefault();
            if (copied != null) context.TrackCreated(copied.TargetSheetId);
            bool created = copied != null && (copied.Status == SheetCopyStatusCode.CREATED || copied.Status == SheetCopyStatusCode.PARTIAL);
            Check(result, "SC31_CREATE", "Sheet-only copy", created, "CREATED", copied == null ? "No result" : copied.Status.ToString(), "Production SheetCopy created a target Sheet.", QaSeverity.CRITICAL);
            bool unchanged = string.Equals(before, SheetCopyVerificationService.CaptureSourceFingerprint(context.Document, source), StringComparison.Ordinal);
            Check(result, "SC31_SOURCE", "Source unchanged", unchanged, before, SheetCopyVerificationService.CaptureSourceFingerprint(context.Document, source), "Source fingerprint remains identical.", QaSeverity.CRITICAL);
        }
    }
}
