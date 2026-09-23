using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSegmentResult
    {
        public int SegmentIndex { get; set; }
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public ElementId InstanceId { get; set; } = ElementId.InvalidElementId;
        public XYZ PlannedPosition { get; set; }
        public XYZ ActualPosition { get; set; }
        public ScheduleSplitStatusCode Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ScheduleSplitExecutionResult
    {
        public WorkflowOutcome Outcome { get { return WorkflowOutcomeMapper.FromStatus(Status.ToString()); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public ElementId SourceScheduleId { get; set; } = ElementId.InvalidElementId;
        public ElementId WorkingScheduleId { get; set; } = ElementId.InvalidElementId;
        public ScheduleSplitStatusCode Status { get; set; }
        public int SegmentCount { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ScheduleSegmentResult> SegmentResults { get; } = new List<ScheduleSegmentResult>();
        public List<ElementId> CreatedInstanceIds { get; } = new List<ElementId>();
        public List<string> Messages { get; } = new List<string>();
    }

    public sealed class ScheduleSplitBatchResult
    {
        public WorkflowOutcome Outcome
        {
            get { return Failed > 0 ? (Created == 0 ? WorkflowOutcome.Failed : WorkflowOutcome.Partial) : (Created == 0 ? WorkflowOutcome.NoChange : WorkflowOutcome.Succeeded); }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public OperationMetrics Metrics { get; } = new OperationMetrics();
        public int Requested { get; set; }
        public int Created { get; set; }
        public int Partial { get; set; }
        public int Blocked { get; set; }
        public int Failed { get; set; }
        public List<ScheduleSplitExecutionResult> Results { get; } = new List<ScheduleSplitExecutionResult>();
    }
}
