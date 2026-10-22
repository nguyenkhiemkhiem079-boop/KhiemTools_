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
            List<IRuntimeQaFixture> selectedFixtures = (fixtures ?? Enumerable.Empty<IRuntimeQaFixture>())
                .Where(fixture => fixture != null)
                .ToList();
            bool prepared = false;
            string preflightMessage = selectedFixtures.Count == 0 ? "No fixtures were selected; no model snapshot was taken." : "Runtime QA preflight did not run.";
            if (selectedFixtures.Count == 0)
            {
                context?.ResetRunFingerprint();
            }
            else if (context != null)
            {
                prepared = context.TryPrepareForRun(out preflightMessage);
            }
            else
            {
                preflightMessage = "BLOCKED_UNSAFE_DOCUMENT: runtime QA context is missing.";
            }

            var run = new QaRunResult
            {
                RunId = context == null ? Guid.NewGuid().ToString("N") : context.RunId,
                KToolsVersion = typeof(RuntimeQaRunner).Assembly.GetName().Version == null ? "unknown" : typeof(RuntimeQaRunner).Assembly.GetName().Version.ToString(),
                GitCommit = Environment.GetEnvironmentVariable("KHIMTOOLS_GIT_COMMIT") ?? "unavailable",
                RevitVersion = !prepared || context.Document == null || context.Document.Application == null ? "not inspected" : context.Document.Application.VersionNumber,
                DocumentTitle = !prepared || context.Document == null ? "<not inspected before preflight>" : context.Document.Title,
                KhimNotWorkingInRevitAttested = context == null ? (bool?)null : context.IsSafeRevitWindowConfirmed,
                DisposableQaCopyAttested = context == null ? (bool?)null : context.IsDisposableQaCopyConfirmed,
                RuntimeQaPreflightPassed = selectedFixtures.Count == 0 ? (bool?)null : prepared,
                RuntimeQaPreflightMessage = preflightMessage,
                Timestamp = DateTime.Now,
                MachineNeutralEnvironment = "Revit API command thread; no user or machine identifiers collected",
                Suites = _registry.GetSuites().ToList()
            };

            if (!prepared && selectedFixtures.Count > 0)
            {
                foreach (IRuntimeQaFixture fixture in selectedFixtures)
                    run.Fixtures.Add(CreateBlocked(fixture, preflightMessage));
            }
            else
            {
                foreach (IRuntimeQaFixture fixture in selectedFixtures)
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
            }

            bool? wholeRunRollbackVerified = null;
            if (context != null && context.InitialFingerprint != null)
            {
                string rollbackMessage;
                run.ModelRollbackVerified = RuntimeQaSafetyGuard.VerifyRollback(context.Document, context.InitialFingerprint,
                    context.CreatedElementIds, out rollbackMessage);
                run.ModelRollbackMessage = rollbackMessage;
                wholeRunRollbackVerified = run.ModelRollbackVerified;
                context.RestoreUiState();
            }
            else
            {
                run.ModelRollbackVerified = null;
                run.ModelRollbackMessage = preflightMessage;
            }
            run.CertificationStatus = Certification(run.Fixtures, wholeRunRollbackVerified);
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

        private static QaFixtureResult CreateBlocked(IRuntimeQaFixture fixture, string reason)
        {
            DateTime now = DateTime.Now;
            return new QaFixtureResult
            {
                FixtureId = fixture.Id,
                SuiteId = fixture.Suite,
                Name = fixture.Name,
                Status = QaStatus.BLOCKED,
                StartedAt = now,
                FinishedAt = now,
                Warnings = new List<string> { reason },
                Checks = new List<QaCheckResult>
                {
                    new QaCheckResult
                    {
                        CheckId = fixture.Id + "_PREFLIGHT",
                        Name = "Runtime QA safety preflight",
                        Status = QaStatus.BLOCKED,
                        Severity = QaSeverity.CRITICAL,
                        Expected = "Safe Revit window and detached disposable QA copy confirmed; complete model fingerprint captured",
                        Actual = "Fixture not started",
                        Message = reason
                    }
                }
            };
        }

        private static string Certification(IEnumerable<QaFixtureResult> fixtures, bool? modelRollbackVerified)
        {
            var rows = (fixtures ?? Enumerable.Empty<QaFixtureResult>()).ToList();
            if (modelRollbackVerified == false) return "FAIL";
            if (rows.Any(f => f.IsCriticalFailure)) return "FAIL";
            if (rows.Any(f => f.Status == QaStatus.FAIL)) return "FAIL";
            if (rows.Any(f => f.Status == QaStatus.BLOCKED || f.Status == QaStatus.NOT_RUN)) return "INCOMPLETE";
            return rows.Count == 0 ? "INCOMPLETE" : "PASS";
        }
    }
}
