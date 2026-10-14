using System;
using KhimTools.Core.Workflow;

namespace KhimTools.Core.Logging
{
    /// <summary>Optional structured fields kept separate from user-facing messages.</summary>
    public sealed class WorkflowLogContext
    {
        public string Module { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string DocumentFingerprint { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public TimeSpan Duration { get; set; }
        public WorkflowOutcome Outcome { get; set; } = WorkflowOutcome.Ready;
    }
}
