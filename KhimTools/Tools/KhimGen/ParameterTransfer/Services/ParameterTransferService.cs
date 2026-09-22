using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterTransfer.Services
{
    /// <summary>Neutral typed parameter identity, snapshot and write boundary shared by sheet-scoped tools.</summary>
    public static class ParameterTransferService
    {
        public static ParameterTransferPlan Describe(Parameter parameter, Element sourceElement, Func<Parameter, bool> protectedPolicy = null)
        {
            if (parameter == null) return null;
            ParameterKey key = CreateKey(parameter);
            bool protectedValue = protectedPolicy != null && protectedPolicy(parameter);
            return new ParameterTransferPlan { Key = key, ParameterName = parameter.Definition == null ? string.Empty : parameter.Definition.Name ?? string.Empty, SourceElementId = sourceElement == null ? ElementId.InvalidElementId : sourceElement.Id, SourceValue = Snapshot(parameter), Protected = protectedValue, Selected = !protectedValue, CanSync = !parameter.IsReadOnly && !protectedValue, Reason = protectedValue ? "Protected by source policy." : parameter.IsReadOnly ? "Source parameter is read-only." : string.Empty };
        }

        public static ParameterKey CreateKey(Parameter parameter)
        {
            var key = new ParameterKey { StorageType = parameter == null ? StorageType.None : parameter.StorageType };
            if (parameter == null) return key;
            key.DataTypeId = SafeDataType(parameter);
            try
            {
                int raw = parameter.Id == null ? 0 : parameter.Id.IntegerValue;
                if (raw < 0 && Enum.IsDefined(typeof(BuiltInParameter), raw))
                {
                    key.IdentityKind = ParameterIdentityKind.BUILT_IN;
                    key.BuiltInParameter = (BuiltInParameter)raw;
                    return key;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] built-in identity probe: " + ex.Message); }
            try
            {
                if (parameter.IsShared)
                {
                    Guid guid = parameter.GUID;
                    if (guid != Guid.Empty) { key.IdentityKind = ParameterIdentityKind.SHARED_GUID; key.SharedGuid = guid; return key; }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] shared GUID probe: " + ex.Message); }
            key.IdentityKind = ParameterIdentityKind.DEFINITION_NAME;
            key.DefinitionName = parameter.Definition == null ? string.Empty : parameter.Definition.Name ?? string.Empty;
            return key;
        }

        public static Parameter FindMatchingParameter(Element target, ParameterKey key)
        {
            if (target == null || key == null) return null;
            if (key.IdentityKind == ParameterIdentityKind.BUILT_IN)
            {
                try { Parameter builtIn = target.get_Parameter(key.BuiltInParameter); if (builtIn != null) return builtIn; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] built-in target probe: " + ex.Message); }
            }
            foreach (Parameter candidate in target.Parameters)
            {
                ParameterKey candidateKey = CreateKey(candidate);
                if (key.Equals(candidateKey)) return candidate;
            }
            if (key.IdentityKind != ParameterIdentityKind.DEFINITION_NAME) return null;
            foreach (Parameter candidate in target.Parameters)
            {
                string name = candidate.Definition == null ? string.Empty : candidate.Definition.Name ?? string.Empty;
                if (string.Equals(name, key.DefinitionName, StringComparison.OrdinalIgnoreCase) && candidate.StorageType == key.StorageType && string.Equals(SafeDataType(candidate), key.DataTypeId, StringComparison.Ordinal)) return candidate;
            }
            return null;
        }

        public static ParameterValueSnapshot Snapshot(Parameter parameter)
        {
            var snapshot = new ParameterValueSnapshot { Key = CreateKey(parameter), StorageType = parameter == null ? StorageType.None : parameter.StorageType };
            if (parameter == null) { snapshot.IsBlank = true; return snapshot; }
            snapshot.DataTypeId = SafeDataType(parameter);
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String: snapshot.StringValue = parameter.AsString(); snapshot.IsBlank = string.IsNullOrEmpty(snapshot.StringValue); snapshot.DisplayValue = snapshot.StringValue ?? string.Empty; break;
                    case StorageType.Integer: snapshot.IntegerValue = parameter.AsInteger(); snapshot.DisplayValue = snapshot.IntegerValue.ToString(); break;
                    case StorageType.Double: snapshot.DoubleValue = parameter.AsDouble(); snapshot.DisplayValue = snapshot.DoubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture); break;
                    case StorageType.ElementId: snapshot.ElementIdValue = parameter.AsElementId() ?? ElementId.InvalidElementId; snapshot.IsBlank = snapshot.ElementIdValue == ElementId.InvalidElementId; snapshot.DisplayValue = snapshot.ElementIdValue.IntegerValue.ToString(); break;
                    default: snapshot.IsBlank = true; break;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] snapshot: " + ex.Message); snapshot.IsBlank = true; }
            return snapshot;
        }

        public static ParameterTransferResult TryApplyValue(Parameter target, ParameterValueSnapshot sourceValue, ParameterTransferOptions options = null)
        {
            options = options ?? new ParameterTransferOptions();
            var result = new ParameterTransferResult { Key = sourceValue == null ? null : sourceValue.Key, ParameterName = target == null || target.Definition == null ? string.Empty : target.Definition.Name ?? string.Empty, OldValue = Snapshot(target) };
            if (target == null || sourceValue == null) { result.Status = ParameterTransferStatus.MISSING_TARGET_PARAMETER; result.Message = "Parameter is unavailable."; return result; }
            if (options.ProtectIdentity && IsIdentityParameter(target)) { result.Status = ParameterTransferStatus.SKIPPED_PROTECTED; result.Message = "Sheet identity parameter is protected."; return result; }
            if (target.IsReadOnly) { result.Status = ParameterTransferStatus.READ_ONLY; result.Message = "Target parameter is read-only."; return result; }
            if (target.StorageType != sourceValue.StorageType) { result.Status = ParameterTransferStatus.TYPE_MISMATCH; result.Message = "Storage types differ."; return result; }
            if (!string.IsNullOrEmpty(sourceValue.DataTypeId) && !string.IsNullOrEmpty(SafeDataType(target)) && !string.Equals(sourceValue.DataTypeId, SafeDataType(target), StringComparison.Ordinal)) { result.Status = ParameterTransferStatus.DATA_TYPE_MISMATCH; result.Message = "Parameter data/spec types differ."; return result; }
            if (sourceValue.StorageType == StorageType.ElementId && !options.AllowElementId) { result.Status = ParameterTransferStatus.UNSAFE_ELEMENT_REFERENCE; result.Message = "ElementId references are blocked by default."; return result; }
            if (sourceValue.IsBlank && !options.OverwriteBlankSource) { result.Status = ParameterTransferStatus.BLANK_SOURCE_SKIPPED; result.Message = "Blank source value was skipped."; return result; }
            if (Equivalent(result.OldValue, sourceValue)) { result.Status = ParameterTransferStatus.NO_CHANGE; result.NewValue = result.OldValue; result.Message = "Target already has the source value."; return result; }
            try
            {
                switch (sourceValue.StorageType)
                {
                    case StorageType.String: target.Set(sourceValue.StringValue ?? string.Empty); break;
                    case StorageType.Integer: target.Set(sourceValue.IntegerValue); break;
                    case StorageType.Double: target.Set(sourceValue.DoubleValue); break;
                    case StorageType.ElementId: target.Set(sourceValue.ElementIdValue); break;
                    default: result.Status = ParameterTransferStatus.TYPE_MISMATCH; result.Message = "Unsupported storage type."; return result;
                }
                result.NewValue = Snapshot(target); result.Changed = true; result.Status = ParameterTransferStatus.COPIED; result.Message = "Copied typed value."; return result;
            }
            catch (Exception ex) { result.Status = ParameterTransferStatus.FAILED; result.Message = ex.Message; return result; }
        }

        public static bool IsIdentityParameter(Parameter parameter)
        {
            try { BuiltInParameter bip = (BuiltInParameter)parameter.Id.IntegerValue; return bip == BuiltInParameter.SHEET_NUMBER || bip == BuiltInParameter.SHEET_NAME; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] identity probe: " + ex.Message); return false; }
        }

        public static string SafeDataType(Parameter parameter)
        {
            try { return parameter == null || parameter.Definition == null || parameter.Definition.GetDataType() == null ? string.Empty : parameter.Definition.GetDataType().TypeId ?? string.Empty; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ParameterTransfer] data type probe: " + ex.Message); return string.Empty; }
        }

        private static bool Equivalent(ParameterValueSnapshot left, ParameterValueSnapshot right)
        {
            if (left == null || right == null || left.StorageType != right.StorageType) return false;
            switch (left.StorageType)
            {
                case StorageType.String: return string.Equals(left.StringValue ?? string.Empty, right.StringValue ?? string.Empty, StringComparison.Ordinal);
                case StorageType.Integer: return left.IntegerValue == right.IntegerValue;
                case StorageType.Double: return Math.Abs(left.DoubleValue - right.DoubleValue) <= 1e-9;
                case StorageType.ElementId: return left.ElementIdValue == right.ElementIdValue;
                default: return false;
            }
        }
    }
}
