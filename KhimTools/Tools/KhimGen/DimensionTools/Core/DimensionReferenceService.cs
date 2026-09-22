using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionReferenceService
    {
        public static IList<DimensionReferenceInfo> FromGrid(Document doc, View view, IEnumerable<Grid> grids)
        {
            var result = new List<DimensionReferenceInfo>(); if (grids == null) return result;
            foreach (Grid grid in grids) { if (grid == null || grid.Curve == null) continue; Reference reference = new Reference(grid); result.Add(Create(doc, view, reference, grid, DimensionReferenceRole.GRID, Midpoint(grid.Curve))); }
            return NormalizeAndSort(view, result, view == null ? XYZ.BasisX : view.RightDirection);
        }
        public static IList<DimensionReferenceInfo> FromElements(Document doc, View view, IEnumerable<Element> elements, DimensionReferenceRole role, DimensionAxis axis)
        {
            var result = new List<DimensionReferenceInfo>(); if (elements == null) return result; ViewPlane plane = ViewPlane.FromView(view); Options options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine, View = view };
            foreach (Element element in elements)
            {
                if (element == null) continue; FamilyInstance family = element as FamilyInstance; if (family != null) { IList<Reference> familyRefs = TryGetFamilyReferences(family); foreach (Reference reference in familyRefs) result.Add(Create(doc, view, reference, element, role, element.Location is LocationPoint ? ((LocationPoint)element.Location).Point : Midpoint((element.Location as LocationCurve)?.Curve))); }
                GeometryElement geometry = null; try { geometry = element.get_Geometry(options); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] geometry references: " + ex.Message); }
                if (geometry != null) foreach (GeometryObject obj in geometry) AddGeometryReferences(doc, view, element, obj, role, result);
            }
            XYZ sortAxis = axis == DimensionAxis.VERTICAL_IN_VIEW ? (plane == null ? XYZ.BasisY : plane.UpDirection) : (plane == null ? XYZ.BasisX : plane.RightDirection); return NormalizeAndSort(view, result, sortAxis);
        }
        public static IList<DimensionReferenceInfo> FromDimension(Document doc, View view, Dimension dimension)
        {
            var result = new List<DimensionReferenceInfo>(); if (dimension == null) return result; var adapter = new DimensionApiAdapter(); foreach (Reference reference in adapter.ReadReferences(dimension)) { Element element = reference == null ? null : doc.GetElement(reference.ElementId); result.Add(Create(doc, view, reference, element, DimensionReferenceRole.GENERIC_FACE, XYZ.Zero)); } return NormalizeAndSort(view, result, view == null ? XYZ.BasisX : view.RightDirection);
        }
        public static Reference ResolveStableReference(Document doc, DimensionReferenceInfo info) { return info == null ? null : string.IsNullOrWhiteSpace(info.StableRepresentation) ? info.Reference : new DimensionApiAdapter().ResolveStableReference(doc, info.StableRepresentation); }
        public static DimensionSnapshot CaptureSnapshot(Document doc, View view, Dimension dimension) { var snapshot = new DimensionSnapshot { DimensionId = dimension.Id, UniqueId = dimension.UniqueId, ViewId = dimension.OwnerViewId, DimensionTypeId = dimension.GetTypeId(), Curve = dimension.Curve as Line, Pinned = dimension.Pinned, Locked = dimension.IsLocked, HasManualOverride = !string.IsNullOrEmpty(dimension.ValueOverride) }; snapshot.References.AddRange(FromDimension(doc, view, dimension)); foreach (DimensionReferenceInfo info in snapshot.References) snapshot.StableReferences.Add(info.StableRepresentation); foreach (DimensionSegment segment in dimension.Segments) { snapshot.SegmentValues.Add(segment.Value.HasValue ? segment.Value.Value : 0); snapshot.TextPositions.Add(segment.TextPosition); } return snapshot; }
        public static IList<DimensionReferenceInfo> NormalizeAndSort(View view, IEnumerable<DimensionReferenceInfo> input, XYZ axis)
        {
            ViewPlane plane = ViewPlane.FromView(view); var map = new Dictionary<string, DimensionReferenceInfo>(StringComparer.Ordinal); foreach (DimensionReferenceInfo info in input ?? Enumerable.Empty<DimensionReferenceInfo>()) { if (info == null || info.Reference == null) continue; string key = string.IsNullOrWhiteSpace(info.StableRepresentation) ? info.ElementId.IntegerValue + ":" + info.Reference.ElementReferenceType + ":" + (info.WorldPoint ?? XYZ.Zero) : info.StableRepresentation; if (!map.ContainsKey(key)) { info.ProjectedPosition = plane == null ? (info.WorldPoint ?? XYZ.Zero).DotProduct(axis) : plane.ToView(info.WorldPoint ?? XYZ.Zero).DotProduct(axis); map.Add(key, info); } } return map.Values.OrderBy(x => x.ProjectedPosition).ToList();
        }
        private static DimensionReferenceInfo Create(Document doc, View view, Reference reference, Element source, DimensionReferenceRole role, XYZ point) { var adapter = new DimensionApiAdapter(); return new DimensionReferenceInfo { ElementId = source == null ? (reference == null ? ElementId.InvalidElementId : reference.ElementId) : source.Id, ElementUniqueId = source == null ? string.Empty : source.UniqueId, Reference = reference, StableRepresentation = adapter.ConvertStableReference(doc, reference), ReferenceKind = reference == null ? string.Empty : reference.ElementReferenceType.ToString(), WorldPoint = point ?? XYZ.Zero, ViewCoordinate = ViewPlane.FromView(view)?.ToView(point ?? XYZ.Zero) ?? (point ?? XYZ.Zero), SourceRole = role, IsValid = reference != null }; }
        private static IList<Reference> TryGetFamilyReferences(FamilyInstance family) { try { return family.GetReferences(FamilyInstanceReferenceType.CenterLeftRight) ?? new List<Reference>(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] family references: " + ex.Message); return new List<Reference>(); } }
        private static void AddGeometryReferences(Document doc, View view, Element source, GeometryObject obj, DimensionReferenceRole role, IList<DimensionReferenceInfo> result)
        {
            GeometryInstance instance = obj as GeometryInstance; if (instance != null) { foreach (GeometryObject nested in instance.GetInstanceGeometry()) AddGeometryReferences(doc, view, source, nested, role, result); return; }
            Solid solid = obj as Solid; if (solid == null || solid.Faces == null) return; foreach (Face face in solid.Faces) { PlanarFace planar = face as PlanarFace; if (planar == null || planar.Reference == null) continue; result.Add(Create(doc, view, planar.Reference, source, role, planar.Origin)); }
        }
        private static XYZ Midpoint(Curve curve) { return curve == null ? XYZ.Zero : curve.Evaluate(0.5, true); }
    }
}
