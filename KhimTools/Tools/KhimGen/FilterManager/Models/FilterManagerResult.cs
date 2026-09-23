using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterTargetResult
    {
        public WorkflowOutcome Outcome { get { return FilterOutcome(Status); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public FilterManagerStatusCode Status { get; set; }
        public int AppliedCount { get; set; }
        public int RemovedCount { get; set; }
        public string Message { get; set; } = string.Empty;

        internal static WorkflowOutcome FilterOutcome(FilterManagerStatusCode status)
        {
            switch (status)
            {
                case FilterManagerStatusCode.READY: return WorkflowOutcome.Ready;
                case FilterManagerStatusCode.NO_CHANGE: return WorkflowOutcome.NoChange;
                case FilterManagerStatusCode.APPLIED:
                case FilterManagerStatusCode.UPDATED:
                case FilterManagerStatusCode.ADDED:
                case FilterManagerStatusCode.REMOVED_FROM_VIEW: return WorkflowOutcome.Succeeded;
                case FilterManagerStatusCode.PARTIAL: return WorkflowOutcome.Partial;
                case FilterManagerStatusCode.FAILED:
                case FilterManagerStatusCode.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                default: return WorkflowOutcome.Blocked;
            }
        }
    }

    public sealed class FilterManagerResult
    {
        public WorkflowOutcome Outcome { get { return FilterTargetResult.FilterOutcome(Status); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public OperationMetrics Metrics { get; } = new OperationMetrics();
        public FilterManagerStatusCode Status { get; set; }
        public int RequestedTargets { get; set; }
        public int AppliedTargets { get; set; }
        public int FailedTargets { get; set; }
        public int NoChangeTargets { get; set; }
        public IList<FilterTargetResult> Targets { get; } = new List<FilterTargetResult>();
        public string Message { get; set; } = string.Empty;
    }
}
