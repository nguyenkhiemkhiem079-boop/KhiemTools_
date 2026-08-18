using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Models;
using Newtonsoft.Json;

namespace KhimTools.SlabJoin.Services
{
    /// <summary>
    /// Quản lý Save/Load/Delete template cấu hình Join Elements.
    /// Lưu file JSON trong %AppData%/KhimTools/JoinTemplates/
    /// </summary>
    public static class JoinTemplateManager
    {
        private static readonly string TemplateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools", "JoinTemplates");

        public static void Save(JoinTemplate template)
        {
            Directory.CreateDirectory(TemplateDir);
            string filePath = GetFilePath(template.Name);
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static JoinTemplate Load(string name)
        {
            string filePath = GetFilePath(name);
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<JoinTemplate>(json);
        }

        public static void Delete(string name)
        {
            string filePath = GetFilePath(name);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public static List<string> ListTemplateNames()
        {
            if (!Directory.Exists(TemplateDir)) return new List<string>();
            return Directory.GetFiles(TemplateDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
        }

        /// <summary>
        /// Chuyển đổi CategoryMatchRule list → JoinTemplateRule list (serialize-safe).
        /// </summary>
        public static List<JoinTemplateRule> ToTemplateRules(List<CategoryMatchRule> rules)
        {
            return rules.Select(r => new JoinTemplateRule
            {
                CategoryA = r.CategoryA.ToString(),
                CategoryB = r.CategoryB.ToString()
            }).ToList();
        }

        /// <summary>
        /// Chuyển đổi JoinTemplateRule list → CategoryMatchRule list.
        /// </summary>
        public static List<CategoryMatchRule> FromTemplateRules(List<JoinTemplateRule> templateRules)
        {
            var result = new List<CategoryMatchRule>();
            foreach (var tr in templateRules)
            {
                if (Enum.TryParse(tr.CategoryA, out BuiltInCategory catA) &&
                    Enum.TryParse(tr.CategoryB, out BuiltInCategory catB))
                {
                    result.Add(new CategoryMatchRule { CategoryA = catA, CategoryB = catB });
                }
            }
            return result;
        }

        private static string GetFilePath(string name) =>
            Path.Combine(TemplateDir, SanitizeFileName(name) + ".json");

        private static string SanitizeFileName(string name) =>
            string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}
