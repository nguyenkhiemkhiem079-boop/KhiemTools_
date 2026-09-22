using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.DimensionTools.Models;

namespace KhimTools.DimensionTools.Core
{
    public static class DimensionStyleService
    {
        public static DimensionType Resolve(Document doc, DimensionOptions options) { if (doc == null) return null; if (options != null && options.DimensionTypeId != null && options.DimensionTypeId != ElementId.InvalidElementId) return doc.GetElement(options.DimensionTypeId) as DimensionType; return new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>().OrderBy(x => x.Id.IntegerValue).FirstOrDefault(); }
        public static ElementId ResolveId(Document doc, DimensionOptions options) { DimensionType type = Resolve(doc, options); return type == null ? ElementId.InvalidElementId : type.Id; }
        public static bool IsValid(Document doc, ElementId typeId) { return doc != null && typeId != null && typeId != ElementId.InvalidElementId && doc.GetElement(typeId) is DimensionType; }
    }
}
