using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace KhimTools.Core
{
    public enum RegistrationStatus
    {
        Ready,
        Partial,
        Failed
    }

    public class ModuleDiagnosticRecord
    {
        public string ModuleName { get; set; }
        public RegistrationStatus Status { get; set; }
        public int RegisteredCount { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public List<string> Warnings { get; private set; }
        public List<string> Errors { get; private set; }

        public ModuleDiagnosticRecord()
        {
            Status = RegistrationStatus.Ready;
            Warnings = new List<string>();
            Errors = new List<string>();
        }
    }

    /// <summary>
    /// Giám sát và ghi nhận trạng thái khởi tạo giao diện/công cụ (Fault Isolation & Diagnostics).
    /// Ghi nhận chi tiết: Module, Panel, Tool, Command, Exception type, Message, Stack trace.
    /// Không bao giờ nuốt lỗi rỗng và không làm sập chuỗi khởi động.
    /// </summary>
    public static class RegistrationDiagnostics
    {
        private static readonly ConcurrentDictionary<string, ModuleDiagnosticRecord> _records =
            new ConcurrentDictionary<string, ModuleDiagnosticRecord>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _logLock = new object();

        private static string LogFilePath
        {
            get
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string dir = Path.Combine(appData, "Autodesk", "Revit", "Addins", "KhimTools");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    return Path.Combine(dir, "startup_diagnostics.log");
                }
                catch
                {
                    return Path.Combine(Path.GetTempPath(), "khimtools_startup_diagnostics.log");
                }
            }
        }

        public static void Reset()
        {
            _records.Clear();
        }

        public static ModuleDiagnosticRecord GetOrCreate(string moduleName)
        {
            return _records.GetOrAdd(moduleName, delegate(string name) { return new ModuleDiagnosticRecord { ModuleName = name }; });
        }

        public static void RecordSuccess(string moduleName, int count, long elapsedMs)
        {
            var record = GetOrCreate(moduleName);
            record.Status = record.Errors.Count > 0 ? RegistrationStatus.Partial : RegistrationStatus.Ready;
            record.RegisteredCount = count;
            record.ElapsedMilliseconds = elapsedMs;
        }

        public static void RecordWarning(string moduleName, string message)
        {
            var record = GetOrCreate(moduleName);
            record.Warnings.Add(message);
            Trace.WriteLine(string.Format("[K-TOOLS WARN][Module:{0}] {1}", moduleName, message));
        }

        public static void RecordError(string moduleName, string message, Exception ex = null)
        {
            RecordError(moduleName, moduleName, string.Empty, string.Empty, message, ex);
        }

        public static void RecordError(
            string moduleName,
            string panelName,
            string toolName,
            string commandClass,
            string message,
            Exception ex = null)
        {
            var record = GetOrCreate(moduleName);
            record.Status = record.RegisteredCount > 0 ? RegistrationStatus.Partial : RegistrationStatus.Failed;

            var sb = new StringBuilder();
            sb.Append(string.Format("[Module: {0}] [Panel: {1}]", moduleName, panelName));
            if (!string.IsNullOrEmpty(toolName)) sb.Append(string.Format(" [Tool: {0}]", toolName));
            if (!string.IsNullOrEmpty(commandClass)) sb.Append(string.Format(" [Command: {0}]", commandClass));
            sb.Append(string.Format(" - {0}", message));

            if (ex != null)
            {
                sb.Append(string.Format(" | ExceptionType: {0} | Message: {1}", ex.GetType().FullName, ex.Message));
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.Append(string.Format(" | StackTrace: {0}", ex.StackTrace.Replace(Environment.NewLine, " -> ")));
                }
            }

            string detail = sb.ToString();
            record.Errors.Add(detail);
            Trace.WriteLine(string.Format("[K-TOOLS ERROR] {0}", detail));
        }

        public static IReadOnlyDictionary<string, ModuleDiagnosticRecord> GetAllRecords()
        {
            return _records;
        }

        public static string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== K-TOOLS STARTUP & REGISTRATION DIAGNOSTICS REPORT ===");
            sb.AppendLine(string.Format("Timestamp: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));

            foreach (var kvp in _records)
            {
                var r = kvp.Value;
                sb.AppendLine(string.Format("Module: [{0,-16}] Status: {1,-7} Items: {2,2} Duration: {3}ms", r.ModuleName, r.Status, r.RegisteredCount, r.ElapsedMilliseconds));
                if (r.Warnings.Count > 0)
                {
                    foreach (var w in r.Warnings) sb.AppendLine(string.Format("   [WARN] {0}", w));
                }
                if (r.Errors.Count > 0)
                {
                    foreach (var err in r.Errors) sb.AppendLine(string.Format("   [ERROR] {0}", err));
                }
            }

            sb.AppendLine("=========================================================");
            return sb.ToString();
        }

        public static void PersistLog()
        {
            try
            {
                lock (_logLock)
                {
                    string report = GenerateReport();
                    File.AppendAllText(LogFilePath, report + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("[K-TOOLS] Failed to persist diagnostics log: {0}", ex.Message));
            }
        }
    }
}
