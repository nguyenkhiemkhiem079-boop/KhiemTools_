using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyParameterService
    {
        private static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Sheet Number", "Sheet Name", "SHEET_NUMBER", "SHEET_NAME" };

        public static List<SheetCopyParameterResult> CopySafeParameters(Element source, Element target)
        {
            var results = new List<SheetCopyParameterResult>();
            if (source == null || target == null) return results;
            foreach (Parameter sourceParameter in source.Parameters)
            {
                string name = sourceParameter.Definition == null ? string.Empty : sourceParameter.Definition.Name ?? string.Empty;
                if (IsProtected(sourceParameter, name))
                {
                    results.Add(Result(name, SheetCopyStatusCode.SKIPPED_PROTECTED, "Protected identity parameter."));
                    continue;
                }
                Parameter targetParameter = FindMatchingParameter(target, sourceParameter, name);
                if (targetParameter == null) { results.Add(Result(name, SheetCopyStatusCode.MISSING_TARGET_PARAMETER, "Matching target parameter is missing.")); continue; }
                if (targetParameter.IsReadOnly) { results.Add(Result(name, SheetCopyStatusCode.READ_ONLY, "Target parameter is read-only.")); continue; }
                if (targetParameter.StorageType != sourceParameter.StorageType) { results.Add(Result(name, SheetCopyStatusCode.TYPE_MISMATCH, "Parameter storage types differ.")); continue; }
                try
                {
                    if (sourceParameter.StorageType == StorageType.String) targetParameter.Set(sourceParameter.AsString() ?? string.Empty);
                    else if (sourceParameter.StorageType == StorageType.Integer) targetParameter.Set(sourceParameter.AsInteger());
                    else if (sourceParameter.StorageType == StorageType.Double) targetParameter.Set(sourceParameter.AsDouble());
                    else if (sourceParameter.StorageType == StorageType.ElementId) targetParameter.Set(sourceParameter.AsElementId());
                    else { results.Add(Result(name, SheetCopyStatusCode.TYPE_MISMATCH, "Unsupported storage type.")); continue; }
                    results.Add(Result(name, SheetCopyStatusCode.COPIED, "Copied."));
                }
                catch (Exception ex) { results.Add(Result(name, SheetCopyStatusCode.FAILED, ex.Message)); }
            }
            return results;
        }

        public static bool IsProtected(Parameter parameter, string name)
        {
            if (ProtectedNames.Contains(name ?? string.Empty)) return true;
            try
            {
                BuiltInParameter bip = (BuiltInParameter)parameter.Id.IntegerValue;
                return bip == BuiltInParameter.SHEET_NUMBER || bip == BuiltInParameter.SHEET_NAME;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] protected parameter probe: " + ex.Message); return false; }
        }

        private static Parameter FindMatchingParameter(Element target, Parameter source, string name)
        {
            var parameters = target.Parameters.Cast<Parameter>().ToList();
            if (source.IsShared)
            {
                string guid = string.Empty;
                try { guid = source.GUID.ToString(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] shared GUID probe: " + ex.Message); }
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    Parameter shared = parameters.FirstOrDefault(p => p.IsShared && SafeGuid(p) == guid);
                    if (shared != null) return shared;
                }
            }
            Parameter byName = parameters.FirstOrDefault(p => p.Definition != null && string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;
            return null;
        }

        private static string SafeGuid(Parameter parameter) { try { return parameter.GUID.ToString(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] target GUID probe: " + ex.Message); return string.Empty; } }
        private static SheetCopyParameterResult Result(string name, SheetCopyStatusCode status, string message) => new SheetCopyParameterResult { ParameterName = name, Status = status, Message = message };
    }
}
