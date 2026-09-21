using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetGen.Models;

namespace KhimTools.SheetGen.Services
{
    /// <summary>One cached preflight pass resolves stable title-block/view identities and reports actionable row diagnostics.</summary>
    public static class SheetGenPreflightService
    {
        private static readonly HashSet<ViewType> NormalViewTypes = new HashSet<ViewType>
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.EngineeringPlan, ViewType.AreaPlan,
            ViewType.Section, ViewType.Elevation, ViewType.ThreeD, ViewType.DraftingView,
            ViewType.Detail, ViewType.CostReport, ViewType.ColumnSchedule, ViewType.PanelSchedule,
            ViewType.Rendering, ViewType.Walkthrough
        };

        public static SheetContentKind ClassifyView(View view)
        {
            if (view == null || view.IsTemplate) return SheetContentKind.Unsupported;
            if (view.ViewType == ViewType.Legend) return SheetContentKind.Legend;
            if (view.ViewType == ViewType.Schedule || view is ViewSchedule) return SheetContentKind.Schedule;
            return NormalViewTypes.Contains(view.ViewType) ? SheetContentKind.NormalView : SheetContentKind.Unsupported;
        }

        public static List<SheetGenValidationResult> Validate(Document doc, IList<SheetGenItem> items)
        {
            var results = new List<SheetGenValidationResult>();
            if (items == null) return results;

            var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var placedViewIds = new HashSet<int>();
            var titleBlocks = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType().Cast<FamilySymbol>().ToList();
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.DrawingSheet && v.ViewType != ViewType.Internal).ToList();

            foreach (var sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
                existingNumbers.Add(sheet.SheetNumber ?? "");
            foreach (var vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>())
                placedViewIds.Add(vp.ViewId.IntegerValue);
            foreach (var si in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
                placedViewIds.Add(si.ScheduleId.IntegerValue);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAssignedViews = new HashSet<int>();
            for (int i = 0; i < items.Count; i++)
            {
                SheetGenItem item = items[i] ?? new SheetGenItem();
                bool priorWarning = item.StatusCode == SheetGenStatusCode.Warning;
                string priorWarningMessage = item.StatusMessage;
                item.StatusCode = SheetGenStatusCode.Ready;
                item.StatusMessage = "";
                var row = new SheetGenValidationResult { RowIndex = i, SheetNumber = item.SheetNumber ?? "", CanCreate = true };

                string number = (item.SheetNumber ?? "").Trim();
                if (string.IsNullOrWhiteSpace(number))
                    AddError(row, item, SheetGenStatusCode.EmptySheetNumber, "Sheet number is required.", "Enter a unique sheet number.");
                else if (number.Length > 255 || number.Any(char.IsControl))
                    AddError(row, item, SheetGenStatusCode.InvalidSheetNumber, "Sheet number contains unsupported characters.", "Remove control characters and shorten the number.");
                else if (!seen.Add(number))
                    AddError(row, item, SheetGenStatusCode.DuplicateInBatch, $"Sheet number '{number}' is duplicated in this batch.", "Change the duplicate number.");
                else if (existingNumbers.Contains(number))
                    AddError(row, item, SheetGenStatusCode.DuplicateInProject, $"Sheet number '{number}' already exists in the project.", "Change the number or skip this row.");

                if (string.IsNullOrWhiteSpace(item.SheetName))
                    AddWarning(row, item, SheetGenStatusCode.EmptySheetName, "Sheet name is blank; Revit may assign a default name.");
                else if (item.SheetName.Length > 255 || item.SheetName.Any(char.IsControl))
                    AddError(row, item, SheetGenStatusCode.InvalidSheetName, "Sheet name contains unsupported characters.", "Remove control characters and shorten the name.");

                ResolveTitleBlock(doc, item, titleBlocks, row);
                ResolveView(item, views, placedViewIds, row);
                if (row.Severity != SheetValidationSeverity.Error && item.ContentKind == SheetContentKind.NormalView && item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId && !seenAssignedViews.Add(item.AssignedViewId.IntegerValue))
                    AddError(row, item, SheetGenStatusCode.ViewAlreadyPlaced, "The assigned view is used by another row in this batch.", "Choose a different view or clear this assignment.");

                if (row.Severity == SheetValidationSeverity.None)
                {
                    row.Code = priorWarning ? SheetGenStatusCode.Warning : (item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId
                        ? SheetGenStatusCode.Ready : SheetGenStatusCode.NoView);
                    row.Severity = priorWarning || (item.AssignedViewId == null || item.AssignedViewId == ElementId.InvalidElementId)
                        ? SheetValidationSeverity.Warning : SheetValidationSeverity.None;
                    row.Message = priorWarning ? priorWarningMessage : (row.Code == SheetGenStatusCode.NoView ? "No view or schedule assigned; an empty sheet will be created." : "Ready");
                    item.StatusCode = row.Code;
                    item.StatusMessage = row.Message;
                }
                item.StatusCode = row.Code;
                item.StatusMessage = row.Message;
                results.Add(row);
            }
            return results;
        }

        private static void ResolveTitleBlock(Document doc, SheetGenItem item, IList<FamilySymbol> symbols, SheetGenValidationResult row)
        {
            if (item.AllowBlankTitleBlock && string.IsNullOrWhiteSpace(item.TitleBlockName) &&
                (item.TitleBlockId == null || item.TitleBlockId == ElementId.InvalidElementId))
                return;

            FamilySymbol symbol = null;
            if (item.TitleBlockId != null && item.TitleBlockId != ElementId.InvalidElementId)
                symbol = symbols.FirstOrDefault(s => s.Id == item.TitleBlockId);
            if (symbol == null && !string.IsNullOrWhiteSpace(item.TitleBlockName))
            {
                var matches = symbols.Where(s => string.Equals(DisplayName(s), item.TitleBlockName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) symbol = matches[0];
                else if (matches.Count > 1)
                {
                    AddError(row, item, SheetGenStatusCode.TitleBlockAmbiguous, $"Title block '{item.TitleBlockName}' matches multiple types.", "Choose a title block by its exact family/type name.");
                    return;
                }
            }
            if (symbol == null)
            {
                if (item.TitleBlockId != null && item.TitleBlockId != ElementId.InvalidElementId)
                {
                    AddError(row, item, SheetGenStatusCode.InvalidTitleBlockType, "The selected title block is no longer a valid title block type.", "Choose a current title block type.");
                    return;
                }
                AddError(row, item, string.IsNullOrWhiteSpace(item.TitleBlockName) ? SheetGenStatusCode.MissingTitleBlock : SheetGenStatusCode.TitleBlockNotFound,
                    string.IsNullOrWhiteSpace(item.TitleBlockName) ? "No title block is selected. Blank sheets require explicit opt-in." : "The selected title block was not found in the project.",
                    "Choose a current title block or enable the blank-sheet option.");
                return;
            }
            item.TitleBlockId = symbol.Id;
            item.TitleBlockName = DisplayName(symbol);
            item.TitleBlockUniqueId = symbol.UniqueId;
        }

        private static void ResolveView(SheetGenItem item, IList<View> views, ISet<int> placedViewIds, SheetGenValidationResult row)
        {
            string requested = item.AssignedViewName == null ? "" : item.AssignedViewName.Trim();
            if (string.IsNullOrWhiteSpace(requested) || requested.StartsWith("<", StringComparison.Ordinal))
            {
                item.AssignedViewId = ElementId.InvalidElementId;
                item.ContentKind = SheetContentKind.None;
                return;
            }

            View view = null;
            if (item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId)
                view = views.FirstOrDefault(v => v.Id == item.AssignedViewId);
            if (view == null && !string.IsNullOrWhiteSpace(item.AssignedViewUniqueId))
                view = views.FirstOrDefault(v => string.Equals(v.UniqueId, item.AssignedViewUniqueId, StringComparison.Ordinal));
            if (view == null)
            {
                string displayName = requested;
                int close = displayName.IndexOf(']');
                if (displayName.StartsWith("[") && close >= 0) displayName = displayName.Substring(close + 1).Trim();
                var matches = views.Where(v => string.Equals(v.Name, displayName, StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(DisplayViewName(v), requested, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) view = matches[0];
                else if (matches.Count > 1)
                {
                    AddError(row, item, SheetGenStatusCode.ViewAmbiguous, $"View '{requested}' matches multiple views.", "Import the stable view identity or choose a unique view.");
                    return;
                }
            }
            if (view == null)
            {
                AddError(row, item, SheetGenStatusCode.ViewNotFound, $"View '{requested}' was not found.", "Choose an existing view or clear the assignment.");
                return;
            }

            item.AssignedViewId = view.Id;
            item.AssignedViewUniqueId = view.UniqueId;
            item.AssignedViewName = view.Name;
            item.AssignedViewType = view.ViewType.ToString();
            item.ContentKind = ClassifyView(view);
            if (item.ContentKind == SheetContentKind.Unsupported)
            {
                AddError(row, item, SheetGenStatusCode.UnsupportedView, $"View '{view.Name}' ({view.ViewType}) cannot be placed on a sheet.", "Choose a normal view, legend, or schedule.");
                return;
            }
            if (item.ContentKind == SheetContentKind.NormalView && placedViewIds.Contains(view.Id.IntegerValue))
                AddError(row, item, SheetGenStatusCode.ViewAlreadyPlaced, $"View '{view.Name}' is already placed on a sheet.", "Choose an unplaced view or clear the assignment.");
        }

        private static string DisplayName(FamilySymbol symbol) => $"{symbol.FamilyName} : {symbol.Name}";
        private static string DisplayViewName(View view) => $"[{view.ViewType}] {view.Name}";

        private static void AddError(SheetGenValidationResult row, SheetGenItem item, SheetGenStatusCode code, string message, string action)
        {
            if (row.Severity == SheetValidationSeverity.Error) return;
            row.Severity = SheetValidationSeverity.Error; row.Code = code; row.Message = message; row.CanCreate = false; row.SuggestedAction = action;
            item.StatusCode = code; item.StatusMessage = message;
        }

        private static void AddWarning(SheetGenValidationResult row, SheetGenItem item, SheetGenStatusCode code, string message)
        {
            if (row.Severity == SheetValidationSeverity.Error) return;
            row.Severity = SheetValidationSeverity.Warning; row.Code = code; row.Message = message; row.CanCreate = true;
            item.StatusCode = code; item.StatusMessage = message;
        }
    }
}
