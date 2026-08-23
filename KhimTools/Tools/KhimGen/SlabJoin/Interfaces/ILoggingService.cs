using System;
using KhimTools.SlabJoin.Models;

namespace KhimTools.SlabJoin.Interfaces
{
    /// <summary>
    /// Abstraction over a file-based logging service used to record execution
    /// details for the Join/Unjoin Slabs commands (joined pair ids, skipped ids,
    /// errors, and execution time).
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message, optionally including exception details.
        /// </summary>
        void LogError(string message, Exception exception = null);

        /// <summary>
        /// Writes a formatted end-of-run summary (counts, elapsed time, joined/skipped ids)
        /// to the log file.
        /// </summary>
        void WriteSummary(OperationSummary summary);

        /// <summary>
        /// Full path to the log file currently being written to.
        /// </summary>
        string GetLogFilePath();
    }
}
