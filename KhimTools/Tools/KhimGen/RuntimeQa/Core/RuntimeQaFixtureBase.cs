using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    /// <summary>Common exception-safe TransactionGroup boundary for every model fixture.</summary>
    public abstract class RuntimeQaFixtureBase : IRuntimeQaFixture
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Suite { get; }
        public abstract string Description { get; }
        public virtual bool IsCritical { get { return false; } }

        public virtual bool CanRun(RuntimeQaContext context, out string reason)
        {
            return RuntimeQaSafetyGuard.CanRun(context, out reason);
        }

        public QaFixtureResult Run(RuntimeQaContext context)
        {
            var result = new QaFixtureResult
            {
                FixtureId = Id,
                SuiteId = Suite,
                Name = Name,
                Status = QaStatus.NOT_RUN,
                StartedAt = DateTime.Now
            };
            Stopwatch timer = Stopwatch.StartNew();
            string blockedReason = string.Empty;
            if (context == null || !CanRun(context, out blockedReason))
            {
                result.Status = QaStatus.BLOCKED;
                result.Warnings.Add(string.IsNullOrWhiteSpace(blockedReason) ? "Fixture prerequisites are not available." : blockedReason);
                result.Checks.Add(new QaCheckResult
                {
                    CheckId = Id + "_PREREQUISITES",
                    Name = "Runtime prerequisites",
                    Status = QaStatus.BLOCKED,
                    Severity = QaSeverity.WARNING,
                    Expected = "A safe writable local project with required resources",
                    Actual = "Unavailable",
                    Message = blockedReason
                });
                Finish(result, timer);
                return result;
            }

            RuntimeQaModelFingerprint before = RuntimeQaSafetyGuard.CaptureFingerprint(context.Document);
            int initialCreated = context.CreatedElementIds.Count;
            bool groupStarted = false;
            TransactionGroup group = null;
            try
            {
                group = new TransactionGroup(context.Document, "K-TOOLS Runtime QA - " + Id + " (rollback)");
                group.Start();
                groupStarted = true;
                ExecuteFixture(context, result);
                if (result.Status == QaStatus.NOT_RUN) result.Status = ResolveStatus(result);
            }
            catch (Exception ex)
            {
                result.Status = QaStatus.FAIL;
                result.Errors.Add(ex.Message);
                result.Checks.Add(new QaCheckResult
                {
                    CheckId = Id + "_EXCEPTION",
                    Name = "Fixture exception capture",
                    Status = QaStatus.FAIL,
                    Severity = IsCritical ? QaSeverity.CRITICAL : QaSeverity.ERROR,
                    Expected = "Fixture completes without an unhandled exception",
                    Actual = ex.GetType().Name,
                    Message = ex.Message,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.ToString()
                });
            }
            finally
            {
                bool groupRollbackSucceeded = !groupStarted;
                try
                {
                    if (groupStarted && group != null && group.GetStatus() == TransactionStatus.Started)
                        groupRollbackSucceeded = group.RollBack() == TransactionStatus.RolledBack;
                }
                catch (Exception rollbackException)
                {
                    groupRollbackSucceeded = false;
                    result.RollbackVerified = false;
                    result.Errors.Add("Rollback failed: " + rollbackException.Message);
                }
                string rollbackMessage = groupRollbackSucceeded ? string.Empty : "Transaction group rollback was not confirmed.";
                bool rollback = groupRollbackSucceeded && RuntimeQaSafetyGuard.VerifyRollback(context.Document, before,
                    context.CreatedElementIds, out rollbackMessage);
                string additionalRollbackMessage;
                bool additionalRollback;
                try { additionalRollback = VerifyAdditionalRollbackState(context, out additionalRollbackMessage); }
                catch (Exception rollbackException)
                {
                    additionalRollback = false;
                    additionalRollbackMessage = "Additional rollback verification threw " + rollbackException.GetType().Name + ".";
                }
                rollback = rollback && additionalRollback;
                if (!string.IsNullOrWhiteSpace(additionalRollbackMessage))
                    rollbackMessage += " " + additionalRollbackMessage;
                RuntimeQaSafetyGuard.AddRollbackCheck(result, rollback, rollbackMessage);
                result.CreatedElementCount = Math.Max(0, context.CreatedElementIds.Count - initialCreated);
                if (!rollback) result.Status = QaStatus.FAIL;
                while (context.CreatedElementIds.Count > initialCreated) context.CreatedElementIds.RemoveAt(context.CreatedElementIds.Count - 1);
                if (group != null) group.Dispose();
                context.RestoreUiState();
            }
            Finish(result, timer);
            return result;
        }

        protected abstract void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result);

        protected virtual bool VerifyAdditionalRollbackState(RuntimeQaContext context, out string message)
        {
            message = string.Empty;
            return true;
        }

        protected static void Check(QaFixtureResult result, string id, string name, bool passed,
            string expected, string actual, string message, QaSeverity severity = QaSeverity.INFO)
        {
            result.Checks.Add(new QaCheckResult
            {
                CheckId = id,
                Name = name,
                Status = passed ? QaStatus.PASS : QaStatus.FAIL,
                Severity = passed ? QaSeverity.INFO : severity,
                Expected = expected,
                Actual = actual,
                Message = message
            });
        }

        protected static void Block(QaFixtureResult result, string id, string name, string expected, string reason,
            QaSeverity severity = QaSeverity.WARNING)
        {
            result.Checks.Add(new QaCheckResult
            {
                CheckId = id,
                Name = name,
                Status = QaStatus.BLOCKED,
                Severity = severity,
                Expected = expected,
                Actual = "Prerequisite unavailable",
                Message = reason
            });
            result.Warnings.Add(reason);
        }

        protected static void Skip(QaFixtureResult result, string id, string name, string reason)
        {
            result.Checks.Add(new QaCheckResult { CheckId = id, Name = name, Status = QaStatus.SKIPPED,
                Severity = QaSeverity.INFO, Expected = "Selected fixture", Actual = "Excluded", Message = reason });
        }

        private static QaStatus ResolveStatus(QaFixtureResult result)
        {
            if (result.Checks.Exists(c => c.Status == QaStatus.FAIL)) return QaStatus.FAIL;
            if (result.Checks.Exists(c => c.Status == QaStatus.BLOCKED)) return QaStatus.BLOCKED;
            if (result.Checks.Count == 0) return QaStatus.BLOCKED;
            if (result.Checks.Exists(c => c.Status == QaStatus.SKIPPED || c.Status == QaStatus.NOT_RUN)) return QaStatus.SKIPPED;
            return QaStatus.PASS;
        }

        private static void Finish(QaFixtureResult result, Stopwatch timer)
        {
            timer.Stop();
            result.FinishedAt = DateTime.Now;
            result.Duration = timer.Elapsed;
        }
    }
}
