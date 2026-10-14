using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.Core.Settings;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoRuleProfileService
    {
        public static QtoRuleProfile Load(string documentTitle)
        {
            string path = GetRulesPath(documentTitle);
            QtoRuleProfile profile = JsonSettingsPersistence.Load(path, CreateDefault, IsValid, value =>
            {
                if (value.SchemaVersion != 0) return false;
                value.SchemaVersion = 1;
                value.Rules = value.Rules ?? new List<QtoMeasurementRule>();
                MergeMissingDefaults(value);
                return true;
            });
            MergeMissingDefaults(profile);
            return profile;
        }

        public static void Save(string documentTitle, QtoRuleProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string path = GetRulesPath(documentTitle);
            profile.SchemaVersion = 1;
            if (!IsValid(profile)) throw new InvalidDataException("QTO rule profile is invalid.");
            int priorVersion = profile.Version;
            profile.Version = Math.Max(1, profile.Version + 1);
            try
            {
                JsonSettingsPersistence.Save(path, profile, IsValid);
            }
            catch
            {
                profile.Version = priorVersion;
                throw;
            }
        }

        public static string GetProjectFolder(string documentTitle)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KhimTools", "QTO");
            return Path.Combine(root, SafeName(documentTitle));
        }

        private static string GetRulesPath(string documentTitle) => Path.Combine(GetProjectFolder(documentTitle), "rules.json");

        private static QtoRuleProfile CreateDefault()
        {
            var profile = new QtoRuleProfile { SchemaVersion = 1 };
            profile.Rules.AddRange(DefaultRules());
            return profile;
        }

        private static void MergeMissingDefaults(QtoRuleProfile profile)
        {
            profile.Rules = profile.Rules ?? new List<QtoMeasurementRule>();
            foreach (QtoMeasurementRule rule in DefaultRules())
                if (!profile.Rules.Any(x => string.Equals(x.Code, rule.Code, StringComparison.OrdinalIgnoreCase)))
                    profile.Rules.Add(rule);
        }

        private static bool IsValid(QtoRuleProfile profile)
        {
            return profile != null && profile.SchemaVersion == 1 && profile.Version >= 1 &&
                !string.IsNullOrWhiteSpace(profile.ProfileId) && profile.ProfileId.Length <= 200 &&
                !string.IsNullOrWhiteSpace(profile.Name) && profile.Name.Length <= 200 && profile.Rules != null &&
                profile.Rules.Count <= 1000 && profile.Rules.TrueForAll(rule => rule != null &&
                    !string.IsNullOrWhiteSpace(rule.Code) && rule.Code.Length <= 120 &&
                    !double.IsNaN(rule.WastePercent) && !double.IsInfinity(rule.WastePercent) &&
                    rule.WastePercent >= 0 && rule.WastePercent <= 10000 && rule.RoundingDigits >= 0 && rule.RoundingDigits <= 8);
        }

        private static IEnumerable<QtoMeasurementRule> DefaultRules()
        {
            yield return Rule("QS-CONCRETE", "Bê tông", 3);
            yield return Rule("QS-REBAR", "Cốt thép", 2);
            yield return Rule("QS-STEEL", "Thép kết cấu", 2);
            yield return Rule("QS-MASONRY", "Tường xây", 3);
            yield return Rule("QS-PAINT", "Sơn hoàn thiện", 2);
            yield return Rule("QS-PLASTER", "Vữa trát", 2);
            yield return Rule("QS-FINISH", "Hoàn thiện", 2);
            yield return Rule("QS-DOOR", "Cửa đi", 0);
            yield return Rule("QS-WINDOW", "Cửa sổ", 0);
            yield return Rule("QS-MECH-EQUIPMENT", "Thiết bị cơ khí", 0);
            yield return Rule("QS-ELEC-EQUIPMENT", "Thiết bị điện", 0);
            yield return Rule("QS-PLUMBING", "Thiết bị vệ sinh", 0);
            yield return Rule("QS-PIPE", "Ống nước", 2);
            yield return Rule("QS-DUCT", "Ống gió", 2);
            yield return Rule("QS-CABLETRAY", "Máng cáp", 2);
            yield return Rule("QS-CONDUIT", "Ống luồn dây", 2);
            yield return Rule("QS-ROOM", "Diện tích phòng", 2);
        }

        private static QtoMeasurementRule Rule(string code, string description, int digits) =>
            new QtoMeasurementRule { Code = code, Description = description, RoundingDigits = digits };

        private static string SafeName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "Untitled" : value;
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Length <= 80 ? value : value.Substring(0, 80);
        }
    }
}
