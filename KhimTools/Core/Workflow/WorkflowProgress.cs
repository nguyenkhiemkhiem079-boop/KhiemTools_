namespace KhimTools.Core.Workflow
{
    public sealed class WorkflowProgress
    {
        public int Current { get; }
        public int Total { get; }
        public string Operation { get; }
        public string Message { get; }

        public WorkflowProgress(int current, int total, string operation, string message)
        {
            Current = current < 0 ? 0 : current;
            Total = total < 0 ? 0 : total;
            Operation = operation ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
