using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Models
{
    /// <summary>
    /// Model cấu hình thông số bố trí thép Móng (Structural Foundations).
    /// </summary>
    public class FoundationRebarSettings
    {
        public string TemplateName { get; set; } = "Mặc định Móng Đơn (800x800x600)";

        // ── 1. Lớp Thép Dưới (Bottom Mesh X & Y) ────────────────────────────
        public string BotXDiaLabel { get; set; } = "d14";
        public double BotXSpacingMm { get; set; } = 150;
        public bool BotXHookUp { get; set; } = true;

        public string BotYDiaLabel { get; set; } = "d14";
        public double BotYSpacingMm { get; set; } = 150;
        public bool BotYHookUp { get; set; } = true;

        // ── 2. Lớp Thép Trên (Top Mesh X & Y - Dành cho đài móng / móng sâu) 
        public bool EnableTopMesh { get; set; } = false;
        public string TopXDiaLabel { get; set; } = "d12";
        public double TopXSpacingMm { get; set; } = 200;
        public bool TopXHookDown { get; set; } = true;

        public string TopYDiaLabel { get; set; } = "d12";
        public double TopYSpacingMm { get; set; } = 200;
        public bool TopYHookDown { get; set; } = true;

        // ── 3. Thép Đai Mép Móng / Thép Chữ U Gia Cường (Side Ties & Perimeter Edge U-Bars) ────────
        public bool EnableSideTies { get; set; } = true;
        public string SideTieDiaLabel { get; set; } = "d10";
        public double SideTieSpacingMm { get; set; } = 200;

        public bool EnablePerimeterUStirrups { get; set; } = true;
        public string PerimeterStirrupDiaLabel { get; set; } = "d10";
        public double PerimeterStirrupSpacingMm { get; set; } = 200;

        // ── 4. Thép Chờ Cột (Column Dowels / Starter Bars) & Đai Cổ Móng ──────
        public bool EnableColumnDowels { get; set; } = true;
        public string DowelDiaLabel { get; set; } = "d18";
        public int DowelQtyX { get; set; } = 2;
        public int DowelQtyY { get; set; } = 2;
        public double DowelFootLegMm { get; set; } = 300; // Chân quỳ uốn 90° đáy móng
        public double DowelExtensionMm { get; set; } = 600; // Đoạn chờ nhô lên trên mặt móng (L0)
        public bool DowelLegInward { get; set; } = false; // false = Xòe ra ngoài (Outward), true = Úp vào trong (Inward)
        public bool StaggeredDowels { get; set; } = true; // Nối so le 50% thép chờ

        public bool EnableDowelStirrups { get; set; } = true;
        public string DowelStirrupDiaLabel { get; set; } = "d10";
        public int DowelStirrupQty { get; set; } = 3; // Đai lồng cố định chân cột nằm trong lòng móng

        // ── 5. Tiêu Chuẩn Thiết Kế & Vật Liệu ──────────────────────────────
        public string DesignCode { get; set; } = "TCVN 5574:2018"; // TCVN 5574:2018 hoặc Eurocode 2
        public string ConcreteGrade { get; set; } = "B25";
        public string SteelGrade { get; set; } = "CB400-V";
        public double CustomCoverMm { get; set; } = 50; // 50mm cover mặc định cho móng
        public double CustomHookHeightMm { get; set; } = 0; // 0 = Tự động (H_fdn - 2*Cover)

        public IRebarDesignStandard GetDesignStandard()
        {
            return RebarDesignStandardFactory.Create(DesignCode);
        }

        // ── Template JSON Persistence Helper ─────────────────────────────────
        private static string GetTemplateDirectory()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KhimTools",
                "RebarTemplates",
                "Foundation"
            );
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static bool SaveTemplate(FoundationRebarSettings settings, string templateName)
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

        public static FoundationRebarSettings LoadTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return null;
            try
            {
                string filePath = Path.Combine(GetTemplateDirectory(), $"{templateName.Trim()}.json");
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<FoundationRebarSettings>(json);
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
                var defaultSetting = new FoundationRebarSettings();
                SaveTemplate(defaultSetting, defaultSetting.TemplateName);
                list.Add(defaultSetting.TemplateName);
            }
            return list;
        }
    }
}
