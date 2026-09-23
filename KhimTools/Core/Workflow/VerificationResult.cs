using System.Collections.Generic;
using System.Linq;

namespace KhimTools.Core.Workflow
{
    public sealed class VerificationResult
    {
        public bool Passed { get; }
        public IReadOnlyList<WorkflowDiagnostic> Diagnostics { get; }
        public string ExpectedFingerprint { get; }
        public string ActualFingerprint { get; }

        public VerificationResult(
            bool passed,
            IEnumerable<WorkflowDiagnostic> diagnostics = null,
            string expectedFingerprint = null,
            string actualFingerprint = null)
        {
            Passed = passed;
            Diagnostics = (diagnostics ?? Enumerable.Empty<WorkflowDiagnostic>()).ToList().AsReadOnly();
            ExpectedFingerprint = expectedFingerprint ?? string.Empty;
            ActualFingerprint = actualFingerprint ?? string.Empty;
        }

        public bool Success => Passed;
    }
}
