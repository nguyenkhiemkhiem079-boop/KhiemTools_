using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;
namespace KhimTools.DimensionTools.Core
{
    public sealed class DimensionPlanContext
    {
        public DimensionOperation Operation { get; set; }
        public ElementId ViewId { get; set; }
        public string ViewUniqueId { get; set; }
        public IList<ElementId> ElementIds { get; } = new List<ElementId>();
        public IList<string> ElementUniqueIds { get; } = new List<string>();
        public IList<ElementId> DimensionIds { get; } = new List<ElementId>();
        public IList<string> DimensionUniqueIds { get; } = new List<string>();
        public DimensionOptions Options { get; set; } = new DimensionOptions();
        public static DimensionPlanContext From(DimensionContext source)
        {
            var result = new DimensionPlanContext { Operation = source == null ? DimensionOperation.GENERAL : source.Operation, ViewId = source == null || source.View == null ? ElementId.InvalidElementId : source.View.Id, ViewUniqueId = source == null || source.View == null ? string.Empty : source.View.UniqueId, Options = CloneOptions(source == null ? null : source.Options) };
            if (source == null) return result;
            foreach (ElementId id in source.ElementIds) { result.ElementIds.Add(id); Element e = source.Document == null ? null : source.Document.GetElement(id); result.ElementUniqueIds.Add(e == null ? string.Empty : e.UniqueId ?? string.Empty); }
            foreach (ElementId id in source.DimensionIds) { result.DimensionIds.Add(id); Element e = source.Document == null ? null : source.Document.GetElement(id); result.DimensionUniqueIds.Add(e == null ? string.Empty : e.UniqueId ?? string.Empty); }
            return result;
        }
        public static DimensionOptions CloneOptions(DimensionOptions value)
        {
            value = value ?? new DimensionOptions();
            return new DimensionOptions { Axis = value.Axis, OffsetMillimeters = value.OffsetMillimeters, ToleranceMillimeters = value.ToleranceMillimeters, HorizontalMoveMillimeters = value.HorizontalMoveMillimeters, VerticalMoveMillimeters = value.VerticalMoveMillimeters, BoundaryIndex = value.BoundaryIndex, DimensionTypeId = value.DimensionTypeId, UseOuterDimension = value.UseOuterDimension, IncludeOriginal = value.IncludeOriginal, AllowPinned = value.AllowPinned, ReferenceStrategy = value.ReferenceStrategy };
        }
    }
}
