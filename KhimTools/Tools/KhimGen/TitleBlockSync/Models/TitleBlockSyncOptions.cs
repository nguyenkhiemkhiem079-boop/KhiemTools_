namespace KhimTools.TitleBlockSync.Models
{
    public sealed class TitleBlockSyncOptions
    {
        public bool SyncTitleBlockType { get; set; } = true;
        public bool SyncInstanceParameters { get; set; } = true;
        public bool SyncSheetParameters { get; set; } = true;
        public bool OverwriteBlankSource { get; set; }
        public bool AllowPartialTarget { get; set; }
        public bool PreserveTargetPosition { get; set; } = true;
        public bool MatchSourcePosition { get; set; }
    }
}
