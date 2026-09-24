using System;
using System.IO;
using Newtonsoft.Json;

namespace KhimTools.Core.Settings
{
    /// <summary>Small file-level safeguards for user settings; feature schemas stay with their owners.</summary>
    public static class JsonSettingsPersistence
    {
        public static T Load<T>(string path, Func<T> defaults, Func<T, bool> validate, Func<T, bool> migrate = null)
        {
            if (defaults == null) throw new ArgumentNullException("defaults");
            if (!File.Exists(path)) return defaults();
            try
            {
                string json = File.ReadAllText(path);
                T value = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 32
                });
                bool migrated = migrate != null && migrate(value);
                if (value == null || (validate != null && !validate(value)))
                    throw new InvalidDataException("Settings validation failed.");
                if (migrated)
                    Save(path, value, validate);
                return value;
            }
            catch
            {
                T recovered = TryLoadBackup(path, defaults, validate, migrate);
                return (object)recovered != null ? recovered : defaults();
            }
        }

        public static void Save<T>(string path, T value, Func<T, bool> validate)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            if (value == null || (validate != null && !validate(value)))
                throw new InvalidDataException("Settings validation failed; existing file was preserved.");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backup = path + ".bak";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(value, Formatting.Indented));
                // Parse and validate the exact bytes before replacing the last known-good value.
                T roundTrip = JsonConvert.DeserializeObject<T>(File.ReadAllText(temporary), new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 32
                });
                if (roundTrip == null || (validate != null && !validate(roundTrip)))
                    throw new InvalidDataException("Serialized settings failed validation.");
                if (File.Exists(path)) File.Replace(temporary, path, backup, true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static T TryLoadBackup<T>(string path, Func<T> defaults, Func<T, bool> validate, Func<T, bool> migrate)
        {
            string backup = path + ".bak";
            if (!File.Exists(backup)) return default(T);
            try
            {
                T value = JsonConvert.DeserializeObject<T>(File.ReadAllText(backup), new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 32
                });
                if (migrate != null) migrate(value);
                return value != null && (validate == null || validate(value)) ? value : default(T);
            }
            catch { return default(T); }
        }
    }
}
