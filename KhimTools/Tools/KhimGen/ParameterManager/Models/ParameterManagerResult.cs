using System.Collections.Generic;
using KhimTools.Core.Workflow;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                if (RolledBack || Status == ParameterManagerStatus.ALL_OR_NOTHING_ROLLED_BACK) return WorkflowOutcome.RolledBack;
                switch (Status)
                {
                    case ParameterManagerStatus.READY: return WorkflowOutcome.Ready;
                    case ParameterManagerStatus.PREVIEW_ONLY:
                    case ParameterManagerStatus.SKIPPED: return WorkflowOutcome.Skipped;
                    case ParameterManagerStatus.NO_CHANGE: return WorkflowOutcome.NoChange;
                    case ParameterManagerStatus.UPDATED: return VerificationPassed ? WorkflowOutcome.Succeeded : WorkflowOutcome.Failed;
                    case ParameterManagerStatus.PARTIAL: return VerificationPassed ? WorkflowOutcome.Partial : WorkflowOutcome.Failed;
                    case ParameterManagerStatus.FAILED:
                    case ParameterManagerStatus.VERIFICATION_FAILED:
                    case ParameterManagerStatus.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public OperationMetrics Metrics { get; } = new OperationMetrics();
        public ParameterManagerStatus Status { get; set; }
        public int RequestedTargets { get; set; }
        public int ReadyTargets { get; set; }
        public int UpdatedTargets { get; set; }
        public int NoChangeTargets { get; set; }
        public int BlockedTargets { get; set; }
        public int FailedTargets { get; set; }
        public bool VerificationPassed { get; set; }
        public bool RolledBack { get; set; }
        public IList<ParameterEditItem> Items { get; } = new List<ParameterEditItem>();
        public IList<string> Messages { get; } = new List<string>();
        public string Summary { get { return "Requested: " + RequestedTargets + ", Updated: " + UpdatedTargets + ", No change: " + NoChangeTargets + ", Blocked: " + BlockedTargets + ", Failed: " + FailedTargets; } }
    }
}
