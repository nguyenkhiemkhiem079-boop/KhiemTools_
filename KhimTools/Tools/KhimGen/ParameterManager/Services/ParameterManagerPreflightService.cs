using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerPreflightService
    {
        public static ParameterManagerPreflightResult Validate(Document doc, ParameterManagerPlan plan)
        {
            var result = new ParameterManagerPreflightResult { IsValid = true, Status = ParameterManagerStatus.READY };
            if (doc == null || plan == null) { result.IsValid = false; result.Status = ParameterManagerStatus.STALE_PARAMETER_PLAN; result.Errors.Add("Plan or document is unavailable."); return result; }
            string currentFingerprint = BuildCurrentFingerprint(doc, plan); if (!string.Equals(currentFingerprint, plan.Fingerprint, StringComparison.Ordinal)) { result.IsValid = false; result.Status = ParameterManagerStatus.STALE_PARAMETER_PLAN; result.Errors.Add("STALE_PARAMETER_PLAN"); }
            foreach (ParameterEditItem item in plan.Items)
            {
                Element element = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId);
                if (element == null) { item.Status = ParameterManagerStatus.ELEMENT_MISSING; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": ELEMENT_MISSING"); result.Items.Add(item); continue; }
                Parameter parameter = ParameterTransferService.FindMatchingParameter(element, plan.SelectedParameterKey);
                if (parameter == null) { item.Status = ParameterManagerStatus.PARAMETER_MISSING; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": PARAMETER_MISSING"); result.Items.Add(item); continue; }
                if (ParameterManagerPolicy.IsProtected(parameter) && !(plan.Request.Options != null && plan.Request.Options.AllowProtectedParameters)) { item.Status = ParameterManagerStatus.PARAMETER_PROTECTED; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": PARAMETER_PROTECTED"); }
                else if (parameter.IsReadOnly) { item.Status = ParameterManagerStatus.PARAMETER_READ_ONLY; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": READ_ONLY"); }
                else if (item.ProposedValue != null && parameter.StorageType != item.ProposedValue.StorageType) { item.Status = ParameterManagerStatus.STORAGE_TYPE_UNSUPPORTED; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": STORAGE_TYPE_UNSUPPORTED"); }
                else if (item.ProposedValue != null && !string.IsNullOrEmpty(item.ProposedValue.DataTypeId) && !string.IsNullOrEmpty(ParameterTransferService.SafeDataType(parameter)) && !string.Equals(item.ProposedValue.DataTypeId, ParameterTransferService.SafeDataType(parameter), StringComparison.Ordinal)) { item.Status = ParameterManagerStatus.DATA_TYPE_UNSUPPORTED; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": DATA_TYPE_UNSUPPORTED"); }
                else if (element.GroupId != null && element.GroupId != ElementId.InvalidElementId) { item.Status = ParameterManagerStatus.GROUP_CONSTRAINED; item.Action = ParameterManagerAction.BLOCKED; result.IsValid = false; result.Errors.Add(item.ElementId.IntegerValue + ": GROUP_CONSTRAINED"); }
                result.Items.Add(item);
            }
            return result;
        }
        public static string BuildCurrentFingerprint(Document doc, ParameterManagerPlan plan) { var copy = new ParameterManagerPlan { Document = doc, Request = plan.Request, SelectedParameterKey = plan.SelectedParameterKey }; foreach (ParameterEditItem item in plan.Items) { Element e = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId); Parameter p = e == null ? null : ParameterTransferService.FindMatchingParameter(e, plan.SelectedParameterKey); item.CurrentValue = ParameterTransferService.Snapshot(p); copy.Items.Add(item); } return ParameterManagerPlanner.ComputeFingerprint(copy); }
    }
    public static class ParameterManagerPolicy
    {
        public static bool IsProtected(Parameter parameter)
        {
            if (parameter == null || parameter.Definition == null) return true;
            string name = parameter.Definition.Name ?? string.Empty;
            string normalized = name.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            // SHEET_NUMBER and SHEET_NAME are identity fields and remain protected.
            if (ParameterTransferService.IsIdentityParameter(parameter)) return true;
            return normalized == "family" || normalized == "type" || normalized == "category" || normalized == "level" || normalized == "host" || normalized == "phasecreated" || normalized == "phasedemolished" || normalized == "designoption" || normalized == "workset" || normalized == "viewtemplateid" || normalized == "sheetnumber" || normalized == "sheetname" || normalized.Contains("revision");
        }
    }
}
