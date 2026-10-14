using Autodesk.Revit.DB;

namespace KhimTools.ParameterTransfer.Models
{
    public sealed class ParameterValueSnapshot
    {
        public ParameterKey Key { get; set; }
        public StorageType StorageType { get; set; }
        public string DataTypeId { get; set; } = string.Empty;
        public string StringValue { get; set; }
        public int IntegerValue { get; set; }
        public double DoubleValue { get; set; }
        public ElementId ElementIdValue { get; set; } = ElementId.InvalidElementId;
        public bool IsBlank { get; set; }
        public string DisplayValue { get; set; } = string.Empty;
    }
}
