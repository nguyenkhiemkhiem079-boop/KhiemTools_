using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Configuration and user preferences for KhimTools Family Manager.
    /// Persisted as JSON in %AppData%/KhimTools/family_manager_settings.json.
    /// </summary>
    public class FamilyManagerSettings
    {
        public List<FamilyLibrarySource> Sources { get; set; } = new List<FamilyLibrarySource>();

        public Dictionary<string, string> PreferredFamilies { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Column", "M_Concrete-Square-Column" },
            { "Beam", "M_Concrete-Rectangular-Beam" },
            { "Foundation", "M_Footing-Pad-Single" },
            { "Wall", "Generic - 200mm" },
            { "Slab", "Generic 150mm" }
        };

        public bool AlwaysLoadRebarCompletely { get; set; } = true;
        public bool OverwriteExistingTypes { get; set; } = false;
        public bool AutoScanOnOpen { get; set; } = true;

        [JsonIgnore]
        public bool WasFallbackToDefault { get; private set; } = false;

        [JsonIgnore]
        public string LastLoadError { get; private set; }

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KhimTools");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "family_manager_settings.json");

        public static FamilyManagerSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<FamilyManagerSettings>(json);
                    if (settings != null)
                    {
                        if (settings.Sources == null) settings.Sources = new List<FamilyLibrarySource>();
                        if (settings.PreferredFamilies == null) settings.PreferredFamilies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                // AUDIT-03: Do NOT silently swallow corrupt config.
                // Preserve the corrupt file for user recovery and record the incident.
                try
                {
                    string backupPath = SettingsFilePath + $".corrupt.{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                    if (File.Exists(SettingsFilePath))
                    {
                        File.Copy(SettingsFilePath, backupPath, true);
                    }
                    string logDir = Path.Combine(SettingsDirectory, "logs");
                    if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, "family_manager.log");
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Failed to load settings. Backed up to {backupPath}. Error: {ex.Message}\n{ex.StackTrace}\n\n");
                }
                catch { }

                var fallback = CreateDefault();
                fallback.WasFallbackToDefault = true;
                fallback.LastLoadError = ex.Message;
                return fallback;
            }

            return CreateDefault();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Safe failure on save
            }
        }

        public static FamilyManagerSettings CreateDefault()
        {
            return new FamilyManagerSettings
            {
                Sources = new List<FamilyLibrarySource>(),
                AlwaysLoadRebarCompletely = true,
                OverwriteExistingTypes = false,
                AutoScanOnOpen = true
            };
        }
    }
}
