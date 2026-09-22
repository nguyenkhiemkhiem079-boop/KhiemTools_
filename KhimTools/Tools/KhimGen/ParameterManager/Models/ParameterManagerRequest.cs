using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerRequest
    {
        public Document Document { get; set; }
        public ElementScopeMode Scope { get; set; } = ElementScopeMode.CURRENT_SELECTION;
        public ParameterScopeMode ParameterScope { get; set; } = ParameterScopeMode.INSTANCE;
        public ElementId ActiveViewId { get; set; } = ElementId.InvalidElementId;
        public ElementId CategoryId { get; set; } = ElementId.InvalidElementId;
        public IList<ElementId> ElementIds { get; } = new List<ElementId>();
        public ParameterKey SelectedParameterKey { get; set; }
        public IList<ParameterRule> Rules { get; } = new List<ParameterRule>();
        public ParameterManagerOptions Options { get; set; } = new ParameterManagerOptions();
        public bool PreviewOnly { get; set; } = true;
    }
}
