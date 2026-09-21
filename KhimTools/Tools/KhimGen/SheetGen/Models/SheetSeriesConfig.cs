using Autodesk.Revit.DB;

namespace KhimTools.SheetGen.Models
{
    /// <summary>Configuration for one sheet series. Defaults remain compatible with the original SheetGen UI.</summary>
    public class SheetSeriesConfig
    {
        public bool IsEnabled { get; set; } = true;
        public string SeriesName { get; set; } = "Series 1";
        public string Prefix { get; set; } = "KC-";
        public int StartNumber { get; set; } = 101;
        public int Count { get; set; } = 5;
        public int Step { get; set; } = 1;
        public string Suffix { get; set; } = "";
        public int NumberPadding { get; set; } = 2;
        public string NamePattern { get; set; } = "MẶT BẰNG TẦNG {n}";
        public string TitleBlockName { get; set; } = "";
        public ElementId TitleBlockId { get; set; } = ElementId.InvalidElementId;
        public bool AllowBlankTitleBlock { get; set; }
        public string Discipline { get; set; } = "Structural";
    }
}
