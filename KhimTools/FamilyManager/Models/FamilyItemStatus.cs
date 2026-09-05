namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Trạng thái của một Family trong tương quan giữa thư viện đĩa và dự án Revit hiện tại.
    /// </summary>
    public enum FamilyItemStatus
    {
        NotFound,           // Tệp .rfa không tồn tại trên đĩa
        NotLoaded,          // Tồn tại trên đĩa nhưng chưa được nạp vào Revit Document
        Loaded,             // Đã được nạp vào Revit Document
        UpToDate,           // Đã nạp và nội dung khớp hoàn toàn với đĩa (không cần reload)
        UpdateAvailable,    // Tệp trên đĩa mới hơn bản đang nạp trong dự án
        LoadFailed,         // Nạp thất bại (có lỗi chi tiết)
        ReloadFailed        // Tải lại thất bại
    }
}
