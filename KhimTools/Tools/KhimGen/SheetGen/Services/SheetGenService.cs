using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using KhimTools.SheetGen.Models;

namespace KhimTools.SheetGen.Services
{
    public class TitleBlockOption
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public string UniqueId { get; set; }
        public override string ToString() => Name;
    }

    public class ViewOption
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public string UniqueId { get; set; }
        public string ViewType { get; set; }
        public SheetContentKind ContentKind { get; set; }
        public bool IsSupported => ContentKind != SheetContentKind.Unsupported;
        public override string ToString() => $"[{ViewType}] {Name}";
    }

    /// <summary>SheetGen application service. UI-facing methods retain the original signatures for compatibility.</summary>
    public static class SheetGenService
    {
        public static List<TitleBlockOption> GetAvailableTitleBlocks(Document doc)
        {
            var list = new List<TitleBlockOption>();
            if (doc == null) return list;
            foreach (FamilySymbol tb in new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType()
                .Cast<FamilySymbol>().OrderBy(fs => fs.FamilyName).ThenBy(fs => fs.Name))
            {
                list.Add(new TitleBlockOption { Id = tb.Id, Name = $"{tb.FamilyName} : {tb.Name}", UniqueId = tb.UniqueId });
            }
            return list;
        }

        public static List<ViewOption> GetAvailableViews(Document doc)
        {
            var list = new List<ViewOption>();
            if (doc == null) return list;
            foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.DrawingSheet && v.ViewType != ViewType.Internal)
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name))
            {
                var kind = SheetGenPreflightService.ClassifyView(v);
                list.Add(new ViewOption { Id = v.Id, Name = v.Name, UniqueId = v.UniqueId, ViewType = v.ViewType.ToString(), ContentKind = kind });
            }
            return list;
        }

        public static List<SheetGenItem> GenerateFromSeries(List<SheetSeriesConfig> seriesList, List<TitleBlockOption> availableTb)
        {
            var result = new List<SheetGenItem>();
            if (seriesList == null) return result;
            availableTb = availableTb ?? new List<TitleBlockOption>();

            foreach (var series in seriesList.Where(s => s != null && s.IsEnabled))
            {
                int count = Math.Max(0, series.Count);
                int padding = Math.Max(0, series.NumberPadding);
                var tb = availableTb.FirstOrDefault(t => string.Equals(t.Name, series.TitleBlockName, StringComparison.OrdinalIgnoreCase));
                var tbId = tb == null ? series.TitleBlockId : tb.Id;
                if (tbId == null) tbId = ElementId.InvalidElementId;
                for (int i = 0; i < count; i++)
                {
                    int number = series.StartNumber + (i * series.Step);
                    string numberText = padding == 0 ? number.ToString(CultureInfo.InvariantCulture)
                        : number.ToString("D" + padding, CultureInfo.InvariantCulture);
                    string sheetNumber = (series.Prefix ?? "") + numberText + (series.Suffix ?? "");
                    string pattern = series.NamePattern ?? "BẢN VẼ";
                    string name = ExpandTokens(pattern, i + 1, number, padding, out string unknown);
                    var item = new SheetGenItem
                    {
                        IsSelected = true, SheetNumber = sheetNumber, SheetName = name,
                        TitleBlockId = tbId, TitleBlockName = series.TitleBlockName ?? "", TitleBlockUniqueId = tb?.UniqueId ?? "",
                        AllowBlankTitleBlock = series.AllowBlankTitleBlock, Discipline = series.Discipline ?? ""
                    };
                    if (!string.IsNullOrWhiteSpace(unknown))
                    {
                        item.StatusCode = SheetGenStatusCode.Warning;
                        item.StatusMessage = "Unknown name token(s): " + unknown;
                    }
                    result.Add(item);
                }
            }
            return result;
        }

        private static string ExpandTokens(string pattern, int index, int number, int padding, out string unknown)
        {
            var unknownTokens = new List<string>();
            string value = Regex.Replace(pattern, @"\{([^{}]+)\}", m =>
            {
                string token = m.Groups[1].Value;
                if (token == "n" || token == "Index") return index.ToString(CultureInfo.InvariantCulture);
                if (token == "0n") return padding == 0 ? index.ToString(CultureInfo.InvariantCulture) : index.ToString("D" + padding, CultureInfo.InvariantCulture);
                if (token == "Number") return padding == 0 ? number.ToString(CultureInfo.InvariantCulture) : number.ToString("D" + padding, CultureInfo.InvariantCulture);
                unknownTokens.Add(token);
                return m.Value;
            });
            unknown = string.Join(", ", unknownTokens.Distinct(StringComparer.OrdinalIgnoreCase));
            return value;
        }

        public static SheetBatchCreationResult CreateSheetsDetailed(Document doc, IList<SheetGenItem> items,
            SheetGenParameterMapping mapping = null, SheetPlacementAnchor anchor = SheetPlacementAnchor.DefaultCenter)
        {
            var batch = new SheetBatchCreationResult();
            if (doc == null || items == null) return batch;
            var selected = items.Where(i => i != null && i.IsSelected).ToList();
            batch.Requested = selected.Count;
            var validation = SheetGenPreflightService.Validate(doc, selected);
            batch.Valid = validation.Count(v => v.CanCreate);
            Debug.WriteLine("[K-TOOLS][SheetGen] Preflight requested=" + batch.Requested + " valid=" + batch.Valid + " skipped=" + (batch.Requested - batch.Valid));

            using (var group = new TransactionGroup(doc, "K-TOOLS - SheetGen batch"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "SheetGen batch");
                for (int i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    var check = validation[i];
                    if (!check.CanCreate)
                    {
                        var skipped = NewResult(item, i, SheetGenStatusCode.Skipped, SheetValidationSeverity.Error, check.Message);
                        batch.Results.Add(skipped); batch.Skipped++; continue;
                    }

                    var timer = Stopwatch.StartNew();
                    Debug.WriteLine("[K-TOOLS][SheetGen] Create start number=" + item.SheetNumber + " titleBlock=" + item.TitleBlockName + " content=" + item.AssignedViewName);
                    var result = NewResult(item, i, SheetGenStatusCode.Failed, SheetValidationSeverity.Error, "");
                    using (var tx = new Transaction(doc, "K-TOOLS - Create " + item.SheetNumber))
                    {
                        try
                        {
                            KhimTools.Core.Revit.TransactionBoundary.Start(tx, "SheetGen " + item.SheetNumber);
                            ViewSheet sheet = ViewSheet.Create(doc, item.TitleBlockId ?? ElementId.InvalidElementId);
                            if (sheet == null) throw new InvalidOperationException("Revit did not return a sheet.");
                            sheet.SheetNumber = item.SheetNumber.Trim();
                            if (!string.IsNullOrWhiteSpace(item.SheetName)) sheet.Name = item.SheetName.Trim();

                            result.CreatedSheetId = sheet.Id;
                            result.PlacedContentId = ElementId.InvalidElementId;
                            ApplyParameter(sheet, BuiltInParameter.SHEET_DRAWN_BY, item.DrawnBy, "Drawn By", result);
                            ApplyParameter(sheet, BuiltInParameter.SHEET_CHECKED_BY, item.CheckedBy, "Checked By", result);
                            ApplyNamedParameter(sheet, mapping ?? new SheetGenParameterMapping(), item.Discipline, result);

                            if (item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId)
                            {
                                View content = doc.GetElement(item.AssignedViewId) as View;
                                if (content == null) throw new InvalidOperationException("Assigned content disappeared after preflight.");
                                result.PlacedContentId = content.Id;
                                if (item.ContentKind == SheetContentKind.Schedule || content is ViewSchedule)
                                {
                                    ScheduleSheetInstance instance = ScheduleSheetInstance.Create(doc, sheet.Id, content.Id, SheetPlacementService.GetPlacementPoint(sheet, anchor));
                                    if (instance == null) throw new InvalidOperationException("ScheduleSheetInstance.Create returned null.");
                                    result.ContentInstanceId = instance.Id;
                                }
                                else
                                {
                                    if (!Viewport.CanAddViewToSheet(doc, sheet.Id, content.Id))
                                        throw new InvalidOperationException("Viewport.CanAddViewToSheet returned false.");
                                    Viewport viewport = Viewport.Create(doc, sheet.Id, content.Id,
                                        SheetPlacementService.GetPlacementPoint(sheet, anchor));
                                    if (viewport == null) throw new InvalidOperationException("Viewport.Create returned null.");
                                    result.ContentInstanceId = viewport.Id;
                                }
                            }

                            ViewSheet committedCandidate = doc.GetElement(sheet.Id) as ViewSheet;
                            if (committedCandidate == null || !string.Equals(committedCandidate.SheetNumber, item.SheetNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Sheet creation postcondition failed.");
                            if (result.ContentInstanceId != ElementId.InvalidElementId && doc.GetElement(result.ContentInstanceId) == null)
                                throw new InvalidOperationException("Placed content postcondition failed.");
                            KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "SheetGen " + item.SheetNumber);
                            result.Status = SheetGenStatusCode.Created;
                            result.Severity = result.Messages.Count == 0 ? SheetValidationSeverity.None : SheetValidationSeverity.Warning;
                            if (result.Messages.Count == 0) result.Messages.Add("Created");
                            batch.Created++;
                            Debug.WriteLine("[K-TOOLS][SheetGen] Create success number=" + item.SheetNumber + " sheetId=" + result.CreatedSheetId + " contentInstanceId=" + result.ContentInstanceId + " durationMs=" + timer.ElapsedMilliseconds);
                            item.PlacedContentId = result.PlacedContentId;
                            item.ContentInstanceId = result.ContentInstanceId;
                        }
                        catch (Exception ex)
                        {
                            KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "SheetGen " + item.SheetNumber);
                            result.Status = SheetGenStatusCode.Failed;
                            result.Severity = SheetValidationSeverity.Error;
                            result.Messages.Add("Rolled back: " + ex.Message);
                            batch.Failed++;
                            System.Diagnostics.Debug.WriteLine("[K-TOOLS][SheetGen] " + item.SheetNumber + ": " + ex);
                        }
                    }
                    timer.Stop();
                    result.Duration = timer.Elapsed;
                    batch.Results.Add(result);
                }
                KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "SheetGen batch");
            }
            return batch;
        }

        private static SheetCreationResult NewResult(SheetGenItem item, int index, SheetGenStatusCode status, SheetValidationSeverity severity, string message)
        {
            var result = new SheetCreationResult { RowIndex = index, SheetNumber = item.SheetNumber ?? "", SheetName = item.SheetName ?? "", Status = status, Severity = severity };
            if (!string.IsNullOrWhiteSpace(message)) result.Messages.Add(message);
            return result;
        }

        private static void ApplyParameter(ViewSheet sheet, BuiltInParameter parameter, string value, string label, SheetCreationResult result)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            Parameter p = sheet.get_Parameter(parameter);
            if (p == null) { result.Messages.Add(label + " parameter is not available."); return; }
            if (p.IsReadOnly) { result.Messages.Add(label + " parameter is read-only."); return; }
            try { p.Set(value); } catch (Exception ex) { result.Messages.Add(label + " could not be written: " + ex.Message); }
        }

        private static void ApplyNamedParameter(ViewSheet sheet, SheetGenParameterMapping mapping, string value, SheetCreationResult result)
        {
            if (mapping == null || string.IsNullOrWhiteSpace(value)) return;
            Parameter p = sheet.LookupParameter(mapping.DisciplineParameterName);
            if (p == null) { if (mapping.WriteMissingParameterWarnings) result.Messages.Add("Discipline parameter '" + mapping.DisciplineParameterName + "' is not available."); return; }
            if (p.IsReadOnly) { result.Messages.Add("Discipline parameter '" + mapping.DisciplineParameterName + "' is read-only."); return; }
            try { p.Set(value); } catch (Exception ex) { result.Messages.Add("Discipline could not be written: " + ex.Message); }
        }

        public static (int createdCount, List<string> errors) CreateSheets(Document doc, List<SheetGenItem> items)
        {
            var detailed = CreateSheetsDetailed(doc, items);
            var errors = detailed.Results.Where(r => r.Status != SheetGenStatusCode.Created)
                .Select(r => string.IsNullOrWhiteSpace(r.StatusText) ? r.Status.ToString() : r.StatusText).ToList();
            return (detailed.Created, errors);
        }

        public static bool ExportToCsv(string filePath, List<SheetGenItem> items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return false;
                var header = new[] { "Selected", "Sheet Number", "Sheet Name", "Title Block", "Title Block Unique Id", "Allow Blank Title Block",
                    "Assigned View", "Assigned View Type", "Assigned View Unique Id", "Content Kind",
                    "Drawn By", "Checked By", "Discipline", "Status Code", "Status Message" };
                var lines = new List<string> { string.Join(",", header.Select(CsvEscape)) };
                foreach (var it in items ?? new List<SheetGenItem>())
                {
                    var fields = new[] {
                        it.IsSelected ? "true" : "false", it.SheetNumber, it.SheetName, it.TitleBlockName, it.TitleBlockUniqueId,
                        it.AllowBlankTitleBlock ? "true" : "false",
                        it.AssignedViewName, it.AssignedViewType, it.AssignedViewUniqueId,
                        it.ContentKind.ToString(), it.DrawnBy, it.CheckedBy, it.Discipline, it.StatusCode.ToString(),
                        it.StatusMessage
                    };
                    lines.Add(string.Join(",", fields.Select(CsvEscape)));
                }
                File.WriteAllText(filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][SheetGen][CSV] export failed: " + ex);
                return false;
            }
        }

        private static string IdText(ElementId id) => id == null || id == ElementId.InvalidElementId ? "" : id.IntegerValue.ToString(CultureInfo.InvariantCulture);
        private static string CsvEscape(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        public static SheetGenImportResult ImportFromCsvDetailed(string filePath, IList<TitleBlockOption> titleBlocks, IList<ViewOption> views)
        {
            var result = new SheetGenImportResult();
            if (!File.Exists(filePath))
            {
                result.Diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = 0, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = "CSV file does not exist." });
                return result;
            }
            titleBlocks = titleBlocks ?? new List<TitleBlockOption>();
            views = views ?? new List<ViewOption>();
            try
            {
                string text;
                using (var reader = new StreamReader(filePath, new UTF8Encoding(false, true), true)) text = reader.ReadToEnd();
                var records = ParseCsvRecords(text, result.Diagnostics);
                if (records.Count == 0) return result;
                var header = records[0].Select((v, i) => new { Key = NormalizeHeader(v), Index = i })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToDictionary(x => x.Key, x => x.Index, StringComparer.OrdinalIgnoreCase);
                if (!header.ContainsKey("sheetnumber") || !header.ContainsKey("sheetname"))
                {
                    result.Diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = "CSV must contain Sheet Number and Sheet Name columns." });
                    return result;
                }

                for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
                {
                    var row = records[rowIndex];
                    if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
                    if (row.Count < 2)
                    {
                        result.Diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = rowIndex + 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = "Row has fewer than Sheet Number and Sheet Name fields." });
                        continue;
                    }
                    try
                    {
                        var item = new SheetGenItem {
                            IsSelected = BoolField(row, header, "selected", true),
                            SheetNumber = Field(row, header, "sheetnumber"),
                            SheetName = Field(row, header, "sheetname"),
                            TitleBlockName = Field(row, header, "titleblock"),
                            TitleBlockUniqueId = Field(row, header, "titleblockuniqueid"),
                            AllowBlankTitleBlock = BoolField(row, header, "allowblanktitleblock", false),
                            AssignedViewName = Field(row, header, "assignedview"),
                            AssignedViewType = Field(row, header, "assignedviewtype"),
                            AssignedViewUniqueId = Field(row, header, "assignedviewuniqueid"),
                            Discipline = Field(row, header, "discipline"),
                            DrawnBy = Field(row, header, "drawnby"),
                            CheckedBy = Field(row, header, "checkedby")
                        };
                        int id;
                        if (int.TryParse(Field(row, header, "titleblockid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            item.TitleBlockId = new ElementId(id);
                        if (int.TryParse(Field(row, header, "assignedviewid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            item.AssignedViewId = new ElementId(id);
                        if (int.TryParse(Field(row, header, "placedcontentid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            item.PlacedContentId = new ElementId(id);
                        if (int.TryParse(Field(row, header, "contentinstanceid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            item.ContentInstanceId = new ElementId(id);
                        var contentKind = Field(row, header, "contentkind");
                        SheetContentKind parsedKind;
                        if (Enum.TryParse(contentKind, true, out parsedKind)) item.ContentKind = parsedKind;
                        SheetGenStatusCode parsedStatus;
                        if (Enum.TryParse(Field(row, header, "statuscode"), true, out parsedStatus)) item.StatusCode = parsedStatus;
                        item.StatusMessage = Field(row, header, "statusmessage");

                        ResolveImportedTitleBlock(item, titleBlocks, rowIndex, result.Diagnostics);
                        ResolveImportedView(item, views, rowIndex, result.Diagnostics);
                        result.Items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        result.Diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = rowIndex + 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = 0, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = "Could not read CSV: " + ex.Message });
            }
            return result;
        }

        public static List<SheetGenItem> ImportFromCsv(string filePath, List<TitleBlockOption> titleBlocks)
        {
            return ImportFromCsvDetailed(filePath, titleBlocks, null).Items;
        }

        private static void ResolveImportedTitleBlock(SheetGenItem item, IList<TitleBlockOption> options, int row, IList<SheetGenImportDiagnostic> diagnostics)
        {
            if (item.TitleBlockId != null && item.TitleBlockId != ElementId.InvalidElementId)
            {
                var byId = options.FirstOrDefault(t => t.Id == item.TitleBlockId);
                if (byId != null) { item.TitleBlockName = byId.Name; return; }
            }
            if (string.IsNullOrWhiteSpace(item.TitleBlockName)) return;
            var matches = options.Where(t => string.Equals(t.Name, item.TitleBlockName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1) { item.TitleBlockId = matches[0].Id; item.TitleBlockName = matches[0].Name; item.TitleBlockUniqueId = matches[0].UniqueId; }
            else if (matches.Count == 0)
                diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = row + 1, Severity = SheetValidationSeverity.Warning, Code = SheetGenStatusCode.TitleBlockNotFound, Message = "Title block '" + item.TitleBlockName + "' was not found." });
            else
                diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = row + 1, Severity = SheetValidationSeverity.Warning, Code = SheetGenStatusCode.TitleBlockAmbiguous, Message = "Title block '" + item.TitleBlockName + "' is ambiguous." });
        }

        private static void ResolveImportedView(SheetGenItem item, IList<ViewOption> options, int row, IList<SheetGenImportDiagnostic> diagnostics)
        {
            if (options == null || string.IsNullOrWhiteSpace(item.AssignedViewName) || item.AssignedViewName.StartsWith("<", StringComparison.Ordinal)) return;
            ViewOption match = null;
            if (!string.IsNullOrWhiteSpace(item.AssignedViewUniqueId))
                match = options.FirstOrDefault(v => string.Equals(v.UniqueId, item.AssignedViewUniqueId, StringComparison.Ordinal));
            if (match == null && item.AssignedViewId != null && item.AssignedViewId != ElementId.InvalidElementId)
                match = options.FirstOrDefault(v => v.Id == item.AssignedViewId);
            if (match == null)
            {
                string name = item.AssignedViewName;
                int close = name.IndexOf(']');
                if (name.StartsWith("[", StringComparison.Ordinal) && close >= 0) name = name.Substring(close + 1).Trim();
                var matches = options.Where(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.ToString(), item.AssignedViewName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) match = matches[0];
                else if (matches.Count > 1)
                {
                    diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = row + 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ViewAmbiguous, Message = "Assigned view '" + item.AssignedViewName + "' matches multiple views." });
                    return;
                }
            }
            if (match == null)
            {
                diagnostics.Add(new SheetGenImportDiagnostic { RowIndex = row + 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ViewNotFound, Message = "Assigned view '" + item.AssignedViewName + "' was not found." });
                return;
            }
            item.AssignedViewId = match.Id; item.AssignedViewUniqueId = match.UniqueId; item.AssignedViewName = match.Name;
            item.AssignedViewType = match.ViewType; item.ContentKind = match.ContentKind;
        }

        private static string NormalizeHeader(string value)
        {
            return new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static string Field(IList<string> row, IDictionary<string, int> header, string key)
        {
            int index; return header.TryGetValue(key, out index) && index < row.Count ? (row[index] ?? "").Trim() : "";
        }

        private static bool BoolField(IList<string> row, IDictionary<string, int> header, string key, bool fallback)
        {
            string value = Field(row, header, key);
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            bool parsed; return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        public static List<List<string>> ParseCsvRecords(string text, IList<SheetGenImportDiagnostic> diagnostics = null)
        {
            var rows = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (text ?? "").Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { current.Add(field.ToString()); field.Clear(); }
                else if ((c == '\r' || c == '\n') && !quoted)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    current.Add(field.ToString()); field.Clear();
                    if (current.Any(v => !string.IsNullOrWhiteSpace(v))) rows.Add(current);
                    current = new List<string>();
                }
                else field.Append(c);
            }
            if (quoted)
            {
                diagnostics?.Add(new SheetGenImportDiagnostic { RowIndex = rows.Count + 1, Severity = SheetValidationSeverity.Error, Code = SheetGenStatusCode.ImportMalformed, Message = "Unclosed quoted field." });
            }
            current.Add(field.ToString());
            if (current.Any(v => !string.IsNullOrWhiteSpace(v))) rows.Add(current);
            return rows;
        }
    }
}
