using System;
using Autodesk.Revit.DB;
using KhimTools.Core.Logging;

namespace KhimTools.Core.Workflow
{
    public enum WorkflowExecutionState { SUCCESS, USER_CANCEL, VALIDATION_FAILURE, REVIT_FAILURE, KTOOL_FAILURE, NO_CHANGE, PREVIEW_ONLY }
    public enum WorkflowTransactionState { NOT_STARTED, COMMITTED, ROLLED_BACK, FAILED }
    public enum WorkflowPostconditionState { NOT_RUN, PASSED, FAILED, NOT_APPLICABLE }

    /// <summary>Structured per-command runtime evidence. It deliberately stores a stable document key, never a local path.</summary>
    public sealed class WorkflowExecutionRecord
    {
        public string Command { get; set; } = string.Empty;
        public string DocumentKey { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string InputSummary { get; set; } = string.Empty;
        public WorkflowExecutionState State { get; set; }
        public bool ValidationPassed { get; set; }
        public bool TransactionStarted { get; set; }
        public int RequestedElementCount { get; set; }
        public WorkflowTransactionState TransactionState { get; set; } = WorkflowTransactionState.NOT_STARTED;
        public TransactionStatus? RevitTransactionStatus { get; set; }
        public WorkflowPostconditionState PostconditionState { get; set; } = WorkflowPostconditionState.NOT_RUN;
        public string Postcondition { get; set; } = string.Empty;
        public int AffectedElementCount { get; set; }
        public int WarningCount { get; set; }
        public int FailureCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string ExceptionType { get; set; } = string.Empty;
        public string ExceptionCategory { get; set; } = string.Empty;
        public string RollbackResult { get; set; } = string.Empty;
        public bool RollbackVerified { get; set; }
        public bool ModelUnchanged { get; set; }

        public bool IsConsistent(WorkflowOutcome outcome)
        {
            if (State == WorkflowExecutionState.SUCCESS)
                return TransactionState == WorkflowTransactionState.COMMITTED && RevitTransactionStatus == TransactionStatus.Committed &&
                    PostconditionState == WorkflowPostconditionState.PASSED &&
                    (outcome == WorkflowOutcome.Succeeded || outcome == WorkflowOutcome.Partial);
            if (State == WorkflowExecutionState.NO_CHANGE)
                return outcome == WorkflowOutcome.NoChange && AffectedElementCount == 0 && ModelUnchanged &&
                    TransactionState != WorkflowTransactionState.ROLLED_BACK;
            if (State == WorkflowExecutionState.PREVIEW_ONLY)
                return outcome == WorkflowOutcome.Skipped && TransactionState == WorkflowTransactionState.NOT_STARTED &&
                    !TransactionStarted && AffectedElementCount == 0 && ModelUnchanged;
            if (State == WorkflowExecutionState.USER_CANCEL || State == WorkflowExecutionState.VALIDATION_FAILURE)
                return TransactionState == WorkflowTransactionState.NOT_STARTED && !TransactionStarted && AffectedElementCount == 0 && ModelUnchanged && outcome != WorkflowOutcome.Succeeded && outcome != WorkflowOutcome.Partial;
            if (State == WorkflowExecutionState.REVIT_FAILURE || State == WorkflowExecutionState.KTOOL_FAILURE)
                return outcome != WorkflowOutcome.Succeeded && outcome != WorkflowOutcome.Partial &&
                    TransactionState != WorkflowTransactionState.COMMITTED &&
                    (TransactionState != WorkflowTransactionState.ROLLED_BACK || RollbackVerified) &&
                    (TransactionState != WorkflowTransactionState.ROLLED_BACK || AffectedElementCount == 0) &&
                    (TransactionState == WorkflowTransactionState.NOT_STARTED || ModelUnchanged || RollbackVerified);
            return false;
        }

        public void AssertConsistent(WorkflowOutcome outcome)
        {
            if (!IsConsistent(outcome))
                throw new InvalidOperationException("WORKFLOW_RESULT_INVARIANT: state=" + State + "; transaction=" + TransactionState + "; postcondition=" + PostconditionState + "; outcome=" + outcome);
        }

        public static WorkflowExecutionState Classify(Exception exception)
        {
            if (exception == null) return WorkflowExecutionState.KTOOL_FAILURE;
            if (exception.Message != null && (exception.Message.StartsWith("TRANSACTION_", StringComparison.Ordinal) || exception.Message.StartsWith("KTOOL_FAILURE", StringComparison.Ordinal)))
                return WorkflowExecutionState.KTOOL_FAILURE;
            string type = exception.GetType().FullName ?? string.Empty;
            return type.StartsWith("Autodesk.Revit.Exceptions.", StringComparison.Ordinal)
                ? WorkflowExecutionState.REVIT_FAILURE : WorkflowExecutionState.KTOOL_FAILURE;
        }

        public static WorkflowExecutionRecord Create(string command, string operation, string inputSummary,
            WorkflowExecutionState state, WorkflowOutcome outcome, TransactionStatus? transactionStatus,
            WorkflowPostconditionState postconditionState, string postcondition, int requestedCount,
            int affectedCount, int warningCount, int failureCount, TimeSpan duration,
            bool transactionStarted, bool rollbackVerified, bool modelUnchanged, Exception exception = null, string documentKey = null)
        {
            WorkflowTransactionState transactionState = transactionStatus == TransactionStatus.Committed ? WorkflowTransactionState.COMMITTED :
                transactionStatus == TransactionStatus.RolledBack ? WorkflowTransactionState.ROLLED_BACK :
                transactionStatus.HasValue ? WorkflowTransactionState.FAILED : WorkflowTransactionState.NOT_STARTED;
            var record = new WorkflowExecutionRecord
            {
                Command = command ?? string.Empty,
                DocumentKey = documentKey ?? string.Empty,
                Operation = operation ?? string.Empty,
                InputSummary = inputSummary ?? string.Empty,
                State = state,
                ValidationPassed = state != WorkflowExecutionState.VALIDATION_FAILURE,
                TransactionStarted = transactionStarted,
                RequestedElementCount = requestedCount,
                TransactionState = transactionState,
                RevitTransactionStatus = transactionStatus,
                PostconditionState = postconditionState,
                Postcondition = postcondition ?? string.Empty,
                AffectedElementCount = affectedCount,
                WarningCount = warningCount,
                FailureCount = failureCount,
                Duration = duration,
                ExceptionType = exception == null ? string.Empty : exception.GetType().FullName,
                ExceptionCategory = exception == null ? string.Empty : Classify(exception).ToString(),
                RollbackResult = transactionStatus == TransactionStatus.RolledBack ? transactionStatus.Value.ToString() : string.Empty,
                RollbackVerified = rollbackVerified,
                ModelUnchanged = modelUnchanged
            };
            record.AssertConsistent(outcome);
            KToolsLog.Current.Log(outcome == WorkflowOutcome.Succeeded || outcome == WorkflowOutcome.Partial ? WorkflowSeverity.Info : WorkflowSeverity.Error,
                command + "." + operation,
                "state=" + state + ";tx=" + record.TransactionState + ";postcondition=" + record.PostconditionState + ";requested=" + requestedCount + ";affected=" + affectedCount + ";warnings=" + warningCount + ";failures=" + failureCount + ";durationMs=" + duration.TotalMilliseconds.ToString("F0") + ";rollback=" + rollbackVerified,
                state.ToString());
            return record;
        }

        public static WorkflowExecutionRecord RecordNonMutation(string command, string operation, string inputSummary,
            WorkflowExecutionState state, WorkflowOutcome outcome, int requestedCount, int failureCount, string diagnosticCode, string documentKey = null, TimeSpan? duration = null)
        {
            if (state != WorkflowExecutionState.USER_CANCEL && state != WorkflowExecutionState.VALIDATION_FAILURE &&
                state != WorkflowExecutionState.NO_CHANGE && state != WorkflowExecutionState.PREVIEW_ONLY)
                throw new ArgumentOutOfRangeException(nameof(state));
            var record = Create(command, operation, inputSummary, state, outcome, null,
                state == WorkflowExecutionState.PREVIEW_ONLY ? WorkflowPostconditionState.NOT_APPLICABLE : WorkflowPostconditionState.NOT_RUN,
                diagnosticCode ?? string.Empty, requestedCount, 0, 0, failureCount, duration ?? TimeSpan.Zero,
                false, false, true, null, documentKey);
            return record;
        }
    }
}
