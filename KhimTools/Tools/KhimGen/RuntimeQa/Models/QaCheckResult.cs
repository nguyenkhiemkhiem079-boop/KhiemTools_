using System;

namespace KhimTools.RuntimeQa.Models
{
    public sealed class QaCheckResult
    {
        public string CheckId { get; set; }
        public string Name { get; set; }
        public QaStatus Status { get; set; }
        public QaSeverity Severity { get; set; }
        public string Expected { get; set; }
        public string Actual { get; set; }
        public string Message { get; set; }
        public long DurationMs { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }

        public bool IsFailure
        {
            get { return Status == QaStatus.FAIL || (Severity == QaSeverity.CRITICAL && Status != QaStatus.PASS); }
        }
    }
}
