namespace KhimTools.ParameterTransfer.Models
{
    public sealed class ParameterTransferOptions
    {
        public bool OverwriteBlankSource { get; set; }
        public bool AllowElementId { get; set; }
        public bool ProtectIdentity { get; set; } = true;
    }
}
