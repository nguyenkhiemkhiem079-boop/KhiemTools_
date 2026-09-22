using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    public sealed class RuntimeQaRunner
    {
        private readonly RuntimeQaRegistry _registry;

        public RuntimeQaRunner(RuntimeQaRegistry registry = null)
        {
            _registry = registry ?? RuntimeQaRegistry.Default;
        }

        public QaRunResult RunAll(RuntimeQaContext context)
        {
            return Run(context, _registry.Fixtures);
        }

        public QaRunResult RunSuite(RuntimeQaContext context, string suiteId)
        {
            return Run(context, _registry.ForSuite(suiteId));
        }

        public QaRunResult RunFixture(RuntimeQaContext context, string fixtureId)
        {
            IRuntimeQaFixture fixture = _registry.Find(fixtureId);
            return Run(context, fixture == null ? Enumerable.Empty<IRuntimeQaFixture>() : new[] { fixture });
        }

        public QaRunResult Run(RuntimeQaContext context, IEnumerable<IRuntimeQaFixture> fixtures)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var run = new QaRunResult
            {
                RunId = context == null ? Guid.NewGuid().ToString("N") : context.RunId,
                KToolsVersion = typeof(RuntimeQaRunner).Assembly.GetName().Version == null ? "unknown" : typeof(RuntimeQaRunner).Assembly.GetName().Version.ToString(),
                GitCommit = Environment.GetEnvironmentVariable("KHIMTOOLS_GIT_COMMIT") ?? "unavailable",
                RevitVersion = context == null || context.Document == null || context.Document.Application == null ? "unknown" : context.Document.Application.VersionNumber,
                DocumentTitle = context == null || context.Document == null ? "<none>" : context.Document.Title,
                Timestamp = DateTime.Now,
                MachineNeutralEnvironment = "Revit API command thread; no user or machine identifiers collected",
                Suites = _registry.GetSuites().ToList()
            };
            foreach (IRuntimeQaFixture fixture in fixtures ?? Enumerable.Empty<IRuntimeQaFixture>())
            {
                if (context != null && context.IsCancellationRequested != null && context.IsCancellationRequested())
                {
                    run.Fixtures.Add(CreateNotRun(fixture, "Cancelled before fixture start."));
                    continue;
                }
                QaFixtureResult result = fixture.Run(context);
                run.Fixtures.Add(result);
                if (context != null && context.StopOnCriticalFailure && fixture.IsCritical && result.Status == QaStatus.FAIL) break;
            }
            if (context != null)
            {
                string rollbackMessage;
                run.ModelRollbackVerified = RuntimeQaSafetyGuard.VerifyRollback(context.Document, context.InitialFingerprint,
                    context.CreatedElementIds, out rollbackMessage);
                context.RestoreUiState();
            }
            run.CertificationStatus = Certification(run.Fixtures);
            stopwatch.Stop();
            run.Duration = stopwatch.Elapsed;
            RuntimeQaReportWriter.Write(run, context == null ? null : context.OutputDirectory, context != null && context.PreserveArtifacts);
            return run;
        }

        private static QaFixtureResult CreateNotRun(IRuntimeQaFixture fixture, string reason)
        {
            return new QaFixtureResult
            {
                FixtureId = fixture == null ? "UNKNOWN" : fixture.Id,
                SuiteId = fixture == null ? "CORE" : fixture.Suite,
                Name = fixture == null ? "Unknown fixture" : fixture.Name,
                Status = QaStatus.NOT_RUN,
                StartedAt = DateTime.Now,
                FinishedAt = DateTime.Now,
                Warnings = new List<string> { reason }
            };
        }

        private static string Certification(IEnumerable<QaFixtureResult> fixtures)
        {
            var rows = (fixtures ?? Enumerable.Empty<QaFixtureResult>()).ToList();
            if (rows.Any(f => f.IsCriticalFailure)) return "FAIL";
            if (rows.Any(f => f.Status == QaStatus.FAIL)) return "FAIL";
            if (rows.Any(f => f.Status == QaStatus.BLOCKED || f.Status == QaStatus.NOT_RUN)) return "INCOMPLETE";
            return rows.Count == 0 ? "INCOMPLETE" : "PASS";
        }
    }
}
