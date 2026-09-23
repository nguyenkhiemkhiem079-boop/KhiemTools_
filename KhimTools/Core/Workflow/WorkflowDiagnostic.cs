using System;

namespace KhimTools.Core.Workflow
{
    /// <summary>
    /// A serializable, UI-neutral explanation of a workflow decision or failure.
    /// </summary>
    public sealed class WorkflowDiagnostic
    {
        public string Code { get; }
        public WorkflowSeverity Severity { get; }
        public string Message { get; }
        public bool CanExecute { get; }
        public string ElementUniqueId { get; }
        public string Context { get; }

        public WorkflowDiagnostic(
            string code,
            WorkflowSeverity severity,
            string message,
            bool canExecute,
            string elementUniqueId = null,
            string context = null)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            CanExecute = canExecute;
            ElementUniqueId = elementUniqueId ?? string.Empty;
            Context = context ?? string.Empty;
        }

        public static WorkflowDiagnostic Info(string code, string message, string context = null)
        {
            return new WorkflowDiagnostic(code, WorkflowSeverity.Info, message, true, null, context);
        }

        public static WorkflowDiagnostic Warning(string code, string message, bool canExecute = true, string context = null)
        {
            return new WorkflowDiagnostic(code, WorkflowSeverity.Warning, message, canExecute, null, context);
        }

        public static WorkflowDiagnostic Error(string code, string message, bool canExecute = false, string elementUniqueId = null, string context = null)
        {
            return new WorkflowDiagnostic(code, WorkflowSeverity.Error, message, canExecute, elementUniqueId, context);
        }

        public override string ToString()
        {
            return string.Concat(Code, " [", Severity, "] ", Message);
        }
    }
}
