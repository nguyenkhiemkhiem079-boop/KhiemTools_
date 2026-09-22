namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyOptions
    {
        public ViewCopyPolicy ViewPolicy { get; set; } = ViewCopyPolicy.DUPLICATE_VIEWS;
        public bool ReuseLegends { get; set; } = true;
        public bool ReuseSchedules { get; set; } = true;
        public bool CopySafeSheetParameters { get; set; } = true;
        public bool CopySafeTitleBlockParameters { get; set; } = true;
        public bool CopySafeAnnotations { get; set; } = true;
        public bool CopyRevisions { get; set; }
        public bool ApplyKToolsViewNaming { get; set; } = true;
        public bool AllowOptionalSkips { get; set; }
        public bool AutoResolveDetailNumbers { get; set; }
        public bool PreserveViewportType { get; set; } = true;
        public bool PreserveViewportTitles { get; set; } = true;
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string NumberPattern { get; set; } = "{SheetNumber}-COPY";
        public string NamePattern { get; set; } = "{SheetName}";
    }
}
