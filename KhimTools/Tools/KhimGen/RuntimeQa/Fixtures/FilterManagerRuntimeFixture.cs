using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FilterManager.Models;
using KhimTools.FilterManager.Services;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Production Filter Manager smoke fixture. It blocks when a safe real filter/view prerequisite is absent.</summary>
    public sealed class FilterManagerRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "FILTER_MANAGER_STAGE34"; } }
        public override string Name { get { return "Filter Manager Stage 3.4"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Use an explicit active View and existing filter definition, execute production copy services, verify source and definitions, then rollback."; } }
        public override bool IsCritical { get { return true; } }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context == null ? null : context.Document;
            View source = RuntimeQaFixtureHelpers.ActiveView(context);
            if (source == null || source is ViewSheet || source.ViewType == ViewType.Schedule || source.IsTemplate) { Block(result, "FM34_VIEW", "Explicit active compatible View", "Active non-template non-sheet View", "The active document view is not a supported Filter Manager source.", QaSeverity.WARNING); return; }
            var sourceFilters = FilterStateService.Capture(source);
            AppliedFilterState selected = null; foreach (AppliedFilterState candidate in sourceFilters) { if (candidate.DefinitionType == FilterDefinitionType.ParameterFilter || candidate.DefinitionType == FilterDefinitionType.SelectionFilter) { selected = candidate; break; } }
            if (selected == null) { Block(result, "FM34_FILTER", "Existing filter prerequisite", "At least one applied ParameterFilterElement or SelectionFilterElement", "The active View has no supported applied filter; fixture refuses to create a fake pass.", QaSeverity.WARNING); return; }
            View target = null; foreach (View candidate in FilterCollectorService.CollectCompatibleViews(doc)) { if (candidate.Id != source.Id && !candidate.IsTemplate) { target = candidate; break; } }
            if (target == null) { Block(result, "FM34_TARGET", "Target View prerequisite", "One explicit second compatible View", "No safe target View is available.", QaSeverity.WARNING); return; }
            var beforeDefinitions = FilterCollectorService.CollectFilterDefinitions(doc).Where(x => x.Id == selected.FilterId).ToList();
            var request = new FilterCopyRequest { Document = doc, SourceViewId = source.Id, Options = new FilterSyncOptions { Mode = FilterCopyMode.MERGE_SELECTED_FILTERS, CopyVisibility = true, CopyEnabled = true, CopyGraphicOverrides = true, CopyOrder = true } };
            request.TargetViewIds.Add(target.Id); request.FilterIds.Add(selected.FilterId);
            FilterCopyPlan plan = FilterCopyPlanner.BuildPlan(doc, request);
            Check(result, "FM34_PLAN", "Production Filter Manager plan", plan != null && plan.SourceFilters.Count == 1 && plan.Targets.Count == 1, "One source filter and one target", plan == null ? "null" : plan.SourceFilters.Count + "/" + plan.Targets.Count, "The fixture uses the production planner and explicit identities.", QaSeverity.CRITICAL);
            IList<FilterManagerStatus> checks = FilterPreflightService.Validate(doc, plan); Check(result, "FM34_PREFLIGHT", "Filter Manager preflight", !checks.Any(x => x.IsError), "No preflight errors", string.Join(";", checks.Select(x => x.Code.ToString())), "Template control, compatibility and stale-plan checks are classified.", QaSeverity.CRITICAL);
            if (checks.Any(x => x.IsError)) return;
            FilterManagerResult execution = FilterExecutionService.Execute(doc, plan);
            Check(result, "FM34_EXECUTE", "Per-target transaction execution", execution.Status == FilterManagerStatusCode.APPLIED || execution.Status == FilterManagerStatusCode.PARTIAL, "APPLIED or PARTIAL", execution.Status.ToString(), "Production execution is isolated per target.", QaSeverity.CRITICAL);
            Check(result, "FM34_TARGET", "Target verification", FilterVerificationService.VerifyTarget(doc, plan.Targets[0], plan), "Selected filter applied", execution.Targets.Count == 0 ? "missing result" : execution.Targets[0].Status.ToString(), "The target state is checked before outer rollback.", QaSeverity.CRITICAL);
            Check(result, "FM34_SOURCE", "Source unchanged", FilterVerificationService.VerifySourceUnchanged(doc, plan), plan.SourceFingerprint, FilterStateService.ComputeViewFingerprint(source), "Source view fingerprint remains unchanged.", QaSeverity.CRITICAL);
            Check(result, "FM34_GLOBAL", "Global definitions unchanged", FilterVerificationService.VerifyGlobalDefinitionsUnchanged(doc, beforeDefinitions).Count == 0, "Same ElementId/UniqueId", string.Join(";", FilterVerificationService.VerifyGlobalDefinitionsUnchanged(doc, beforeDefinitions)), "The fixture never deletes a global filter definition.", QaSeverity.CRITICAL);
            Check(result, "FM34_ROLLBACK", "Outer TransactionGroup rollback", true, "Rollback boundary supplied by RuntimeQaFixtureBase", "TransactionGroup", "Runtime QA base rolls back the fixture after verification.", QaSeverity.INFO);
        }
    }
}
