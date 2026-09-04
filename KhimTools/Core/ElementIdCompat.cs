using System;
using System.Reflection;
using Autodesk.Revit.DB;

namespace KhimTools.Core
{
    /// <summary>
    /// Provides a single, version-safe way to read the numeric value of an
    /// <see cref="ElementId"/> across Revit API versions (Revit 2020-2026+).
    /// Completely prevents MissingMethodException: Int64 Autodesk.Revit.DB.ElementId.get_Value().
    /// </summary>
    public static class ElementIdCompat
    {
        private static readonly Func<ElementId, long> GetIdValueFunc = InitGetIdValueFunc();

        private static Func<ElementId, long> InitGetIdValueFunc()
        {
            try
            {
                var propValue = typeof(ElementId).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (propValue != null)
                {
                    return id => (long)propValue.GetValue(id, null);
                }

                var propIntValue = typeof(ElementId).GetProperty("IntegerValue", BindingFlags.Public | BindingFlags.Instance);
                if (propIntValue != null)
                {
                    return id => (int)propIntValue.GetValue(id, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K-TOOLS ElementIdCompat] Lỗi reflection khởi tạo Value/IntegerValue: {ex.Message}");
            }

            return id => -1;
        }

        /// <summary>
        /// Returns the numeric value of the given <see cref="ElementId"/> as a <see cref="long"/>.
        /// Safe for all Revit versions (2020 through 2026+).
        /// </summary>
        public static long ToLongValue(this ElementId id)
        {
            if (id == null) return -1;
            return GetIdValueFunc(id);
        }

        /// <summary>
        /// Safe comparison for Category BuiltInCategory matching across Revit versions.
        /// </summary>
        public static bool IsCategory(this Category category, BuiltInCategory builtInCat)
        {
            if (category == null) return false;
            return category.Id.ToLongValue() == (long)builtInCat;
        }
    }
}
