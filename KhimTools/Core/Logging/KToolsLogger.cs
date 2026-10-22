using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using KhimTools.Core.Workflow;

namespace KhimTools.Core.Logging
{
    /// <summary>
    /// UI-neutral structured logging facade. A future host may replace the sink without
    /// changing workflow services; no dialog or Revit UI call is made here.
    /// </summary>
    public sealed class KToolsLogger : IKToolsLogger
    {
        private const long MaxLogFileBytes = 4 * 1024 * 1024;
        private const int RetainedLogFiles = 3;
        private static readonly object FileLock = new object();
        private readonly string _logDirectory;

        public KToolsLogger()
            : this(GetDefaultLogDirectory())
        {
        }

        public KToolsLogger(string logDirectory)
        {
            _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
                ? GetDefaultLogDirectory()
                : logDirectory;
        }

        public void Log(WorkflowSeverity severity, string operation, string message, string code = null)
        {
            string prefix = string.IsNullOrEmpty(code) ? string.Empty : "[" + code + "] ";
            string entry = string.Concat(
                "[", DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture), "]",
                "[", severity, "]",
                "[", Normalize(operation), "] ",
                prefix,
                Normalize(message));
            Debug.WriteLine(entry);
            TryAppend(entry);
        }

        public void Log(WorkflowSeverity severity, WorkflowLogContext context, string message, string code = null)
        {
            if (context == null)
            {
                Log(severity, string.Empty, message, code);
                return;
            }

            string details = string.Concat(
                "module=", context.Module ?? string.Empty,
                ";operation=", context.Operation ?? string.Empty,
                ";document=", context.DocumentFingerprint ?? string.Empty,
                ";targets=", context.TargetCount,
                ";durationMs=", context.Duration.TotalMilliseconds.ToString("F0"),
                ";outcome=", context.Outcome);
            Log(severity, context.Operation, details + ";message=" + (message ?? string.Empty), code);
        }

        public void Exception(string operation, Exception exception, string code = null)
        {
            Log(WorkflowSeverity.Error, operation, exception == null ? "Unknown exception." : exception.Message, code);
            if (exception != null)
            {
                Debug.WriteLine(exception.ToString());
                TryAppend("exception=" + Normalize(exception.ToString()));
            }
        }

        private void TryAppend(string entry)
        {
            try
            {
                lock (FileLock)
                {
                    Directory.CreateDirectory(_logDirectory);
                    string path = GetCurrentLogPath();
                    RotateIfNeeded(path);
                    File.AppendAllText(path, entry + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception loggingException)
            {
                // Logging must never prevent Revit from loading or running a command.
                Debug.WriteLine("[K-TOOLS][Logging][FILE_SINK_FAILED] " + loggingException.Message);
            }
        }

        private string GetCurrentLogPath()
        {
            return Path.Combine(_logDirectory, "K-TOOLS_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
        }

        private static void RotateIfNeeded(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < MaxLogFileBytes)
            {
                return;
            }

            string oldest = path + "." + RetainedLogFiles;
            if (File.Exists(oldest)) File.Delete(oldest);

            for (int index = RetainedLogFiles - 1; index >= 1; index--)
            {
                string source = path + "." + index;
                string destination = path + "." + (index + 1);
                if (File.Exists(source)) File.Move(source, destination);
            }

            File.Move(path, path + ".1");
        }

        private static string GetDefaultLogDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Path.GetTempPath();
            }

            return Path.Combine(localAppData, "K-TOOLS", "Logs");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
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
