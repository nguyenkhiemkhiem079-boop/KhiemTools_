using System;
using Autodesk.Revit.DB;

namespace KhimTools.Core.Revit
{
    /// <summary>Small guard for deterministic Revit transaction lifecycle outcomes.</summary>
    public static class TransactionBoundary
    {
        /// <summary>
        /// Executes a synchronous model mutation while this core boundary owns the
        /// transaction lifecycle. The callback must not start or complete its own
        /// transaction.
        /// </summary>
        public static T Execute<T>(Document document, string operation, Func<T> mutation,
            Action<Transaction> configure = null, Predicate<T> shouldCommit = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));

            using (var transaction = new Transaction(document, operation))
            {
                Start(transaction, operation);
                try
                {
                    configure?.Invoke(transaction);
                    T result = mutation();
                    if (shouldCommit != null && !shouldCommit(result))
                    {
                        RollBack(transaction, operation);
                        return result;
                    }

                    Commit(transaction, operation);
                    return result;
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        RollBack(transaction, operation);
                    throw;
                }
            }
        }

        public static void Execute(Document document, string operation, Action mutation,
            Action<Transaction> configure = null)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            Execute(document, operation, () => { mutation(); return true; }, configure);
        }

        /// <summary>Executes a batch whose individual transactions are owned by their services.</summary>
        public static T ExecuteGroup<T>(Document document, string operation, Func<T> mutation)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));

            using (var group = new TransactionGroup(document, operation))
            {
                Start(group, operation);
                try
                {
                    T result = mutation();
                    Assimilate(group, operation);
                    return result;
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started)
                        RollBack(group, operation);
                    throw;
                }
            }
        }

        public static void Start(Transaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            TransactionStatus status = transaction.Start();
            if (status != TransactionStatus.Started) throw Failure(operation, "START", status);
        }

        public static void Commit(Transaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            TransactionStatus status = transaction.Commit();
            if (status != TransactionStatus.Committed) throw Failure(operation, "COMMIT", status);
        }

        public static void RollBack(Transaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.GetStatus() != TransactionStatus.Started) return;
            TransactionStatus status = transaction.RollBack();
            if (status != TransactionStatus.RolledBack) throw Failure(operation, "ROLLBACK", status);
        }

        public static void Start(SubTransaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            TransactionStatus status = transaction.Start();
            if (status != TransactionStatus.Started) throw Failure(operation, "SUBTRANSACTION_START", status);
        }

        public static void Commit(SubTransaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            TransactionStatus status = transaction.Commit();
            if (status != TransactionStatus.Committed) throw Failure(operation, "SUBTRANSACTION_COMMIT", status);
        }

        public static void RollBack(SubTransaction transaction, string operation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.GetStatus() != TransactionStatus.Started) return;
            TransactionStatus status = transaction.RollBack();
            if (status != TransactionStatus.RolledBack) throw Failure(operation, "SUBTRANSACTION_ROLLBACK", status);
        }

        public static void Start(TransactionGroup group, string operation)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            TransactionStatus status = group.Start();
            if (status != TransactionStatus.Started) throw Failure(operation, "GROUP_START", status);
        }

        public static void Assimilate(TransactionGroup group, string operation)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            TransactionStatus status = group.Assimilate();
            if (status != TransactionStatus.Committed) throw Failure(operation, "GROUP_COMMIT", status);
        }

        public static void RollBack(TransactionGroup group, string operation)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (group.GetStatus() != TransactionStatus.Started) return;
            TransactionStatus status = group.RollBack();
            if (status != TransactionStatus.RolledBack) throw Failure(operation, "GROUP_ROLLBACK", status);
        }

        private static InvalidOperationException Failure(string operation, string phase, TransactionStatus status)
        {
            return new InvalidOperationException("TRANSACTION_" + phase + "_FAILED: " + (operation ?? "Unknown operation") + "; status=" + status);
        }
    }
}
