using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyItem
    {
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public string SourceSheetUniqueId { get; set; } = string.Empty;
        public string SourceSheetNumber { get; set; } = string.Empty;
        public string SourceSheetName { get; set; } = string.Empty;
        public string TargetSheetNumber { get; set; } = string.Empty;
        public string TargetSheetName { get; set; } = string.Empty;
        public ElementId SourceTitleBlockTypeId { get; set; } = ElementId.InvalidElementId;
        public string SourceTitleBlockTypeUniqueId { get; set; } = string.Empty;
        public bool SourceHasTitleBlock { get; set; }
        public int SourceTitleBlockCount { get; set; }
        public int ViewportCount { get; set; }
        public int LegendCount { get; set; }
        public int ScheduleCount { get; set; }
        public int AnnotationCount { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsPlaceholder { get; set; }
        public ViewCopyPolicy ViewPolicy { get; set; } = ViewCopyPolicy.DUPLICATE_VIEWS;
        public SheetCopyStatusCode Status { get; set; } = SheetCopyStatusCode.READY;
        public string Message { get; set; } = string.Empty;
        public string SourceFingerprint { get; set; } = string.Empty;
        public List<SheetCopyContentItem> Contents { get; } = new List<SheetCopyContentItem>();
        public List<SheetCopyDiagnostic> Diagnostics { get; } = new List<SheetCopyDiagnostic>();
        public string DisplayLabel => string.IsNullOrWhiteSpace(SourceSheetName)
            ? SourceSheetNumber : SourceSheetNumber + " - " + SourceSheetName;
    }
}
