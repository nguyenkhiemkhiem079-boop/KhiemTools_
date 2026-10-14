using Autodesk.Revit.DB;

namespace KhimTools.ParameterTransfer.Models
{
    public sealed class ParameterTransferPlan
    {
        public ParameterKey Key { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public ParameterValueSnapshot SourceValue { get; set; }
        public ElementId SourceElementId { get; set; } = ElementId.InvalidElementId;
        public bool Protected { get; set; }
        public bool Selected { get; set; }
        public bool CanSync { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
