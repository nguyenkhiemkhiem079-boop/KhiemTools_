namespace KhimTools.DoorSchedule.Models;

/// <summary>Explains a missing/incomplete preview without misreporting a model as empty.</summary>
public sealed class PreviewFeedback
{
    public string StatusText { get; init; } = "";
    public string EmptyText { get; init; } = "";
    public bool CanCreate { get; init; }
    public bool ShowDiagnosticTab { get; init; }

    public static PreviewFeedback From(PreviewResult preview)
    {
        var errors = preview.Diagnostics.Where(d => string.Equals(d.Severity, "ERROR", StringComparison.OrdinalIgnoreCase)).ToList();
        string inventory = preview.RawInventory.Count == 0 ? "Chưa đọc được tồn kho host"
            : "Tồn kho host: " + string.Join("; ", preview.RawInventory.Select(kv => $"{(kv.Key == CategoryKind.Doors ? "Cửa đi" : "Cửa sổ")}: {kv.Value}"));
        string counts = $"Đã đọc {preview.TotalCount} instance theo scope/phase; {preview.Rows.Count} dòng xem trước.";
        if (errors.Count > 0)
        {
            var error = errors[0];
            string detail = $"{error.Code}: {error.Message}";
            return new PreviewFeedback
            {
                StatusText = $"{inventory}. {counts} Có {errors.Count} lỗi; chưa thể Tạo hoặc Xuất CSV. {detail}",
                EmptyText = "Chưa có dữ liệu xem trước hoàn chỉnh.\n" + detail + "\nMở tab Cảnh báo / Nguồn dữ liệu để xem nguyên nhân.",
                ShowDiagnosticTab = preview.Rows.Count == 0
            };
        }
        if (preview.TotalCount == 0)
        {
            string reason = preview.Diagnostics.FirstOrDefault(d => d.Code == "HOST_EMPTY_LINKS_PRESENT")?.Message
                ?? (preview.RawInventory.Values.Sum() > 0
                    ? "Host có cửa nhưng không có instance trong phạm vi hiện tại. Chọn Tất cả các tầng và kiểm tra Phase / Phase Filter / Design Options."
                    : preview.RawInventory.Count > 0
                        ? "Host không có instance thuộc Doors / Windows đã chọn. Kiểm tra category của family và cửa có nằm trong Revit Link hay không."
                        : "Chưa xác định được dữ liệu host. Kiểm tra thiết lập và Làm mới xem trước.");
            return new PreviewFeedback { StatusText = $"{inventory}. {counts} {reason}", EmptyText = reason, ShowDiagnosticTab = true };
        }
        return new PreviewFeedback
        {
            StatusText = $"{inventory}. {counts} {WarningCount(preview)} cảnh báo. Đã kiểm tra; có thể Tạo / Cập nhật.",
            CanCreate = true
        };
    }

    private static int WarningCount(PreviewResult preview) => preview.Diagnostics.Count(d => string.Equals(d.Severity, "WARNING", StringComparison.OrdinalIgnoreCase));
}
