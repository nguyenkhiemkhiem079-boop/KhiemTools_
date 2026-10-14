using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class ViewTemplateFilterControlService
    {
        public static bool HasTemplate(View view)
        {
            return view != null && view.ViewTemplateId != null && view.ViewTemplateId != ElementId.InvalidElementId;
        }

        public static bool AreFiltersControlledByTemplate(View view, out string reason)
        {
            if (!HasTemplate(view)) { reason = string.Empty; return false; }
            ICollection<ElementId> controlled = GetTemplateParameterIds(view);
            ICollection<ElementId> nonControlled = GetNonControlledTemplateParameterIds(view);
            reason = controlled.Count == 0 && nonControlled.Count == 0
                ? "ViewTemplateId is set; template parameter capability is unavailable in this Revit API." : "The active View Template controls filter-related view state.";
            return true;
        }

        public static ViewTemplateControlStatus GetStatus(View view, out ElementId controllingTemplateId, out string reason)
        {
            controllingTemplateId = ElementId.InvalidElementId;
            if (!HasTemplate(view)) { reason = "No View Template is assigned."; return ViewTemplateControlStatus.NO_TEMPLATE; }
            controllingTemplateId = view.ViewTemplateId;
            ICollection<ElementId> controlled = GetTemplateParameterIds(view);
            ICollection<ElementId> nonControlled = GetNonControlledTemplateParameterIds(view);
            if (controlled.Count == 0 && nonControlled.Count == 0) { reason = "Template control API is unavailable in the active Revit API."; return ViewTemplateControlStatus.UNKNOWN_TEMPLATE_CONTROL; }
            reason = "Filter visibility/graphics are controlled by the assigned View Template.";
            return ViewTemplateControlStatus.FILTER_SETTINGS_CONTROLLED_BY_TEMPLATE;
        }

        public static ICollection<ElementId> GetTemplateParameterIds(View view)
        {
            return InvokeIds(view, "GetTemplateParameterIds");
        }

        public static ICollection<ElementId> GetNonControlledTemplateParameterIds(View view)
        {
            return InvokeIds(view, "GetNonControlledTemplateParameterIds");
        }

        private static ICollection<ElementId> InvokeIds(View view, string name)
        {
            if (view == null) return new List<ElementId>();
            MethodInfo method = view.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 0);
            if (method == null) return new List<ElementId>();
            try { return (method.Invoke(view, null) as IEnumerable<ElementId> ?? Enumerable.Empty<ElementId>()).ToList(); }
            catch (Exception) { return new List<ElementId>(); }
        }
    }
}
