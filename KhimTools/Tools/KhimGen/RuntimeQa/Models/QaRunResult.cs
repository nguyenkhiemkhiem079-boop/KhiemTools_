using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RuntimeQa.Models
{
    public sealed class QaRunResult
    {
        public string RunId { get; set; }
        public string KToolsVersion { get; set; }
        public string GitCommit { get; set; }
        public string RevitVersion { get; set; }
        public string DocumentTitle { get; set; }
        public DateTime Timestamp { get; set; }
        public string MachineNeutralEnvironment { get; set; }
        public List<QaSuiteDefinition> Suites { get; set; } = new List<QaSuiteDefinition>();
        public List<QaFixtureResult> Fixtures { get; set; } = new List<QaFixtureResult>();
        public int Passed { get { return Fixtures.Count(f => f.Status == QaStatus.PASS); } }
        public int Failed { get { return Fixtures.Count(f => f.Status == QaStatus.FAIL); } }
        public int Blocked { get { return Fixtures.Count(f => f.Status == QaStatus.BLOCKED); } }
        public int Skipped { get { return Fixtures.Count(f => f.Status == QaStatus.SKIPPED); } }
        public int NotRun { get { return Fixtures.Count(f => f.Status == QaStatus.NOT_RUN); } }
        public int Total { get { return Fixtures.Count; } }
        public TimeSpan Duration { get; set; }
        public bool ModelRollbackVerified { get; set; }
        public string ModelRollbackMessage { get; set; }
        public string CertificationStatus { get; set; }
        public string ReportDirectory { get; set; }
    }
}
