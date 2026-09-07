using System;

namespace KhimTools.Architectural.QuickArchi.Models
{
    /// <summary>
    /// Các tham số thiết lập cho bộ công cụ dựng nhanh kiến trúc (Quick Archi).
    /// </summary>
    public class QuickArchiSettings
    {
        public double WallHeightMm { get; set; }
        public double WallOffsetMm { get; set; }
        public bool IsStructural { get; set; }
        public bool AutoCreateRooms { get; set; }
        public bool AutoTagRooms { get; set; }

        public QuickArchiSettings()
        {
            WallHeightMm = 3000.0;
            WallOffsetMm = 0.0;
            IsStructural = false;
            AutoCreateRooms = false;
            AutoTagRooms = false;
        }

        public bool Validate(out string errorMessage)
        {
            if (WallHeightMm <= 0)
            {
                errorMessage = "Chiều cao tường phải lớn hơn 0 mm.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
