using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsModelScanner
    {
        public static QsScanResult Scan(Document doc, View view, QsScanScope scope = QsScanScope.ActiveView)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var result = new QsScanResult { StartedAt = DateTime.Now, Scope = scope };
            FilteredElementCollector collector = scope == QsScanScope.ActiveView && view != null
                ? new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
                : new FilteredElementCollector(doc).WhereElementIsNotElementType();
            foreach (var e in collector)
            {
                if (e.Category == null) continue;
                var d = new QsElementData { ElementId = e.Id, UniqueId = e.UniqueId, CategoryId = e.Category.Id.IntegerValue,
                    CategoryName = e.Category.Name ?? "", TypeId = e.GetTypeId(), SourceDocumentTitle = doc.Title };
                var type = doc.GetElement(d.TypeId) as ElementType;
                d.TypeName = type?.Name ?? ""; d.FamilyName = (type as FamilySymbol)?.FamilyName ?? "";
                d.Mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                d.TypeMark = type?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString() ?? "";
                try { d.LevelId = e.LevelId; d.LevelName = doc.GetElement(d.LevelId) is Level l ? l.Name : ""; } catch { }
                try { d.VolumeInternal = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0; } catch { }
                try { d.AreaInternal = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0; } catch { }
                try { d.LengthInternal = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0; } catch { }
                try { d.MaterialNames.AddRange(e.GetMaterialIds(false).Select(id => (doc.GetElement(id) as Material)?.Name).Where(x => !string.IsNullOrWhiteSpace(x))); } catch { }
                d.HasGeometry = d.VolumeInternal > 1e-9 || d.AreaInternal > 1e-9 || d.LengthInternal > 1e-9;
                result.Elements.Add(d);
            }
            result.CompletedAt = DateTime.Now;
            result.ReadinessScore = result.Elements.Count == 0 ? 0 : 100;
            return result;
        }
    }
}
