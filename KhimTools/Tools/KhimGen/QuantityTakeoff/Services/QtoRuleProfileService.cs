using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.Core.Logging;
using Newtonsoft.Json;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoRuleProfileService
    {
        public static QtoRuleProfile Load(string documentTitle)
        {
            string path = GetRulesPath(documentTitle);
            QtoRuleProfile profile = null;
            try
            {
                if (File.Exists(path))
                    profile = JsonConvert.DeserializeObject<QtoRuleProfile>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                KToolsLog.Current.Exception("QTO.RuleProfile.Load", ex, "RULE_PROFILE_LOAD");
            }

            profile = profile ?? CreateDefault();
            MergeMissingDefaults(profile);
            return profile;
        }

        public static void Save(string documentTitle, QtoRuleProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string path = GetRulesPath(documentTitle);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            profile.Version = Math.Max(1, profile.Version + 1);
            File.WriteAllText(path, JsonConvert.SerializeObject(profile, Formatting.Indented));
        }

        public static string GetProjectFolder(string documentTitle)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KhimTools", "QTO");
            return Path.Combine(root, SafeName(documentTitle));
        }

        private static string GetRulesPath(string documentTitle) => Path.Combine(GetProjectFolder(documentTitle), "rules.json");

        private static QtoRuleProfile CreateDefault()
        {
            var profile = new QtoRuleProfile();
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
