using System.Collections.Generic;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerResult
    {
        public ParameterManagerStatus Status { get; set; }
        public int RequestedTargets { get; set; }
        public int ReadyTargets { get; set; }
        public int UpdatedTargets { get; set; }
        public int NoChangeTargets { get; set; }
        public int BlockedTargets { get; set; }
        public int FailedTargets { get; set; }
        public bool VerificationPassed { get; set; }
        public bool RolledBack { get; set; }
        public IList<ParameterEditItem> Items { get; } = new List<ParameterEditItem>();
        public IList<string> Messages { get; } = new List<string>();
        public string Summary { get { return "Requested: " + RequestedTargets + ", Updated: " + UpdatedTargets + ", No change: " + NoChangeTargets + ", Blocked: " + BlockedTargets + ", Failed: " + FailedTargets; } }
    }
}
