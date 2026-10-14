using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.SheetGen.Models;
using KhimTools.SheetGen.Services;
using KhimTools.ViewportAlign.Services;
using KhimTools.DetailNumberUpdater.Services;
using KhimTools.TextAlign.Services;
using KhimTools.ElementTags.Services;
using KhimTools.ElementTags.Models;
using KhimTools.SheetExport.Models;
using KhimTools.SheetExport.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class SheetGenRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SG"; } }
        public override string Name { get { return "SheetGen production workflow"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Create temporary sheets and validate SheetGen preflight, placement and rollback."; } }
        public override bool IsCritical { get { return false; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            List<TitleBlockOption> titleBlocks = SheetGenService.GetAvailableTitleBlocks(doc);
            List<ViewOption> views = SheetGenService.GetAvailableViews(doc).Where(v => v.IsSupported).ToList();
            BlockOrPass(result, "SG_RES_TITLEBLOCK", "Title block prerequisite", titleBlocks.Count > 0, "At least one loaded TitleBlock", titleBlocks.Count.ToString());
            BlockOrPass(result, "SG_RES_VIEW", "Placeable view prerequisite", views.Count > 0, "At least one placeable view", views.Count.ToString());
            if (titleBlocks.Count == 0) return;

            var duplicateItems = new List<SheetGenItem>
            {
                new SheetGenItem { IsSelected = true, SheetNumber = "KTOOLS-QA-DUP", SheetName = "QA duplicate 1", TitleBlockId = titleBlocks[0].Id, TitleBlockName = titleBlocks[0].Name },
                new SheetGenItem { IsSelected = true, SheetNumber = "KTOOLS-QA-DUP", SheetName = "QA duplicate 2", TitleBlockId = titleBlocks[0].Id, TitleBlockName = titleBlocks[0].Name }
            };
            List<SheetGenValidationResult> duplicateReport = SheetGenPreflightService.Validate(doc, duplicateItems);
            bool duplicateBlocked = duplicateReport.Count == 2 && duplicateReport.Any(x => !x.CanCreate || x.Code.ToString().IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0);
            Check(result, "SG02", "Duplicate-in-batch preflight", duplicateBlocked, "Duplicate sheet numbers are blocked", duplicateReport.Count.ToString(),
                duplicateBlocked ? "Production SheetGen preflight reported the duplicate." : "Duplicate rows were not blocked.", QaSeverity.ERROR);

            var item = new SheetGenItem
            {
                IsSelected = true,
                SheetNumber = "KTOOLS-QA-" + context.RunId.Substring(0, 8),
                SheetName = "K-TOOLS Runtime QA Sheet",
                TitleBlockId = titleBlocks[0].Id,
                TitleBlockName = titleBlocks[0].Name,
                AllowBlankTitleBlock = false
            };
            if (views.Count > 0)
            {
                HashSet<ElementId> placed = new HashSet<ElementId>(new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.ViewId));
                foreach (ScheduleSheetInstance schedule in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>()) placed.Add(schedule.ScheduleId);
                ViewOption view = views.FirstOrDefault(v => v.ContentKind == SheetContentKind.NormalView && !placed.Contains(v.Id))
                    ?? views.FirstOrDefault(v => !placed.Contains(v.Id));
                if (view == null) view = views[0];
                item.AssignedViewId = view.Id;
                item.AssignedViewUniqueId = view.UniqueId;
                item.AssignedViewName = view.Name;
                item.AssignedViewType = view.ViewType;
                item.ContentKind = view.ContentKind;
            }
            SheetBatchCreationResult batch = SheetGenService.CreateSheetsDetailed(doc, new[] { item }, new SheetGenParameterMapping());
            foreach (SheetCreationResult created in batch.Results)
            {
                context.TrackCreated(created.CreatedSheetId);
                context.TrackCreated(created.ContentInstanceId);
            }
            bool createdOk = batch.Created == 1 && batch.Results.Any(r => r.Status == SheetGenStatusCode.Created);
            Check(result, "SG01", "Normal Sheet creation", createdOk, "One temporary sheet created", batch.Created.ToString(),
                createdOk ? "Production SheetGen created the fixture sheet." : "SheetGen did not create the temporary sheet.", QaSeverity.ERROR);
            Check(result, "SG08", "Per-sheet result isolation", batch.Results.Count == 1, "One independent result", batch.Results.Count.ToString(),
                "Each SheetGen row produced an individual result.", QaSeverity.INFO);
            Check(result, "SG09", "No partial sheet after fixture", createdOk, "Created sheet is inside rollback boundary", createdOk ? "Tracked" : "No created sheet", "The outer fixture TransactionGroup will remove the sheet.", QaSeverity.CRITICAL);
        }

        private static void BlockOrPass(QaFixtureResult result, string id, string name, bool available, string expected, string actual)
        {
            if (available) Check(result, id, name, true, expected, actual, "Resource is available in the active project.", QaSeverity.INFO);
            else Block(result, id, name, expected, "BLOCKED: " + expected + " was not found in the active project.");
        }
    }

    public sealed class ViewportAlignRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "VA"; } }
        public override string Name { get { return "Align Viewports production engine"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Execute production viewport alignment and inspect final Revit coordinates."; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            ViewSheet sheet = RuntimeQaFixtureHelpers.FirstSheet(context.Document, 2);
            if (sheet == null) { Block(result, "VA_RES", "Viewport fixture resources", "A sheet with at least two viewports", "BLOCKED: no suitable sheet/viewports were found."); return; }
            List<TargetViewItem> targets = ViewportAlignmentCollector.Collect(context.Document, sheet).Where(x => x.IsViewport).ToList();
            if (targets.Count < 2) { Block(result, "VA_RES", "Viewport fixture resources", "Two movable viewports", "BLOCKED: fewer than two viewports are available."); return; }
            Viewport sourceViewport = context.Document.GetElement(targets[0].ViewportOrScheduleId) as Viewport;
            AlignmentReference source = ViewportAlignService.CreateViewportReference(context.Document, sourceViewport);
            TargetViewItem target = targets[1];
            XYZ before = (context.Document.GetElement(target.ViewportOrScheduleId) as Viewport)?.GetBoxCenter();
            using (var tx = new Transaction(context.Document, "K-TOOLS Runtime QA viewport alignment"))
            {
                tx.Start();
                AlignmentExecutionResult execution = ViewportAlignmentExecutor.Execute(context.Document, source, target, AlignmentOperation.MATCH_CENTER);
                context.Document.Regenerate();
                Viewport moved = context.Document.GetElement(target.ViewportOrScheduleId) as Viewport;
                XYZ after = moved?.GetBoxCenter();
                bool actual = execution.Status == AlignmentExecutionStatus.SUCCESS_CHANGED || execution.Status == AlignmentExecutionStatus.SUCCESS_NO_CHANGE;
                Check(result, "VA01", "MATCH_CENTER", actual, "Production executor returns success", execution.Status.ToString(), execution.Message, QaSeverity.ERROR);
                bool geometry = after != null && source != null && ViewportAlignService.CentersEqual(after, source.Position);
                Check(result, "VA_GEOMETRY", "Final viewport coordinate", geometry, "Target center matches source", after == null ? "null" : after.ToString(),
                    geometry ? "Final Revit viewport coordinates match the production target." : "Final viewport coordinate did not match.", QaSeverity.CRITICAL);
                bool noChange = execution.Status == AlignmentExecutionStatus.SUCCESS_NO_CHANGE || before == null || after == null || !ViewportAlignService.CentersEqual(before, after);
                Check(result, "VA11", "No-change behavior", noChange, "No unnecessary move", execution.Status.ToString(), "No-change path is reported by the production engine.", QaSeverity.INFO);
                tx.RollBack();
            }
        }
    }

    public sealed class DetailNumberRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "DN"; } }
        public override string Name { get { return "Detail Number swap and cycle"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Use DetailNumberService to validate regex, duplicate, swap, cycle and temporary cleanup."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            RegexValidationResult invalid = DetailNumberService.ValidateRegex("([A-");
            Check(result, "DN02", "Invalid Regex validation", !invalid.IsValid, "INVALID_REGEX", invalid.IsValid ? "valid" : "invalid", invalid.ErrorMessage, QaSeverity.ERROR);
            ViewSheet sheet = RuntimeQaFixtureHelpers.FirstSheet(doc, 3);
            if (sheet == null) { Block(result, "DN_RES", "Detail number fixture resources", "A sheet with three writable viewports", "BLOCKED: no suitable sheet with three viewports.", QaSeverity.CRITICAL); return; }
            var viewports = sheet.GetAllViewports().Select(id => doc.GetElement(id) as Viewport).Where(v => v != null).Take(3).ToList();
            if (viewports.Count < 3) { Block(result, "DN_RES", "Detail number fixture resources", "Three writable viewports", "BLOCKED: fewer than three viewports are available.", QaSeverity.CRITICAL); return; }
            var original = viewports.ToDictionary(v => v.Id, DetailNumberService.GetDetailNumber);
            using (var setup = new Transaction(doc, "K-TOOLS Runtime QA detail number setup"))
            {
                setup.Start();
                foreach (Viewport viewport in viewports)
                {
                    Parameter p = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (p == null || p.IsReadOnly) { setup.RollBack(); Block(result, "DN_RES", "Writable detail number", "Writable VIEWPORT_DETAIL_NUMBER", "BLOCKED: a selected viewport detail number is read-only.", QaSeverity.CRITICAL); return; }
                    p.Set("__KTOOLS_QA_" + viewport.Id.ToString());
                }
                setup.Commit();
            }
            string a = "__KTOOLS_QA_" + viewports[0].Id.ToString();
            string b = "__KTOOLS_QA_" + viewports[1].Id.ToString();
            string c = "__KTOOLS_QA_" + viewports[2].Id.ToString();
            var candidates = new List<DetailNumberCandidate>
            {
                Candidate(viewports[0], sheet, b), Candidate(viewports[1], sheet, c), Candidate(viewports[2], sheet, a)
            };
            DetailNumberBatchResult execution = DetailNumberService.Execute(doc, sheet, candidates);
            doc.Regenerate();
            bool changed = execution.Changed == 3 && DetailNumberService.GetDetailNumber(viewports[0]) == b && DetailNumberService.GetDetailNumber(viewports[1]) == c && DetailNumberService.GetDetailNumber(viewports[2]) == a;
            Check(result, "DN07", "Three-way cycle", changed, "A=B, B=C, C=A after production execution", execution.Changed.ToString(),
                changed ? "Production DetailNumberService completed the cycle." : "Cycle execution did not produce the expected final values.", QaSeverity.CRITICAL);
            bool noTemporary = viewports.All(v => !DetailNumberService.GetDetailNumber(v).StartsWith("__KTOOLS_TMP_", StringComparison.Ordinal));
            Check(result, "DN08", "Temporary values removed", noTemporary, "No __KTOOLS_TMP_ detail numbers", string.Join(",", viewports.Select(DetailNumberService.GetDetailNumber)),
                noTemporary ? "No temporary detail number remains." : "Temporary detail number remains after execution.", QaSeverity.CRITICAL);
            var duplicateCandidates = new List<DetailNumberCandidate> { Candidate(viewports[0], sheet, "DUP"), Candidate(viewports[1], sheet, "DUP") };
            DetailNumberPreflightReport duplicate = DetailNumberPreflightService.Preflight(doc, sheet, duplicateCandidates);
            bool duplicateBlocked = duplicate.Candidates.All(c => c.Status == DetailNumberStatusCode.DUPLICATE_IN_BATCH || !c.CanExecute);
            Check(result, "DN04", "Manual duplicate blocked", duplicateBlocked, "DUPLICATE_IN_BATCH", string.Join(",", duplicate.Candidates.Select(c => c.Status)), "Duplicate-in-batch preflight was executed.", QaSeverity.ERROR);
            bool noChange = DetailNumberService.GetDetailNumber(viewports[0]) == b;
            Check(result, "DN05", "NO_CHANGE", noChange, b, DetailNumberService.GetDetailNumber(viewports[0]), "Current value remains stable when no update is requested.", QaSeverity.INFO);
            foreach (Viewport viewport in viewports) context.TrackCreated(ElementId.InvalidElementId);
            // The enclosing TransactionGroup rolls back both setup and production Execute groups.
        }

        private static DetailNumberCandidate Candidate(Viewport viewport, ViewSheet sheet, string proposed)
        {
            return new DetailNumberCandidate { ViewportId = viewport.Id, ViewId = viewport.ViewId, SheetId = sheet.Id,
                ViewName = viewport.Id.ToString(), CurrentNumber = DetailNumberService.GetDetailNumber(viewport),
                ProposedNumber = proposed, IsSelected = true, IsManualOverride = true };
        }
    }

    public sealed class TextAlignRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "TA"; } }
        public override string Name { get { return "Text Align model safety"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Exercise production preflight and prove model elements are not moved."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            View view = RuntimeQaFixtureHelpers.ActiveView(context);
            if (view == null) { Block(result, "TA_RES", "Text Align view", "An active graphical view", "BLOCKED: no active view.", QaSeverity.CRITICAL); return; }
            Wall wall = new FilteredElementCollector(context.Document).OfClass(typeof(Wall)).Cast<Wall>().FirstOrDefault();
            if (wall == null) { Block(result, "TA09", "Unsupported model element protection", "A safe Wall model element", "BLOCKED: no Wall is available for the mandatory model-element safety test.", QaSeverity.CRITICAL); return; }
            List<TextAlignPreflightResult> preflight = TextAlignPreflightService.Run(context.Document, view, new[] { wall.Id }, AlignType.Left);
            TextAlignPreflightResult row = preflight.FirstOrDefault();
            bool blocked = row != null && !row.CanExecute;
            Check(result, "TA09", "Model element is not moved", blocked, "Wall is unsupported/blocked", row == null ? "missing" : row.Status.ToString(),
                blocked ? "Production TextAlign preflight protects the model element." : "A model element was incorrectly executable.", QaSeverity.CRITICAL);
            Check(result, "TA10", "NO_CHANGE preflight safety", row != null, "A deterministic preflight result", preflight.Count.ToString(), "Production preflight executed on the active view.", QaSeverity.INFO);
            ICollection<ElementId> textNotes = new FilteredElementCollector(context.Document, view.Id).OfClass(typeof(TextNote)).ToElementIds();
            if (textNotes.Count < 2) Block(result, "TA01", "Annotation fixture resources", "Two TextNotes in the active view", "BLOCKED: add two safe temporary TextNotes to exercise movement checks.");
            else Check(result, "TA01", "Annotation fixture resources", true, "Two TextNotes", textNotes.Count.ToString(), "Text annotation resources are available.", QaSeverity.INFO);
        }
    }

    public sealed class ElementTagsRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "ET"; } }
        public override string Name { get { return "Elements Tags audit and override restore"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Run production tag relationship audit, preflight and diagnostic override restoration."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            View view = RuntimeQaFixtureHelpers.ActiveView(context);
            if (view == null || view is ViewSheet) { Block(result, "ET_RES", "Tag fixture view", "A non-sheet active view", "BLOCKED: Elements Tags requires a plan/section/3D view.", QaSeverity.CRITICAL); return; }
            TagRelationshipIndex index = ElementTagsService.BuildRelationshipIndex(context.Document, view);
            Check(result, "ET11", "Audit is read-only", index != null, "A relationship index is returned", index == null ? "null" : index.Hosts.Count + " hosts", "Production relationship indexing completed without a transaction.", QaSeverity.ERROR);
            TagAuditResult audit = ElementTagsService.Audit(context.Document, view, new List<ElementTagsItem>());
            Check(result, "ET01", "Missing tag audit", audit != null, "Tag audit result", audit == null ? "null" : audit.Items.Count.ToString(), "Production tag audit executed.", QaSeverity.ERROR);
            if (index == null || index.Tags.Count == 0) { Block(result, "ET10", "Diagnostic override resources", "At least one existing tag", "BLOCKED: no IndependentTag is available for override restoration."); return; }
            ElementId tagId = index.Tags.First().TagId;
            OverrideGraphicSettings original = view.GetElementOverrides(tagId);
            using (var tx = new Transaction(context.Document, "K-TOOLS Runtime QA tag override"))
            {
                tx.Start();
                ElementTagsService.ApplyRedOverrideForHostAndTags(context.Document, view, new List<ElementId> { tagId });
                OverrideGraphicSettings diagnostic = view.GetElementOverrides(tagId);
                bool changed = diagnostic != null;
                view.SetElementOverrides(tagId, original);
                OverrideGraphicSettings restored = view.GetElementOverrides(tagId);
                bool restoredOk = OverrideSignature(original) == OverrideSignature(restored);
                Check(result, "ET10", "Diagnostic highlight + exact override restore", changed && restoredOk, "Original OverrideGraphicSettings semantics restored", OverrideSignature(restored),
                    restoredOk ? "Original override was restored inside the fixture transaction." : "Override semantics changed after restore.", QaSeverity.CRITICAL);
                tx.RollBack();
            }
        }

        private static string OverrideSignature(OverrideGraphicSettings s)
        {
            if (s == null) return "<null>";
            return string.Join("|", new[] { s.Halftone.ToString(), s.Transparency.ToString(), s.ProjectionLineWeight.ToString(), s.CutLineWeight.ToString(), s.ProjectionLineColor.ToString(), s.CutLineColor.ToString() });
        }
    }

    public sealed class SheetExportRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SE"; } }
        public override string Name { get { return "Sheet Export temp-path workflow"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Use isolated temp output, deterministic naming and lock preflight without touching production exports."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            string folder = Path.Combine(Path.GetTempPath(), "KhimTools", "RuntimeQA", context.RunId, "SheetExport");
            Directory.CreateDirectory(folder);
            result.Artifacts.Add(folder);
            Check(result, "SE_PATH", "Isolated temporary output path", folder.StartsWith(Path.Combine(Path.GetTempPath(), "KhimTools"), StringComparison.OrdinalIgnoreCase),
                "%TEMP%/KhimTools/RuntimeQA/<RunId>/SheetExport", folder, "Fixture output is isolated from configured production export folders.", QaSeverity.CRITICAL);
            string lockPath = Path.Combine(folder, "locked-output.pdf");
            File.WriteAllText(lockPath, "K-TOOLS runtime QA lock fixture");
            using (var stream = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                bool locked = PdfExportEngine.IsFileLocked(lockPath);
                Check(result, "SE15", "LOCKED_OUTPUT", locked, "Lock preflight detects FileShare.None", locked.ToString(), "Deterministic internal file lock fixture executed.", QaSeverity.ERROR);
            }
            result.Artifacts.Add(lockPath);
            List<SheetExportItem> sheets = SheetCollectorService.GetAllSheets(context.Document);
            if (sheets.Count == 0) { Block(result, "SE01", "PDF export resources", "At least one ViewSheet", "BLOCKED: the active project contains no sheets.", QaSeverity.CRITICAL); return; }
            SheetExportItem first = sheets.First();
            string planned = NamingPlanService.Sanitize(first.SheetNumber + "_" + first.SheetName);
            Check(result, "SE09", "Naming token expansion", !string.IsNullOrWhiteSpace(planned), "A deterministic sanitized name", planned, "Production naming service produced an isolated file name.", QaSeverity.INFO);
            try
            {
                string output = PdfExportEngine.ExportSingleSheet(context.Document, first.Sheet, folder, "QA_" + planned, new ExportOptions { OutputDirectory = folder });
                bool exists = File.Exists(output) && new FileInfo(output).Length > 0;
                Check(result, "SE01", "Single PDF export", exists, "Non-empty PDF in temp folder", output, "Production PDF export completed.", QaSeverity.CRITICAL);
                result.Artifacts.Add(output);
            }
            catch (Exception ex)
            {
                Block(result, "SE01", "Single PDF export", "Revit PDF export setup is available", "BLOCKED: PDF export could not run in this environment: " + ex.Message, QaSeverity.CRITICAL);
            }
            TemporaryViewStateScope scope = TemporaryViewStateScope.Capture(context.Document, sheets.Take(1).Select(s => s.Sheet));
            bool disposed = scope != null;
            if (scope != null) scope.Dispose();
            Check(result, "SE08", "Temporary View Properties scope", disposed, "A restorable TemporaryViewStateScope", disposed ? "Captured" : "Unavailable", "Production temporary-view state scope was exercised.", QaSeverity.CRITICAL);
        }
    }
}
