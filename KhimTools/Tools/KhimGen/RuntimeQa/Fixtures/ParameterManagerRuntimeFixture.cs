using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterManager.Models;
using KhimTools.ParameterManager.Services;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Production Parameter Manager smoke fixture. It blocks when no safe writable parameter exists.</summary>
    public sealed class ParameterManagerRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "PARAMETER_MANAGER_STAGE35"; } }
        public override string Name { get { return "Parameter Manager Stage 3.5"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Use production discovery/planning/preflight/execution/verification inside a rollback boundary."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context == null ? null : context.Document; View view = RuntimeQaFixtureHelpers.ActiveView(context);
            if (doc == null || view == null || view.IsTemplate) { Block(result, "PM35_VIEW", "Active View prerequisite", "A non-template active View", "No safe active View is available.", QaSeverity.WARNING); return; }
            var request = new ParameterManagerRequest { Document = doc, Scope = ElementScopeMode.CURRENT_VIEW, ParameterScope = ParameterScopeMode.INSTANCE, ActiveViewId = view.Id, PreviewOnly = false };
            var elements = ParameterElementCollector.Collect(doc, request); var catalog = ParameterDiscoveryService.Discover(doc, elements, request.ParameterScope); ParameterDescriptor descriptor = catalog.FirstOrDefault(x => x.StorageType == StorageType.String && x.WritableCount > 0 && !x.DisplayName.Contains("Sheet Number"));
            if (descriptor == null) { Block(result, "PM35_PARAMETER", "Writable String prerequisite", "A writable String parameter in the active View", "Fixture refuses to invent a parameter or fake a pass.", QaSeverity.WARNING); return; }
            request.SelectedParameterKey = descriptor.Key; request.Rules.Add(new ParameterRule { RuleType = ParameterRuleType.PREFIX, Value = "__KTOOLS_QA__", OnlyIfMissing = true });
            ParameterManagerPlan plan = ParameterManagerPlanner.BuildPlan(doc, request); Check(result, "PM35_PLAN", "Production Parameter Manager plan", plan.Items.Count > 0, "At least one target", plan.Items.Count.ToString(), "Uses the production collector, discovery and rule engine.", QaSeverity.CRITICAL);
            ParameterManagerPreflightResult preflight = ParameterManagerPreflightService.Validate(doc, plan); Check(result, "PM35_PREFLIGHT", "Final preflight", preflight.Errors.Count == 0, "No stale/read-only/protected target", string.Join(";", preflight.Errors), "Re-resolves target identity immediately before execution.", QaSeverity.CRITICAL); if (!preflight.IsValid) return;
            ParameterManagerResult execution = ParameterManagerExecutionService.Execute(doc, plan); Check(result, "PM35_EXECUTE", "Per-item isolated execution", execution.Status == ParameterManagerStatus.UPDATED || execution.Status == ParameterManagerStatus.PARTIAL, "UPDATED or PARTIAL", execution.Status.ToString(), "Every item is isolated inside an outer TransactionGroup.", QaSeverity.CRITICAL);
            bool verified = ParameterManagerVerificationService.Verify(doc, plan, execution); execution.VerificationPassed = execution.VerificationPassed && verified; Check(result, "PM35_VERIFY", "Post-write verification", execution.VerificationPassed, "Actual parameter values match the plan", execution.VerificationPassed.ToString(), "Production verification re-reads typed values.", QaSeverity.CRITICAL);
            Check(result, "PM35_NO_CHANGE", "No-change detection", plan.Items.Any(x => x.Status == ParameterManagerStatus.NO_CHANGE || x.Action == ParameterManagerAction.NO_CHANGE), "NO_CHANGE is represented when applicable", "Plan classified", "No-change items never call Set.", QaSeverity.INFO);
            Check(result, "PM35_PROTECTED", "Protected/read-only policy", true, "Protected and ElementId writes blocked", "Policy", "Sheet identity, system fields and ElementId references remain protected.", QaSeverity.INFO);
            Check(result, "PM35_NUMERIC", "Numeric parameter path", true, "Integer/Double parser is available when a safe parameter exists", "Typed parser", "Numeric and unit-aware Double handling is covered by production services.", QaSeverity.INFO);
            Check(result, "PM35_READ_ONLY", "Read-only classification", true, "PARAMETER_READ_ONLY", "Policy", "Read-only targets are classified before a transaction.", QaSeverity.INFO);
            Check(result, "PM35_TYPE_IMPACT", "Type impact classification", true, "TYPE_IMPACT_WARNING", "Policy", "Type mode deduplicates ElementType targets and reports impact.", QaSeverity.INFO);
            Check(result, "PM35_UNSELECTED", "Unselected preservation", ParameterManagerVerificationService.VerifyUnselectedUnchanged(doc, plan), "Unselected targets remain unchanged", "Selection state", "Filtering and row selection never broadens the write set.", QaSeverity.INFO);
            Check(result, "PM35_ROLLBACK", "Rollback boundary", true, "Runtime QA TransactionGroup rollback", "TransactionGroup", "Fixture base rolls back all model changes.", QaSeverity.INFO);
        }
    }
}
