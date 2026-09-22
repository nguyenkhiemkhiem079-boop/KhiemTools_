using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerFilterService
    {
        public static IList<Element> Apply(IEnumerable<Element> source, string search, Func<Element, bool> predicate)
        {
            IEnumerable<Element> values = source ?? Enumerable.Empty<Element>();
            if (!string.IsNullOrWhiteSpace(search)) values = values.Where(e => ((e.Name ?? string.Empty) + " " + (e.Category == null ? string.Empty : e.Category.Name)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (predicate != null) values = values.Where(predicate);
            return values.OrderBy(e => e.Id.IntegerValue).ToList();
        }
    }
}
