using System;
using Autodesk.Revit.DB;

namespace KhimTools.Core.Revit
{
    /// <summary>Small guard for deterministic Revit transaction lifecycle outcomes.</summary>
    public static class TransactionBoundary
    {
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
