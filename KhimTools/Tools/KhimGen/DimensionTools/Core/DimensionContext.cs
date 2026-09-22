using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public sealed class DimensionContext
    {
        public Document Document { get; set; }
        public View View { get; set; }
        public DimensionOperation Operation { get; set; }
        public IList<ElementId> ElementIds { get; private set; }
        public IList<ElementId> DimensionIds { get; private set; }
        public List<DimensionReferenceInfo> References { get; private set; }
        public DimensionOptions Options { get; set; }
        public Line DimensionLine { get; set; }
        public DimensionContext() { ElementIds = new List<ElementId>(); DimensionIds = new List<ElementId>(); References = new List<DimensionReferenceInfo>(); Options = new DimensionOptions(); }
    }
}
