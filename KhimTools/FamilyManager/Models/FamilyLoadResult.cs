using System.Collections.Generic;
using System.Linq;

namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Result object tracking outcomes of a family load operation.
    /// Provides counts, named failure dictionary, and a Success flag.
    /// </summary>
    public class FamilyLoadResult
    {
        // Named lists for internal tracking
        private readonly List<string> _loaded = new List<string>();
        private readonly List<string> _upToDate = new List<string>();
        private readonly Dictionary<string, string> _failures = new Dictionary<string, string>();
        private readonly List<string> _diagnosticLog = new List<string>();

        public int LoadedCount => _loaded.Count;
        public int UpToDateCount => _upToDate.Count;
        public int FailedCount => _failures.Count;

        /// <summary>True if no failures occurred.</summary>
        public bool Success => _failures.Count == 0;

        /// <summary>Dictionary of familyName → failureReason for all failed items.</summary>
        public IReadOnlyDictionary<string, string> Failures => _failures;

        public IReadOnlyList<string> LoadedNames => _loaded;
        public IReadOnlyList<string> UpToDateNames => _upToDate;
        public IReadOnlyList<string> DiagnosticLog => _diagnosticLog;

        public long DurationMs { get; set; }

        public void RecordLoaded(string familyName)
        {
            _loaded.Add(familyName);
            _diagnosticLog.Add($"[OK] Loaded: {familyName}");
        }

        public void RecordUpToDate(string familyName)
        {
            _upToDate.Add(familyName);
            _diagnosticLog.Add($"[=] UpToDate: {familyName}");
        }

        public void RecordFailure(string familyName, string reason)
        {
            _failures[familyName] = reason;
            _diagnosticLog.Add($"[FAIL] {familyName}: {reason}");
        }

        public void AddDiagnostic(string message)
        {
            _diagnosticLog.Add(message);
        }

        public string SummaryText
        {
            get
            {
                if (_failures.Count == 0)
                    return $"Success: {_loaded.Count} loaded, {_upToDate.Count} already up to date.";
                return $"Completed with warnings: {_loaded.Count} loaded, {_upToDate.Count} up to date, {_failures.Count} failed.";
            }
        }
    }
}
