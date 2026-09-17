using System;
using System.IO;
using Newtonsoft.Json;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class KqsProjectStore
    {
        public static string GetPath(string projectFolder) { if (string.IsNullOrWhiteSpace(projectFolder)) throw new ArgumentException(nameof(projectFolder)); return Path.Combine(projectFolder, ".kqs", "project.kqs.json"); }
        public static KqsProjectStoreData Open(string projectFolder) { var path = GetPath(projectFolder); if (!File.Exists(path)) return new KqsProjectStoreData(); var data = JsonConvert.DeserializeObject<KqsProjectStoreData>(File.ReadAllText(path)); return data ?? new KqsProjectStoreData(); }
        public static void Save(string projectFolder, KqsProjectStoreData data) { var path = GetPath(projectFolder); Directory.CreateDirectory(Path.GetDirectoryName(path)); var temp = path + ".tmp"; File.WriteAllText(temp, JsonConvert.SerializeObject(data, Formatting.Indented)); if (File.Exists(path)) File.Copy(path, path + ".bak-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); File.Copy(temp, path, true); File.Delete(temp); }
        public static void AppendAudit(KqsProjectStoreData data, KqsAuditEvent audit) { data.Audit.Add(audit); }
        public static bool TryApplyChange(KqsProjectStoreData data, KqsChangeEnvelope change, long currentVersion) { if (change.BaseVersion != currentVersion) return false; data.SyncToken = Math.Max(data.SyncToken, change.NewVersion); data.PendingChanges.Add(change); return true; }
    }
}
