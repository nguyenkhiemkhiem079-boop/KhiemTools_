namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Phạm vi xử lý Join/Unjoin/Switch.
    /// </summary>
    public enum ScopeMode
    {
        /// <summary>Chỉ xử lý các phần tử hiển thị trong view hiện tại.</summary>
        CurrentView,

        /// <summary>Xử lý toàn bộ phần tử trong model.</summary>
        AllModel,

        /// <summary>Chỉ xử lý các phần tử đang được chọn trong viewport.</summary>
        Selection
    }
}
