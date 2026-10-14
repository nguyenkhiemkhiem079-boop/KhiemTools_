using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.Core.Workflow;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class ParameterSyncResult
    {
        public ParameterKey ParameterKey { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public TitleBlockSyncScope Scope { get; set; }
        public ParameterValueSnapshot OldValue { get; set; }
        public ParameterValueSnapshot NewValue { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    public sealed class TitleBlockSyncExecutionResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                switch (Status)
                {
                    case TitleBlockSyncStatusCode.READY: return WorkflowOutcome.Ready;
                    case TitleBlockSyncStatusCode.NO_CHANGE: return WorkflowOutcome.NoChange;
                    case TitleBlockSyncStatusCode.SYNCED: return WorkflowOutcome.Succeeded;
                    case TitleBlockSyncStatusCode.PARTIAL: return WorkflowOutcome.Partial;
                    case TitleBlockSyncStatusCode.SKIPPED:
                    case TitleBlockSyncStatusCode.BLANK_SOURCE_SKIPPED: return WorkflowOutcome.Skipped;
                    case TitleBlockSyncStatusCode.FAILED:
                    case TitleBlockSyncStatusCode.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetSheetId { get; set; } = ElementId.InvalidElementId;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public ElementId OldTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId NewTypeId { get; set; } = ElementId.InvalidElementId;
        public bool TypeChanged { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ParameterSyncResult> ParameterResults { get; } = new List<ParameterSyncResult>();
        public List<string> Messages { get; } = new List<string>();
    }
    public sealed class TitleBlockSyncBatchResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                if (Failed > 0) return Synced > 0 ? WorkflowOutcome.Partial : WorkflowOutcome.Failed;
                if (Partial > 0) return WorkflowOutcome.Partial;
                if (Blocked > 0) return Synced > 0 ? WorkflowOutcome.Partial : WorkflowOutcome.Blocked;
                return Synced > 0 ? WorkflowOutcome.Succeeded : WorkflowOutcome.NoChange;
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public OperationMetrics Metrics { get; } = new OperationMetrics();
        public int Requested { get; set; }
        public int Synced { get; set; }
        public int NoChange { get; set; }
        public int Partial { get; set; }
        public int Blocked { get; set; }
        public int Failed { get; set; }
        public int TypeChanges { get; set; }
        public List<TitleBlockSyncExecutionResult> Results { get; } = new List<TitleBlockSyncExecutionResult>();
    }
}
