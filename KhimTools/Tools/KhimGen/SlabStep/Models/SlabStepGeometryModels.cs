using System.Collections.Generic;
using System;
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

    public enum SlabScanScope { ActiveView, CurrentLevel, EntireModel }
    public enum SlabStepCandidateStatus { New, ExistingExact, ExistingPartial, Conflict, Unsupported }

    public sealed class SlabPanelInfo
    {
        public Floor Floor { get; set; }
        public ElementId FloorId => Floor?.Id;
        public ElementId LevelId { get; set; }
        public ElementId FloorTypeId { get; set; }
        public double TopElevation { get; set; }
        public double BottomElevation { get; set; }
        public double Thickness { get; set; }
        public double HeightOffset { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }
        public IList<CurveLoop> BoundaryLoops { get; set; } = new List<CurveLoop>();
        public IList<Curve> BoundaryCurves { get; set; } = new List<Curve>();
        public bool IsHorizontal { get; set; }
        public bool IsShapeEdited { get; set; }
        public bool IsSupported { get; set; }
        public string UnsupportedReason { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string TypeName { get; set; } = "";
    }

    public sealed class SlabScanOptions
    {
        public SlabScanScope Scope { get; set; } = SlabScanScope.ActiveView;
        public double MinStepHeightMm { get; set; } = 10;
        public double MaxStepHeightMm { get; set; } = 500;
        public double GeometryToleranceMm { get; set; } = 2;
        public double MinSharedLengthMm { get; set; } = 100;
    }

    public sealed class SlabStepCandidate
    {
        public SlabPanelInfo High { get; set; }
        public SlabPanelInfo Low { get; set; }
        public double StepHeight { get; set; }
        public IList<SharedBoundarySegment> Boundaries { get; set; } = new List<SharedBoundarySegment>();
        public double TotalBoundaryLength => Boundaries == null ? 0 : SumLength(Boundaries);
        public SlabStepCandidateStatus Status { get; set; } = SlabStepCandidateStatus.New;
        public string Message { get; set; } = "";
        private static double SumLength(IEnumerable<SharedBoundarySegment> values)
        { double total = 0; foreach (SharedBoundarySegment value in values) total += value.LengthInternal; return total; }
    }

    public sealed class SlabScanResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public int BoundingBoxPairs { get; set; }
        public int GeometryComparisons { get; set; }
        public IList<SlabPanelInfo> Panels { get; set; } = new List<SlabPanelInfo>();
        public IList<SlabStepCandidate> Candidates { get; set; } = new List<SlabStepCandidate>();
    }
}
