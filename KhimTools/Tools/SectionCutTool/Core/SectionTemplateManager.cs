using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.SectionCutTool.Models;
using Newtonsoft.Json;

namespace KhimTools.SectionCutTool.Core
{
    /// <summary>
    /// Quản lý lưu, tải, xóa Template JSON cho cấu hình Section Cut trong thư mục AppData.
    /// </summary>
    public static class SectionTemplateManager
    {
        private static readonly string TemplateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools", "SectionTemplates");

        public static void SaveTemplate(SectionCutSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.Name)) return;

            Directory.CreateDirectory(TemplateDir);
            string safeName = SanitizeFileName(settings.Name.Trim());
            string filePath = Path.Combine(TemplateDir, safeName + ".json");
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static SectionCutSettings LoadTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string safeName = SanitizeFileName(name.Trim());
            string filePath = Path.Combine(TemplateDir, safeName + ".json");
            if (!File.Exists(filePath)) return null;

            string json = File.ReadAllText(filePath);
            var jsonSettings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            return JsonConvert.DeserializeObject<SectionCutSettings>(json, jsonSettings);
        }

        public static void DeleteTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            string safeName = SanitizeFileName(name.Trim());
            string filePath = Path.Combine(TemplateDir, safeName + ".json");
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public static List<string> ListTemplates()
        {
            if (!Directory.Exists(TemplateDir)) return new List<string>();

            return Directory.GetFiles(TemplateDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToList();
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c));
        }
    }
}
