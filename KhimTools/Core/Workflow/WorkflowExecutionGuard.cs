namespace KhimTools.Core.Workflow
{
    public static class WorkflowExecutionGuard
    {
        public static WorkflowDiagnostic ValidateFingerprint(string expected, string actual)
        {
            if (WorkflowFingerprint.Matches(expected, actual))
            {
                return WorkflowDiagnostic.Info("PLAN_CURRENT", "Plan fingerprint matches the current source snapshot.");
            }

            return WorkflowDiagnostic.Error(
                "PLAN_STALE",
                "Execution was blocked because the plan fingerprint does not match the current source snapshot.");
        }
    }
}
