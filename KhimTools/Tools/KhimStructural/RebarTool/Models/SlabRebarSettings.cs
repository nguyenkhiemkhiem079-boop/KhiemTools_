using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KhimTools.Core.Settings;

namespace KhimTools.RebarTool.Models
{
    public class SlabRebarSettings
    {
        public int SchemaVersion { get; set; }
        public string TemplateName { get; set; } = "Mặc định Sàn 2 Lớp (150mm)";

        // ── 1. Bottom Mat (Lớp Dưới) ─────────────────────────────────────────
        public string BotXDiaLabel { get; set; } = "d10";
        public bool BottomMeshEnabled { get; set; } = true;
        public bool BottomInvertLayer { get; set; }
        public double BotXSpacingMm { get; set; } = 150;
        public string BotYDiaLabel { get; set; } = "d10";
        public double BotYSpacingMm { get; set; } = 150;
        public bool TopMeshEnabled { get; set; }
        public bool TopInvertLayer { get; set; }
        public string TopMeshXDiaLabel { get; set; } = "d10";
        public double TopMeshXSpacingMm { get; set; } = 150;
        public string TopMeshYDiaLabel { get; set; } = "d10";
        public double TopMeshYSpacingMm { get; set; } = 150;
        public bool BotAnchorHooks { get; set; } = true;
        public double BotHookTailD { get; set; } = 12;

        // ── 2. Top Support Hats (Lớp Trên / Mũ Gối) ──────────────────────────
        public string TopXDiaLabel { get; set; } = "d10";
        public double TopXSpacingMm { get; set; } = 150;
        public string TopYDiaLabel { get; set; } = "d10";
        public double TopYSpacingMm { get; set; } = 150;
        public string TopExtensionRatio { get; set; } = "L/4"; // L/4 hoặc L/3
        public bool SupportEnabled { get; set; } = true;
        public bool SupportFullSpan { get; set; }
        public bool TopHookDown { get; set; } = true;
        public double TopHookTailMm { get; set; } = 100;

        // ── 3. Chair Rebar (Thép Chân Chó) ──────────────────────────────────
        public bool EnableChairRebar { get; set; } = true;
        public string ChairDiaLabel { get; set; } = "d10";
        public double ChairSpacingXmm { get; set; } = 800;
        public double ChairSpacingYmm { get; set; } = 800;
        public double ChairHookLenMm { get; set; } = 100;

        // ── 4. Opening Trim Bars (Gia Cường Lỗ Mở) ─────────────────────────
        public bool EnableOpeningTrimBars { get; set; } = true;
        public string OpeningTrimDiaLabel { get; set; } = "d12";
        public int OpeningTrimBarQty { get; set; } = 2;
        public bool IncludeDiagonalCornerBars { get; set; } = true;

        // ── 5. Design Standard & Materials ──────────────────────────────────
        public string DesignCode { get; set; } = "TCVN 5574:2018"; // TCVN 5574:2018 hoặc Eurocode 2
        public string ConcreteGrade { get; set; } = "B25";
        public string SteelGrade { get; set; } = "CB300-V";
        public double CustomLdMultiplier { get; set; } = 35;

        public KhimTools.RebarTool.Core.IRebarDesignStandard GetDesignStandard()
        {
            return KhimTools.RebarTool.Core.RebarDesignStandardFactory.Create(DesignCode);
        }

        // ── Template JSON Persistence Helper ─────────────────────────────────
        private static string GetTemplateDirectory()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KhimTools",
                "RebarTemplates",
                "Slab"
            );
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static bool SaveTemplate(SlabRebarSettings settings, string templateName)
        {
            if (settings == null || string.IsNullOrWhiteSpace(templateName)) return false;
            try
            {
                settings.TemplateName = templateName.Trim();
                settings.SchemaVersion = 1;
                if (!IsValid(settings)) return false;
                string filePath = GetTemplatePath(templateName);
                JsonSettingsPersistence.Save(filePath, settings, IsValid);
                return true;
            }
            catch { return false; }
        }

        public static SlabRebarSettings LoadTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return null;
            try
            {
                string filePath = GetTemplatePath(templateName);
                if (!File.Exists(filePath)) return null;
                return JsonSettingsPersistence.Load<SlabRebarSettings>(filePath, () => null, IsValid,
                    value => { if (value.SchemaVersion != 0) return false; value.SchemaVersion = 1; return true; });
            }
            catch { return null; }
        }

        public static List<string> GetSavedTemplateNames()
        {
            var list = new List<string>();
            try
            {
                string dir = GetTemplateDirectory();
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    list.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch { }

            if (list.Count == 0)
            {
                var defaultSetting = new SlabRebarSettings();
                SaveTemplate(defaultSetting, defaultSetting.TemplateName);
                list.Add(defaultSetting.TemplateName);
            }
            return list;
        }

        private static string GetTemplatePath(string name)
        {
            string safe = string.Concat(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if (string.IsNullOrWhiteSpace(safe) || safe == "." || safe == "..") throw new InvalidDataException("Invalid template name.");
            return Path.Combine(GetTemplateDirectory(), safe + ".json");
        }

        private static bool IsValid(SlabRebarSettings settings)
        {
            if (settings == null || settings.SchemaVersion != 1 || string.IsNullOrWhiteSpace(settings.TemplateName) ||
                settings.TemplateName.Length > 100 || (settings.DesignCode != "TCVN 5574:2018" && settings.DesignCode != "Eurocode 2")) return false;
            if (settings.BotXSpacingMm < 50 || settings.BotXSpacingMm > 500 || settings.BotYSpacingMm < 50 || settings.BotYSpacingMm > 500 ||
                settings.TopMeshXSpacingMm < 50 || settings.TopMeshXSpacingMm > 500 || settings.TopMeshYSpacingMm < 50 || settings.TopMeshYSpacingMm > 500 ||
                settings.TopXSpacingMm < 50 || settings.TopXSpacingMm > 500 || settings.TopYSpacingMm < 50 || settings.TopYSpacingMm > 500 ||
                settings.ChairSpacingXmm < 300 || settings.ChairSpacingXmm > 2000 || settings.ChairSpacingYmm < 300 || settings.ChairSpacingYmm > 2000 ||
                settings.ChairHookLenMm < 50 || settings.ChairHookLenMm > 300 ||
                (settings.TopExtensionRatio != "L/3" && settings.TopExtensionRatio != "L/4" && settings.TopExtensionRatio != "L/5")) return false;
            if ((settings.BottomMeshEnabled && (string.IsNullOrWhiteSpace(settings.BotXDiaLabel) || string.IsNullOrWhiteSpace(settings.BotYDiaLabel))) ||
                (settings.TopMeshEnabled && (string.IsNullOrWhiteSpace(settings.TopMeshXDiaLabel) || string.IsNullOrWhiteSpace(settings.TopMeshYDiaLabel))) ||
                (settings.SupportEnabled && (string.IsNullOrWhiteSpace(settings.TopXDiaLabel) || string.IsNullOrWhiteSpace(settings.TopYDiaLabel))) ||
                (settings.EnableChairRebar && string.IsNullOrWhiteSpace(settings.ChairDiaLabel))) return false;
            foreach (PropertyInfo property in settings.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(double))
                {
                    double value = (double)property.GetValue(settings, null);
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1000000) return false;
                }
                if (property.PropertyType == typeof(int))
                {
                    int value = (int)property.GetValue(settings, null);
                    if (value < 0 || value > 1000000) return false;
                }
            }
            return true;
        }
    }
}
