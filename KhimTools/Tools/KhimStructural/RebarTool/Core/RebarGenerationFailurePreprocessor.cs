using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Immutable snapshot of a Revit failure raised while a rectangular-column
    /// rebar transaction is being committed. FailureMessageAccessor instances are
    /// only valid during failure processing, so the data needed for the K-TOOLS
    /// report is copied here before the transaction is rolled back.
    /// </summary>
    public sealed class RebarGenerationFailureRecord
    {
        public string FailureDefinitionId { get; }
        public string Description { get; }
        public IReadOnlyList<ElementId> FailingElementIds { get; }
        public FailureSeverity Severity { get; }

        public bool RequiresRollback => Severity == FailureSeverity.Error ||
                                        Severity == FailureSeverity.DocumentCorruption;

        internal RebarGenerationFailureRecord(
            string failureDefinitionId,
            string description,
            IEnumerable<ElementId> failingElementIds,
            FailureSeverity severity)
        {
            FailureDefinitionId = failureDefinitionId ?? string.Empty;
            Description = description ?? string.Empty;
            FailingElementIds = (failingElementIds ?? Enumerable.Empty<ElementId>()).ToList();
            Severity = severity;
        }

        public string GetFailingElementIdsText()
        {
            return FailingElementIds.Count == 0
                ? "None reported by Revit"
                : string.Join(", ", FailingElementIds.Select(id => id?.ToString() ?? "<null>"));
        }
    }

    /// <summary>
    /// Captures Revit failures for structural rebar creation without resolving a
    /// failure or deleting elements. An error rolls back the whole column child
    /// transaction so an invalid or incomplete reinforcement cage is never kept.
    /// </summary>
    public sealed class RebarGenerationFailurePreprocessor : IFailuresPreprocessor
    {
        private readonly List<RebarGenerationFailureRecord> _records =
            new List<RebarGenerationFailureRecord>();

        public IReadOnlyList<RebarGenerationFailureRecord> Records => _records;

        public bool HasUnrecoverableFailure => _records.Any(record => record.RequiresRollback);

        public string Summary => _records.Count == 0
            ? "No Revit failure messages were reported."
            : string.Join(" | ", _records.Select(record =>
                record.Severity + ": " + record.Description + " [" + record.GetFailingElementIdsText() + "]"));

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            if (failuresAccessor == null)
            {
                return FailureProcessingResult.Continue;
            }

            IList<FailureMessageAccessor> failureMessages = failuresAccessor.GetFailureMessages();
            if (failureMessages == null || failureMessages.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            bool mustRollBack = false;
            foreach (FailureMessageAccessor failureMessage in failureMessages)
            {
                FailureSeverity severity = failureMessage.GetSeverity();
                FailureDefinitionId definitionId = failureMessage.GetFailureDefinitionId();

                var record = new RebarGenerationFailureRecord(
                    definitionId?.Guid.ToString("D"),
                    failureMessage.GetDescriptionText(),
                    failureMessage.GetFailingElementIds(),
                    severity);
                _records.Add(record);

                if (record.RequiresRollback)
                {
                    mustRollBack = true;
                }
            }

            // Warnings are left to Revit's normal handling. Errors are never
            // resolved, downgraded, or fixed by deleting structural elements.
            return mustRollBack
                ? FailureProcessingResult.ProceedWithRollBack
                : FailureProcessingResult.Continue;
        }
    }
}
