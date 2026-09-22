using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyParameterService
    {
        private static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sheet Number", "Sheet Name", "SHEET_NUMBER", "SHEET_NAME" };
        public static List<SheetCopyParameterResult> CopySafeParameters(Element source, Element target)
        {
            var results = new List<SheetCopyParameterResult>(); if (source == null || target == null) return results;
            foreach (Parameter sourceParameter in source.Parameters)
            {
                string name = sourceParameter.Definition == null ? string.Empty : sourceParameter.Definition.Name ?? string.Empty;
                if (IsProtected(sourceParameter, name)) { results.Add(Result(name, SheetCopyStatusCode.SKIPPED_PROTECTED, "Protected identity parameter.")); continue; }
                Parameter targetParameter = ParameterTransferService.FindMatchingParameter(target, ParameterTransferService.CreateKey(sourceParameter));
                if (targetParameter == null) { results.Add(Result(name, SheetCopyStatusCode.MISSING_TARGET_PARAMETER, "Matching target parameter is missing.")); continue; }
                ParameterTransferResult applied = ParameterTransferService.TryApplyValue(targetParameter, ParameterTransferService.Snapshot(sourceParameter), new ParameterTransferOptions { AllowElementId = false, ProtectIdentity = true });
                results.Add(Result(name, Map(applied.Status), applied.Message));
            }
            return results;
        }
        public static bool IsProtected(Parameter parameter, string name)
        {
            if (ProtectedNames.Contains(name ?? string.Empty)) return true;
            try { BuiltInParameter bip = (BuiltInParameter)parameter.Id.IntegerValue; return bip == BuiltInParameter.SHEET_NUMBER || bip == BuiltInParameter.SHEET_NAME; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SheetCopy] protected parameter probe: " + ex.Message); return false; }
        }
        private static SheetCopyStatusCode Map(ParameterTransferStatus status)
        {
            switch (status)
            {
                case ParameterTransferStatus.COPIED: return SheetCopyStatusCode.COPIED;
                case ParameterTransferStatus.NO_CHANGE: return SheetCopyStatusCode.NO_CHANGE;
                case ParameterTransferStatus.READ_ONLY: return SheetCopyStatusCode.READ_ONLY;
                case ParameterTransferStatus.MISSING_TARGET_PARAMETER: return SheetCopyStatusCode.MISSING_TARGET_PARAMETER;
                case ParameterTransferStatus.UNSAFE_ELEMENT_REFERENCE: return SheetCopyStatusCode.TYPE_MISMATCH;
                case ParameterTransferStatus.TYPE_MISMATCH:
                case ParameterTransferStatus.DATA_TYPE_MISMATCH: return SheetCopyStatusCode.TYPE_MISMATCH;
                case ParameterTransferStatus.SKIPPED_PROTECTED: return SheetCopyStatusCode.SKIPPED_PROTECTED;
                default: return SheetCopyStatusCode.FAILED;
            }
        }
        private static SheetCopyParameterResult Result(string name, SheetCopyStatusCode status, string message) { return new SheetCopyParameterResult { ParameterName = name, Status = status, Message = message }; }
    }
}
