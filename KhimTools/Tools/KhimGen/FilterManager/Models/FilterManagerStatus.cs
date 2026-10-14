namespace KhimTools.FilterManager.Models
{
    public sealed class FilterManagerStatus
    {
        public FilterManagerStatusCode Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public override string ToString() { return Code + (string.IsNullOrWhiteSpace(Message) ? string.Empty : ": " + Message); }
    }
}
