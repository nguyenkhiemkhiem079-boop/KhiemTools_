using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsBoqService
    {
        public static QsClassificationResult Classify(QtoLine item, IEnumerable<QsBoqRule> rules, ISet<string> activeCodes = null)
        {
            var matches = (rules ?? Enumerable.Empty<QsBoqRule>()).Where(r => r.Enabled && (activeCodes == null || activeCodes.Contains(r.TargetBoqCode)) && Match(r, item)).OrderBy(r => r.Priority).ThenByDescending(Specificity).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
            if (matches.Count == 0) return new QsClassificationResult { Status = QsClassificationStatus.Unclassified, Message = "No matching BOQ rule" };
            if (matches.Count > 1 && matches[0].Priority == matches[1].Priority && Specificity(matches[0]) == Specificity(matches[1])) return new QsClassificationResult { Status = QsClassificationStatus.Ambiguous, Message = "AMBIGUOUS CLASSIFICATION" };
            return new QsClassificationResult { Status = QsClassificationStatus.Classified, BoqCode = matches[0].TargetBoqCode, Message = matches[0].Id };
        }
        private static int Specificity(QsBoqRule r) => new[] { r.Category, r.FamilyPattern, r.TypePattern, r.MaterialPattern, r.MeasurementCode }.Count(x => !string.IsNullOrWhiteSpace(x));
        private static bool Match(QsBoqRule r, QtoLine x) => MatchText(r.Category, x.Category) && MatchText(r.FamilyPattern, x.TypeName) && MatchText(r.TypePattern, x.TypeName) && MatchText(r.MaterialPattern, x.Material) && MatchText(r.MeasurementCode, x.Code);
        private static bool MatchText(string pattern, string value) { if (string.IsNullOrWhiteSpace(pattern)) return true; value ??= ""; if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)) return Regex.IsMatch(value, pattern.Substring(6), RegexOptions.IgnoreCase); return value.Equals(pattern, StringComparison.OrdinalIgnoreCase) || value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0; }
    }
}
