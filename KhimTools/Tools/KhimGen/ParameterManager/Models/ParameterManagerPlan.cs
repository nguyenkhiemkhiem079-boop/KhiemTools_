using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerPlan
    {
        public Document Document { get; set; }
        public ParameterManagerRequest Request { get; set; }
        public ParameterKey SelectedParameterKey { get; set; }
        public string ParameterDisplayName { get; set; } = string.Empty;
        public string SourceScopeFingerprint { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsPreview { get; set; } = true;
        public bool IsStale { get; set; }
        public IList<ElementId> TargetElementIds { get; } = new List<ElementId>();
        public IList<ElementId> TargetTypeIds { get; } = new List<ElementId>();
        public IList<ParameterEditItem> Items { get; } = new List<ParameterEditItem>();
        public IList<string> Warnings { get; } = new List<string>();
        public IList<string> Blocked { get; } = new List<string>();
        public ParameterManagerPlan CloneReadOnly()
        {
            var copy = new ParameterManagerPlan { Document = Document, Request = Request, SelectedParameterKey = SelectedParameterKey, ParameterDisplayName = ParameterDisplayName, SourceScopeFingerprint = SourceScopeFingerprint, Fingerprint = Fingerprint, CreatedAtUtc = CreatedAtUtc, IsPreview = IsPreview, IsStale = IsStale };
            foreach (ElementId id in TargetElementIds) copy.TargetElementIds.Add(id);
            foreach (ElementId id in TargetTypeIds) copy.TargetTypeIds.Add(id);
            foreach (ParameterEditItem item in Items) copy.Items.Add(item);
            foreach (string warning in Warnings) copy.Warnings.Add(warning);
            foreach (string blocked in Blocked) copy.Blocked.Add(blocked);
            return copy;
        }
    }
}
