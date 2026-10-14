using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterValueParser
    {
        private static readonly Regex MillimeterPattern = new Regex(@"^\s*([-+]?\d+(?:[\.,]\d+)?)\s*(mm|millimeter|millimeters)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(ParameterValueSnapshot current, string text, ParameterManagerOptions options, out ParameterValueSnapshot parsed, out ParameterManagerStatus status, out string error)
        {
            parsed = Clone(current); status = ParameterManagerStatus.READY; error = string.Empty;
            if (current == null) { status = ParameterManagerStatus.PARAMETER_MISSING; error = "Parameter is missing."; return false; }
            options = options ?? new ParameterManagerOptions();
            text = text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) && current.StorageType != StorageType.String && current.StorageType != StorageType.Integer && current.StorageType != StorageType.Double) { status = ParameterManagerStatus.INVALID_VALUE; error = "Blank is not valid for this storage type."; return false; }
            try
            {
                switch (current.StorageType)
                {
                    case StorageType.String: parsed.StringValue = text; parsed.DisplayValue = text; parsed.IsBlank = string.IsNullOrEmpty(text); return true;
                    case StorageType.Integer:
                        if (IsYesNo(current)) { bool b; if (!TryParseBoolean(text, out b)) { status = ParameterManagerStatus.INVALID_VALUE; error = "Expected Yes/No or 0/1."; return false; } parsed.IntegerValue = b ? 1 : 0; }
                        else { int i; if (!int.TryParse(text, NumberStyles.Integer, options.Culture, out i)) { status = ParameterManagerStatus.INVALID_VALUE; error = "Expected an integer."; return false; } parsed.IntegerValue = i; }
                        parsed.DisplayValue = parsed.IntegerValue.ToString(options.Culture); parsed.IsBlank = false; return true;
                    case StorageType.Double:
                        double d; Match mm = MillimeterPattern.Match(text); if (mm.Success) { double millimeters; if (!double.TryParse(mm.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out millimeters)) { status = ParameterManagerStatus.INVALID_VALUE; error = "Invalid millimeter value."; return false; } d = UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters); } else if (!double.TryParse(text, NumberStyles.Float, options.Culture, out d)) { status = ParameterManagerStatus.INVALID_VALUE; error = "Expected a numeric value."; return false; }
                        parsed.DoubleValue = d; parsed.DisplayValue = d.ToString("R", CultureInfo.InvariantCulture); parsed.IsBlank = false; return true;
                    case StorageType.ElementId: status = ParameterManagerStatus.UNSAFE_ELEMENT_REFERENCE; error = "ElementId writes are blocked by default."; return false;
                    default: status = ParameterManagerStatus.INVALID_VALUE; error = "Unsupported storage type."; return false;
                }
            }
            catch (Exception ex) { status = ParameterManagerStatus.INVALID_VALUE; error = ex.Message; return false; }
        }

        public static ParameterValueSnapshot Clear(ParameterValueSnapshot current)
        {
            ParameterValueSnapshot result = Clone(current); if (result == null) return null;
            result.StringValue = string.Empty; result.IntegerValue = 0; result.DoubleValue = 0d; result.ElementIdValue = ElementId.InvalidElementId; result.DisplayValue = string.Empty; result.IsBlank = true; return result;
        }
        public static bool IsYesNo(ParameterValueSnapshot value) { return value != null && !string.IsNullOrEmpty(value.DataTypeId) && value.DataTypeId.IndexOf("boolean", StringComparison.OrdinalIgnoreCase) >= 0; }
        private static bool TryParseBoolean(string text, out bool value) { if (bool.TryParse(text, out value)) return true; if (text == "1" || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)) { value = true; return true; } if (text == "0" || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase)) { value = false; return true; } value = false; return false; }
        public static ParameterValueSnapshot Clone(ParameterValueSnapshot source) { if (source == null) return null; return new ParameterValueSnapshot { Key = source.Key, StorageType = source.StorageType, DataTypeId = source.DataTypeId, StringValue = source.StringValue, IntegerValue = source.IntegerValue, DoubleValue = source.DoubleValue, ElementIdValue = source.ElementIdValue, IsBlank = source.IsBlank, DisplayValue = source.DisplayValue }; }
    }
}
