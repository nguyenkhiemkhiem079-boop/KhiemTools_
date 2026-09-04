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
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Ready;
        public int RegisteredCount { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
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
                catch (Exception ex)
                {
                    Trace.WriteLine($"[K-TOOLS RegistrationDiagnostics] Lỗi truy cập thư mục Addins ({ex.Message}), chuyển sang thư mục Temp.");
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
            return _records.GetOrAdd(moduleName, name => new ModuleDiagnosticRecord { ModuleName = name });
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
            Trace.WriteLine($"[K-TOOLS WARN][Module:{moduleName}] {message}");
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
            sb.Append($"[Module: {moduleName}] [Panel: {panelName}]");
            if (!string.IsNullOrEmpty(toolName)) sb.Append($" [Tool: {toolName}]");
            if (!string.IsNullOrEmpty(commandClass)) sb.Append($" [Command: {commandClass}]");
            sb.Append($" - {message}");

            if (ex != null)
            {
                sb.Append($" | ExceptionType: {ex.GetType().FullName} | Message: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.Append($" | StackTrace: {ex.StackTrace.Replace(Environment.NewLine, " -> ")}");
                }
            }

            string detail = sb.ToString();
            record.Errors.Add(detail);
            Trace.WriteLine($"[K-TOOLS ERROR] {detail}");
        }

        public static IReadOnlyDictionary<string, ModuleDiagnosticRecord> GetAllRecords()
        {
            return _records;
        }

        public static string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== K-TOOLS STARTUP & REGISTRATION DIAGNOSTICS REPORT ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            foreach (var kvp in _records)
            {
                var r = kvp.Value;
                sb.AppendLine($"Module: [{r.ModuleName,-16}] Status: {r.Status,-7} Items: {r.RegisteredCount,2} Duration: {r.ElapsedMilliseconds}ms");
                if (r.Warnings.Count > 0)
                {
                    foreach (var w in r.Warnings) sb.AppendLine($"   [WARN] {w}");
                }
                if (r.Errors.Count > 0)
                {
                    foreach (var err in r.Errors) sb.AppendLine($"   [ERROR] {err}");
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
                Trace.WriteLine($"[K-TOOLS] Failed to persist diagnostics log: {ex.Message}");
            }
        }
    }
}
