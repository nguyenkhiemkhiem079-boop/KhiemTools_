using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.ScheduleSplit.Models
{
    public sealed class ScheduleSplitOptions
    {
        public ScheduleSplitMode Mode { get; set; } = ScheduleSplitMode.BY_SEGMENT_COUNT;
        public ScheduleSplitSourceMode SourceMode { get; set; } = ScheduleSplitSourceMode.WORKING_COPY;
        public ScheduleSplitDistribution Distribution { get; set; } = ScheduleSplitDistribution.ONE_SEGMENT_PER_SHEET;
        public ScheduleSplitDirection Direction { get; set; } = ScheduleSplitDirection.VERTICAL_STACK;
        public ScheduleSplitAnchor Anchor { get; set; } = ScheduleSplitAnchor.SOURCE_POSITION;
        public int SegmentCount { get; set; } = 2;
        public double MaxHeightMillimeters { get; set; } = 180.0;
        public double SegmentGapMillimeters { get; set; } = 10.0;
        public bool AllowWithWarningOnCollision { get; set; }
        public bool ReLayoutExistingSegments { get; set; }
        public bool AllowModifySharedSource { get; set; }
        public bool PreserveSourcePosition { get; set; } = true;
        public XYZ UserDefinedOffset { get; set; }
        public List<double> ManualSegmentHeightsMillimeters { get; } = new List<double>();
    }
}
