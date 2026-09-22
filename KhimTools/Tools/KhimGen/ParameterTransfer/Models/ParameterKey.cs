using System;
using Autodesk.Revit.DB;

namespace KhimTools.ParameterTransfer.Models
{
    public enum ParameterIdentityKind { BUILT_IN, SHARED_GUID, DEFINITION_NAME }

    public sealed class ParameterKey : IEquatable<ParameterKey>
    {
        public ParameterIdentityKind IdentityKind { get; set; }
        public BuiltInParameter BuiltInParameter { get; set; } = BuiltInParameter.INVALID;
        public Guid SharedGuid { get; set; }
        public string DefinitionName { get; set; } = string.Empty;
        public StorageType StorageType { get; set; }
        public string DataTypeId { get; set; } = string.Empty;

        public bool Equals(ParameterKey other)
        {
            if (other == null || IdentityKind != other.IdentityKind || StorageType != other.StorageType || !string.Equals(DataTypeId, other.DataTypeId, StringComparison.Ordinal)) return false;
            if (IdentityKind == ParameterIdentityKind.BUILT_IN) return BuiltInParameter == other.BuiltInParameter;
            if (IdentityKind == ParameterIdentityKind.SHARED_GUID) return SharedGuid == other.SharedGuid;
            return string.Equals(DefinitionName, other.DefinitionName, StringComparison.OrdinalIgnoreCase);
        }
        public override bool Equals(object obj) { return Equals(obj as ParameterKey); }
        public override int GetHashCode() { return Tuple.Create(IdentityKind, BuiltInParameter, SharedGuid, DefinitionName == null ? string.Empty : DefinitionName.ToUpperInvariant(), StorageType, DataTypeId ?? string.Empty).GetHashCode(); }
        public override string ToString() { return IdentityKind + ":" + (IdentityKind == ParameterIdentityKind.BUILT_IN ? BuiltInParameter.ToString() : IdentityKind == ParameterIdentityKind.SHARED_GUID ? SharedGuid.ToString() : DefinitionName); }
    }
}
