namespace KhimTools.Core.Workflow
{
    /// <summary>Stable outcome vocabulary shared by Revit workflows and adapters.</summary>
    public enum WorkflowOutcome
    {
        Ready,
        NoChange,
        Succeeded,
        Partial,
        Skipped,
        Blocked,
        Failed,
        RolledBack
    }
}
