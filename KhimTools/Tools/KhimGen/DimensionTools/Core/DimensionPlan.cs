using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;
using KhimTools.Core.Workflow;

namespace KhimTools.DimensionTools.Core
{
    public sealed class DimensionPlan : IWorkflowPlan
    {
        public DimensionContext Context { get; set; }
        public ElementId ViewId { get; set; }
        public DimensionOperation Operation { get; set; }
        public IList<DimensionReferenceInfo> References { get; private set; }
        public IList<DimensionPlanItem> Items { get; private set; }
        public Line DimensionLine { get; set; }
        public ElementId DimensionTypeId { get; set; }
        public int ExpectedSegments { get; set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
        public string Fingerprint { get; set; }
        public IList<WorkflowDiagnostic> Diagnostics { get; private set; }
        public DimensionStatus Status { get; set; }
        public bool CanExecute { get { return Status == DimensionStatus.READY && Errors.Count == 0; } }
        public DimensionPlan() { References = new List<DimensionReferenceInfo>(); Items = new List<DimensionPlanItem>(); Warnings = new List<string>(); Errors = new List<string>(); Diagnostics = new List<WorkflowDiagnostic>(); Status = DimensionStatus.READY; DimensionTypeId = ElementId.InvalidElementId; }
    }
}
