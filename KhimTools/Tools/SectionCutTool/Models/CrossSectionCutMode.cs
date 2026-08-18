using System;

namespace KhimTools.SectionCutTool.Models
{
    /// <summary>
    /// Chế độ xác định vị trí cắt ngang trên cấu kiện.
    /// </summary>
    public enum CrossSectionCutMode
    {
        /// <summary>Cắt theo các vị trí tương đối % (VD: 15%, 50%, 85%)</summary>
        RelativePositions,

        /// <summary>Cắt theo bước khoảng cách đều (VD: mỗi 1000mm)</summary>
        FixedSpacing,

        /// <summary>Tự động nhận diện các vị trí đặc trưng (Gối trái, Giữa nhịp, Gối phải)</summary>
        KeyPositionsAuto,

        /// <summary>Chọn điểm thủ công (Pick point)</summary>
        ManualPick
    }
}
