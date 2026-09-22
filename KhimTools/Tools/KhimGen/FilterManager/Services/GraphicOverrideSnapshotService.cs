using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class GraphicOverrideSnapshotService
    {
        public static GraphicOverrideSnapshot Capture(OverrideGraphicSettings overrides)
        {
            var snapshot = new GraphicOverrideSnapshot { Overrides = overrides, IsEmpty = overrides == null };
            if (overrides == null) { snapshot.Fingerprint = "<null>"; return snapshot; }
            foreach (PropertyInfo property in overrides.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                try { snapshot.Fields[property.Name] = Format(property.GetValue(overrides, null)); }
                catch (Exception) { snapshot.Fields[property.Name] = "<unavailable>"; }
            }
            snapshot.Fingerprint = string.Join("|", snapshot.Fields.Select(x => x.Key + "=" + x.Value));
            return snapshot;
        }

        public static string Fingerprint(OverrideGraphicSettings overrides) { return Capture(overrides).Fingerprint; }
        public static bool Equivalent(GraphicOverrideSnapshot left, GraphicOverrideSnapshot right)
        {
            return string.Equals(left == null ? string.Empty : left.Fingerprint, right == null ? string.Empty : right.Fingerprint, StringComparison.Ordinal);
        }

        private static string Format(object value)
        {
            if (value == null) return "<null>";
            ElementId id = value as ElementId; if (id != null) return "ElementId:" + id.IntegerValue.ToString(CultureInfo.InvariantCulture);
            Color color = value as Color; if (color != null) return "Color:" + color.Red + "," + color.Green + "," + color.Blue;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
