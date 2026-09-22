using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterRuleEngine
    {
        public static bool TryApply(ParameterValueSnapshot current, IEnumerable<ParameterRule> rules, ParameterManagerOptions options, out ParameterValueSnapshot proposed, out ParameterManagerStatus status, out string error)
        {
            proposed = ParameterValueParser.Clone(current); status = ParameterManagerStatus.READY; error = string.Empty; if (current == null) { status = ParameterManagerStatus.PARAMETER_MISSING; error = "Parameter is missing."; return false; }
            foreach (ParameterRule rule in (rules ?? Enumerable.Empty<ParameterRule>()))
            {
                if (rule == null) continue;
                if (!Enum.IsDefined(typeof(ParameterRuleType), rule.RuleType)) { status = ParameterManagerStatus.INVALID_RULE; error = "Unknown rule type."; return false; }
                if (!rule.IsValid(out error)) { status = ParameterManagerStatus.INVALID_RULE_ARGUMENT; return false; }
                if (rule.RuleType == ParameterRuleType.CLEAR_VALUE) { proposed = ParameterValueParser.Clear(proposed); continue; }
                if (proposed.StorageType == Autodesk.Revit.DB.StorageType.ElementId) { status = ParameterManagerStatus.UNSAFE_ELEMENT_REFERENCE; error = "ElementId writes are blocked by default."; return false; }
                if (proposed.StorageType == Autodesk.Revit.DB.StorageType.String)
                {
                    string value = proposed.StringValue ?? string.Empty;
                    switch (rule.RuleType)
                    {
                        case ParameterRuleType.SET_VALUE: value = rule.Value ?? string.Empty; break;
                        case ParameterRuleType.PREFIX: if (!rule.OnlyIfMissing && !(options != null && options.PrefixOnlyIfMissing) || !value.StartsWith(rule.Value ?? string.Empty, StringComparison.Ordinal)) value = (rule.Value ?? string.Empty) + value; break;
                        case ParameterRuleType.SUFFIX: if (!rule.OnlyIfMissing && !(options != null && options.SuffixOnlyIfMissing) || !value.EndsWith(rule.Value ?? string.Empty, StringComparison.Ordinal)) value = value + (rule.Value ?? string.Empty); break;
                        case ParameterRuleType.FIND_REPLACE: value = Replace(value, rule.Find ?? string.Empty, rule.Replace ?? string.Empty, rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase); break;
                        case ParameterRuleType.TRIM: value = value.Trim(); break;
                        case ParameterRuleType.UPPERCASE: value = value.ToUpper((options == null || options.Culture == null) ? CultureInfo.InvariantCulture : options.Culture); break;
                        case ParameterRuleType.LOWERCASE: value = value.ToLower((options == null || options.Culture == null) ? CultureInfo.InvariantCulture : options.Culture); break;
                        case ParameterRuleType.TITLE_CASE: value = (options == null || options.Culture == null ? CultureInfo.InvariantCulture : options.Culture).TextInfo.ToTitleCase(value); break;
                        case ParameterRuleType.REMOVE_PREFIX: if (value.StartsWith(rule.Value ?? string.Empty, rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) value = value.Substring((rule.Value ?? string.Empty).Length); break;
                        case ParameterRuleType.REMOVE_SUFFIX: if (value.EndsWith(rule.Value ?? string.Empty, rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - (rule.Value ?? string.Empty).Length); break;
                        case ParameterRuleType.SUBSTRING_START_LENGTH: if (rule.StartIndex > value.Length || rule.Length < -1) { status = ParameterManagerStatus.RULE_NOT_APPLICABLE; error = "Substring range is outside the current value."; return false; } value = value.Substring(rule.StartIndex, rule.Length < 0 ? value.Length - rule.StartIndex : Math.Min(rule.Length, value.Length - rule.StartIndex)); break;
                        case ParameterRuleType.SUBSTRING_REMOVE_START_LENGTH: if (rule.StartIndex > value.Length) { status = ParameterManagerStatus.RULE_NOT_APPLICABLE; error = "Substring range is outside the current value."; return false; } int remove = rule.Length < 0 ? value.Length - rule.StartIndex : Math.Min(rule.Length, value.Length - rule.StartIndex); value = value.Remove(rule.StartIndex, remove); break;
                        case ParameterRuleType.REGEX_REPLACE: try { value = Regex.Replace(value, rule.Find ?? string.Empty, rule.Replace ?? string.Empty, rule.CaseSensitive ? RegexOptions.CultureInvariant : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); } catch (ArgumentException ex) { status = ParameterManagerStatus.INVALID_REGEX; error = ex.Message; return false; } break;
                    }
                    proposed.StringValue = value; proposed.DisplayValue = value; proposed.IsBlank = string.IsNullOrEmpty(value);
                }
                else if (proposed.StorageType == Autodesk.Revit.DB.StorageType.Integer || proposed.StorageType == Autodesk.Revit.DB.StorageType.Double)
                {
                    if (proposed.StorageType == Autodesk.Revit.DB.StorageType.Integer && ParameterValueParser.IsYesNo(proposed) && rule.RuleType != ParameterRuleType.SET_VALUE && rule.RuleType != ParameterRuleType.CLEAR_VALUE) { status = ParameterManagerStatus.RULE_NOT_APPLICABLE; error = "Yes/No parameters do not support arithmetic."; return false; }
                    double number = proposed.StorageType == Autodesk.Revit.DB.StorageType.Integer ? proposed.IntegerValue : proposed.DoubleValue;
                    if (rule.RuleType == ParameterRuleType.SET_VALUE) { if (!ParameterValueParser.TryParse(proposed, rule.Value, options, out proposed, out status, out error)) return false; }
                    else if (rule.RuleType == ParameterRuleType.NUMERIC_ADD) number += rule.NumericStart;
                    else if (rule.RuleType == ParameterRuleType.NUMERIC_SUBTRACT) number -= rule.NumericStart;
                    else if (rule.RuleType == ParameterRuleType.NUMERIC_MULTIPLY) number *= (rule.NumericStart == 0d ? 1d : rule.NumericStart);
                    else if (rule.RuleType == ParameterRuleType.NUMERIC_DIVIDE) { if (Math.Abs(rule.NumericStart) <= 1e-12) { status = ParameterManagerStatus.INVALID_RULE_ARGUMENT; error = "Cannot divide by zero."; return false; } number /= rule.NumericStart; }
                    else if (rule.RuleType == ParameterRuleType.NUMERIC_INCREMENT) number = rule.NumericStart + rule.NumericStep;
                    else { status = ParameterManagerStatus.RULE_NOT_APPLICABLE; error = "String rule cannot be applied to a numeric parameter."; return false; }
                    if (proposed.StorageType == Autodesk.Revit.DB.StorageType.Integer) { proposed.IntegerValue = Convert.ToInt32(number); proposed.DisplayValue = proposed.IntegerValue.ToString(CultureInfo.InvariantCulture); } else { proposed.DoubleValue = number; proposed.DisplayValue = number.ToString("R", CultureInfo.InvariantCulture); }
                }
            }
            return true;
        }

        public static bool TryCompileRegex(ParameterRule rule, out string error) { error = string.Empty; try { Regex re = new Regex(rule == null ? string.Empty : rule.Find ?? string.Empty); return true; } catch (ArgumentException ex) { error = ex.Message; return false; } }
        private static string Replace(string input, string find, string replace, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(find)) return input;
            int start = 0; int index; var builder = new System.Text.StringBuilder(); while ((index = input.IndexOf(find, start, comparison)) >= 0) { builder.Append(input, start, index - start); builder.Append(replace ?? string.Empty); start = index + find.Length; } builder.Append(input, start, input.Length - start); return builder.ToString();
        }
    }
}
