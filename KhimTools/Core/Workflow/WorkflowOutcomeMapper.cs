using System;

namespace KhimTools.Core.Workflow
{
    /// <summary>Maps legacy status-code vocabularies to the shared outcome vocabulary.</summary>
    public static class WorkflowOutcomeMapper
    {
        public static WorkflowOutcome FromStatus(string status, bool verificationPassed = true, bool rolledBack = false)
        {
            if (rolledBack) return WorkflowOutcome.RolledBack;
            if (!verificationPassed) return WorkflowOutcome.Failed;

            string value = (status ?? string.Empty).ToUpperInvariant();
            if (value.Contains("NO_CHANGE") || value.Contains("UNCHANGED")) return WorkflowOutcome.NoChange;
            if (value.Contains("PARTIAL")) return WorkflowOutcome.Partial;
            if (value.Contains("SKIP")) return WorkflowOutcome.Skipped;
            if (value.Contains("BLOCK") || value.Contains("INVALID") || value.Contains("MISSING") || value.Contains("UNSUPPORTED")) return WorkflowOutcome.Blocked;
            if (value.Contains("FAIL") || value.Contains("ERROR") || value.Contains("ROLLBACK")) return WorkflowOutcome.Failed;
            if (value.Contains("READY")) return WorkflowOutcome.Ready;
            return WorkflowOutcome.Succeeded;
        }

        public static WorkflowOutcome FromException(Exception exception)
        {
            return exception == null ? WorkflowOutcome.Failed : WorkflowOutcome.Failed;
        }
    }
}
