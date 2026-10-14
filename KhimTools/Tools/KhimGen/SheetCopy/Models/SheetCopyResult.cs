using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyParameterResult
    {
        public string ParameterName { get; set; } = string.Empty;
        public SheetCopyStatusCode Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SheetCopyContentResult
    {
        public ElementId SourceElementId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetElementId { get; set; } = ElementId.InvalidElementId;
        public SheetCopyContentKind ContentKind { get; set; }
        public SheetCopyAction Action { get; set; }
        public SheetCopyStatusCode Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SheetCopyExecutionResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                switch (Status)
                {
                    case SheetCopyStatusCode.READY: return WorkflowOutcome.Ready;
                    case SheetCopyStatusCode.NO_CHANGE: return WorkflowOutcome.NoChange;
                    case SheetCopyStatusCode.CREATED:
                    case SheetCopyStatusCode.COPIED: return WorkflowOutcome.Succeeded;
                    case SheetCopyStatusCode.PARTIAL: return WorkflowOutcome.Partial;
                    case SheetCopyStatusCode.SKIPPED:
                    case SheetCopyStatusCode.SKIPPED_PROTECTED: return WorkflowOutcome.Skipped;
                    case SheetCopyStatusCode.FAILED:
                    case SheetCopyStatusCode.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public string SourceSheetNumber { get; set; } = string.Empty;
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public SheetCopyStatusCode Status { get; set; }
        public TimeSpan Duration { get; set; }
        public TransactionStatus? TransactionResult { get; set; }
        public bool TransactionStarted { get; set; }
        public bool VerificationPassed { get; set; }
        public bool VerificationAttempted { get; set; }
        public bool RollbackVerified { get; set; }
        public string ExceptionType { get; set; } = string.Empty;
        public List<ElementId> CreatedViewIds { get; } = new List<ElementId>();
        public List<ElementId> CreatedViewportIds { get; } = new List<ElementId>();
        public List<ElementId> CreatedScheduleInstanceIds { get; } = new List<ElementId>();
        public List<ElementId> CopiedAnnotationIds { get; } = new List<ElementId>();
        public List<SheetCopyParameterResult> ParameterResults { get; } = new List<SheetCopyParameterResult>();
        public List<SheetCopyContentResult> ContentResults { get; } = new List<SheetCopyContentResult>();
        public List<string> Messages { get; } = new List<string>();
    }

    public sealed class SheetCopyBatchResult
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
        public int Ready { get; set; }
        public int Created { get; set; }
        public int Partial { get; set; }
        public int Blocked { get; set; }
        public int Failed { get; set; }
        public List<SheetCopyExecutionResult> Results { get; } = new List<SheetCopyExecutionResult>();
        public IEnumerable<SheetCopyExecutionResult> Errors => Results.FindAll(r => r.Status == SheetCopyStatusCode.FAILED || r.Status == SheetCopyStatusCode.POST_VERIFY_FAILED);
    }
}
