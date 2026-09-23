using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;
namespace KhimTools.DimensionTools.Core
{
    internal static class DimensionPlanSnapshotAdapter
    {
        public static DimensionReferenceSnapshot Capture(DimensionReferenceInfo r)
        {
            if (r == null) return null;
            return new DimensionReferenceSnapshot { ElementId = r.ElementId, ElementUniqueId = r.ElementUniqueId ?? string.Empty, StableRepresentation = r.StableRepresentation ?? string.Empty, ReferenceKind = r.ReferenceKind ?? string.Empty, WorldPoint = Point(r.WorldPoint), ViewCoordinate = Point(r.ViewCoordinate), ReferenceDirection = Point(r.ReferenceDirection), SourceRole = r.SourceRole, ProjectedPosition = r.ProjectedPosition, GeometryFingerprint = r.GeometryFingerprint ?? string.Empty, IsValid = r.IsValid, Diagnostic = r.Diagnostic ?? string.Empty };
        }
        public static DimensionLineSnapshot Capture(Line line) { return line == null ? null : new DimensionLineSnapshot { Start = Point(line.GetEndPoint(0)), End = Point(line.GetEndPoint(1)) }; }
        public static XYZ ToXyz(DimensionPointSnapshot p) { return p == null ? null : new XYZ(p.X, p.Y, p.Z); }
        public static Line ToLine(DimensionLineSnapshot line) { XYZ a = line == null ? null : ToXyz(line.Start); XYZ b = line == null ? null : ToXyz(line.End); return a == null || b == null || a.IsAlmostEqualTo(b) ? null : Line.CreateBound(a, b); }
        public static string LineToken(DimensionLineSnapshot line) { if (line == null || line.Start == null || line.End == null) return string.Empty; return string.Join(",", line.Start.X.ToString("R"), line.Start.Y.ToString("R"), line.Start.Z.ToString("R"), line.End.X.ToString("R"), line.End.Y.ToString("R"), line.End.Z.ToString("R")); }
        private static DimensionPointSnapshot Point(XYZ p) { return p == null ? null : new DimensionPointSnapshot(p.X, p.Y, p.Z); }
    }
}
