using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Logging;
using KhimTools.QuantityTakeoff.Models;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QsModelScanner
    {
        public static QsScanResult Scan(Document doc, View view, QsScanScope scope = QsScanScope.ActiveView,
            IEnumerable<ElementId> selectionIds = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var result = new QsScanResult { StartedAt = DateTime.Now, Scope = scope };
            FilteredElementCollector collector;
            switch (scope)
            {
                case QsScanScope.ActiveView:
                    if (view == null || view.Document != doc || view.IsTemplate)
                        throw new ArgumentException("An active, non-template view from the scanned document is required.", nameof(view));
                    collector = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType();
                    break;
                case QsScanScope.Selection:
                    if (selectionIds == null)
                        throw new ArgumentNullException(nameof(selectionIds), "Selection scope requires explicit selected element IDs.");
                    var ids = selectionIds.Where(id => id != null && id != ElementId.InvalidElementId).Distinct().ToList();
                    if (ids.Count == 0)
                    {
                        result.CompletedAt = DateTime.Now;
                        return result;
                    }
                    collector = new FilteredElementCollector(doc, ids);
                    collector = collector.WhereElementIsNotElementType();
                    break;
                case QsScanScope.EntireModel:
                    collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported QS scan scope.");
            }
            foreach (var e in collector)
            {
                if (e.Category == null) continue;
                var d = new QsElementData { ElementId = e.Id, UniqueId = e.UniqueId, CategoryId = e.Category.Id.IntegerValue,
                    CategoryName = e.Category.Name ?? "", TypeId = e.GetTypeId(), SourceDocumentTitle = doc.Title };
                var type = doc.GetElement(d.TypeId) as ElementType;
                d.TypeName = type?.Name ?? ""; d.FamilyName = (type as FamilySymbol)?.FamilyName ?? "";
                d.Mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                d.TypeMark = type?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString() ?? "";
                try { d.LevelId = e.LevelId; d.LevelName = doc.GetElement(d.LevelId) is Level l ? l.Name : ""; }
                catch (Exception ex) { KToolsLog.Current.Exception("QTO.Scan.Level", ex, "LEVEL_READ"); }
                try { d.VolumeInternal = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0; }
                catch (Exception ex) { KToolsLog.Current.Exception("QTO.Scan.Volume", ex, "VOLUME_READ"); }
                try { d.AreaInternal = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0; }
                catch (Exception ex) { KToolsLog.Current.Exception("QTO.Scan.Area", ex, "AREA_READ"); }
                try { d.LengthInternal = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0; }
                catch (Exception ex) { KToolsLog.Current.Exception("QTO.Scan.Length", ex, "LENGTH_READ"); }
                try { d.MaterialNames.AddRange(e.GetMaterialIds(false).Select(id => (doc.GetElement(id) as Material)?.Name).Where(x => !string.IsNullOrWhiteSpace(x))); }
                catch (Exception ex) { KToolsLog.Current.Exception("QTO.Scan.Materials", ex, "MATERIAL_READ"); }
                d.HasGeometry = d.VolumeInternal > 1e-9 || d.AreaInternal > 1e-9 || d.LengthInternal > 1e-9;
                result.Elements.Add(d);
            }
            result.CompletedAt = DateTime.Now;
            result.ReadinessScore = result.Elements.Count == 0 ? 0 : 100;
            return result;
        }
    }
}
