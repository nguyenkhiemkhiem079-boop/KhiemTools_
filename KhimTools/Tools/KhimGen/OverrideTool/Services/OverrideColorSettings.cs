using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using KhimTools.Core.Settings;

namespace KhimTools.OverrideTool.Services
{
    public class OverrideColorPreset
    {
        public string Name { get; set; }
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }

        [JsonIgnore]
        public Color DrawingColor => Color.FromArgb(R, G, B);

        [JsonIgnore]
        public string HexColor => $"#{R:X2}{G:X2}{B:X2}";
    }

    public class OverrideColorSettings
    {
        public int SchemaVersion { get; set; }
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools", "override_colors.json");

        public List<OverrideColorPreset> Presets { get; set; } = DefaultPresets();

        public static OverrideColorSettings Load()
        {
            return JsonSettingsPersistence.Load(SettingsPath,
                () => new OverrideColorSettings { SchemaVersion = 1 }, IsValid,
                settings =>
                {
                    if (settings.SchemaVersion != 0 || settings.Presets == null) return false;
                    Normalize(settings);
                    settings.SchemaVersion = 1;
                    return true;
                });
        }

        public void Save()
        {
            try
            {
                SchemaVersion = 1;
                Normalize(this);
                JsonSettingsPersistence.Save(SettingsPath, this, IsValid);
            }
            catch { }
        }

        private static void Normalize(OverrideColorSettings settings)
        {
            var normalized = DefaultPresets();
            for (int i = 0; i < Math.Min(16, settings.Presets.Count); i++)
            {
                var preset = settings.Presets[i];
                if (preset != null && preset.R >= 0 && preset.R <= 255 &&
                    preset.G >= 0 && preset.G <= 255 && preset.B >= 0 && preset.B <= 255)
                    normalized[i] = preset;
            }
            settings.Presets = normalized;
        }

        private static bool IsValid(OverrideColorSettings settings)
        {
            if (settings == null || settings.SchemaVersion != 1 || settings.Presets == null || settings.Presets.Count != 16) return false;
            return settings.Presets.TrueForAll(p => p != null && !string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 100 &&
                p.R >= 0 && p.R <= 255 && p.G >= 0 && p.G <= 255 && p.B >= 0 && p.B <= 255);
        }

        public static List<OverrideColorPreset> DefaultPresets()
        {
            return new List<OverrideColorPreset>
            {
                new OverrideColorPreset { Name = "Đỏ",         R = 220, G = 20,  B = 20  },
                new OverrideColorPreset { Name = "Xanh Lá",    R = 34,  G = 180, B = 34  },
                new OverrideColorPreset { Name = "Ngọc",       R = 0,   G = 190, B = 190 },
                new OverrideColorPreset { Name = "Xám Nhạt",   R = 180, G = 180, B = 180 },
                new OverrideColorPreset { Name = "Cam",        R = 255, G = 140, B = 0   },
                new OverrideColorPreset { Name = "Teal",       R = 0,   G = 128, B = 128 },
                new OverrideColorPreset { Name = "Xám Đậm",    R = 80,  G = 80,  B = 80  },
                new OverrideColorPreset { Name = "Xanh Dương", R = 30,  G = 100, B = 220 },
                new OverrideColorPreset { Name = "Vàng",       R = 240, G = 210, B = 0   },
                new OverrideColorPreset { Name = "Tím",        R = 130, G = 0,   B = 200 },
                new OverrideColorPreset { Name = "Hồng",       R = 240, G = 90,  B = 160 },
                new OverrideColorPreset { Name = "Nâu",        R = 140, G = 80,  B = 20  },
                new OverrideColorPreset { Name = "Xanh Navy",  R = 30,  G = 58,  B = 138 },
                new OverrideColorPreset { Name = "Xanh Lime",  R = 132, G = 204, B = 22  },
                new OverrideColorPreset { Name = "Tím Indigo", R = 79,  G = 70,  B = 229 },
                new OverrideColorPreset { Name = "Đen",        R = 24,  G = 24,  B = 27  },
            };
        }
    }
}
