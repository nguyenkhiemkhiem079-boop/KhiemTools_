using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                switch (Status)
                {
                    case DimensionStatus.READY: return WorkflowOutcome.Ready;
                    case DimensionStatus.NO_CHANGE: return WorkflowOutcome.NoChange;
                    case DimensionStatus.SKIPPED: return WorkflowOutcome.Skipped;
                    case DimensionStatus.CREATED:
                    case DimensionStatus.UPDATED:
                    case DimensionStatus.REPLACED: return VerificationPassed ? WorkflowOutcome.Succeeded : WorkflowOutcome.Failed;
                    case DimensionStatus.PARTIAL: return VerificationPassed ? WorkflowOutcome.Partial : WorkflowOutcome.Failed;
                    case DimensionStatus.FAILED:
                    case DimensionStatus.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public DimensionOperation Operation { get; set; }
        public IList<ElementId> SourceElementIds { get; private set; }
        public IList<ElementId> SourceDimensionIds { get; private set; }
        public IList<ElementId> CreatedDimensionIds { get; private set; }
        public IList<ElementId> DeletedDimensionIds { get; private set; }
        public DimensionStatus Status { get; set; }
        public int ExpectedSegmentCount { get; set; }
        public int ActualSegmentCount { get; set; }
        public string Message { get; set; }
        public TimeSpan Duration { get; set; }
        public bool VerificationPassed { get; set; }
        public DimensionResult() { SourceElementIds = new List<ElementId>(); SourceDimensionIds = new List<ElementId>(); CreatedDimensionIds = new List<ElementId>(); DeletedDimensionIds = new List<ElementId>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = DimensionStatus.READY; }
    }
}
