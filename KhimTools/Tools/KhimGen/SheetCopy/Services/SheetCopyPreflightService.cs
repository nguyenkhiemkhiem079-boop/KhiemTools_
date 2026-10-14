using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyPreflightService
    {
        public static List<SheetCopyDiagnostic> Validate(Document doc, SheetCopyPlan plan)
        {
            var diagnostics = new List<SheetCopyDiagnostic>();
            if (doc == null || plan == null) { diagnostics.Add(Error(SheetCopyStatusCode.STALE_COPY_PLAN, "Document or copy plan is unavailable.")); return diagnostics; }
            var existing = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SheetCopyItem item in plan.Items)
            {
                item.Diagnostics.Clear();
                item.Status = SheetCopyStatusCode.READY;
                item.Message = string.Empty;
                ViewSheet sourceSheet = item.SourceSheetId == null ? null : doc.GetElement(item.SourceSheetId) as ViewSheet;
                if (sourceSheet == null || sourceSheet.IsPlaceholder)
                { Add(item, diagnostics, Error(SheetCopyStatusCode.SOURCE_SHEET_MISSING, "Source Sheet no longer exists.")); continue; }
                string number = (item.TargetSheetNumber ?? string.Empty).Trim();
                if (!SheetCopyNamingService.IsValidTargetNumber(number, out string numberMessage)) Add(item, diagnostics, Error(numberMessage.Contains("required") ? SheetCopyStatusCode.EMPTY_TARGET_NUMBER : SheetCopyStatusCode.INVALID_TARGET_NUMBER, numberMessage));
                else if (!pending.Add(number)) Add(item, diagnostics, Error(SheetCopyStatusCode.DUPLICATE_IN_BATCH, "Target Sheet number is duplicated in this batch."));
                else if (existing.Contains(number)) Add(item, diagnostics, Error(SheetCopyStatusCode.DUPLICATE_IN_PROJECT, "Target Sheet number already exists in the project."));
                if (item.SourceTitleBlockCount > 1)
                    Add(item, diagnostics, Error(SheetCopyStatusCode.MULTIPLE_TITLE_BLOCKS, "Multiple title blocks require explicit resolution."));
                if (item.SourceHasTitleBlock && (item.SourceTitleBlockTypeId == null || item.SourceTitleBlockTypeId == ElementId.InvalidElementId))
                    Add(item, diagnostics, Error(SheetCopyStatusCode.TITLE_BLOCK_MISSING, "Source title block type is unavailable."));
                foreach (SheetCopyContentItem content in item.Contents)
                {
                    if (content.ContentKind == SheetCopyContentKind.NORMAL_VIEWPORT && content.Action == SheetCopyAction.CREATE)
                        ValidateView(doc, item, content, diagnostics, plan.Options.ViewPolicy);
                    else if (content.ContentKind == SheetCopyContentKind.LEGEND_VIEWPORT && content.Action == SheetCopyAction.REUSE)
                    {
                        if (doc.GetElement(content.SourceViewId) == null || !Viewport.CanAddViewToSheet(doc, item.SourceSheetId, content.SourceViewId))
                            Add(item, diagnostics, Error(SheetCopyStatusCode.LEGEND_PLACEMENT_UNSUPPORTED, "Legend cannot be placed on the target Sheet."));
                    }
                    else if (content.ContentKind == SheetCopyContentKind.SCHEDULE && content.IsSegmented)
                        Add(item, diagnostics, Warning(SheetCopyStatusCode.SEGMENTED_SCHEDULE_DEFERRED, "Segmented schedule placement is deferred to Stage 3.2."));
                    else if (content.ContentKind == SheetCopyContentKind.REVISION_RELATED)
                        Add(item, diagnostics, Warning(SheetCopyStatusCode.SYSTEM_REVISION_SCHEDULE, "System revision schedule is managed by Revit and is not copied."));
                    else if (content.ContentKind == SheetCopyContentKind.REVISION_RELATED || content.Status == SheetCopyStatusCode.REVISION_CONTENT_SKIPPED.ToString())
                        Add(item, diagnostics, Warning(SheetCopyStatusCode.REVISION_CONTENT_SKIPPED, "Revision content is skipped by default."));
                }
                if (item.Status == SheetCopyStatusCode.READY) Add(item, diagnostics, Info(SheetCopyStatusCode.READY, "Ready"));
            }
            return diagnostics;
        }

        public static bool IsStale(Document doc, SheetCopyPlan plan)
        {
            if (doc == null || plan == null) return true;
            foreach (SheetCopyItem item in plan.Items)
            {
                ViewSheet sheet = doc.GetElement(item.SourceSheetId) as ViewSheet;
                if (sheet == null || !string.Equals(sheet.UniqueId, item.SourceSheetUniqueId, StringComparison.Ordinal)) return true;
                SheetCopyItem current = SheetCopyCollector.AnalyzeSheet(doc, sheet);
                if (!string.Equals(item.SourceFingerprint, current.SourceFingerprint, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void ValidateView(Document doc, SheetCopyItem item, SheetCopyContentItem content, List<SheetCopyDiagnostic> diagnostics, ViewCopyPolicy policy)
        {
            View view = doc.GetElement(content.SourceViewId) as View;
            if (view == null) { Add(item, diagnostics, Error(SheetCopyStatusCode.VIEW_MISSING, "Source View no longer exists.")); return; }
            ViewDuplicateOption option = ToDuplicateOption(policy);
            bool supported = false;
            try { supported = view.CanViewBeDuplicated(option); } catch (Exception ex) { Add(item, diagnostics, Error(SheetCopyStatusCode.VIEW_DUPLICATION_UNSUPPORTED, "View duplication capability probe failed: " + ex.Message)); return; }
            if (!supported) Add(item, diagnostics, Error(SheetCopyStatusCode.VIEW_DUPLICATION_UNSUPPORTED, "Requested View duplication mode is unsupported; no silent downgrade will be used."));
            if (content.BoxCenter == null) Add(item, diagnostics, Error(SheetCopyStatusCode.VIEW_PLACEMENT_UNSUPPORTED, "Viewport placement geometry is unavailable."));
        }

        public static ViewDuplicateOption ToDuplicateOption(ViewCopyPolicy policy)
        {
            switch (policy)
            {
                case ViewCopyPolicy.DUPLICATE_VIEWS_WITH_DETAILING: return ViewDuplicateOption.WithDetailing;
                case ViewCopyPolicy.DUPLICATE_AS_DEPENDENT: return ViewDuplicateOption.AsDependent;
                default: return ViewDuplicateOption.Duplicate;
            }
        }

        private static SheetCopyDiagnostic Error(SheetCopyStatusCode code, string message) => new SheetCopyDiagnostic { StatusCode = code, Severity = SheetCopySeverity.ERROR, Message = message, CanExecute = false };
        private static SheetCopyDiagnostic Warning(SheetCopyStatusCode code, string message) => new SheetCopyDiagnostic { StatusCode = code, Severity = SheetCopySeverity.WARNING, Message = message, CanExecute = true };
        private static SheetCopyDiagnostic Info(SheetCopyStatusCode code, string message) => new SheetCopyDiagnostic { StatusCode = code, Severity = SheetCopySeverity.INFO, Message = message, CanExecute = true };
        private static void Add(SheetCopyItem item, List<SheetCopyDiagnostic> diagnostics, SheetCopyDiagnostic diagnostic)
        {
            item.Diagnostics.Add(diagnostic); diagnostics.Add(diagnostic);
            if (diagnostic.Severity == SheetCopySeverity.ERROR) { item.Status = diagnostic.StatusCode; item.Message = diagnostic.Message; }
            else if (item.Status == SheetCopyStatusCode.READY) item.Message = diagnostic.Message;
        }
    }
}
