using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionReferenceService
    {
        private const double FingerprintPrecision = 1000000.0;

        public static IList<DimensionReferenceInfo> FromGrid(Document doc, View view, IEnumerable<Grid> grids)
        {
            var result = new List<DimensionReferenceInfo>();
            foreach (Grid grid in grids ?? Enumerable.Empty<Grid>())
            {
                if (grid == null || grid.Curve == null) continue;
                XYZ tangent = grid.Curve.ComputeDerivatives(0.5, true).BasisX;
                result.Add(Create(doc, view, new Reference(grid), grid, DimensionReferenceRole.GRID, Midpoint(grid.Curve), tangent));
            }
            return NormalizeAndSort(view, result, view == null ? XYZ.BasisX : view.RightDirection);
        }

        public static IList<DimensionReferenceInfo> FromElements(Document doc, View view, IEnumerable<Element> elements, DimensionReferenceRole role, DimensionAxis axis)
        {
            var result = new List<DimensionReferenceInfo>();
            ViewPlane plane = ViewPlane.FromView(view);
            if (doc == null || plane == null) return result;
            XYZ dimensionAxis = axis == DimensionAxis.VERTICAL_IN_VIEW ? plane.UpDirection : plane.RightDirection;
            var options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine, View = view };
            foreach (Element element in elements ?? Enumerable.Empty<Element>())
            {
                if (element == null) continue;
                FamilyInstance family = element as FamilyInstance;
                if (family != null)
                {
                    foreach (Reference reference in GetFamilyReferences(family, role, axis))
                    {
                        XYZ point; XYZ direction;
                        ResolveReferenceGeometry(doc, reference, element, out point, out direction);
                        result.Add(Create(doc, view, reference, element, role, point ?? LocationPointOf(element), direction));
                    }
                }
                if (role == DimensionReferenceRole.CENTER || role == DimensionReferenceRole.COLUMN_CENTER) continue;
                GeometryElement geometry = null;
                try { geometry = element.get_Geometry(options); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] geometry references: " + ex.Message); }
                if (geometry != null) foreach (GeometryObject obj in geometry) AddGeometryReferences(doc, view, element, obj, Transform.Identity, role, dimensionAxis, result);
            }
            result = SelectCompatibleReferenceGroup(result, dimensionAxis, axis).ToList();
            XYZ sortAxis = axis == DimensionAxis.AUTO && result.Count > 0 && result[0].ReferenceDirection != null ? result[0].ReferenceDirection : dimensionAxis;
            return NormalizeAndSort(view, result, sortAxis);
        }

        public static IList<DimensionReferenceInfo> FromDimension(Document doc, View view, Dimension dimension)
        {
            var result = new List<DimensionReferenceInfo>();
            if (doc == null || dimension == null) return result;
            foreach (Reference reference in new DimensionApiAdapter().ReadReferences(dimension))
            {
                Element element = reference == null ? null : doc.GetElement(reference.ElementId);
                XYZ point; XYZ direction;
                ResolveReferenceGeometry(doc, reference, element, out point, out direction);
                result.Add(Create(doc, view, reference, element, DimensionReferenceRole.GENERIC_REFERENCE, point ?? LocationPointOf(element), direction));
            }
            return result;
        }

        public static IList<DimensionReferenceInfo> FromReferences(Document doc, View view, IEnumerable<Reference> references, DimensionReferenceRole role)
        {
            var result = new List<DimensionReferenceInfo>();
            foreach (Reference reference in references ?? Enumerable.Empty<Reference>())
            {
                if (reference == null) continue;
                Element source = doc.GetElement(reference.ElementId);
                XYZ point; XYZ direction;
                ResolveReferenceGeometry(doc, reference, source, out point, out direction);
                if (point == null) { try { point = reference.GlobalPoint; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] picked reference point: " + ex.Message); } }
                result.Add(Create(doc, view, reference, source, role, point ?? LocationPointOf(source), direction));
            }
            return result;
        }

        public static Reference ResolveStableReference(Document doc, DimensionReferenceInfo info)
        {
            if (doc == null || info == null) return null;
            Reference resolved = string.IsNullOrWhiteSpace(info.StableRepresentation) ? info.Reference : new DimensionApiAdapter().ResolveStableReference(doc, info.StableRepresentation);
            if (resolved == null) return null;
            Element source = doc.GetElement(resolved.ElementId);
            if (source == null) return null;
            if (!string.IsNullOrWhiteSpace(info.ElementUniqueId) && !string.Equals(source.UniqueId, info.ElementUniqueId, StringComparison.Ordinal)) return null;
            return resolved;
        }

        public static DimensionSnapshot CaptureSnapshot(Document doc, View view, Dimension dimension)
        {
            var snapshot = new DimensionSnapshot
            {
                DimensionId = dimension.Id,
                UniqueId = dimension.UniqueId,
                ViewId = dimension.OwnerViewId,
                DimensionTypeId = dimension.GetTypeId(),
                Curve = dimension.Curve as Line,
                Pinned = dimension.Pinned,
                Locked = dimension.IsLocked,
                HasEqualityConstraint = dimension.AreSegmentsEqual,
                HasManualOverride = HasDimensionOverride(dimension)
            };
            snapshot.References.AddRange(FromDimension(doc, view, dimension));
            foreach (DimensionReferenceInfo info in snapshot.References) snapshot.StableReferences.Add(info.StableRepresentation);
            foreach (DimensionSegment segment in dimension.Segments)
            {
                snapshot.SegmentValues.Add(segment.Value.HasValue ? segment.Value.Value : 0);
                snapshot.TextPositions.Add(segment.TextPosition);
                snapshot.HasManualOverride = snapshot.HasManualOverride || HasSegmentOverride(segment);
            }
            return snapshot;
        }

        public static IList<DimensionReferenceInfo> NormalizeAndSort(View view, IEnumerable<DimensionReferenceInfo> input, XYZ worldAxis)
        {
            ViewPlane plane = ViewPlane.FromView(view);
            XYZ axis = worldAxis == null || worldAxis.IsZeroLength() ? (plane == null ? XYZ.BasisX : plane.RightDirection) : worldAxis.Normalize();
            var map = new Dictionary<string, DimensionReferenceInfo>(StringComparer.Ordinal);
            foreach (DimensionReferenceInfo info in input ?? Enumerable.Empty<DimensionReferenceInfo>())
            {
                if (info == null || info.Reference == null) continue;
                string key = SemanticKey(info);
                if (map.ContainsKey(key)) continue;
                XYZ point = info.WorldPoint ?? XYZ.Zero;
                info.ProjectedPosition = plane == null ? point.DotProduct(axis) : plane.ProjectAlong(point, axis);
                map.Add(key, info);
            }
            return map.Values.OrderBy(x => x.ProjectedPosition).ThenBy(SemanticKey, StringComparer.Ordinal).ToList();
        }

        public static string ComputeLiveGeometryFingerprint(Document doc, DimensionReferenceInfo info, Reference resolved)
        {
            if (doc == null || info == null || resolved == null) return string.Empty;
            Element source = doc.GetElement(resolved.ElementId);
            XYZ point; XYZ direction;
            ResolveReferenceGeometry(doc, resolved, source, out point, out direction);
            return GeometryFingerprint(source, point ?? LocationPointOf(source), direction);
        }

        public static void RefreshResolvedGeometry(Document doc, View view, DimensionReferenceInfo info, Reference resolved)
        {
            Element source = doc.GetElement(resolved.ElementId);
            XYZ point; XYZ direction;
            ResolveReferenceGeometry(doc, resolved, source, out point, out direction);
            info.Reference = resolved;
            info.WorldPoint = point ?? LocationPointOf(source) ?? info.WorldPoint ?? XYZ.Zero;
            info.ReferenceDirection = direction;
            ViewPlane plane = ViewPlane.FromView(view);
            info.ViewCoordinate = plane == null ? info.WorldPoint : plane.ToView(info.WorldPoint);
        }

        private static DimensionReferenceInfo Create(Document doc, View view, Reference reference, Element source, DimensionReferenceRole role, XYZ point, XYZ direction)
        {
            XYZ actualPoint = point ?? XYZ.Zero;
            ViewPlane plane = ViewPlane.FromView(view);
            return new DimensionReferenceInfo
            {
                ElementId = source == null ? (reference == null ? ElementId.InvalidElementId : reference.ElementId) : source.Id,
                ElementUniqueId = source == null ? string.Empty : source.UniqueId,
                Reference = reference,
                StableRepresentation = new DimensionApiAdapter().ConvertStableReference(doc, reference),
                ReferenceKind = reference == null ? string.Empty : reference.ElementReferenceType.ToString(),
                WorldPoint = actualPoint,
                ViewCoordinate = plane == null ? actualPoint : plane.ToView(actualPoint),
                ReferenceDirection = direction,
                SourceRole = role,
                GeometryFingerprint = GeometryFingerprint(source, actualPoint, direction),
                IsValid = reference != null
            };
        }

        private static IEnumerable<Reference> GetFamilyReferences(FamilyInstance family, DimensionReferenceRole role, DimensionAxis axis)
        {
            var result = new List<Reference>();
            FamilyInstanceReferenceType[] types = role == DimensionReferenceRole.CENTER || role == DimensionReferenceRole.COLUMN_CENTER
                ? new[] { axis == DimensionAxis.VERTICAL_IN_VIEW ? FamilyInstanceReferenceType.CenterFrontBack : FamilyInstanceReferenceType.CenterLeftRight }
                : new[] { FamilyInstanceReferenceType.Left, FamilyInstanceReferenceType.Right, FamilyInstanceReferenceType.Front, FamilyInstanceReferenceType.Back, FamilyInstanceReferenceType.StrongReference };
            foreach (FamilyInstanceReferenceType type in types)
            {
                try { result.AddRange(family.GetReferences(type) ?? new List<Reference>()); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] family references: " + ex.Message); }
            }
            foreach (string name in new[] { "Left", "Right", "Front", "Back", "Center (Left/Right)", "Center (Front/Back)" })
            {
                try { Reference named = family.GetReferenceByName(name); if (named != null) result.Add(named); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] named reference: " + ex.Message); }
            }
            var unique = new Dictionary<string, Reference>(StringComparer.Ordinal);
            foreach (Reference reference in result)
            {
                string stable = new DimensionApiAdapter().ConvertStableReference(family.Document, reference);
                if (!unique.ContainsKey(stable)) unique.Add(stable, reference);
            }
            return unique.Values;
        }

        private static IEnumerable<DimensionReferenceInfo> SelectCompatibleReferenceGroup(IList<DimensionReferenceInfo> input, XYZ requestedAxis, DimensionAxis axis)
        {
            IList<DimensionReferenceInfo> directional = input.Where(x => x.ReferenceDirection != null && !x.ReferenceDirection.IsZeroLength()).ToList();
            if (directional.Count == 0) return input;
            if (axis == DimensionAxis.HORIZONTAL_IN_VIEW || axis == DimensionAxis.VERTICAL_IN_VIEW)
            {
                IList<DimensionReferenceInfo> compatible = directional.Where(x => DimensionGeometryService.AreParallel(x.ReferenceDirection, requestedAxis)).ToList();
                return compatible.Count > 0 ? compatible : input.Where(x => x.ReferenceDirection == null).ToList();
            }
            var groups = new List<List<DimensionReferenceInfo>>();
            foreach (DimensionReferenceInfo info in directional)
            {
                List<DimensionReferenceInfo> group = groups.FirstOrDefault(x => DimensionGeometryService.AreParallel(x[0].ReferenceDirection, info.ReferenceDirection));
                if (group == null) { group = new List<DimensionReferenceInfo>(); groups.Add(group); }
                group.Add(info);
            }
            List<DimensionReferenceInfo> selected = groups.OrderByDescending(x => x.Select(y => y.ElementId).Distinct().Count()).ThenByDescending(x => x.Count).First();
            return selected;
        }

        private static void AddGeometryReferences(Document doc, View view, Element source, GeometryObject obj, Transform transform, DimensionReferenceRole role, XYZ dimensionAxis, IList<DimensionReferenceInfo> result)
        {
            GeometryInstance instance = obj as GeometryInstance;
            if (instance != null)
            {
                Transform nestedTransform = transform.Multiply(instance.Transform);
                GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                if (symbolGeometry != null) foreach (GeometryObject nested in symbolGeometry) AddGeometryReferences(doc, view, source, nested, nestedTransform, role, dimensionAxis, result);
                return;
            }
            Solid solid = obj as Solid;
            if (solid == null || solid.Faces == null || solid.Volume <= 0) return;
            ViewPlane plane = ViewPlane.FromView(view);
            foreach (Face face in solid.Faces)
            {
                PlanarFace planar = face as PlanarFace;
                if (planar == null || planar.Reference == null) continue;
                XYZ normal = transform.OfVector(planar.FaceNormal).Normalize();
                if (plane != null && Math.Abs(normal.DotProduct(plane.ViewDirection)) > 0.999) continue;
                result.Add(Create(doc, view, planar.Reference, source, role, transform.OfPoint(planar.Origin), normal));
            }
        }

        private static void ResolveReferenceGeometry(Document doc, Reference reference, Element source, out XYZ point, out XYZ direction)
        {
            point = null; direction = null;
            if (doc == null || reference == null || source == null) return;
            try
            {
                GeometryObject geometry = source.GetGeometryObjectFromReference(reference);
                PlanarFace face = geometry as PlanarFace;
                if (face != null) { point = face.Origin; direction = face.FaceNormal; return; }
                Edge edge = geometry as Edge;
                if (edge != null) { point = edge.Evaluate(0.5); direction = edge.ComputeDerivatives(0.5).BasisX; return; }
                Curve curve = geometry as Curve;
                if (curve != null) { point = Midpoint(curve); direction = curve.ComputeDerivatives(0.5, true).BasisX; }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] reference geometry: " + ex.Message); }
        }

        private static bool HasDimensionOverride(Dimension dimension) { return !string.IsNullOrEmpty(dimension.ValueOverride) || !string.IsNullOrEmpty(dimension.Prefix) || !string.IsNullOrEmpty(dimension.Suffix) || !string.IsNullOrEmpty(dimension.Above) || !string.IsNullOrEmpty(dimension.Below); }
        private static bool HasSegmentOverride(DimensionSegment segment) { return !string.IsNullOrEmpty(segment.ValueOverride) || !string.IsNullOrEmpty(segment.Prefix) || !string.IsNullOrEmpty(segment.Suffix) || !string.IsNullOrEmpty(segment.Above) || !string.IsNullOrEmpty(segment.Below); }
        private static string SemanticKey(DimensionReferenceInfo info) { return !string.IsNullOrWhiteSpace(info.StableRepresentation) ? info.StableRepresentation : string.Join(":", ElementIdValue(info.ElementId), info.ReferenceKind ?? string.Empty, PointToken(info.WorldPoint)); }
        private static string GeometryFingerprint(Element source, XYZ point, XYZ direction)
        {
            string location = string.Empty;
            Level level = source as Level;
            LocationPoint locationPoint = source == null ? null : source.Location as LocationPoint;
            LocationCurve locationCurve = source == null ? null : source.Location as LocationCurve;
            if (level != null) location = "L:" + Round(level.Elevation);
            else if (locationPoint != null) location = "P:" + PointToken(locationPoint.Point) + ":" + Round(locationPoint.Rotation);
            else if (locationCurve != null && locationCurve.Curve != null) location = "C:" + PointToken(locationCurve.Curve.GetEndPoint(0)) + ":" + PointToken(locationCurve.Curve.GetEndPoint(1));
            return string.Join("|", source == null ? string.Empty : source.UniqueId, source == null ? string.Empty : ElementIdValue(source.GetTypeId()).ToString(CultureInfo.InvariantCulture), location, PointToken(point), PointToken(direction));
        }
        private static XYZ LocationPointOf(Element element) { LocationPoint point = element == null ? null : element.Location as LocationPoint; if (point != null) return point.Point; LocationCurve curve = element == null ? null : element.Location as LocationCurve; return curve == null ? XYZ.Zero : Midpoint(curve.Curve); }
        private static string PointToken(XYZ point) { return point == null ? string.Empty : string.Join(",", Round(point.X), Round(point.Y), Round(point.Z)); }
        private static string Round(double value) { return (Math.Round(value * FingerprintPrecision) / FingerprintPrecision).ToString("R", CultureInfo.InvariantCulture); }
        private static long ElementIdValue(ElementId id) { return id == null ? -1 : id.Value; }
        private static XYZ Midpoint(Curve curve) { return curve == null ? XYZ.Zero : curve.Evaluate(0.5, true); }
    }
}
