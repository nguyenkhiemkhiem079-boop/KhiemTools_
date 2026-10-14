using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerPreflightResult
    {
        public bool IsValid { get; set; }
        public ParameterManagerStatus Status { get; set; }
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public string ElementUniqueId { get; set; } = string.Empty;
        public ParameterKey ParameterKey { get; set; }
        public ParameterValueSnapshot CurrentValue { get; set; }
        public ParameterValueSnapshot ProposedValue { get; set; }
        public bool CanExecute { get; set; }
        public string Message { get; set; } = string.Empty;
        public IList<string> Errors { get; } = new List<string>();
        public IList<string> Warnings { get; } = new List<string>();
        public IList<ParameterEditItem> Items { get; } = new List<ParameterEditItem>();
    }
}
