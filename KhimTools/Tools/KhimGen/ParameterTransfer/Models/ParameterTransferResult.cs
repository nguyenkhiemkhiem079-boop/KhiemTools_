namespace KhimTools.ParameterTransfer.Models
{
    public enum ParameterTransferStatus { READY, COPIED, NO_CHANGE, MISSING_TARGET_PARAMETER, READ_ONLY, TYPE_MISMATCH, DATA_TYPE_MISMATCH, UNSAFE_ELEMENT_REFERENCE, SKIPPED_PROTECTED, BLANK_SOURCE_SKIPPED, FAILED }
    public sealed class ParameterTransferResult
    {
        public ParameterKey Key { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public ParameterTransferStatus Status { get; set; }
        public ParameterValueSnapshot OldValue { get; set; }
        public ParameterValueSnapshot NewValue { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
