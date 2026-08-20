using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Core
{
    public static class RebarTemplateManager
    {
        private static readonly string TemplateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools", "RebarTemplates");

        // --- Column Templates ---
        public static void SaveColumnTemplate(ColumnRebarSettings settings)
        {
            Directory.CreateDirectory(TemplateDir);
            string filePath = GetFilePath("col_" + settings.Name);
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static ColumnRebarSettings LoadColumnTemplate(string name)
        {
            string filePath = GetFilePath("col_" + name);
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ColumnRebarSettings>(json);
        }

        public static void DeleteColumnTemplate(string name)
        {
            string filePath = GetFilePath("col_" + name);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public static List<string> ListColumnTemplates()
        {
            if (!Directory.Exists(TemplateDir)) return new List<string>();
            return Directory.GetFiles(TemplateDir, "col_*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f).Substring(4))
                .OrderBy(n => n)
                .ToList();
        }

        // --- Beam Templates ---
        public static void SaveBeamTemplate(BeamRebarSettings settings)
        {
            Directory.CreateDirectory(TemplateDir);
            string filePath = GetFilePath("beam_" + settings.Name);
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static BeamRebarSettings LoadBeamTemplate(string name)
        {
            string filePath = GetFilePath("beam_" + name);
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<BeamRebarSettings>(json);
        }

        public static void DeleteBeamTemplate(string name)
        {
            string filePath = GetFilePath("beam_" + name);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public static List<string> ListBeamTemplates()
        {
            if (!Directory.Exists(TemplateDir)) return new List<string>();
            return Directory.GetFiles(TemplateDir, "beam_*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f).Substring(5))
                .OrderBy(n => n)
                .ToList();
        }

        private static string GetFilePath(string name) =>
            Path.Combine(TemplateDir, SanitizeFileName(name) + ".json");

        private static string SanitizeFileName(string name) =>
            string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}
