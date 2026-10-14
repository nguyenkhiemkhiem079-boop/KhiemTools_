using System;
using System.IO;
using Newtonsoft.Json;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.Core.Settings;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class KqsProjectStore
    {
        public static string GetPath(string projectFolder) { if (string.IsNullOrWhiteSpace(projectFolder)) throw new ArgumentException(nameof(projectFolder)); return Path.Combine(projectFolder, ".kqs", "project.kqs.json"); }
        public static KqsProjectStoreData Open(string projectFolder)
        {
            var path = GetPath(projectFolder);
            return JsonSettingsPersistence.Load(path, () => new KqsProjectStoreData(), IsValid);
        }
        public static void Save(string projectFolder, KqsProjectStoreData data)
        {
            var path = GetPath(projectFolder);
            JsonSettingsPersistence.Save(path, data, IsValid);
        }
        public static void AppendAudit(KqsProjectStoreData data, KqsAuditEvent audit) { data.Audit.Add(audit); }
        public static bool TryApplyChange(KqsProjectStoreData data, KqsChangeEnvelope change, long currentVersion) { if (change.BaseVersion != currentVersion) return false; data.SyncToken = Math.Max(data.SyncToken, change.NewVersion); data.PendingChanges.Add(change); return true; }

        private static bool IsValid(KqsProjectStoreData data)
        {
            return data != null && data.SchemaVersion == "1" && !string.IsNullOrWhiteSpace(data.ProjectId) &&
                data.ProjectId.Length <= 200 && data.SyncToken >= 0 && data.Audit != null && data.Audit.Count <= 100000 &&
                data.PendingChanges != null && data.PendingChanges.Count <= 100000 && data.Models != null && data.Models.Count <= 10000;
        }
    }
}
