namespace KhimTools.Core.Workflow
{
    public enum WorkflowTransactionPolicy
    {
        AtomicOperation,
        PerItemIsolation,
        BatchAllOrNothing
    }

    public sealed class ExecutionPolicy
    {
        public WorkflowTransactionPolicy TransactionPolicy { get; }
        public bool RegenerateBeforeVerification { get; }
        public bool AllowPartialSuccess { get; }

        public ExecutionPolicy(
            WorkflowTransactionPolicy transactionPolicy,
            bool regenerateBeforeVerification = true,
            bool allowPartialSuccess = false)
        {
            TransactionPolicy = transactionPolicy;
            RegenerateBeforeVerification = regenerateBeforeVerification;
            AllowPartialSuccess = allowPartialSuccess;
        }

        public static ExecutionPolicy Atomic => new ExecutionPolicy(WorkflowTransactionPolicy.AtomicOperation);
        public static ExecutionPolicy PerItem => new ExecutionPolicy(WorkflowTransactionPolicy.PerItemIsolation, true, true);
        public static ExecutionPolicy Batch => new ExecutionPolicy(WorkflowTransactionPolicy.BatchAllOrNothing);
    }
}
