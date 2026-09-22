using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionReferenceInfo
    {
        public ElementId ElementId { get; set; }
        public string ElementUniqueId { get; set; }
        public Reference Reference { get; set; }
        public string StableRepresentation { get; set; }
        public string ReferenceKind { get; set; }
        public XYZ WorldPoint { get; set; }
        public XYZ ViewCoordinate { get; set; }
        public DimensionReferenceRole SourceRole { get; set; }
        public double ProjectedPosition { get; set; }
        public bool IsValid { get; set; }
        public string Diagnostic { get; set; }
    }
}
