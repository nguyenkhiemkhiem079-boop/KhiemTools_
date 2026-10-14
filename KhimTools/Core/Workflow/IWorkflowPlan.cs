using System.Collections.Generic;

namespace KhimTools.Core.Workflow
{
    /// <summary>
    /// Minimal contract for plans that may cross an internal adapter boundary.
    /// Implementations must expose stable data, never a UI object.
    /// </summary>
    public interface IWorkflowPlan
    {
        string Fingerprint { get; }
        IList<WorkflowDiagnostic> Diagnostics { get; }
    }
}
