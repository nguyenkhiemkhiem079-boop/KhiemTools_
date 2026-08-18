using System;
using System.Collections.Generic;

namespace KhimTools.SectionCutTool.Models
{
    /// <summary>
    /// Model cấu hình thông số cắt mặt cắt (Section Cut Settings) có thể lưu/nạp qua JSON Template.
    /// </summary>
    public class SectionCutSettings
    {
        public string Name { get; set; } = "Default";

        // 1. Loại mặt cắt
        public bool CreateLongitudinal { get; set; } = true;
        public bool CreateCrossSection { get; set; } = true;

        // 2. Chế độ cắt ngang
        public CrossSectionCutMode CrossSectionMode { get; set; } = CrossSectionCutMode.KeyPositionsAuto;
        public List<double> RelativePositions { get; set; } = new List<double> { 0.15, 0.50, 0.85 }; // 15%, 50%, 85%
        public double SpacingMm { get; set; } = 1000.0;

        // 3. Tỷ lệ View
        public int LongitudinalScale { get; set; } = 50;  // 1:50
        public int CrossSectionScale { get; set; } = 20;  // 1:20

        // 4. Bù trừ Crop Box (Offsets tính theo mm)
        public double CropOffsetLeftMm { get; set; } = 200.0;
        public double CropOffsetRightMm { get; set; } = 200.0;
        public double CropOffsetTopMm { get; set; } = 200.0;
        public double CropOffsetBottomMm { get; set; } = 200.0;
        public double FarClipOffsetMm { get; set; } = 150.0;

        // 5. Quy tắc đặt tên (Naming Patterns)
        public string LongitudinalNamingPattern { get; set; } = "MC-D-{Mark}";
        public string CrossSectionNamingPattern { get; set; } = "MC-N-{Mark}-{Index}";

        // 6. View Family Type (Loại mặt cắt trong Revit)
        public string SectionViewTypeName { get; set; } = "";

        // 7. View Template & Hiển thị
        public bool ApplyViewTemplate { get; set; } = true;
        public string ViewTemplateName { get; set; } = "";
        public string LongitudinalViewTemplateName { get; set; } = "";
        public string CrossSectionViewTemplateName { get; set; } = "";
        public bool HideCropRegionAfterCreation { get; set; } = false;
        public bool SetFineDetailLevel { get; set; } = true;
    }
}
