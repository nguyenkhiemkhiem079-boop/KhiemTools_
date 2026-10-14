using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterEditItem
    {
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public string UniqueId { get; set; } = string.Empty;
        public ElementId TypeId { get; set; } = ElementId.InvalidElementId;
        public string CategoryName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public ParameterKey ParameterKey { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public ParameterValueSnapshot CurrentValue { get; set; }
        public ParameterValueSnapshot ProposedValue { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsTypeTarget { get; set; }
        public bool IsManualOverride { get; set; }
        public int TypeImpactCount { get; set; }
        public ParameterManagerStatus Status { get; set; } = ParameterManagerStatus.READY;
        public ParameterManagerAction Action { get; set; } = ParameterManagerAction.SKIP;
        public string Message { get; set; } = string.Empty;
        public IList<string> Warnings { get; } = new List<string>();
        public string CurrentDisplay { get { return CurrentValue == null ? string.Empty : CurrentValue.DisplayValue ?? string.Empty; } }
        public string ProposedDisplay { get { return ProposedValue == null ? string.Empty : ProposedValue.DisplayValue ?? string.Empty; } }
    }
}
