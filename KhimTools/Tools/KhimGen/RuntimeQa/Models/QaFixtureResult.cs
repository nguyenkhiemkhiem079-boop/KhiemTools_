using System;
using System.Collections.Generic;

namespace KhimTools.RuntimeQa.Models
{
    public sealed class QaFixtureResult
    {
        public string FixtureId { get; set; }
        public string SuiteId { get; set; }
        public string Name { get; set; }
        public QaStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public List<QaCheckResult> Checks { get; set; } = new List<QaCheckResult>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public int CreatedElementCount { get; set; }
        public bool RollbackVerified { get; set; }
        public List<string> Artifacts { get; set; } = new List<string>();

        public bool IsCriticalFailure
        {
            get { return Checks.Exists(c => c != null && c.Severity == QaSeverity.CRITICAL && c.Status == QaStatus.FAIL); }
        }
    }
}
