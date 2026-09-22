using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.FilterManager.Models
{
    /// <summary>Document-local, normalized snapshot of a filter OverrideGraphicSettings value.</summary>
    public sealed class GraphicOverrideSnapshot
    {
        public string Fingerprint { get; set; } = string.Empty;
        public IDictionary<string, string> Fields { get; } = new SortedDictionary<string, string>();
        public OverrideGraphicSettings Overrides { get; set; }
        public bool IsEmpty { get; set; }
        public GraphicOverrideSnapshot Clone()
        {
            var copy = new GraphicOverrideSnapshot { Fingerprint = Fingerprint, Overrides = Overrides, IsEmpty = IsEmpty };
            foreach (var item in Fields) copy.Fields[item.Key] = item.Value;
            return copy;
        }
    }
}
