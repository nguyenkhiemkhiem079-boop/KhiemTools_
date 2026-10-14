using System.Collections.Generic;
using KhimTools.DimensionTools.Models;
using KhimTools.Core.Workflow;

namespace KhimTools.DimensionTools.Core
{
    public sealed class DimensionPlan : IWorkflowPlan
    {
        public DimensionPlanContext Context { get; set; }
        public Autodesk.Revit.DB.ElementId ViewId { get; set; }
        public string ViewUniqueId { get; set; }
        public string DocumentIdentityKey { get; set; }
        public string SourceStateFingerprint { get; set; }
        public DimensionOperation Operation { get; set; }
        public IList<DimensionReferenceSnapshot> References { get; private set; }
        public DimensionLineSnapshot DimensionLine { get; set; }
        public Autodesk.Revit.DB.ElementId DimensionTypeId { get; set; }
        public int ExpectedSegments { get; set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
        public string Fingerprint { get; set; }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public DimensionStatus Status { get; set; }
        public bool CanExecute { get { return Status == DimensionStatus.READY && Errors.Count == 0; } }
        public DimensionPlan() { References = new List<DimensionReferenceSnapshot>(); Warnings = new List<string>(); Errors = new List<string>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = DimensionStatus.READY; DimensionTypeId = Autodesk.Revit.DB.ElementId.InvalidElementId; }
    }
}
