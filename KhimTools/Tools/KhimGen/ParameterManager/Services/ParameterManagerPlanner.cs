using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;
using KhimTools.Core.Workflow;

namespace KhimTools.ParameterManager.Services
{
    public static class ParameterManagerPlanner
    {
        public static ParameterManagerPlan BuildPlan(Document doc, ParameterManagerRequest request)
        {
            var plan = new ParameterManagerPlan { DocumentIdentityKey = DocumentIdentity.From(doc).StableKey, Request = ParameterManagerPlanRequest.From(request), SelectedParameterKey = request == null ? null : ParameterManagerPlanRequest.CloneKey(request.SelectedParameterKey), IsPreview = request == null || request.PreviewOnly };
            if (doc == null || request == null || request.SelectedParameterKey == null) { plan.Warnings.Add("Select a parameter before preview."); return plan; }
            IList<Element> collected = ParameterElementCollector.Collect(doc, request);
            IList<Element> targets = request.ParameterScope == ParameterScopeMode.TYPE ? DeduplicateTypes(doc, collected, plan) : collected;
            if (request.Scope == ElementScopeMode.WHOLE_PROJECT && (request.Options == null || !request.Options.ConfirmWholeProject)) plan.Warnings.Add("WHOLE_PROJECT requires explicit confirmation.");
            plan.SourceScopeFingerprint = WorkflowFingerprint.Compute(new[] { plan.DocumentIdentityKey }.Concat(collected.OrderBy(x => x.Id.ToLongValue()).Select(x => x.UniqueId ?? x.Id.ToLongValue().ToString())));
            plan.ParameterDisplayName = request.SelectedParameterKey.ToString();
            ParameterRule regexRule = request.Rules == null ? null : request.Rules.FirstOrDefault(r => r != null && r.RuleType == ParameterRuleType.REGEX_REPLACE);
            if (regexRule != null) { string regexError; if (!ParameterRuleEngine.TryCompileRegex(regexRule, out regexError)) { plan.Warnings.Add("INVALID_REGEX: " + regexError); plan.Blocked.Add("INVALID_REGEX"); } }
            foreach (Element source in targets)
            {
                if (source == null) continue;
                Element instance = request.ParameterScope == ParameterScopeMode.TYPE ? FindRepresentative(doc, collected, source.Id) : source;
                Parameter parameter = ParameterTransferService.FindMatchingParameter(source, request.SelectedParameterKey);
                var item = new ParameterEditItem { ElementId = instance == null ? source.Id : instance.Id, UniqueId = instance == null ? source.UniqueId : instance.UniqueId, TypeId = instance == null ? source.GetTypeId() : instance.GetTypeId(), IsTypeTarget = request.ParameterScope == ParameterScopeMode.TYPE, IsSelected = true, CategoryName = source.Category == null ? string.Empty : source.Category.Name, FamilyName = GetFamilyName(source), TypeName = source.Name ?? string.Empty, ParameterKey = request.SelectedParameterKey, ParameterName = parameter == null || parameter.Definition == null ? string.Empty : parameter.Definition.Name };
                if (parameter == null) { item.Status = ParameterManagerStatus.PARAMETER_MISSING; item.Action = ParameterManagerAction.BLOCKED; item.Message = "Parameter is missing on this target."; plan.Blocked.Add(item.ElementId.IntegerValue + ": PARAMETER_MISSING"); plan.Items.Add(item); continue; }
                item.CurrentValue = ParameterTransferService.Snapshot(parameter);
                string error; ParameterManagerStatus status; ParameterValueSnapshot proposed;
                if (!ParameterRuleEngine.TryApply(item.CurrentValue, request.Rules, request.Options, out proposed, out status, out error)) { item.Status = status; item.Action = ParameterManagerAction.BLOCKED; item.Message = error; plan.Blocked.Add(item.ElementId.IntegerValue + ": " + status); }
                else if (Equivalent(item.CurrentValue, proposed)) { item.ProposedValue = proposed; item.Status = ParameterManagerStatus.NO_CHANGE; item.Action = ParameterManagerAction.NO_CHANGE; }
                else { item.ProposedValue = proposed; item.Status = ParameterManagerStatus.READY; item.Action = ParameterManagerAction.UPDATE; }
                if (item.IsTypeTarget) { item.TypeImpactCount = collected.Count(e => e.GetTypeId() == source.Id); int projectImpact = 0; foreach (Element candidate in new FilteredElementCollector(doc).WhereElementIsNotElementType()) if (candidate.GetTypeId() == source.Id) projectImpact++; item.Warnings.Add("TYPE_IMPACT_WARNING: " + item.TypeImpactCount + " selected instances / " + projectImpact + " project instances."); if (item.TypeImpactCount > 1 && request.Options != null && request.Options.IncludeTypeImpactWarning) item.Status = ParameterManagerStatus.TYPE_IMPACT_WARNING; plan.TargetTypeIds.Add(source.Id); }
                plan.TargetElementIds.Add(item.ElementId); plan.Items.Add(item);
            }
            ParameterRule numbering = request.Rules == null ? null : request.Rules.FirstOrDefault(r => r != null && r.RuleType == ParameterRuleType.NUMERIC_INCREMENT);
            if (numbering != null) ApplyDeterministicNumbering(plan.Items, numbering);
            plan.Fingerprint = ComputeFingerprint(plan);
            return plan;
        }

        public static string ComputeFingerprint(ParameterManagerPlan plan)
        {
            if (plan == null) return string.Empty;
            var tokens = new List<string> { plan.DocumentIdentityKey ?? string.Empty };
            tokens.AddRange(plan.Items.OrderBy(i => i.UniqueId, StringComparer.Ordinal).Select(i => string.Join(":", i.UniqueId ?? string.Empty, i.TypeId == null ? string.Empty : i.TypeId.ToLongValue().ToString(), i.CurrentDisplay ?? string.Empty, i.ParameterKey == null ? string.Empty : i.ParameterKey.ToString())));
            return WorkflowFingerprint.Compute(tokens);
        }
        private static IList<Element> DeduplicateTypes(Document doc, IList<Element> source, ParameterManagerPlan plan)
        {
            var result = new List<Element>(); var ids = new HashSet<int>(); foreach (Element e in source) { ElementId typeId = e.GetTypeId(); Element type = typeId == null ? null : doc.GetElement(typeId); if (type != null && ids.Add(type.Id.IntegerValue)) result.Add(type); else if (type != null) plan.Warnings.Add(e.Id.IntegerValue + ": TYPE_TARGET_DEDUPLICATED"); } return result;
        }
        private static Element FindRepresentative(Document doc, IList<Element> source, ElementId typeId) { return source.FirstOrDefault(e => e.GetTypeId() == typeId) ?? doc.GetElement(typeId); }
        private static string GetFamilyName(Element e) { try { ElementType type = e as ElementType ?? null; if (type != null && type.FamilyName != null) return type.FamilyName; ElementId tid = e.GetTypeId(); Element t = e.Document.GetElement(tid); ElementType et = t as ElementType; return et == null ? string.Empty : et.FamilyName; } catch { return string.Empty; } }
        private static void ApplyDeterministicNumbering(IEnumerable<ParameterEditItem> items, ParameterRule rule) { int index = 0; foreach (ParameterEditItem item in items.OrderBy(i => i.ElementId.IntegerValue)) { double value = rule.NumericStart + index * rule.NumericStep; string text = Math.Round(value).ToString(rule.Padding > 0 ? new string('0', rule.Padding) : "0", System.Globalization.CultureInfo.InvariantCulture); if (item.ProposedValue != null && item.ProposedValue.StorageType == StorageType.String) { item.ProposedValue.StringValue = text; item.ProposedValue.DisplayValue = text; item.Status = ParameterManagerStatus.READY; item.Action = ParameterManagerAction.UPDATE; } index++; } }
        private static bool Equivalent(ParameterValueSnapshot a, ParameterValueSnapshot b) { if (a == null || b == null || a.StorageType != b.StorageType) return false; if (a.StorageType == StorageType.String) return string.Equals(a.StringValue ?? string.Empty, b.StringValue ?? string.Empty, StringComparison.Ordinal); if (a.StorageType == StorageType.Integer) return a.IntegerValue == b.IntegerValue; if (a.StorageType == StorageType.Double) return Math.Abs(a.DoubleValue - b.DoubleValue) <= 1e-9; return a.ElementIdValue == b.ElementIdValue; }
    }
}
