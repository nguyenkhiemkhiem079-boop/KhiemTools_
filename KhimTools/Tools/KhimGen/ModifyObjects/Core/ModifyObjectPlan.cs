using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;

namespace KhimTools.ModifyObjects.Core
{
    public sealed class ModifyObjectPlan : IWorkflowPlan
    {
        public ModifyObjectContext Context { get; set; }
        public IList<ModifyObjectSourceSnapshot> Sources { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
        public ModifyObjectStatus Status { get; set; }
        public string Fingerprint { get; set; }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public bool CanExecute { get { return Status == ModifyObjectStatus.READY && Errors.Count == 0; } }
        public ModifyObjectPlan() { Sources = new List<ModifyObjectSourceSnapshot>(); Warnings = new List<string>(); Errors = new List<string>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = ModifyObjectStatus.READY; }
        public bool IsStale(Document doc) { return doc == null || Sources.Any(s => ModifyObjectPlanBuilder.BuildFingerprint(doc.GetElement(s.ElementId)) != s.GeometryFingerprint); }
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
        public WorkflowOutcome Outcome { get { return WorkflowOutcomeMapper.FromStatus(Status.ToString(), VerificationPassed, false); } }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public ModifyObjectStatus Status { get; set; }
        public string Summary { get; set; }
        public string Message { get; set; }
        public IList<ElementId> CreatedElementIds { get; private set; }
        public IList<ElementId> ModifiedElementIds { get; private set; }
        public IList<ElementId> DeletedSourceIds { get; private set; }
        public bool VerificationPassed { get; set; }
        public bool PreviewOnly { get; set; }
        public ModifyObjectResult() { CreatedElementIds = new List<ElementId>(); ModifiedElementIds = new List<ElementId>(); DeletedSourceIds = new List<ElementId>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = ModifyObjectStatus.READY; }
    }
}
