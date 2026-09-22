using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionSnapshot
    {
        public ElementId DimensionId { get; set; }
        public string UniqueId { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId DimensionTypeId { get; set; }
        public Line Curve { get; set; }
        public IList<DimensionReferenceInfo> References { get; private set; }
        public IList<string> StableReferences { get; private set; }
        public IList<double> SegmentValues { get; private set; }
        public IList<XYZ> TextPositions { get; private set; }
        public bool Pinned { get; set; }
        public bool Locked { get; set; }
        public bool HasManualOverride { get; set; }
        public DimensionSnapshot() { References = new List<DimensionReferenceInfo>(); StableReferences = new List<string>(); SegmentValues = new List<double>(); TextPositions = new List<XYZ>(); }
    }
}
