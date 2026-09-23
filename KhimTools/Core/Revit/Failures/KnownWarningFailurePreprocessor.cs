using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.Core.Revit.Failures
{
    /// <summary>
    /// Safe failure policy: only warning definition GUIDs explicitly supplied by the
    /// caller may be deleted. Errors and unknown failures remain visible/rollback-capable.
    /// </summary>
    public sealed class KnownWarningFailurePreprocessor : IFailuresPreprocessor
    {
        private readonly ISet<string> _approvedWarningIds;
        private readonly IList<FailureRecord> _records = new List<FailureRecord>();

        public KnownWarningFailurePreprocessor(IEnumerable<string> approvedWarningIds = null)
        {
            _approvedWarningIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (approvedWarningIds == null)
            {
                return;
            }

            foreach (string id in approvedWarningIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _approvedWarningIds.Add(id.Trim());
                }
            }
        }

        public IReadOnlyList<FailureRecord> Records => (IReadOnlyList<FailureRecord>)_records;

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            if (failuresAccessor == null)
            {
                return FailureProcessingResult.Continue;
            }

            IList<FailureMessageAccessor> messages = failuresAccessor.GetFailureMessages();
            if (messages == null || messages.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            bool rollback = false;
            foreach (FailureMessageAccessor message in messages)
            {
                FailureDefinitionId definition = message.GetFailureDefinitionId();
                string id = definition == null || definition.Guid == Guid.Empty ? string.Empty : definition.Guid.ToString("D");
                FailureSeverity severity = message.GetSeverity();
                _records.Add(new FailureRecord(id, message.GetDescriptionText(), severity));

                if (severity == FailureSeverity.Warning && _approvedWarningIds.Contains(id))
                {
                    failuresAccessor.DeleteWarning(message);
                }
                else if (severity == FailureSeverity.Error || severity == FailureSeverity.DocumentCorruption)
                {
                    rollback = true;
                }
            }

            return rollback ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }

    public sealed class FailureRecord
    {
        public string DefinitionId { get; }
        public string Description { get; }
        public FailureSeverity Severity { get; }

        public FailureRecord(string definitionId, string description, FailureSeverity severity)
        {
            DefinitionId = definitionId ?? string.Empty;
            Description = description ?? string.Empty;
            Severity = severity;
        }
    }
}
