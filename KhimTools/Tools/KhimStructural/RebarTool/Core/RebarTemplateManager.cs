using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KhimTools.Core.Settings;

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
            if (settings == null || string.IsNullOrWhiteSpace(settings.Name)) throw new InvalidDataException("Column template name is required.");
            settings.SchemaVersion = 1;
            if (!IsValid(settings)) throw new InvalidDataException("Column template is invalid.");
            Directory.CreateDirectory(TemplateDir);
            string filePath = GetFilePath("col_" + settings.Name);
            JsonSettingsPersistence.Save(filePath, settings, IsValid);
        }

        public static ColumnRebarSettings LoadColumnTemplate(string name)
        {
            string filePath = GetFilePath("col_" + name);
            if (!File.Exists(filePath)) return null;
            return JsonSettingsPersistence.Load<ColumnRebarSettings>(filePath, () => null, IsValid,
                value => { if (value.SchemaVersion != 0) return false; value.SchemaVersion = 1; return true; });
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
            if (settings == null || string.IsNullOrWhiteSpace(settings.Name)) throw new InvalidDataException("Beam template name is required.");
            settings.SchemaVersion = 1;
            if (!IsValid(settings)) throw new InvalidDataException("Beam template is invalid.");
            Directory.CreateDirectory(TemplateDir);
            string filePath = GetFilePath("beam_" + settings.Name);
            JsonSettingsPersistence.Save(filePath, settings, IsValid);
        }

        public static BeamRebarSettings LoadBeamTemplate(string name)
        {
            string filePath = GetFilePath("beam_" + name);
            if (!File.Exists(filePath)) return null;
            return JsonSettingsPersistence.Load<BeamRebarSettings>(filePath, () => null, IsValid,
                value => { if (value.SchemaVersion != 0) return false; value.SchemaVersion = 1; return true; });
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

        private static bool IsValid(ColumnRebarSettings value) =>
            value != null && value.SchemaVersion == 1 && !string.IsNullOrWhiteSpace(value.Name) &&
            value.Name.Length <= 100 && Enum.IsDefined(typeof(ColumnTieLayoutType), value.TieLayout) &&
            NumericValuesAreSafe(value);

        private static bool IsValid(BeamRebarSettings value) =>
            value != null && value.SchemaVersion == 1 && !string.IsNullOrWhiteSpace(value.Name) &&
            value.Name.Length <= 100 && NumericValuesAreSafe(value);

        private static bool NumericValuesAreSafe(object value)
        {
            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Type type = property.PropertyType;
                if (type == typeof(double))
                {
                    double number = (double)property.GetValue(value, null);
                    if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > 1000000) return false;
                }
                else if (type == typeof(int))
                {
                    int number = (int)property.GetValue(value, null);
                    if (number < 0 || number > 1000000) return false;
                }
            }
            return true;
        }
    }
}
