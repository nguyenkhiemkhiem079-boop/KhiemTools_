using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterCollectorService
    {
        public static IList<View> CollectCompatibleViews(Document doc)
        {
            if (doc == null) return new List<View>();
            return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => v != null && !(v is ViewSheet) && v.ViewType != ViewType.Schedule)
                .OrderBy(v => v.IsTemplate ? 1 : 0).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static IList<FilterDefinitionInfo> CollectFilterDefinitions(Document doc)
        {
            var result = new List<FilterDefinitionInfo>();
            if (doc == null) return result;
            foreach (ParameterFilterElement filter in new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>())
                result.Add(Create(filter, FilterDefinitionType.ParameterFilter, true));
            foreach (SelectionFilterElement filter in new FilteredElementCollector(doc).OfClass(typeof(SelectionFilterElement)).Cast<SelectionFilterElement>())
                result.Add(Create(filter, FilterDefinitionType.SelectionFilter, false));
            return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id.IntegerValue).ToList();
        }

        public static FilterDefinitionInfo Describe(Document doc, ElementId filterId)
        {
            if (doc == null || filterId == null || filterId == ElementId.InvalidElementId) return null;
            Element element = doc.GetElement(filterId);
            if (element is ParameterFilterElement) return Create(element, FilterDefinitionType.ParameterFilter, true);
            if (element is SelectionFilterElement) return Create(element, FilterDefinitionType.SelectionFilter, false);
            return element == null ? null : Create(element, FilterDefinitionType.Unsupported, false);
        }

        private static FilterDefinitionInfo Create(Element element, FilterDefinitionType type, bool supportsEnabled)
        {
            return new FilterDefinitionInfo { Id = element.Id, UniqueId = element.UniqueId ?? string.Empty, Name = element.Name ?? string.Empty, DefinitionType = type, SupportsEnabledState = supportsEnabled };
        }
    }
}
