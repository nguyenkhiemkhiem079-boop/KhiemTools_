using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.SectionCutTool.Models;
using Newtonsoft.Json;
using KhimTools.Core.Settings;

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
            settings.SchemaVersion = 1;
            if (!IsValid(settings)) throw new InvalidDataException("Section template is invalid.");

            Directory.CreateDirectory(TemplateDir);
            string safeName = SanitizeFileName(settings.Name.Trim());
            string filePath = Path.Combine(TemplateDir, safeName + ".json");
            JsonSettingsPersistence.Save(filePath, settings, IsValid);
        }

        public static SectionCutSettings LoadTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string safeName = SanitizeFileName(name.Trim());
            string filePath = Path.Combine(TemplateDir, safeName + ".json");
            if (!File.Exists(filePath)) return null;

            return JsonSettingsPersistence.Load<SectionCutSettings>(filePath,
                () => null, IsValid,
                value =>
                {
                    if (value.SchemaVersion != 0) return false;
                    value.SchemaVersion = 1;
                    return true;
                });
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

        private static bool IsValid(SectionCutSettings s)
        {
            if (s == null || s.SchemaVersion != 1 || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 100 ||
                !Enum.IsDefined(typeof(CutDirection), s.DirectionFilter) || !Enum.IsDefined(typeof(CrossSectionCutMode), s.CrossSectionMode) ||
                s.LongitudinalScale < 1 || s.CrossSectionScale < 1 || s.RelativePositions == null || s.RelativePositions.Count > 100)
                return false;
            if (s.RelativePositions.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x < 0 || x > 1)) return false;
            return new[] { s.SpacingMm, s.CropOffsetLeftMm, s.CropOffsetRightMm, s.CropOffsetTopMm,
                s.CropOffsetBottomMm, s.FarClipOffsetMm }.All(x => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0 && x <= 1000000);
        }
    }
}
