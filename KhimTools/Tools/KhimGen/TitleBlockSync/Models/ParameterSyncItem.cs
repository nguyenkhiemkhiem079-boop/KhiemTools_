using System.Collections.Generic;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class ParameterSyncItem
    {
        public TitleBlockSyncScope Scope { get; set; }
        public ParameterTransferPlan TransferPlan { get; set; }
        public bool Selected { get; set; }
        public int TargetTotal { get; set; }
        public int Matched { get; set; }
        public int Different { get; set; }
        public int Missing { get; set; }
        public int ReadOnly { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<ParameterSyncTargetPlan> Targets { get; } = new List<ParameterSyncTargetPlan>();
    }
    public sealed class ParameterSyncTargetPlan
    {
        public ParameterKey Key { get; set; }
        public ParameterValueSnapshot OldValue { get; set; }
        public ParameterValueSnapshot NewValue { get; set; }
        public bool CanExecute { get; set; }
        public TitleBlockSyncStatusCode Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
