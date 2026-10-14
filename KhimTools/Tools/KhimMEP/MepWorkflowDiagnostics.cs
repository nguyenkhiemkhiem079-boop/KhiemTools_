using System;
using Autodesk.Revit.DB;
using KhimTools.Core.Logging;
using KhimTools.Core.Workflow;

namespace KhimTools.MEP
{
    internal static class MepWorkflowDiagnostics
    {
        internal static void Log(string command, Document document, string operation,
            int requested, int eligible, int processed, int changed, int skipped, int failed,
            TimeSpan duration, string exceptionType = null, string transactionState = "NONE_READ_ONLY",
            string postcondition = "NOT_APPLICABLE")
        {
            string documentKey = document == null ? string.Empty : DocumentIdentity.From(document).StableKey;
            string context = "document=" + documentKey + ";operation=" + operation +
                ";requested=" + requested + ";eligible=" + eligible + ";processed=" + processed +
                ";changed=" + changed + ";skipped=" + skipped + ";failed=" + failed +
                ";transaction=" + transactionState + ";postcondition=" + postcondition + ";durationMs=" + duration.TotalMilliseconds.ToString("F0",
                    System.Globalization.CultureInfo.InvariantCulture) + ";exception=" + (exceptionType ?? string.Empty);
            WorkflowDiagnostic diagnostic = failed == 0 && string.IsNullOrEmpty(exceptionType)
                ? WorkflowDiagnostic.Info("MEP_WORKFLOW_COMPLETED", command + " completed.", context)
                : WorkflowDiagnostic.Error("MEP_WORKFLOW_PARTIAL_OR_FAILED", command + " reported failed work.", true, context: context);
            KToolsLog.Current.Log(diagnostic.Severity, command, diagnostic.ToString(), diagnostic.Code);
        }
    }
}
