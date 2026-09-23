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
        public WorkflowOutcome Outcome { get { return WorkflowOutcomeMapper.FromStatus(Status.ToString()); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public string SourceSheetNumber { get; set; } = string.Empty;
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public SheetCopyStatusCode Status { get; set; }
        public TimeSpan Duration { get; set; }
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
            get { return Failed > 0 ? (Created == 0 ? WorkflowOutcome.Failed : WorkflowOutcome.Partial) : (Created == 0 ? WorkflowOutcome.NoChange : WorkflowOutcome.Succeeded); }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
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
