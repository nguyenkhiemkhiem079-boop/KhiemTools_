using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Atomic, per-document/per-host store for structured Manual v1 arrays; RebarConfiguration v1 remains unchanged.</summary>
    public sealed class BeamManualDetailingPlanStoreV1
    {
        public const int MaximumSerializedBytes = 2 * 1024 * 1024;
        private readonly string _path;
        private readonly string _documentKey;
        private readonly string _hostUniqueId;

        public BeamManualDetailingPlanStoreV1(string directory, string documentKey, string hostUniqueId)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Plan-store directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(documentKey)) throw new ArgumentException("Revit document identity is required.", nameof(documentKey));
            if (string.IsNullOrWhiteSpace(hostUniqueId)) throw new ArgumentException("Beam host identity is required.", nameof(hostUniqueId));
            _documentKey = documentKey;
            _hostUniqueId = hostUniqueId;
            _path = Path.Combine(directory, Hash(documentKey + "\n" + hostUniqueId) + ".beam-manual-v1.json");
        }

        public static BeamManualDetailingPlanStoreV1 ForCurrentUser(string documentKey, string hostUniqueId)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Per-user application data directory is unavailable.");
            return new BeamManualDetailingPlanStoreV1(Path.Combine(root, "KhimTools", "BeamManualPlans"), documentKey, hostUniqueId);
        }

        public BeamManualDetailingPlanV1 Load()
        {
            if (!File.Exists(_path)) return null;
            BeamManualDetailingPlanV1 plan = DeserializeBounded(File.ReadAllText(_path, Encoding.UTF8));
            ValidateIdentity(plan);
            BeamManualValidationResultV1 validation = BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: false);
            if (!validation.IsValid) throw new InvalidDataException("Stored Beam Manual v1 plan is invalid:" + Environment.NewLine + validation.ToDiagnosticText());
            return plan;
        }

        /// <summary>expectedRevision provides optimistic concurrency; a stale editor cannot overwrite a newer plan.</summary>
        public BeamManualDetailingPlanV1 Save(BeamManualDetailingPlanV1 plan, int expectedRevision)
        {
            ValidateIdentity(plan);
            if (expectedRevision < 0 || plan.Revision != expectedRevision)
                throw new InvalidDataException("Plan revision does not match the editor's expected revision.");
            BeamManualValidationResultV1 validation = BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: false);
            if (!validation.IsValid) throw new InvalidDataException("Beam Manual v1 plan is invalid:" + Environment.NewLine + validation.ToDiagnosticText());
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string lockPath = _path + ".lock";
            using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                BeamManualDetailingPlanV1 current = File.Exists(_path) ? DeserializeBounded(File.ReadAllText(_path, Encoding.UTF8)) : null;
                if ((current?.Revision ?? 0) != expectedRevision)
                    throw new IOException("Manual Beam plan changed since it was loaded. Reload and review before saving.");
                var next = DeserializeBounded(JsonConvert.SerializeObject(plan, Formatting.None));
                next.Revision = checked(expectedRevision + 1);
                next.GeometryStatus = "NOT_SOLVED";
                next.CodeVerified = false;
                string json = JsonConvert.SerializeObject(next, Formatting.Indented);
                EnsureSize(json);
                string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string backup = _path + ".r" + expectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bak";
                try
                {
                    File.WriteAllText(temporary, json, new UTF8Encoding(false));
                    DeserializeBounded(File.ReadAllText(temporary, Encoding.UTF8));
                    if (File.Exists(_path)) File.Replace(temporary, _path, backup, true);
                    else File.Move(temporary, _path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return next;
            }
        }

        private void ValidateIdentity(BeamManualDetailingPlanV1 plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!string.Equals(plan.DocumentKey, _documentKey, StringComparison.Ordinal) ||
                !string.Equals(plan.HostUniqueId, _hostUniqueId, StringComparison.Ordinal))
                throw new InvalidDataException("Plan belongs to another document or Beam host.");
        }

        private static BeamManualDetailingPlanV1 DeserializeBounded(string json)
        {
            EnsureSize(json);
            BeamManualDetailingPlanV1 result = JsonConvert.DeserializeObject<BeamManualDetailingPlanV1>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });
            if (result == null) throw new InvalidDataException("Manual Beam plan file is empty or invalid.");
            return result;
        }

        private static void EnsureSize(string value)
        {
            if (value == null || Encoding.UTF8.GetByteCount(value) > MaximumSerializedBytes)
                throw new InvalidDataException("Manual Beam plan exceeds the 2 MiB software resource limit.");
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(byteValue => byteValue.ToString("x2")));
        }
    }
}
