using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core.Workflow;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerPlan : IWorkflowPlan
    {
        /// <summary>Stable identity captured at preview time; never a live Document.</summary>
        public string DocumentIdentityKey { get; set; } = string.Empty;
        public ParameterManagerPlanRequest Request { get; set; } = ParameterManagerPlanRequest.Empty;
        public ParameterKey SelectedParameterKey { get; set; }
        public string ParameterDisplayName { get; set; } = string.Empty;
        public string SourceScopeFingerprint { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public IList<WorkflowDiagnostic> Diagnostics { get; } = new List<WorkflowDiagnostic>();
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
            var copy = new ParameterManagerPlan { DocumentIdentityKey = DocumentIdentityKey, Request = Request == null ? ParameterManagerPlanRequest.Empty : Request.Clone(), SelectedParameterKey = ParameterManagerPlanRequest.CloneKey(SelectedParameterKey), ParameterDisplayName = ParameterDisplayName, SourceScopeFingerprint = SourceScopeFingerprint, Fingerprint = Fingerprint, CreatedAtUtc = CreatedAtUtc, IsPreview = IsPreview, IsStale = IsStale };
            foreach (ElementId id in TargetElementIds) copy.TargetElementIds.Add(id);
            foreach (ElementId id in TargetTypeIds) copy.TargetTypeIds.Add(id);
            foreach (ParameterEditItem item in Items) copy.Items.Add(CloneItem(item));
            foreach (string warning in Warnings) copy.Warnings.Add(warning);
            foreach (string blocked in Blocked) copy.Blocked.Add(blocked);
            foreach (WorkflowDiagnostic diagnostic in Diagnostics) copy.Diagnostics.Add(diagnostic);
            return copy;
        }

        private static ParameterEditItem CloneItem(ParameterEditItem item)
        {
            if (item == null) return null;
            var copy = new ParameterEditItem
            {
                ElementId = item.ElementId,
                UniqueId = item.UniqueId,
                TypeId = item.TypeId,
                CategoryName = item.CategoryName,
                FamilyName = item.FamilyName,
                TypeName = item.TypeName,
                ParameterKey = ParameterManagerPlanRequest.CloneKey(item.ParameterKey),
                ParameterName = item.ParameterName,
                CurrentValue = CloneValue(item.CurrentValue),
                ProposedValue = CloneValue(item.ProposedValue),
                IsSelected = item.IsSelected,
                IsTypeTarget = item.IsTypeTarget,
                IsManualOverride = item.IsManualOverride,
                TypeImpactCount = item.TypeImpactCount,
                Status = item.Status,
                Action = item.Action,
                Message = item.Message
            };
            foreach (string warning in item.Warnings) copy.Warnings.Add(warning);
            return copy;
        }

        private static ParameterValueSnapshot CloneValue(ParameterValueSnapshot value)
        {
            if (value == null) return null;
            return new ParameterValueSnapshot
            {
                Key = ParameterManagerPlanRequest.CloneKey(value.Key),
                StorageType = value.StorageType,
                DataTypeId = value.DataTypeId,
                StringValue = value.StringValue,
                IntegerValue = value.IntegerValue,
                DoubleValue = value.DoubleValue,
                ElementIdValue = value.ElementIdValue,
                IsBlank = value.IsBlank,
                DisplayValue = value.DisplayValue
            };
        }
    }
}
