using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    public sealed class ModifyObjectPlan
    {
        public ModifyObjectContext Context { get; set; }
        public IList<ModifyObjectSourceSnapshot> Sources { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
        public ModifyObjectStatus Status { get; set; }
        public string Fingerprint { get; set; }
        public bool CanExecute { get { return Status == ModifyObjectStatus.READY && Errors.Count == 0; } }
        public ModifyObjectPlan() { Sources = new List<ModifyObjectSourceSnapshot>(); Warnings = new List<string>(); Errors = new List<string>(); Status = ModifyObjectStatus.READY; }
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
        public ModifyObjectStatus Status { get; set; }
        public string Summary { get; set; }
        public string Message { get; set; }
        public IList<ElementId> CreatedElementIds { get; private set; }
        public IList<ElementId> ModifiedElementIds { get; private set; }
        public IList<ElementId> DeletedSourceIds { get; private set; }
        public bool VerificationPassed { get; set; }
        public bool PreviewOnly { get; set; }
        public ModifyObjectResult() { CreatedElementIds = new List<ElementId>(); ModifiedElementIds = new List<ElementId>(); DeletedSourceIds = new List<ElementId>(); Status = ModifyObjectStatus.READY; }
    }
}
