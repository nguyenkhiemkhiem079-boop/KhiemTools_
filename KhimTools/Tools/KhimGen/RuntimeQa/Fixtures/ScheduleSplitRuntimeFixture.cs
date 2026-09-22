using System;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.ScheduleSplit.Models;
using KhimTools.ScheduleSplit.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class ScheduleSplitRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "SCHEDULE_SPLIT_STAGE32"; } }
        public override string Name { get { return "Split Schedule Stage 3.2"; } }
        public override string Suite { get { return "DOCUMENTATION"; } }
        public override string Description { get { return "Discover an explicit ScheduleSheetInstance, verify split API capability and run deterministic preflight inside rollback isolation."; } }
        public override bool IsCritical { get { return true; } }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            ScheduleSourceInfo source = null;
            foreach (ScheduleSourceInfo candidate in ScheduleSplitCollector.Collect(context.Document)) { source = candidate; break; }
            if (source == null) { Block(result, "SS32_PREREQ", "Schedule placement", "At least one non-system ScheduleSheetInstance", "No eligible schedule placement exists in the test model."); return; }
            ViewSchedule schedule = context.Document.GetElement(source.ScheduleId) as ViewSchedule;
            Check(result, "SS32_API", "Split API capability", ScheduleSplitApiAdapter.CanSplit(schedule, out string reason), "Available", reason, "Actual Revit split API is exposed through the adapter.", QaSeverity.CRITICAL);
            Check(result, "SS32_INSTANCE", "Explicit source identity", source.InstanceId != ElementId.InvalidElementId && source.SourceSheetId != ElementId.InvalidElementId, "ScheduleSheetInstance + ViewSheet", source.InstanceId + " / " + source.SourceSheetId, "Source selection is explicit.", QaSeverity.CRITICAL);
            string before = source.Fingerprint;
            var request = new ScheduleSplitRequest { Document = context.Document, SourceScheduleId = source.ScheduleId, SourceInstanceId = source.InstanceId, SourceSheetId = source.SourceSheetId, Options = new ScheduleSplitOptions { SegmentCount = 2, SourceMode = ScheduleSplitSourceMode.WORKING_COPY, Distribution = ScheduleSplitDistribution.ALL_SEGMENTS_ON_SAME_SHEET, AllowWithWarningOnCollision = true } };
            request.TargetSheets.Add(new ScheduleTargetSheet { SheetId = source.SourceSheetId, SheetNumber = source.SourceSheetNumber, SheetName = source.SourceSheetName, Order = 0 });
            ScheduleSplitPlan plan = ScheduleSplitPlanner.BuildPlan(context.Document, request);
            Check(result, "SS32_PLAN", "Preflight plan", plan != null && plan.SourceInstanceId == source.InstanceId, "READY or explicit diagnostic", plan == null ? "null" : plan.Status.ToString(), "Planner retains source identity.", QaSeverity.CRITICAL);
            var diagnostics = ScheduleSplitPreflightService.Validate(context.Document, plan, request.Options);
            Check(result, "SS32_PREFLIGHT", "Preflight safety", diagnostics.All(d => d.Severity != ScheduleSplitSeverity.ERROR) || diagnostics.Any(d => d.Status == ScheduleSplitStatusCode.INSUFFICIENT_TARGET_SHEETS), "Classified diagnostics", string.Join(";", diagnostics.Select(d => d.Status.ToString()).Distinct()), "Preflight returns classified outcomes.", QaSeverity.ERROR);
            if (plan.Status == ScheduleSplitStatusCode.READY && diagnostics.All(d => d.Severity != ScheduleSplitSeverity.ERROR))
            {
                ScheduleSplitExecutionResult execution = ScheduleSplitExecutionService.Execute(context.Document, plan, request.Options);
                Check(result, "SS32_EXECUTE", "Working-copy split execution", execution.Status == ScheduleSplitStatusCode.CREATED, "CREATED", execution.Status.ToString(), "Production execution runs within the fixture rollback group.", QaSeverity.CRITICAL);
                ScheduleSourceInfo after = ScheduleSplitCollector.Find(context.Document, source.ScheduleId, source.InstanceId);
                Check(result, "SS32_SOURCE", "Working-copy source unchanged", after != null && string.Equals(before, after.Fingerprint, StringComparison.Ordinal), before, after == null ? "missing" : after.Fingerprint, "Source fingerprint remains unchanged.", QaSeverity.CRITICAL);
            }
        }
    }
}
