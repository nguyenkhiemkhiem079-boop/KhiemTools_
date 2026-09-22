using Autodesk.Revit.DB;

namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyContentItem
    {
        public ElementId SourceElementId { get; set; } = ElementId.InvalidElementId;
        public string SourceUniqueId { get; set; } = string.Empty;
        public ElementId SourceViewId { get; set; } = ElementId.InvalidElementId;
        public string SourceViewUniqueId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public SheetCopyContentKind ContentKind { get; set; }
        public SheetCopyAction Action { get; set; } = SheetCopyAction.SKIP;
        public ElementId TargetElementId { get; set; } = ElementId.InvalidElementId;
        public ElementId TargetViewId { get; set; } = ElementId.InvalidElementId;
        public XYZ BoxCenter { get; set; }
        public XYZ LabelOffset { get; set; }
        public double LabelLineLength { get; set; }
        public string DetailNumber { get; set; } = string.Empty;
        public ElementId ViewportTypeId { get; set; } = ElementId.InvalidElementId;
        public object ViewportRotation { get; set; }
        public bool IsPinned { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public bool IsSegmented { get; set; }
        public int SegmentIndex { get; set; } = -1;
    }
}
