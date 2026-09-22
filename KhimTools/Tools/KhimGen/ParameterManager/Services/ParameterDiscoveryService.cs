using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterDiscoveryService
    {
        public static IList<ParameterDescriptor> Discover(Document doc, IEnumerable<Element> elements, ParameterScopeMode scope)
        {
            var catalog = new Dictionary<string, ParameterDescriptor>(StringComparer.OrdinalIgnoreCase);
            if (doc == null || elements == null) return catalog.Values.ToList();
            var targets = new List<Element>();
            if (scope == ParameterScopeMode.TYPE)
            {
                var typeIds = new HashSet<int>();
                foreach (Element instance in elements) { ElementId typeId = instance == null ? null : instance.GetTypeId(); Element type = typeId == null ? null : doc.GetElement(typeId); if (type != null && typeIds.Add(type.Id.IntegerValue)) targets.Add(type); }
            }
            else targets.AddRange(elements);
            foreach (Element element in targets)
            {
                if (element == null) continue;
                Element source = scope == ParameterScopeMode.TYPE ? element : element;
                foreach (Parameter parameter in source.Parameters)
                {
                    if (parameter == null || parameter.Definition == null) continue;
                    ParameterKey key = ParameterTransferService.CreateKey(parameter);
                    string identity = key.ToString();
                    string mapKey = identity + "|" + (scope == ParameterScopeMode.TYPE ? "TYPE" : "INSTANCE");
                    ParameterDescriptor descriptor;
                    if (!catalog.TryGetValue(mapKey, out descriptor))
                    {
                        descriptor = new ParameterDescriptor { Key = key, DisplayName = parameter.Definition.Name ?? string.Empty, IdentityDisplay = identity, StorageType = parameter.StorageType, DataTypeId = ParameterTransferService.SafeDataType(parameter), IsTypeParameter = scope == ParameterScopeMode.TYPE };
                        catalog.Add(mapKey, descriptor);
                    }
                    descriptor.TotalCount++;
                    if (!parameter.IsReadOnly) descriptor.WritableCount++; else descriptor.ReadOnlyCount++;
                    ParameterValueSnapshot snapshot = ParameterTransferService.Snapshot(parameter);
                    if (snapshot != null) { descriptor.CoverageCount++; string value = snapshot.DisplayValue ?? string.Empty; if (!descriptor.DistinctValues.Contains(value)) descriptor.DistinctValues.Add(value); }
                }
            }
            var byName = catalog.Values.GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var group in byName.Where(g => g.Count() > 1)) foreach (ParameterDescriptor item in group) item.DisplayName = item.DisplayName + " [" + item.IdentityDisplay + "]";
            foreach (ParameterDescriptor item in catalog.Values) item.MissingCount = Math.Max(0, targets.Count - item.TotalCount);
            return catalog.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.IdentityDisplay, StringComparer.Ordinal).ToList();
        }

        public static ParameterDescriptor Find(IList<ParameterDescriptor> catalog, ParameterKey key)
        {
            return catalog == null || key == null ? null : catalog.FirstOrDefault(x => key.Equals(x.Key));
        }
    }
}
