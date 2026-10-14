using Autodesk.Revit.DB;
namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionReferenceSnapshot
    {
        public ElementId ElementId { get; set; }
        public string ElementUniqueId { get; set; }
        public string StableRepresentation { get; set; }
        public string ReferenceKind { get; set; }
        public DimensionPointSnapshot WorldPoint { get; set; }
        public DimensionPointSnapshot ViewCoordinate { get; set; }
        public DimensionPointSnapshot ReferenceDirection { get; set; }
        public DimensionReferenceRole SourceRole { get; set; }
        public double ProjectedPosition { get; set; }
        public string GeometryFingerprint { get; set; }
        public bool IsValid { get; set; }
        public string Diagnostic { get; set; }
    }
}
