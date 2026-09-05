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
            { "Foundation", "Móng cọc 1 tim" },
            { "Wall", "Generic - 200mm" },
            { "Slab", "Generic 150mm" }
        };

        public bool AlwaysLoadRebarCompletely { get; set; } = true;
        public bool OverwriteExistingTypes { get; set; } = false;
        public bool AutoScanOnOpen { get; set; } = true;

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
            catch
            {
                // Fallback to default if file corrupt or unreadable
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
