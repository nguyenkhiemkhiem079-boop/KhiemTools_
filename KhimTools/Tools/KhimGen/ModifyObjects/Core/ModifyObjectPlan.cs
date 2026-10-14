using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.ModifyObjects.Core
{
    public sealed class ModifyObjectPlan : IWorkflowPlan
    {
        public ModifyObjectPlanContext Context { get; set; }
        public string DocumentIdentityKey { get { return Context == null ? string.Empty : Context.DocumentIdentityKey; } }
        public IList<ModifyObjectSourceSnapshot> Sources { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
        public ModifyObjectStatus Status { get; set; }
        public string Fingerprint { get; set; }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public bool CanExecute { get { return Status == ModifyObjectStatus.READY && Errors.Count == 0; } }
        public ModifyObjectPlan() { Sources = new List<ModifyObjectSourceSnapshot>(); Warnings = new List<string>(); Errors = new List<string>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = ModifyObjectStatus.READY; }
        public bool IsStale(Document doc) { return doc == null || Context == null || !string.Equals(DocumentIdentityKey, KhimTools.Core.Workflow.DocumentIdentity.From(doc).StableKey, System.StringComparison.Ordinal) || Sources.Any(s => { Element element = doc.GetElement(s.ElementId); return element == null || !string.Equals(element.UniqueId, s.UniqueId, System.StringComparison.Ordinal) || ModifyObjectPlanBuilder.BuildFingerprint(element) != s.GeometryFingerprint; }); }
    }
    public sealed class ModifyObjectPreflightResult
    {
        public bool IsValid { get; set; }
        public IList<string> Errors { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<ModifyObjectStatus> Statuses { get; private set; }
        public ModifyObjectPreflightResult() { Errors = new List<string>(); Warnings = new List<string>(); Statuses = new List<ModifyObjectStatus>(); }
    }
    public sealed class ModifyObjectResult
    {
        public WorkflowOutcome Outcome
        {
            get
            {
                switch (Status)
                {
                    case ModifyObjectStatus.READY: return WorkflowOutcome.Ready;
                    case ModifyObjectStatus.PREVIEW_ONLY: return WorkflowOutcome.Skipped;
                    case ModifyObjectStatus.NO_CHANGE: return WorkflowOutcome.NoChange;
                    case ModifyObjectStatus.CREATED:
                    case ModifyObjectStatus.MODIFIED:
                    case ModifyObjectStatus.DELETED_SOURCE: return VerificationPassed && TransactionResult == TransactionStatus.Committed && PostconditionPassed ? WorkflowOutcome.Succeeded : WorkflowOutcome.Failed;
                    case ModifyObjectStatus.PARTIAL: return VerificationPassed && TransactionResult == TransactionStatus.Committed && PostconditionPassed ? WorkflowOutcome.Partial : WorkflowOutcome.Failed;
                    case ModifyObjectStatus.FAILED:
                    case ModifyObjectStatus.POST_VERIFY_FAILED: return WorkflowOutcome.Failed;
                    default: return WorkflowOutcome.Blocked;
                }
            }
        }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public ModifyObjectStatus Status { get; set; }
        public string Summary { get; set; }
        public string Message { get; set; }
        public IList<ElementId> CreatedElementIds { get; private set; }
        public IList<ElementId> ModifiedElementIds { get; private set; }
        public IList<ElementId> DeletedSourceIds { get; private set; }
        public IList<ElementId> CreatedSupportElementIds { get; private set; }
        public bool VerificationPassed { get; set; }
        public bool PostconditionPassed { get; set; }
        public bool HostVerificationRequired { get; set; }
        public TransactionStatus? TransactionResult { get; set; }
        public TransactionStatus? RollbackResult { get; set; }
        public bool RollbackVerified { get; set; }
        public string Operation { get; set; }
        public string DocumentIdentityKey { get; set; }
        public int AffectedElementCount { get; set; }
        public int WarningCount { get; set; }
        public int FailureCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string FailureKind { get; set; }
        public string ExceptionType { get; set; }
        public string Postcondition { get; set; }
        public WorkflowExecutionRecord ExecutionDiagnostics { get; set; }
        public bool PreviewOnly { get; set; }
        public ModifyObjectResult() { CreatedElementIds = new List<ElementId>(); ModifiedElementIds = new List<ElementId>(); DeletedSourceIds = new List<ElementId>(); CreatedSupportElementIds = new List<ElementId>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = ModifyObjectStatus.READY; }
    }
}
