using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionOptions
    {
        public DimensionAxis Axis { get; set; }
        public double OffsetMillimeters { get; set; }
        public double ToleranceMillimeters { get; set; } = 1.0;
        public double HorizontalMoveMillimeters { get; set; }
        public double VerticalMoveMillimeters { get; set; }
        public int BoundaryIndex { get; set; } = 1;
        public ElementId DimensionTypeId { get; set; }
        public bool UseOuterDimension { get; set; }
        public bool IncludeOriginal { get; set; } = true;
        public bool AllowPinned { get; set; }
        public string ReferenceStrategy { get; set; }
        public DimensionOptions() { Axis = DimensionAxis.AUTO; DimensionTypeId = ElementId.InvalidElementId; ReferenceStrategy = "EXPLICIT"; }
    }
}
