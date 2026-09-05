namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Các nhóm Family logic được quản lý bởi KhimTools Family Manager.
    /// Thiết kế theo hướng Data-driven để mở rộng các nhóm tương lai mà không phải viết lại hệ thống.
    /// </summary>
    public enum FamilyGroupType
    {
        Structure,      // Kết cấu (Cột, Dầm, Móng...) - SELECTIVE LOAD
        Rebar,          // Cốt thép (Rebar Shapes, Bar types...) - FULL LIBRARY LOAD
        Architecture,   // Kiến trúc (Cửa, Tường, Trần...)
        MEP,            // Cơ điện (Ống gió, Van, Thiết bị...)
        Annotation,     // Ký hiệu & Chú thích (Tag, Callout, Dim...)
        Detail,         // Chi tiết 2D
        Steel,          // Kết cấu thép chuyên biệt
        Precast,        // Bê tông đúc sẵn
        Formwork        // Ván khuôn
    }
}
