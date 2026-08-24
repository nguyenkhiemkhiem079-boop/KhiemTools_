using System;

namespace KhimTools.SlabStep.Models
{
    public class SlabStepSettings
    {
        public string SelectedFamilyName { get; set; } = string.Empty;
        public string SelectedSymbolName { get; set; } = string.Empty;
        
        public string HeightParameterName { get; set; } = "h"; // mặc định tham số h trong family RINCO_AN_Step
        public string HighSlabThicknessParameter { get; set; } = string.Empty;
        public string LowSlabThicknessParameter { get; set; } = string.Empty;
        
        public bool ReverseOrientation { get; set; } = false;
        
        public double MaxDistanceToleranceMm { get; set; } = 300.0; // 30cm dung sai tìm cạnh ranh giới
    }
}
