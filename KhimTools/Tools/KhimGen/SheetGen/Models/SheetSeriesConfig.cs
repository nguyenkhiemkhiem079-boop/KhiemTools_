using System;
using Autodesk.Revit.DB;

namespace KhimTools.SheetGen.Models
{
    /// <summary>
    /// Cấu hình thông tin cho một phân hệ / chuỗi bản vẽ (Sheet Series).
    /// </summary>
    public class SheetSeriesConfig
    {
        public bool IsEnabled { get; set; } = true;
        public string SeriesName { get; set; } = "Series 1";
        public string Prefix { get; set; } = "KC-";
        public int StartNumber { get; set; } = 101;
        public int Count { get; set; } = 5;
        public int Step { get; set; } = 1;
        public string Suffix { get; set; } = "";
        public string NamePattern { get; set; } = "MẶT BẰNG TẦNG {n}";
        public string TitleBlockName { get; set; } = "";
        public ElementId TitleBlockId { get; set; } = ElementId.InvalidElementId;
        public string Discipline { get; set; } = "Structural";
    }
}