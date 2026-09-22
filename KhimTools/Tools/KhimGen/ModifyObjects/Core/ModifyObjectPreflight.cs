using System;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    public static class ModifyObjectPreflight
    {
        public static ModifyObjectPreflightResult Validate(Document doc, ModifyObjectPlan plan)
        {
            var result = new ModifyObjectPreflightResult { IsValid = plan != null && plan.CanExecute };
            if (plan == null) { result.IsValid = false; result.Errors.Add("Plan is unavailable."); return result; }
            if (plan.IsStale(doc)) { result.IsValid = false; result.Errors.Add("STALE_PLAN: model changed after preview."); result.Statuses.Add(ModifyObjectStatus.STALE_PLAN); }
            foreach (ModifyObjectSourceSnapshot source in plan.Sources)
            {
                Element element = doc == null ? null : doc.GetElement(source.ElementId);
                if (element == null) { result.IsValid = false; result.Errors.Add("Element missing: " + source.ElementId.IntegerValue); result.Statuses.Add(ModifyObjectStatus.INVALID_SELECTION); continue; }
                if (element.Pinned) { result.IsValid = false; result.Errors.Add("PINNED: " + element.Id.IntegerValue); result.Statuses.Add(ModifyObjectStatus.PINNED); }
                if (source.GroupId != null && source.GroupId != ElementId.InvalidElementId) { result.IsValid = false; result.Errors.Add("GROUP_CONSTRAINED: " + element.Id.IntegerValue); result.Statuses.Add(ModifyObjectStatus.GROUP_CONSTRAINED); }
                if (element.DesignOption != null) result.Warnings.Add("DESIGN_OPTION_BLOCKED review required: " + element.Id.IntegerValue);
            }
            return result;
        }
    }
}
