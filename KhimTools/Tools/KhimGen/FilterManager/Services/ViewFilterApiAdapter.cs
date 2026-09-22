using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Services
{
    /// <summary>Small compatibility surface for filter APIs that vary by Revit release.</summary>
    public static class ViewFilterApiAdapter
    {
        public static ICollection<ElementId> GetAppliedFilters(View view)
        {
            return view == null ? new List<ElementId>() : view.GetFilters();
        }

        public static IList<ElementId> GetOrderedFilters(View view)
        {
            if (view == null) return new List<ElementId>();
            MethodInfo method = Find(view, "GetOrderedFilters", 0);
            if (method != null)
            {
                try
                {
                    object value = method.Invoke(view, null);
                    var ids = value as IEnumerable<ElementId>;
                    if (ids != null) return ids.ToList();
                }
                catch (Exception) { /* capability is reported below */ }
            }
            return GetAppliedFilters(view).ToList();
        }

        public static bool IsApplied(View view, ElementId filterId) { return GetAppliedFilters(view).Any(x => x == filterId); }
        public static void AddFilter(View view, ElementId filterId) { view.AddFilter(filterId); }
        public static void RemoveFilter(View view, ElementId filterId) { view.RemoveFilter(filterId); }
        public static bool GetVisibility(View view, ElementId filterId) { return view.GetFilterVisibility(filterId); }
        public static void SetVisibility(View view, ElementId filterId, bool visible) { view.SetFilterVisibility(filterId, visible); }
        public static OverrideGraphicSettings GetOverrides(View view, ElementId filterId) { return view.GetFilterOverrides(filterId); }
        public static void SetOverrides(View view, ElementId filterId, OverrideGraphicSettings overrides) { view.SetFilterOverrides(filterId, overrides ?? new OverrideGraphicSettings()); }

        public static bool SupportsEnabledState(View view)
        {
            return Find(view, "GetIsFilterEnabled", 1) != null || Find(view, "IsFilterEnabled", 1) != null;
        }

        public static bool GetEnabled(View view, ElementId filterId, out bool supported)
        {
            MethodInfo method = Find(view, "GetIsFilterEnabled", 1) ?? Find(view, "IsFilterEnabled", 1);
            supported = method != null;
            if (method == null) return true;
            try { return Convert.ToBoolean(method.Invoke(view, new object[] { filterId })); }
            catch (Exception) { supported = false; return true; }
        }

        public static bool SetEnabled(View view, ElementId filterId, bool enabled, out bool supported)
        {
            MethodInfo method = Find(view, "SetIsFilterEnabled", 2) ?? Find(view, "SetFilterEnabled", 2);
            supported = method != null;
            if (method == null) return false;
            try { method.Invoke(view, new object[] { filterId, enabled }); return true; }
            catch (Exception) { supported = false; return false; }
        }

        public static bool SetOrder(View view, IList<ElementId> orderedFilterIds)
        {
            MethodInfo method = Find(view, "SetFilterOrder", 1);
            if (method == null) return false;
            try
            {
                Type parameterType = method.GetParameters()[0].ParameterType;
                object value = orderedFilterIds.ToList();
                if (!parameterType.IsInstanceOfType(value) && parameterType.IsArray) value = orderedFilterIds.ToArray();
                method.Invoke(view, new[] { value }); return true;
            }
            catch (Exception) { return false; }
        }

        public static bool SupportsFilterOrderRead(View view) { return Find(view, "GetOrderedFilters", 0) != null; }
        public static bool SupportsFilterOrderWrite(View view) { return Find(view, "SetFilterOrder", 1) != null; }

        private static MethodInfo Find(View view, string name, int parameterCount)
        {
            if (view == null) return null;
            return view.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null,
                Enumerable.Repeat(typeof(ElementId), parameterCount).ToArray(), null)
                ?? view.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal) && m.GetParameters().Length == parameterCount);
        }
    }
}
