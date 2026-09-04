using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

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
    }

    public class OverrideColorSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools", "override_colors.json");

        public List<OverrideColorPreset> Presets { get; set; } = DefaultPresets();

        public static OverrideColorSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonConvert.DeserializeObject<OverrideColorSettings>(json);
                    if (loaded?.Presets != null && loaded.Presets.Count >= 12)
                        return loaded;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K-TOOLS OverrideColorSettings] Lỗi đọc cấu hình màu từ '{SettingsPath}': {ex.Message}");
            }
            return new OverrideColorSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K-TOOLS OverrideColorSettings] Lỗi lưu cấu hình màu vào '{SettingsPath}': {ex.Message}");
            }
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
            };
        }
    }
}