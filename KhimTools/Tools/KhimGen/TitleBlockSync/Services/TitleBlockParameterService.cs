using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.TitleBlockSync.Models;

namespace KhimTools.TitleBlockSync.Services
{
    public static class TitleBlockParameterService
    {
        public static IList<ParameterTransferPlan> DiscoverTitleBlockParameters(Element element)
        {
            return Discover(element, false);
        }
        public static IList<ParameterTransferPlan> DiscoverSheetParameters(ViewSheet sheet)
        {
            return Discover(sheet, true);
        }
        private static IList<ParameterTransferPlan> Discover(Element element, bool sheet)
        {
            var list = new List<ParameterTransferPlan>();
            if (element == null) return list;
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter == null || parameter.Definition == null) continue;
                bool sharedParameter = parameter.IsShared; // shared GUID identity is resolved by ParameterTransferService
                bool protectedParameter = sheet ? TitleBlockCollector.IsProtectedSheetParameter(parameter) : TitleBlockCollector.IsProtectedTitleBlockParameter(parameter);
                if (TitleBlockCollector.IsGlobalProjectParameter(parameter)) protectedParameter = true;
                var plan = ParameterTransferService.Describe(parameter, element, p => protectedParameter);
                plan.Protected = protectedParameter;
                plan.CanSync = !protectedParameter && !parameter.IsReadOnly && parameter.StorageType != StorageType.ElementId;
                plan.Reason = protectedParameter ? (TitleBlockCollector.IsGlobalProjectParameter(parameter) ? "GLOBAL_PROJECT_PARAMETER" : "Protected parameter") : (plan.CanSync ? "Ready" : "UNSAFE_ELEMENT_REFERENCE");
                list.Add(plan);
            }
            return list;
        }
        public static IList<ParameterTransferPlan> SelectDefaults(Element element, bool sheet)
        {
            var result = new List<ParameterTransferPlan>();
            foreach (ParameterTransferPlan plan in Discover(element, sheet)) if (plan.CanSync && !plan.Protected) result.Add(plan);
            return result;
        }
        public static ParameterSyncResult ToResult(ParameterTransferResult result, TitleBlockSyncScope scope)
        {
            return new ParameterSyncResult { ParameterKey = result.Key, ParameterName = result.ParameterName, Scope = scope, OldValue = result.OldValue, NewValue = result.NewValue, Changed = result.Changed, Message = result.Message, Status = Map(result.Status) };
        }
        public static TitleBlockSyncStatusCode Map(ParameterTransferStatus status)
        {
            switch (status)
            {
                case ParameterTransferStatus.NO_CHANGE: return TitleBlockSyncStatusCode.NO_CHANGE;
                case ParameterTransferStatus.READ_ONLY: return TitleBlockSyncStatusCode.PARAMETER_READ_ONLY;
                case ParameterTransferStatus.TYPE_MISMATCH: return TitleBlockSyncStatusCode.PARAMETER_TYPE_MISMATCH;
                case ParameterTransferStatus.DATA_TYPE_MISMATCH: return TitleBlockSyncStatusCode.PARAMETER_DATA_TYPE_MISMATCH;
                case ParameterTransferStatus.UNSAFE_ELEMENT_REFERENCE: return TitleBlockSyncStatusCode.UNSAFE_ELEMENT_REFERENCE;
                case ParameterTransferStatus.SKIPPED_PROTECTED: return TitleBlockSyncStatusCode.PARAMETER_PROTECTED;
                case ParameterTransferStatus.BLANK_SOURCE_SKIPPED: return TitleBlockSyncStatusCode.BLANK_SOURCE_SKIPPED;
                case ParameterTransferStatus.MISSING_TARGET_PARAMETER: return TitleBlockSyncStatusCode.PARAMETER_MISSING;
                case ParameterTransferStatus.COPIED: return TitleBlockSyncStatusCode.SYNCED;
                default: return TitleBlockSyncStatusCode.FAILED;
            }
        }
    }
}

// Data/spec identity is obtained through Definition.GetDataType().
