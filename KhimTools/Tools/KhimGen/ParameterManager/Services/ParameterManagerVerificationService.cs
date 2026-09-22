using System;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerVerificationService
    {
        public static bool Verify(Document doc, ParameterManagerPlan plan, ParameterManagerResult result)
        {
            if (doc == null || plan == null) return false;
            bool ok = true;
            foreach (ParameterEditItem item in plan.Items)
            {
                if (item.Action != ParameterManagerAction.UPDATE || item.ProposedValue == null) continue;
                Element target = doc.GetElement(plan.Request.ParameterScope == ParameterScopeMode.TYPE ? item.TypeId : item.ElementId); Parameter p = ParameterTransferService.FindMatchingParameter(target, plan.SelectedParameterKey); ParameterValueSnapshot actual = ParameterTransferService.Snapshot(p);
                if (!Equivalent(actual, item.ProposedValue)) { item.Status = ParameterManagerStatus.VERIFICATION_FAILED; ok = false; }
            }
            return ok;
        }
        public static bool VerifyUnselectedUnchanged(Document doc, ParameterManagerPlan plan) { if (doc == null || plan == null) return false; foreach (ParameterEditItem item in plan.Items) if (!item.IsSelected && item.Action == ParameterManagerAction.UPDATE) return false; return true; }
        private static bool Equivalent(ParameterValueSnapshot a, ParameterValueSnapshot b) { if (a == null || b == null || a.StorageType != b.StorageType) return false; switch (a.StorageType) { case StorageType.String: return string.Equals(a.StringValue ?? string.Empty, b.StringValue ?? string.Empty, StringComparison.Ordinal); case StorageType.Integer: return a.IntegerValue == b.IntegerValue; case StorageType.Double: return Math.Abs(a.DoubleValue - b.DoubleValue) <= 1e-9; case StorageType.ElementId: return a.ElementIdValue == b.ElementIdValue; default: return false; } }
    }
}
