using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterElementCollector
    {
        public static IList<Element> Collect(Document doc, ParameterManagerRequest request)
        {
            var result = new List<Element>();
            if (doc == null || request == null) return result;
            if (request.Scope == ElementScopeMode.CURRENT_SELECTION)
            {
                foreach (ElementId id in request.ElementIds ?? new List<ElementId>()) { Element e = doc.GetElement(id); if (e != null && e.GetTypeId() != null && e.GetTypeId() != ElementId.InvalidElementId) result.Add(e); }
            }
            else if (request.Scope == ElementScopeMode.CURRENT_VIEW || request.Scope == ElementScopeMode.SELECTED_CATEGORY_IN_VIEW)
            {
                ElementId viewId = request.ActiveViewId == null ? ElementId.InvalidElementId : request.ActiveViewId;
                if (viewId != ElementId.InvalidElementId)
                {
                    FilteredElementCollector collector = new FilteredElementCollector(doc, viewId).WhereElementIsNotElementType();
                    foreach (Element e in collector)
                    {
                        if (e == null || e.Category == null || e.GetTypeId() == null || e.GetTypeId() == ElementId.InvalidElementId) continue;
                        if (request.Scope == ElementScopeMode.SELECTED_CATEGORY_IN_VIEW && request.CategoryId != null && request.CategoryId != ElementId.InvalidElementId && e.Category.Id != request.CategoryId) continue;
                        result.Add(e);
                    }
                }
            }
            else if (request.Scope == ElementScopeMode.WHOLE_PROJECT && request.Options != null && request.Options.ConfirmWholeProject)
            {
                foreach (Element e in new FilteredElementCollector(doc).WhereElementIsNotElementType())
                    if (e != null && e.Category != null && e.GetTypeId() != null && e.GetTypeId() != ElementId.InvalidElementId) result.Add(e);
            }
            if (request.Options != null && request.Options.Filter != null) result = result.Where(request.Options.Filter).ToList();
            var unique = new Dictionary<int, Element>();
            foreach (Element e in result) if (!unique.ContainsKey(e.Id.IntegerValue)) unique.Add(e.Id.IntegerValue, e);
            return unique.Values.OrderBy(e => e.Id.IntegerValue).ToList();
        }

        public static IList<Element> ResolveTargets(Document doc, ParameterManagerRequest request)
        {
            IList<Element> elements = Collect(doc, request);
            if (request == null || request.ParameterScope != ParameterScopeMode.TYPE) return elements;
            var types = new Dictionary<int, Element>();
            foreach (Element e in elements) { ElementId typeId = e.GetTypeId(); Element type = typeId == null ? null : doc.GetElement(typeId); if (type != null && !types.ContainsKey(type.Id.IntegerValue)) types.Add(type.Id.IntegerValue, type); }
            return types.Values.OrderBy(e => e.Id.IntegerValue).ToList();
        }

        public static IList<ElementId> CollectCategories(Document doc, ElementId viewId)
        {
            var ids = new Dictionary<int, ElementId>();
            if (doc == null || viewId == null || viewId == ElementId.InvalidElementId) return ids.Values.ToList();
            foreach (Element e in new FilteredElementCollector(doc, viewId).WhereElementIsNotElementType()) if (e != null && e.Category != null && !ids.ContainsKey(e.Category.Id.IntegerValue)) ids.Add(e.Category.Id.IntegerValue, e.Category.Id);
            return ids.Values.OrderBy(x => x.IntegerValue).ToList();
        }
    }
}
