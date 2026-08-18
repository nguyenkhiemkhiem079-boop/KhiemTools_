using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Models
{
    public class SlabRebarSettings
    {
        public string TemplateName { get; set; } = "Mặc định Sàn 2 Lớp (150mm)";

        // ── 1. Bottom Mat (Lớp Dưới) ─────────────────────────────────────────
        public string BotXDiaLabel { get; set; } = "d10";
        public double BotXSpacingMm { get; set; } = 150;
        public string BotYDiaLabel { get; set; } = "d10";
        public double BotYSpacingMm { get; set; } = 150;
        public bool BotAnchorHooks { get; set; } = true;
        public double BotHookTailD { get; set; } = 12;

        // ── 2. Top Support Hats (Lớp Trên / Mũ Gối) ──────────────────────────
        public string TopXDiaLabel { get; set; } = "d10";
        public double TopXSpacingMm { get; set; } = 150;
        public string TopYDiaLabel { get; set; } = "d10";
        public double TopYSpacingMm { get; set; } = 150;
        public string TopExtensionRatio { get; set; } = "L/4"; // L/4 hoặc L/3
        public bool TopHookDown { get; set; } = true;
        public double TopHookTailMm { get; set; } = 100;

        // ── 3. Chair Rebar (Thép Chân Chó) ──────────────────────────────────
        public bool EnableChairRebar { get; set; } = true;
        public string ChairDiaLabel { get; set; } = "d10";
        public double ChairSpacingXmm { get; set; } = 800;
        public double ChairSpacingYmm { get; set; } = 800;

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
                string filePath = Path.Combine(GetTemplateDirectory(), $"{settings.TemplateName}.json");
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch { return false; }
        }

        public static SlabRebarSettings LoadTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return null;
            try
            {
                string filePath = Path.Combine(GetTemplateDirectory(), $"{templateName.Trim()}.json");
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<SlabRebarSettings>(json);
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
    }
}
