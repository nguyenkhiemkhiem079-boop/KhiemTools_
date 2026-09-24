using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Core
{
    public sealed class RebarConfiguration
    {
        public int SchemaVersion { get; set; } = 1;
        public int Revision { get; set; }
        public string ProjectKey { get; set; }
        public string UpdatedUtc { get; set; }
        public Dictionary<string, decimal> Project { get; set; } = new Dictionary<string, decimal>();
        public Dictionary<string, Dictionary<string, decimal>> Members { get; set; } = new Dictionary<string, Dictionary<string, decimal>>();
        public Dictionary<string, Dictionary<string, decimal>> Elements { get; set; } = new Dictionary<string, Dictionary<string, decimal>>();

        public decimal Resolve(string member, string key, decimal fallback, string element = null)
        {
            if (element != null && Elements.TryGetValue(element, out var local) && local.TryGetValue(key, out var value)) return value;
            if (Members.TryGetValue(member, out var type) && type.TryGetValue(key, out value)) return value;
            return Project.TryGetValue(key, out value) ? value : fallback;
        }

        public void Validate()
        {
            if (SchemaVersion != 1 || Revision < 0 || Project == null || Members == null || Elements == null || Members.Count > 50 || Elements.Count > 10000)
                throw new InvalidDataException("Cấu hình Rebar không hợp lệ hoặc phiên bản chưa được hỗ trợ.");
            ValidateValues(Project);
            foreach (var values in Members.Values) ValidateValues(values);
            foreach (var values in Elements.Values) ValidateValues(values);
        }

        private static void ValidateValues(Dictionary<string, decimal> values)
        {
            if (values == null || values.Count > 500) throw new InvalidDataException("Danh sách thông số không hợp lệ.");
            foreach (var entry in values)
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 120 || entry.Value < 0 || entry.Value > 1000000)
                    throw new InvalidDataException("Thông số Rebar ngoài phạm vi: " + entry.Key);
        }
    }

    public sealed class RebarConfigurationStore
    {
        private readonly string _path;
        private readonly string _projectKey;
        public RebarConfigurationStore(string directory, string projectKey)
        {
            if (string.IsNullOrWhiteSpace(projectKey)) throw new ArgumentException(nameof(projectKey));
            _projectKey = projectKey;
            using (var hash = SHA256.Create())
                _path = Path.Combine(directory, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(projectKey))).Replace("-", "").Substring(0, 32) + ".json");
        }

        public RebarConfiguration Load()
        {
            if (!File.Exists(_path)) return new RebarConfiguration { ProjectKey = _projectKey };
            try { return ValidateProject(Parse(File.ReadAllText(_path))); }
            catch (Exception primaryError)
            {
                string directory = Path.GetDirectoryName(_path);
                string pattern = Path.GetFileName(_path) + ".r*.bak";
                if (Directory.Exists(directory))
                {
                    foreach (string backup in Directory.GetFiles(directory, pattern).OrderByDescending(File.GetLastWriteTimeUtc))
                    {
                        try { return ValidateProject(Parse(File.ReadAllText(backup))); }
                        catch { }
                    }
                }
                throw new InvalidDataException("Rebar configuration is corrupt and no valid project backup is available.", primaryError);
            }
        }

        private RebarConfiguration ValidateProject(RebarConfiguration config)
        {
            if (config.ProjectKey != _projectKey) throw new InvalidDataException("Cấu hình thuộc dự án khác.");
            return config;
        }

        public static RebarConfiguration Parse(string json)
        {
            if (json == null || json.Length > 2000000) throw new InvalidDataException("File cấu hình quá lớn.");
            var config = JsonConvert.DeserializeObject<RebarConfiguration>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 12 });
            if (config == null) throw new InvalidDataException("File cấu hình trống.");
            config.Validate();
            return config;
        }

        public void Save(RebarConfiguration config)
        {
            config.Validate();
            if (config.ProjectKey != _projectKey) throw new InvalidDataException("Cấu hình thuộc dự án khác.");
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            // Serialize writers and reject stale forms rather than overwriting newer settings.
            using (new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var current = Load();
                if (current.Revision != config.Revision) throw new IOException("Cấu hình đã thay đổi. Nạp lại trước khi lưu.");
                var next = Parse(JsonConvert.SerializeObject(config));
                next.Revision++;
                next.UpdatedUtc = DateTime.UtcNow.ToString("o");
                string temp = Path.Combine(Path.GetDirectoryName(_path), Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(temp, JsonConvert.SerializeObject(next, Formatting.Indented));
                    if (File.Exists(_path)) File.Replace(temp, _path, _path + ".r" + current.Revision + ".bak");
                    else File.Move(temp, _path);
                    config.Revision = next.Revision;
                    config.UpdatedUtc = next.UpdatedUtc;
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
    }
}
