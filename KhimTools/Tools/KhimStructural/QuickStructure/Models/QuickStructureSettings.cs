using System;

namespace KhimTools.Structural.QuickStructure.Models
{
    /// <summary>
    /// Các tham số thiết lập cho bộ công cụ dựng nhanh kết cấu (Quick Structure).
    /// </summary>
    public class QuickStructureSettings
    {
        public bool CreateColumns { get; set; }
        public bool CreateBeams { get; set; }
        public bool CreateFootings { get; set; }
        public bool CreateFloor { get; set; }

        public double BaseOffsetMm { get; set; }
        public double TopOffsetMm { get; set; }
        public double BeamZOffsetMm { get; set; }
        public double FloorOffsetMm { get; set; }

        public QuickStructureSettings()
        {
            CreateColumns = true;
            CreateBeams = true;
            CreateFootings = false;
            CreateFloor = false;

            BaseOffsetMm = 0.0;
            TopOffsetMm = 0.0;
            BeamZOffsetMm = 0.0;
            FloorOffsetMm = 0.0;
        }

        public bool Validate(out string errorMessage)
        {
            if (CreateFloor)
            {
                errorMessage = "Quick Structure floor generation is not implemented.";
                return false;
            }

            if (CreateFootings && !CreateColumns)
            {
                errorMessage = "Footing generation requires column creation in the same operation.";
                return false;
            }

            if (!CreateColumns && !CreateBeams && !CreateFootings)
            {
                errorMessage = "Vui lòng chọn ít nhất một tác vụ mô hình hóa (Cột, Dầm hoặc Móng).";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
