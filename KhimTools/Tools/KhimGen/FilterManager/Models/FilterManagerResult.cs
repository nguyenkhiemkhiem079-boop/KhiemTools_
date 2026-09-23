using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.FilterManager.Models
{
    public sealed class FilterTargetResult
    {
        public WorkflowOutcome Outcome { get { return WorkflowOutcomeMapper.FromStatus(Status.ToString()); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public FilterManagerStatusCode Status { get; set; }
        public int AppliedCount { get; set; }
        public int RemovedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FilterManagerResult
    {
        public WorkflowOutcome Outcome { get { return WorkflowOutcomeMapper.FromStatus(Status.ToString()); } }
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
