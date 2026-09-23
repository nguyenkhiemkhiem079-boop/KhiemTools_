using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Domain.Models.Mep;

namespace KhimTools.MEP.Penetrations
{
    public sealed class MepOpeningClash
    {
        public ElementId MepElementId { get; internal set; }
        public ElementId HostElementId { get; internal set; }
        public string MepCategory { get; internal set; }
        public string HostCategory { get; internal set; }
        public XYZ ApproximateCenter { get; internal set; }
        public double RecommendedWidthMm { get; internal set; }
        public double RecommendedHeightMm { get; internal set; }
        public string MeasurementSource { get; internal set; }
    }

    public sealed class MepOpeningAnalysisResult
    {
        public int Requested { get; internal set; }
        public int Eligible { get; internal set; }
        public int Processed { get; internal set; }
        public int Skipped { get; internal set; }
        public int Failed { get; internal set; }
        public int HostCount { get; internal set; }
        public int HostGeometrySkipped { get; internal set; }
        public int HostGeometryFailed { get; internal set; }
        public IList<MepOpeningClash> Clashes { get; } = new List<MepOpeningClash>();
    }

    /// <summary>Read-only clash analysis. A result is a recommendation, never an opening-creation command.</summary>
    public static class MepOpeningAnalysisService
    {
        private static readonly BuiltInCategory[] HostCategories =
        {
            BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls
        };
        private const double GeometryTolerance = 1e-9;

        public static MepOpeningAnalysisResult Analyze(Document doc, View view,
            IEnumerable<MEPCurve> requestedCurves, double clearanceEachSideMm)
        {
            if (doc == null || view == null || view.Document != doc || requestedCurves == null)
                throw new ArgumentException("An active document, its view, and MEP curves are required.");
            if (double.IsNaN(clearanceEachSideMm) || double.IsInfinity(clearanceEachSideMm) || clearanceEachSideMm < 0)
                throw new ArgumentOutOfRangeException(nameof(clearanceEachSideMm));

            var result = new MepOpeningAnalysisResult();
            var curves = requestedCurves.Where(x => x != null).GroupBy(x => x.Id)
                .Select(x => x.First()).OrderBy(x => x.Id.ToLongValue()).ToList();
            result.Requested = curves.Count;
            var hosts = new List<HostGeometry>();
            foreach (Element host in new FilteredElementCollector(doc, view.Id)
                .WherePasses(new ElementMulticategoryFilter(HostCategories))
                .WhereElementIsNotElementType().ToElements())
            {
                result.HostCount++;
                try
                {
                    WorldBounds hostBounds = GetBounds(host.get_BoundingBox(view));
                    List<Solid> hostSolids = GetSolids(host);
                    if (hostBounds != null && hostSolids.Count > 0)
                        hosts.Add(new HostGeometry(host, hostBounds, hostSolids));
                    else result.HostGeometrySkipped++;
                }
                catch { result.HostGeometryFailed++; }
            }

            var options = new SolidCurveIntersectionOptions();
            foreach (MEPCurve mep in curves)
            {
                if (!MepCurveMeasurementService.IsSupported(mep) || !(mep.Location is LocationCurve location) ||
                    location.Curve == null || !location.Curve.IsBound)
                {
                    result.Skipped++;
                    continue;
                }
                MepCurveSection section = MepCurveMeasurementService.ReadSection(mep);
                if (section == null)
                {
                    result.Skipped++;
                    continue;
                }
                result.Eligible++;
                try
                {
                    BoundingBoxXYZ mepBox = mep.get_BoundingBox(view);
                    WorldBounds bounds = GetBounds(mepBox);
                    if (bounds == null) { result.Skipped++; continue; }
                    double halfSection = section.MaxDimensionInternal / 2;
                    double padding = UnitUtils.ConvertToInternalUnits(clearanceEachSideMm, UnitTypeId.Millimeters) + halfSection;
                    var outline = new Outline(bounds.Min - new XYZ(padding, padding, padding),
                        bounds.Max + new XYZ(padding, padding, padding));
                    var broadPhase = new BoundingBoxIntersectsFilter(outline);
                    var mepSolids = GetSolids(mep);
                    if (mepSolids.Count == 0) { result.Skipped++; continue; }
                    foreach (HostGeometry host in hosts)
                    {
                        if (!broadPhase.PassesFilter(doc, host.Element.Id)) continue;
                        bool intersects = false;
                        XYZ overlapCenter = null;
                        foreach (Solid mepSolid in mepSolids)
                        {
                            foreach (Solid hostSolid in host.Solids)
                            {
                                try
                                {
                                    Solid overlap = BooleanOperationsUtils.ExecuteBooleanOperation(
                                        mepSolid, hostSolid, BooleanOperationsType.Intersect);
                                    if (overlap != null && overlap.Volume > GeometryTolerance)
                                    { intersects = true; overlapCenter = overlap.ComputeCentroid(); break; }
                                }
                                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                                {
                                    // A failing solid pair is not silently counted as a clash; the pair is marked failed below.
                                    result.Failed++;
                                }
                            }
                            if (intersects) break;
                        }
                        if (!intersects) continue;

                        XYZ center = FindIntersectionCenter(host.Solids, location.Curve, options) ??
                                     overlapCenter ?? location.Curve.Evaluate(0.5, true);
                        MepOpeningDimensions opening = section.IsRound
                            ? MepMeasurementCalculator.RoundOpening(section.WidthMm, clearanceEachSideMm)
                            : MepMeasurementCalculator.RectangularOpening(section.WidthMm, section.HeightMm, clearanceEachSideMm);
                        result.Clashes.Add(new MepOpeningClash
                        {
                            MepElementId = mep.Id,
                            HostElementId = host.Element.Id,
                            MepCategory = mep.Category?.Name ?? string.Empty,
                            HostCategory = host.Element.Category?.Name ?? string.Empty,
                            ApproximateCenter = center,
                            RecommendedWidthMm = opening.WidthMm,
                            RecommendedHeightMm = opening.HeightMm,
                            MeasurementSource = section.Source
                        });
                    }
                    result.Processed++;
                }
                catch (Exception)
                {
                    result.Failed++;
                }
            }
            return result;
        }

        private static XYZ FindIntersectionCenter(IList<Solid> solids, Curve curve, SolidCurveIntersectionOptions options)
        {
            foreach (Solid solid in solids)
            {
                using (SolidCurveIntersection intersection = solid.IntersectWithCurve(curve, options))
                {
                    if (intersection != null && intersection.SegmentCount > 0)
                    {
                        Curve segment = intersection.GetCurveSegment(0);
                        return segment.Evaluate(0.5, true);
                    }
                }
            }
            return null;
        }

        private static List<Solid> GetSolids(Element element)
        {
            var solids = new List<Solid>();
            GeometryElement geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false });
            if (geometry != null) CollectSolids(geometry, solids);
            return solids;
        }

        private static void CollectSolids(GeometryElement geometry, IList<Solid> solids)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Volume > GeometryTolerance) solids.Add(solid);
                else if (item is GeometryInstance instance)
                {
                    GeometryElement nested = instance.GetInstanceGeometry();
                    if (nested != null) CollectSolids(nested, solids);
                }
            }
        }

        private static WorldBounds GetBounds(BoundingBoxXYZ box)
        {
            if (box == null) return null;
            Transform transform = box.Transform ?? Transform.Identity;
            var points = new List<XYZ>();
            foreach (double x in new[] { box.Min.X, box.Max.X })
                foreach (double y in new[] { box.Min.Y, box.Max.Y })
                    foreach (double z in new[] { box.Min.Z, box.Max.Z })
                        points.Add(transform.OfPoint(new XYZ(x, y, z)));
            return new WorldBounds
            {
                Min = new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
                Max = new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z))
            };
        }

        private sealed class WorldBounds { internal XYZ Min { get; set; } internal XYZ Max { get; set; } }
        private sealed class HostGeometry
        {
            internal Element Element { get; }
            internal WorldBounds Bounds { get; }
            internal List<Solid> Solids { get; }
            internal HostGeometry(Element element, WorldBounds bounds, List<Solid> solids)
            { Element = element; Bounds = bounds; Solids = solids; }
        }
    }
}
