using System;
using Autodesk.Revit.DB;
using KhimTools.Core.Logging;
using KhimTools.Core.Workflow;

namespace KhimTools.Architectural
{
    internal static class ArchitecturalDiagnostics
    {
        internal static void Log(string command, Document document, string operation, int requested,
            int affected, int failures, TimeSpan duration, string exceptionType = null)
        {
            string key = document == null ? string.Empty : DocumentIdentity.From(document).StableKey;
            string context = "document=" + key + ";operation=" + operation + ";requested=" + requested +
                ";affected=" + affected + ";failures=" + failures + ";durationMs=" +
                duration.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                ";exception=" + (exceptionType ?? string.Empty);
            var diagnostic = failures == 0 && string.IsNullOrEmpty(exceptionType)
                ? WorkflowDiagnostic.Info("ARCH_WORKFLOW_COMPLETED", command + " completed.", context)
                : WorkflowDiagnostic.Error("ARCH_WORKFLOW_FAILED", command + " had failed work.", true,
                    context: context);
            KToolsLog.Current.Log(diagnostic.Severity, command, diagnostic.ToString(), diagnostic.Code);
        }
    }
}
