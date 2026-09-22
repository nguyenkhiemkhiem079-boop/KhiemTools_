using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterDescriptor
    {
        public string DisplayName { get; set; } = string.Empty;
        public string IdentityDisplay { get; set; } = string.Empty;
        public ParameterKey Key { get; set; }
        public StorageType StorageType { get; set; }
        public string DataTypeId { get; set; } = string.Empty;
        public bool IsTypeParameter { get; set; }
        public string ScopeDisplay { get { return IsTypeParameter ? "TYPE" : "INSTANCE"; } }
        public int TotalCount { get; set; }
        public int WritableCount { get; set; }
        public int ReadOnlyCount { get; set; }
        public int MissingCount { get; set; }
        public int CoverageCount { get; set; }
        public IList<string> DistinctValues { get; } = new List<string>();
        public string Coverage { get { return TotalCount == 0 ? "0%" : (CoverageCount * 100d / TotalCount).ToString("0.#") + "%"; } }
        public override string ToString() { return DisplayName + " [" + ScopeDisplay + " | " + IdentityDisplay + "] " + Coverage; }
    }
}
