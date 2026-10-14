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
        public WorkflowOutcome Outcome
        {
            get
            {
                switch (Status)
                {
                    case ScheduleSplitStatusCode.READY: return WorkflowOutcome.Ready;
                    case ScheduleSplitStatusCode.CREATED: return WorkflowOutcome.Succeeded;
                    case ScheduleSplitStatusCode.PARTIAL: return WorkflowOutcome.Partial;
                    case ScheduleSplitStatusCode.FAILED:
                    case ScheduleSplitStatusCode.POST_VERIFY_FAILED:
                    case ScheduleSplitStatusCode.SEGMENT_CREATE_FAILED:
                    case ScheduleSplitStatusCode.SEGMENT_PLACEMENT_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public ElementId SourceScheduleId { get; set; } = ElementId.InvalidElementId;
        public ElementId WorkingScheduleId { get; set; } = ElementId.InvalidElementId;
        public ScheduleSplitStatusCode Status { get; set; }
        public int SegmentCount { get; set; }
        public TimeSpan Duration { get; set; }
        public TransactionStatus? TransactionResult { get; set; }
        public bool TransactionStarted { get; set; }
        public bool VerificationPassed { get; set; }
        public bool VerificationAttempted { get; set; }
        public bool RollbackVerified { get; set; }
        public string ExceptionType { get; set; } = string.Empty;
        public List<ScheduleSegmentResult> SegmentResults { get; } = new List<ScheduleSegmentResult>();
        public List<ElementId> CreatedInstanceIds { get; } = new List<ElementId>();
        public List<string> Messages { get; } = new List<string>();
    }

    public sealed class ScheduleSplitBatchResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                if (Failed > 0) return Created > 0 ? WorkflowOutcome.Partial : WorkflowOutcome.Failed;
                if (Partial > 0) return WorkflowOutcome.Partial;
                if (Blocked > 0) return Created > 0 ? WorkflowOutcome.Partial : WorkflowOutcome.Blocked;
                return Created > 0 ? WorkflowOutcome.Succeeded : WorkflowOutcome.NoChange;
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public OperationMetrics Metrics { get; } = new OperationMetrics();
        public int Requested { get; set; }
        public int Created { get; set; }
        public int Partial { get; set; }
        public int Blocked { get; set; }
        public int Failed { get; set; }
        public List<ScheduleSplitExecutionResult> Results { get; } = new List<ScheduleSplitExecutionResult>();
    }
}
