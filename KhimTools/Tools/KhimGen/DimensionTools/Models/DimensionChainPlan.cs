using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionChainPlan
    {
        public IList<DimensionReferenceInfo> References { get; private set; }
        public Line DimensionLine { get; set; }
        public int ExpectedSegments { get; set; }
        public DimensionStatus Status { get; set; }
        public string Message { get; set; }
        public DimensionChainPlan() { References = new List<DimensionReferenceInfo>(); Status = DimensionStatus.READY; }
    }
}
