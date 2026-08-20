using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using KhimTools.Core;
using KhimTools.SlabJoin.Interfaces;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Utilities;

namespace KhimTools.SlabJoin.Services
{
    /// <summary>
    /// File-based implementation of <see cref="ILoggingService"/>.
    /// Writes a timestamped log file per command execution to:
    /// %AppData%\KhimTools\Logs\KhimTools_yyyyMMdd_HHmmss.log
    /// </summary>
    public sealed class LoggingService : ILoggingService
    {
        private readonly string _logFilePath;
        private readonly object _syncRoot = new object();

        public LoggingService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDirectory = Path.Combine(appData, "KhimTools", "Logs");

            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (IOException)
            {
                // Fall back to the temp directory if the AppData path is unavailable for any reason.
                logDirectory = Path.Combine(Path.GetTempPath(), "KhimTools", "Logs");
                Directory.CreateDirectory(logDirectory);
            }

            string fileName = $"KhimTools_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            _logFilePath = Path.Combine(logDirectory, fileName);

            WriteLine("=====================================================");
            WriteLine($"Slab Join Tool - Log started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine("=====================================================");
        }

        public void LogInfo(string message) => WriteLine($"[INFO]  {message}");

        public void LogWarning(string message) => WriteLine($"[WARN]  {message}");

        public void LogError(string message, Exception exception = null)
        {
            var sb = new StringBuilder();
            sb.Append($"[ERROR] {message}");
            if (exception != null)
            {
                sb.Append($" | Exception: {exception.GetType().Name}: {exception.Message}");
            }
            WriteLine(sb.ToString());
        }

        public void WriteSummary(OperationSummary summary)
        {
            WriteLine("-----------------------------------------------------");
            WriteLine($"OPERATION SUMMARY: {summary.OperationType}");
            WriteLine("-----------------------------------------------------");
            WriteLine($"Structural floors scanned : {summary.TotalStructuralFloorsScanned}");
            WriteLine($"Candidate pairs found     : {summary.CandidatePairsFound}");
            WriteLine($"Pairs changed             : {summary.SuccessCount}");
            WriteLine($"Pairs already in state    : {summary.AlreadyInDesiredStateCount}");
            WriteLine($"Pairs errored             : {summary.ErrorCount}");
            WriteLine($"Elements skipped (pre-filter) : {summary.SkippedElements.Count}");
            WriteLine($"Elapsed time              : {summary.ElapsedTime.TotalSeconds:F3} s");
            WriteLine(string.Empty);

            if (summary.SkippedElements.Count > 0)
            {
                WriteLine("Skipped elements:");
                foreach (var skipped in summary.SkippedElements)
                {
                    WriteLine($"  - Id {skipped.ElementId.ToLongValue()} : {skipped.Reason}");
                }
                WriteLine(string.Empty);
            }

            var changed = summary.ProcessedPairs.Where(p => p.Success && !p.IsError).ToList();
            if (changed.Count > 0)
            {
                WriteLine("Joined/Unjoined pairs:");
                foreach (var pair in changed)
                {
                    WriteLine($"  - ({pair.FloorIdA.ToLongValue()}, {pair.FloorIdB.ToLongValue()}) : {pair.Message}");
                }
                WriteLine(string.Empty);
            }

            var errors = summary.ProcessedPairs.Where(p => p.IsError).ToList();
            if (errors.Count > 0)
            {
                WriteLine("Pair errors:");
                foreach (var pair in errors)
                {
                    WriteLine($"  - ({pair.FloorIdA.ToLongValue()}, {pair.FloorIdB.ToLongValue()}) : {pair.Message}");
                }
                WriteLine(string.Empty);
            }

            if (summary.Errors.Count > 0)
            {
                WriteLine("Unhandled errors:");
                foreach (var err in summary.Errors)
                {
                    WriteLine($"  - {err}");
                }
                WriteLine(string.Empty);
            }

            WriteLine("=====================================================");
            WriteLine($"Log ended {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine("=====================================================");
        }

        public string GetLogFilePath() => _logFilePath;

        private void WriteLine(string line)
        {
            lock (_syncRoot)
            {
                try
                {
                    string timestamped = $"{DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {line}";
                    File.AppendAllText(_logFilePath, timestamped + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException)
                {
                    // Logging must never crash the command. Swallow I/O failures silently.
                }
                catch (UnauthorizedAccessException)
                {
                    // Same rationale as above.
                }
            }
        }
    }
}
