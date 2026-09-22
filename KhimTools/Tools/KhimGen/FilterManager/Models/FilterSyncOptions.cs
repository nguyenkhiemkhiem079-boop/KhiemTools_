namespace KhimTools.FilterManager.Models
{
    public sealed class FilterSyncOptions
    {
        public bool CopyVisibility { get; set; } = true;
        public bool CopyEnabled { get; set; } = true;
        public bool CopyGraphicOverrides { get; set; } = true;
        public bool CopyOrder { get; set; } = true;
        public bool AddMissingFilters { get; set; } = true;
        public bool ClearOverrides { get; set; }
        public FilterCopyMode Mode { get; set; } = FilterCopyMode.MERGE_SELECTED_FILTERS;
        public bool AllowPartialTargets { get; set; }
        public bool RedirectTemplateControlledTarget { get; set; }
        public bool PreviewOnly { get; set; }
    }
}
