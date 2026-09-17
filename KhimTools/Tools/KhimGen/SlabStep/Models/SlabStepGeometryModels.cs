using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SlabStep.Models
{
    public sealed class SlabGeometryInfo
    {
        public Floor Floor { get; set; }
        public ElementId FloorId => Floor?.Id;
        public double TopElevationInternal { get; set; }
        public double ThicknessInternal { get; set; }
        public double LevelElevation { get; set; }
        public double HeightOffset { get; set; }
        public IList<Curve> BoundaryCurves { get; set; } = new List<Curve>();
    }

    public sealed class SharedBoundaryOptions
    {
        public double GeometryToleranceMm { get; set; } = 2.0;
        public double AngularTolerance { get; set; } = 1e-4;
        public double MinimumLengthMm { get; set; } = 10.0;
    }

    public sealed class SharedBoundarySegment
    {
        public Curve Curve { get; set; }
        public double LengthInternal => Curve?.Length ?? 0;
        public string Warning { get; set; }
    }
}
