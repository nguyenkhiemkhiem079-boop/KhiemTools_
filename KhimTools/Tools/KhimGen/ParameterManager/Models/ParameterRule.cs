using System;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterRule
    {
        public ParameterRuleType RuleType { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Find { get; set; } = string.Empty;
        public string Replace { get; set; } = string.Empty;
        public string Prefix { get { return Value; } set { Value = value ?? string.Empty; } }
        public string Suffix { get { return Value; } set { Value = value ?? string.Empty; } }
        public bool OnlyIfMissing { get; set; }
        public bool CaseSensitive { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; } = -1;
        public double NumericStart { get; set; }
        public double NumericStep { get; set; } = 1d;
        public int Padding { get; set; }
        public ParameterSortMode SortMode { get; set; } = ParameterSortMode.ELEMENT_ID;
        public string CultureName { get; set; } = string.Empty;
        public bool IsValid(out string error)
        {
            error = string.Empty;
            if ((RuleType == ParameterRuleType.SUBSTRING_START_LENGTH || RuleType == ParameterRuleType.SUBSTRING_REMOVE_START_LENGTH) && (StartIndex < 0 || Length < -1)) { error = "INVALID_RULE_ARGUMENT"; return false; }
            if ((RuleType == ParameterRuleType.FIND_REPLACE && Find == null) || (RuleType == ParameterRuleType.REGEX_REPLACE && Find == null)) { error = "INVALID_RULE_ARGUMENT"; return false; }
            return true;
        }
        public override string ToString() { return RuleType + (string.IsNullOrEmpty(Value) ? string.Empty : " = " + Value); }
    }
}
