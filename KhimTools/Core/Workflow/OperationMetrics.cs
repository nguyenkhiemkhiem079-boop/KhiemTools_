using System;

namespace KhimTools.Core.Workflow
{
    public sealed class OperationMetrics
    {
        public int Requested { get; set; }
        public int Ready { get; set; }
        public int Succeeded { get; set; }
        public int NoChange { get; set; }
        public int Skipped { get; set; }
        public int Blocked { get; set; }
        public int Partial { get; set; }
        public int Failed { get; set; }
        public int RolledBack { get; set; }
        public TimeSpan Duration { get; set; }

        public int Completed => Succeeded + NoChange + Skipped + Partial;
    }
}
