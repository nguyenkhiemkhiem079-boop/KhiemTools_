using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace KhimTools.DetailNumberUpdater.Services
{
    public enum DetailNumberStatusCode
    {
        READY,
        NO_CHANGE,
        NO_MATCH,
        INVALID_REGEX,
        DUPLICATE_IN_BATCH,
        DUPLICATE_WITH_EXISTING,
        INVALID_NUMBER,
        READ_ONLY,
        MISSING_VIEWPORT,
        MISSING_VIEW,
        MANUAL_OVERRIDE,
        BLOCKED,
        FAILED
    }

    public sealed class RegexValidationResult
    {
        public bool IsValid { get; set; }
        public string Pattern { get; set; }
        public string ErrorMessage { get; set; }
        public Regex CompiledRegex { get; set; }
    }

    public class DetailNumberCandidate
    {
        public ElementId ViewportId { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId SheetId { get; set; }
        public string ViewName { get; set; }
        public string CurrentNumber { get; set; }
        public string ExtractedBaseNumber { get; set; }
        public string ProposedNumber { get; set; }
        public bool IsSelected { get; set; }
        public bool IsManualOverride { get; set; }
        public bool CanExecute { get; set; }
        public DetailNumberStatusCode Status { get; set; }
        public string Message { get; set; }
    }

    public sealed class DetailNumberPreviewItem : DetailNumberCandidate
    {
        private bool _matchedOverride;
        public Viewport Viewport { get; set; }
        public View View { get; set; }
        public string CurrentDetailNumber
        {
            get { return CurrentNumber; }
            set { CurrentNumber = value; }
        }
        public string NewDetailNumber
        {
            get { return ProposedNumber; }
            set { ProposedNumber = value; }
        }
        public bool IsMatched
        {
            get { return _matchedOverride || !string.IsNullOrWhiteSpace(ExtractedBaseNumber); }
            set
            {
                _matchedOverride = value;
                if (!value) ExtractedBaseNumber = string.Empty;
            }
        }
    }

    public sealed class DetailNumberPreflightSummary
    {
        public int Requested { get; set; }
        public int Ready { get; set; }
        public int NoMatch { get; set; }
        public int NoChange { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    public sealed class DetailNumberPreflightReport
    {
        public RegexValidationResult Regex { get; set; }
        public List<DetailNumberCandidate> Candidates { get; set; }
        public DetailNumberPreflightSummary Summary { get; set; }
        public bool CanApply { get; set; }
    }

    public sealed class DetailNumberExecutionResult
    {
        public ElementId ViewportId { get; set; }
        public string ViewName { get; set; }
        public string OldNumber { get; set; }
        public string NewNumber { get; set; }
        public DetailNumberStatusCode Status { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; }
    }

    public sealed class DetailNumberBatchResult
    {
        public int Requested { get; set; }
        public int Ready { get; set; }
        public int Changed { get; set; }
        public int AlreadyCorrect { get; set; }
        public int Skipped { get; set; }
        public int FailedCount { get; set; }
        public List<DetailNumberExecutionResult> Results { get; } = new List<DetailNumberExecutionResult>();
        public List<string> Errors { get; } = new List<string>();
        public int Success { get { return Changed; } }
        public int Failed { get { return FailedCount; } }
    }

    public static class DetailNumberConflictResolver
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string Resolve(string baseNumber, IEnumerable<string> existingNumbers,
            IEnumerable<string> pendingNumbers, string currentNumber)
        {
            string baseValue = Normalize(baseNumber);
            if (string.IsNullOrEmpty(baseValue)) return string.Empty;

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existingNumbers != null)
            {
                foreach (string number in existingNumbers)
                {
                    string normalized = Normalize(number);
                    if (!string.IsNullOrEmpty(normalized)) used.Add(normalized);
                }
            }
            string current = Normalize(currentNumber);
            if (!string.IsNullOrEmpty(current)) used.Remove(current);
            if (pendingNumbers != null)
            {
                foreach (string number in pendingNumbers)
                {
                    string normalized = Normalize(number);
                    if (!string.IsNullOrEmpty(normalized)) used.Add(normalized);
                }
            }
            if (!used.Contains(baseValue)) return baseValue;
            int suffix = 1;
            while (used.Contains(baseValue + "." + suffix)) suffix++;
            return baseValue + "." + suffix;
        }
    }

    public static class DetailNumberPreflightService
    {
        public static DetailNumberPreflightReport Preflight(Document doc, ViewSheet sheet,
            IEnumerable<DetailNumberCandidate> input, string pattern = DetailNumberService.DefaultPattern)
        {
            var candidates = input == null ? new List<DetailNumberCandidate>() : input.ToList();
            var report = new DetailNumberPreflightReport
            {
                Regex = DetailNumberService.ValidateRegex(pattern),
                Candidates = candidates,
                Summary = new DetailNumberPreflightSummary()
            };
            report.Summary.Requested = candidates.Count(c => c.IsSelected);

            if (!report.Regex.IsValid)
            {
                foreach (var candidate in candidates.Where(c => c.IsSelected))
                {
                    candidate.CanExecute = false;
                    candidate.Status = DetailNumberStatusCode.INVALID_REGEX;
                    candidate.Message = report.Regex.ErrorMessage;
                }
                report.Summary.Skipped = report.Summary.Requested;
                return report;
            }

            var allCurrent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var owners = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            if (doc != null && sheet != null)
            {
                foreach (ElementId viewportId in sheet.GetAllViewports())
                {
                    var viewport = doc.GetElement(viewportId) as Viewport;
                    if (viewport == null) continue;
                    string number = DetailNumberService.GetDetailNumber(viewport);
                    if (string.IsNullOrEmpty(number)) continue;
                    allCurrent.Add(number);
                    if (!owners.ContainsKey(number)) owners[number] = viewportId;
                }
            }

            // Re-resolve generated values against the current model state. Manual values remain authoritative.
            var generatedPending = new List<string>();
            foreach (DetailNumberCandidate candidate in candidates.Where(c => c.IsSelected && !c.IsManualOverride && !string.IsNullOrEmpty(c.ExtractedBaseNumber)))
            {
                Viewport currentViewport = doc == null || candidate.ViewportId == null ? null : doc.GetElement(candidate.ViewportId) as Viewport;
                if (currentViewport != null) candidate.CurrentNumber = DetailNumberService.GetDetailNumber(currentViewport);
                candidate.ProposedNumber = DetailNumberConflictResolver.Resolve(candidate.ExtractedBaseNumber, allCurrent, generatedPending, candidate.CurrentNumber);
                generatedPending.Add(candidate.ProposedNumber);
            }

            foreach (var candidate in candidates.Where(c => c.IsSelected))
            {
                candidate.CanExecute = false;
                var viewport = doc == null || candidate.ViewportId == null ? null : doc.GetElement(candidate.ViewportId) as Viewport;
                if (viewport == null)
                {
                    candidate.Status = DetailNumberStatusCode.MISSING_VIEWPORT;
                    candidate.Message = "Viewport no longer exists.";
                    continue;
                }
                var view = candidate.ViewId == null ? null : doc.GetElement(candidate.ViewId) as View;
                if (view == null)
                {
                    candidate.Status = DetailNumberStatusCode.MISSING_VIEW;
                    candidate.Message = "Referenced view no longer exists.";
                    continue;
                }
                candidate.CurrentNumber = DetailNumberService.GetDetailNumber(viewport);
                string proposed = DetailNumberConflictResolver.Normalize(candidate.ProposedNumber);
                candidate.ProposedNumber = proposed;
                if (string.IsNullOrEmpty(proposed))
                {
                    candidate.Status = DetailNumberStatusCode.INVALID_NUMBER;
                    candidate.Message = "A detail number is required.";
                    continue;
                }
                if (candidate.IsManualOverride)
                {
                    candidate.Status = DetailNumberStatusCode.MANUAL_OVERRIDE;
                    candidate.CanExecute = !string.Equals(candidate.CurrentNumber, proposed, StringComparison.OrdinalIgnoreCase);
                    candidate.Message = candidate.CanExecute ? "Manual value will be validated before apply." : "Manual value already matches.";
                    if (!candidate.CanExecute) candidate.Status = DetailNumberStatusCode.NO_CHANGE;
                }
                else if (string.IsNullOrEmpty(candidate.ExtractedBaseNumber))
                {
                    candidate.Status = DetailNumberStatusCode.NO_MATCH;
                    candidate.Message = "No detail number matched the configured pattern.";
                }
                else if (string.Equals(candidate.CurrentNumber, proposed, StringComparison.OrdinalIgnoreCase))
                {
                    candidate.Status = DetailNumberStatusCode.NO_CHANGE;
                    candidate.Message = "The detail number is already correct.";
                }
                else
                {
                    candidate.Status = DetailNumberStatusCode.READY;
                    candidate.CanExecute = true;
                    candidate.Message = "Ready to update.";
                }

                Parameter parameter = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (parameter == null || parameter.IsReadOnly)
                {
                    candidate.CanExecute = false;
                    candidate.Status = DetailNumberStatusCode.READ_ONLY;
                    candidate.Message = "Detail number parameter is unavailable or read-only.";
                }
            }

            var executable = candidates.Where(c => c.IsSelected && c.CanExecute).ToList();
            foreach (var group in executable.GroupBy(c => DetailNumberConflictResolver.Normalize(c.ProposedNumber), StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() <= 1) continue;
                foreach (var candidate in group)
                {
                    candidate.CanExecute = false;
                    candidate.Status = DetailNumberStatusCode.DUPLICATE_IN_BATCH;
                    candidate.Message = "Two selected viewports request the same number.";
                }
            }

            var changingIds = new HashSet<ElementId>(executable.Where(c => c.CanExecute).Select(c => c.ViewportId));
            foreach (var candidate in candidates.Where(c => c.IsSelected && c.CanExecute))
            {
                ElementId ownerId;
                if (owners.TryGetValue(DetailNumberConflictResolver.Normalize(candidate.ProposedNumber), out ownerId) &&
                    ownerId != null && ownerId != candidate.ViewportId && !changingIds.Contains(ownerId))
                {
                    candidate.CanExecute = false;
                    candidate.Status = DetailNumberStatusCode.DUPLICATE_WITH_EXISTING;
                    candidate.Message = "The proposed number is already used by another viewport on this sheet.";
                }
            }

            report.Summary.Ready = candidates.Count(c => c.IsSelected && c.CanExecute);
            report.Summary.NoMatch = candidates.Count(c => c.IsSelected && c.Status == DetailNumberStatusCode.NO_MATCH);
            report.Summary.NoChange = candidates.Count(c => c.IsSelected && c.Status == DetailNumberStatusCode.NO_CHANGE);
            report.Summary.Failed = candidates.Count(c => c.IsSelected && c.Status == DetailNumberStatusCode.FAILED);
            report.Summary.Skipped = report.Summary.Requested - report.Summary.Ready;
            report.CanApply = report.Regex.IsValid && report.Summary.Ready > 0;
            return report;
        }
    }

    public static class DetailNumberService
    {
        public const string DefaultPattern = @"([A-Za-z0-9]+-CW\d+|[A-Za-z0-9]+-W\d+|CW\d+|W\d+)";

        public static RegexValidationResult ValidateRegex(string pattern)
        {
            string value = string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern;
            try
            {
                return new RegexValidationResult { IsValid = true, Pattern = value, CompiledRegex = new Regex(value, RegexOptions.IgnoreCase) };
            }
            catch (ArgumentException ex)
            {
                return new RegexValidationResult { IsValid = false, Pattern = value, ErrorMessage = ex.Message };
            }
        }

        public static string ExtractDetailNumber(string viewName, string pattern = DefaultPattern)
        {
            RegexValidationResult validation = ValidateRegex(pattern);
            return validation.IsValid ? ExtractDetailNumber(viewName, validation) : string.Empty;
        }

        public static string ExtractDetailNumber(string viewName, RegexValidationResult validation)
        {
            if (!validation.IsValid || string.IsNullOrWhiteSpace(viewName)) return string.Empty;
            Match match = validation.CompiledRegex.Match(viewName);
            return match.Success ? match.Value.Trim() : string.Empty;
        }

        public static string GetDetailNumber(Viewport viewport)
        {
            if (viewport == null) return string.Empty;
            Parameter parameter = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            return parameter == null ? string.Empty : DetailNumberConflictResolver.Normalize(parameter.AsString());
        }

        public static List<Tuple<Viewport, View>> GetViewportOrder(Document doc, ViewSheet sheet)
        {
            var result = new List<Tuple<Viewport, View>>();
            if (doc == null || sheet == null) return result;
            foreach (ElementId viewportId in sheet.GetAllViewports())
            {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport == null) continue;
                var view = doc.GetElement(viewport.ViewId) as View;
                if (view == null) continue;
                result.Add(Tuple.Create(viewport, view));
            }
            return result
                .OrderByDescending(p => p.Item1.GetBoxCenter().Y)
                .ThenBy(p => p.Item1.GetBoxCenter().X)
                .ThenBy(p => p.Item2.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Item1.Id.IntegerValue)
                .ToList();
        }

        public static List<DetailNumberPreviewItem> GeneratePreview(Document doc, ViewSheet sheet, string pattern = DefaultPattern)
        {
            var items = new List<DetailNumberPreviewItem>();
            RegexValidationResult validation = ValidateRegex(pattern);
            var ordered = GetViewportOrder(doc, sheet);
            var existing = ordered.Select(p => GetDetailNumber(p.Item1)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var pending = new List<string>();
            foreach (Tuple<Viewport, View> pair in ordered)
            {
                string current = GetDetailNumber(pair.Item1);
                string baseNumber = validation.IsValid ? ExtractDetailNumber(pair.Item2.Name, validation) : string.Empty;
                var item = new DetailNumberPreviewItem
                {
                    Viewport = pair.Item1,
                    View = pair.Item2,
                    ViewportId = pair.Item1.Id,
                    ViewId = pair.Item2.Id,
                    SheetId = sheet == null ? null : sheet.Id,
                    ViewName = pair.Item2.Name,
                    CurrentNumber = current,
                    ExtractedBaseNumber = baseNumber,
                    ProposedNumber = current,
                    IsSelected = false,
                    IsManualOverride = false,
                    Status = validation.IsValid ? DetailNumberStatusCode.NO_MATCH : DetailNumberStatusCode.INVALID_REGEX,
                    Message = validation.IsValid ? "No detail number matched the configured pattern." : validation.ErrorMessage
                };
                if (validation.IsValid && !string.IsNullOrEmpty(baseNumber))
                {
                    item.ProposedNumber = DetailNumberConflictResolver.Resolve(baseNumber, existing, pending, current);
                    item.IsSelected = !string.Equals(item.ProposedNumber, current, StringComparison.OrdinalIgnoreCase);
                    item.Status = item.IsSelected ? DetailNumberStatusCode.READY : DetailNumberStatusCode.NO_CHANGE;
                    item.Message = item.IsSelected ? "Ready to update." : "The detail number is already correct.";
                    pending.Add(item.ProposedNumber);
                }
                Parameter parameter = pair.Item1.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (parameter == null || parameter.IsReadOnly)
                {
                    item.IsSelected = false;
                    item.Status = DetailNumberStatusCode.READ_ONLY;
                    item.Message = "Detail number parameter is unavailable or read-only.";
                }
                items.Add(item);
            }
            return items;
        }

        public static DetailNumberBatchResult Execute(Document doc, ViewSheet sheet,
            IEnumerable<DetailNumberCandidate> input, string pattern = DefaultPattern)
        {
            var result = new DetailNumberBatchResult();
            DetailNumberPreflightReport report = DetailNumberPreflightService.Preflight(doc, sheet, input, pattern);
            result.Requested = report.Summary.Requested;
            result.Ready = report.Summary.Ready;
            foreach (DetailNumberCandidate candidate in report.Candidates.Where(c => c.IsSelected && !c.CanExecute))
            {
                if (candidate.Status == DetailNumberStatusCode.NO_CHANGE)
                {
                    result.AlreadyCorrect++;
                    result.Results.Add(ToResult(candidate, false));
                }
                else
                {
                    result.Skipped++;
                    result.Results.Add(ToResult(candidate, false));
                    if (!string.IsNullOrEmpty(candidate.Message)) result.Errors.Add(candidate.ViewName + ": " + candidate.Message);
                }
            }

            List<DetailNumberCandidate> pending = report.Candidates.Where(c => c.IsSelected && c.CanExecute).ToList();
            if (pending.Count == 0)
            {
                LogSummary(sheet, report, result);
                return result;
            }

            var tempCandidates = new List<Tuple<DetailNumberCandidate, string>>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Tuple<Viewport, View> pair in GetViewportOrder(doc, sheet))
            {
                string number = GetDetailNumber(pair.Item1);
                if (!string.IsNullOrEmpty(number)) used.Add(number);
            }
            try
            {
                using (var group = new TransactionGroup(doc, "K-TOOLS Detail Number Update"))
                {
                    group.Start();
                    foreach (DetailNumberCandidate candidate in pending)
                    {
                        Viewport viewport = doc.GetElement(candidate.ViewportId) as Viewport;
                        Parameter parameter = viewport == null ? null : viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        if (parameter == null || parameter.IsReadOnly)
                        {
                            candidate.Status = DetailNumberStatusCode.READ_ONLY;
                            candidate.CanExecute = false;
                            result.Skipped++;
                            result.Results.Add(ToResult(candidate, false));
                            continue;
                        }
                        string temporary = "__KTOOLS_TMP_" + candidate.ViewportId.IntegerValue;
                        int suffix = 1;
                        while (used.Contains(temporary)) temporary = "__KTOOLS_TMP_" + candidate.ViewportId.IntegerValue + "_" + suffix++;
                        try
                        {
                            using (var tx = new Transaction(doc, "Stage temporary detail number"))
                            {
                                tx.Start();
                                parameter.Set(temporary);
                                tx.Commit();
                            }
                            used.Add(temporary);
                            tempCandidates.Add(Tuple.Create(candidate, temporary));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[K-TOOLS][DetailNumber] staging failed for " + candidate.ViewportId + ": " + ex);
                            candidate.Status = DetailNumberStatusCode.FAILED;
                            candidate.CanExecute = false;
                            result.FailedCount++;
                            result.Errors.Add(candidate.ViewName + ": " + ex.Message);
                            result.Results.Add(new DetailNumberExecutionResult
                            {
                                ViewportId = candidate.ViewportId,
                                ViewName = candidate.ViewName,
                                OldNumber = candidate.CurrentNumber,
                                NewNumber = candidate.ProposedNumber,
                                Status = DetailNumberStatusCode.FAILED,
                                Changed = false,
                                Message = ex.Message
                            });
                        }
                    }

                    foreach (Tuple<DetailNumberCandidate, string> staged in tempCandidates)
                    {
                        DetailNumberCandidate candidate = staged.Item1;
                        Viewport viewport = doc.GetElement(candidate.ViewportId) as Viewport;
                        Parameter parameter = viewport == null ? null : viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                        bool completed = false;
                        try
                        {
                            if (parameter == null || parameter.IsReadOnly) throw new InvalidOperationException("Detail number parameter became unavailable.");
                            using (var tx = new Transaction(doc, "Apply detail number"))
                            {
                                tx.Start();
                                parameter.Set(DetailNumberConflictResolver.Normalize(candidate.ProposedNumber));
                                tx.Commit();
                            }
                            completed = true;
                            result.Changed++;
                            result.Results.Add(new DetailNumberExecutionResult
                            {
                                ViewportId = candidate.ViewportId,
                                ViewName = candidate.ViewName,
                                OldNumber = candidate.CurrentNumber,
                                NewNumber = candidate.ProposedNumber,
                                Status = DetailNumberStatusCode.READY,
                                Changed = true,
                                Message = "Updated."
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[K-TOOLS][DetailNumber] apply failed for " + candidate.ViewportId + ": " + ex);
                            candidate.Status = DetailNumberStatusCode.FAILED;
                            result.FailedCount++;
                            result.Errors.Add(candidate.ViewName + ": " + ex.Message);
                            result.Results.Add(new DetailNumberExecutionResult
                            {
                                ViewportId = candidate.ViewportId,
                                ViewName = candidate.ViewName,
                                OldNumber = candidate.CurrentNumber,
                                NewNumber = candidate.ProposedNumber,
                                Status = DetailNumberStatusCode.FAILED,
                                Changed = false,
                                Message = ex.Message
                            });
                        }
                        if (!completed) RestoreNumber(doc, candidate, staged.Item2);
                    }
                    group.Assimilate();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][DetailNumber] transaction group failed: " + ex);
                result.FailedCount += pending.Count;
                result.Errors.Add(ex.Message);
            }
            LogSummary(sheet, report, result);
            return result;
        }

        public static (int Success, int Failed, List<string> Errors) ApplyDetailNumbers(Document doc, List<DetailNumberPreviewItem> selected)
        {
            ViewSheet sheet = null;
            if (selected != null && selected.Count > 0 && selected[0].SheetId != null) sheet = doc.GetElement(selected[0].SheetId) as ViewSheet;
            DetailNumberBatchResult result = Execute(doc, sheet, selected, DefaultPattern);
            return (result.Changed, result.FailedCount, result.Errors);
        }

        private static void RestoreNumber(Document doc, DetailNumberCandidate candidate, string temporary)
        {
            try
            {
                Viewport viewport = doc.GetElement(candidate.ViewportId) as Viewport;
                Parameter parameter = viewport == null ? null : viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (parameter == null || parameter.IsReadOnly) return;
                using (var tx = new Transaction(doc, "Restore detail number after failure"))
                {
                    tx.Start();
                    parameter.Set(DetailNumberConflictResolver.Normalize(candidate.CurrentNumber));
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][DetailNumber] restore failed for " + candidate.ViewportId + ": " + ex);
            }
        }

        private static DetailNumberExecutionResult ToResult(DetailNumberCandidate candidate, bool changed)
        {
            return new DetailNumberExecutionResult
            {
                ViewportId = candidate.ViewportId,
                ViewName = candidate.ViewName,
                OldNumber = candidate.CurrentNumber,
                NewNumber = candidate.ProposedNumber,
                Status = candidate.Status,
                Changed = changed,
                Message = candidate.Message
            };
        }

        private static void LogSummary(ViewSheet sheet, DetailNumberPreflightReport report, DetailNumberBatchResult result)
        {
            Debug.WriteLine(string.Format("[K-TOOLS][DetailNumber] sheet={0} regexValid={1} candidates={2} ready={3} changed={4} skipped={5} failed={6}",
                sheet == null ? "<none>" : sheet.SheetNumber, report.Regex.IsValid, report.Candidates.Count,
                report.Summary.Ready, result.Changed, result.Skipped, result.FailedCount));
        }
    }
}
