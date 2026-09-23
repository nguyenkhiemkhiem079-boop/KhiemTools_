using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;

namespace KhimTools.SlabStep.Models
{
    public enum SlabStepExecutionStatus
    {
        READY,
        CREATED,
        VALIDATION_FAILURE,
        REVIT_FAILURE,
        ROLLED_BACK
    }

    public sealed class SlabStepExecutionResult
    {
        public SlabStepExecutionStatus Status { get; set; } = SlabStepExecutionStatus.READY;
        public IList<ElementId> CreatedElementIds { get; } = new List<ElementId>();
        public TransactionStatus? TransactionResult { get; set; }
        public TransactionStatus? RollbackResult { get; set; }
        public bool RollbackVerified { get; set; }
        public string Operation { get; set; } = "SlabStep.Generate";
        public string InputSummary { get; set; } = string.Empty;
        public int WarningCount { get; set; }
        public int FailureCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string DiagnosticCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
    }
}
