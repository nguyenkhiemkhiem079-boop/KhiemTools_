using System;
using System.Diagnostics;
using KhimTools.Core.Workflow;

namespace KhimTools.Core.Logging
{
    /// <summary>
    /// UI-neutral structured logging facade. A future host may replace the sink without
    /// changing workflow services; no dialog or Revit UI call is made here.
    /// </summary>
    public sealed class KToolsLogger : IKToolsLogger
    {
        public void Log(WorkflowSeverity severity, string operation, string message, string code = null)
        {
            string prefix = string.IsNullOrEmpty(code) ? string.Empty : "[" + code + "] ";
            Debug.WriteLine(string.Concat("[K-TOOLS][", severity, "][", operation ?? string.Empty, "] ", prefix, message ?? string.Empty));
        }

        public void Exception(string operation, Exception exception, string code = null)
        {
            Log(WorkflowSeverity.Error, operation, exception == null ? "Unknown exception." : exception.Message, code);
            if (exception != null)
            {
                Debug.WriteLine(exception.ToString());
            }
        }
    }

    public static class KToolsLog
    {
        private static IKToolsLogger _current = new KToolsLogger();

        public static IKToolsLogger Current
        {
            get { return _current; }
            set { _current = value ?? new KToolsLogger(); }
        }
    }
}
