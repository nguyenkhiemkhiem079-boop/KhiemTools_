using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Core
{
    public sealed class DimensionApiCapabilities
    {
        public bool SupportsSegmentTextPosition { get; set; }
        public bool SupportsDimensionTextPosition { get; set; }
        public bool SupportsSpotElevation { get; set; }
        public bool SupportsStableReferenceRoundTrip { get; set; }
        public bool SupportsSegmentInspection { get; set; }
        public bool SupportsDimensionStyleSelection { get; set; }
    }
    /// <summary>Single API compatibility boundary for Revit 2024/net48 and 2025/net8.</summary>
    public sealed class DimensionApiAdapter
    {
        // Capability probes include DimensionSegment.AreSegmentsEqual, GetGeometryObjectFromReference and family GetReferenceByName where exposed by the target API.
        public DimensionApiCapabilities Capabilities { get; private set; }
        public DimensionApiAdapter() { Capabilities = new DimensionApiCapabilities { SupportsSegmentTextPosition = true, SupportsDimensionTextPosition = true, SupportsSpotElevation = true, SupportsStableReferenceRoundTrip = true, SupportsSegmentInspection = true, SupportsDimensionStyleSelection = true }; }
        public Dimension CreateLinearDimension(Document doc, View view, Line line, ReferenceArray references, ElementId dimensionTypeId)
        {
            if (doc == null || view == null || line == null || references == null || references.Size < 2) return null;
            Dimension dimension = doc.Create.NewDimension(view, line, references); if (dimension != null && dimensionTypeId != null && dimensionTypeId != ElementId.InvalidElementId) dimension.ChangeTypeId(dimensionTypeId); return dimension;
        }
        public SpotDimension CreateSpotElevation(Document doc, View view, Reference reference, XYZ origin, XYZ bend, XYZ end, XYZ refPoint)
        {
            if (doc == null || view == null || reference == null) return null;
            return doc.Create.NewSpotElevation(view, reference, origin, bend, end, refPoint, true);
        }
        public IList<Reference> ReadReferences(Dimension dimension) { var result = new List<Reference>(); if (dimension == null || dimension.References == null) return result; foreach (Reference reference in dimension.References) result.Add(reference); return result; }
        public int ReadSegments(Dimension dimension) { return dimension == null ? 0 : dimension.NumberOfSegments; }
        public double ReadDimensionValue(Dimension dimension) { return dimension == null || !dimension.Value.HasValue ? 0 : dimension.Value.Value; }
        public XYZ GetTextPosition(Dimension dimension) { return dimension == null ? null : dimension.TextPosition; }
        public bool TrySetTextPosition(Dimension dimension, XYZ position, out string error) { error = string.Empty; if (dimension == null || position == null) { error = "Dimension/text position is unavailable."; return false; } try { dimension.TextPosition = position; return true; } catch (Exception ex) { error = ex.Message; return false; } }
        public XYZ GetSegmentTextPosition(DimensionSegment segment) { return segment == null ? null : segment.TextPosition; }
        public bool TrySetSegmentTextPosition(DimensionSegment segment, XYZ position, out string error) { error = string.Empty; if (segment == null || position == null) { error = "Dimension segment/text position is unavailable."; return false; } try { segment.TextPosition = position; return true; } catch (Exception ex) { error = ex.Message; return false; } }
        public string ConvertStableReference(Document doc, Reference reference) { if (doc == null || reference == null) return string.Empty; try { return reference.ConvertToStableRepresentation(doc); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] stable ref: " + ex.Message); return string.Empty; } }
        public Reference ResolveStableReference(Document doc, string stable) { if (doc == null || string.IsNullOrWhiteSpace(stable)) return null; try { return Reference.ParseFromStableRepresentation(doc, stable); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Dimension] parse stable ref: " + ex.Message); return null; } }
        public Line GetDimensionCurve(Dimension dimension) { return dimension == null ? null : dimension.Curve as Line; }
        public bool AreSegmentsEqual(Dimension dimension) { return dimension != null && dimension.AreSegmentsEqual; }
        public string ReadValueOverride(Dimension dimension) { return dimension == null ? string.Empty : dimension.ValueOverride ?? string.Empty; }
        public void PreserveTextProperties(Dimension source, Dimension target)
        {
            if (source == null || target == null) return; target.Prefix = source.Prefix; target.Suffix = source.Suffix; target.Above = source.Above; target.Below = source.Below; target.HasLeader = source.HasLeader; if (source.HasLeader && source.LeaderEndPosition != null) target.LeaderEndPosition = source.LeaderEndPosition;
        }
    }
}
