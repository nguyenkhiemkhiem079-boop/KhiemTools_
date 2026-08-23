using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Thông tin chi tiết về 1 lỗi không khởi tạo được thanh thép.
    /// </summary>
    public class RebarGenerationError
    {
        public ElementId HostId { get; set; }
        public string HostName { get; set; }
        public string RebarCategory { get; set; }
        public string ErrorReason { get; set; }

        public override string ToString()
        {
            string hostStr = HostId != null && HostId != ElementId.InvalidElementId
                ? $"{HostName} (ID: {HostId})"
                : HostName ?? "Unknown Host";
            return $"• Host: {hostStr} | Nhóm: {RebarCategory} | Lý do: {ErrorReason}";
        }
    }

    /// <summary>
    /// Báo cáo tổng hợp kết quả sinh thép trên toàn bộ quy trình:
    /// - Đếm số thanh thép tạo thành công / thất bại
    /// - Ghi nhận danh sách lỗi chi tiết kèm ElementId & cấu kiện liên quan
    /// - Cung cấp dữ liệu để hiển thị TaskDialog cảnh báo cho kỹ sư
    /// </summary>
    public class RebarGenerationReport
    {
        public int SuccessBarCount { get; set; }
        public List<RebarGenerationError> Errors { get; } = new List<RebarGenerationError>();

        public int FailureCount => Errors.Count;
        public int AttemptedBarCount => SuccessBarCount + FailureCount;
        public bool HasErrors => Errors.Count > 0;

        public void AddSuccess(int count = 1)
        {
            if (count > 0) SuccessBarCount += count;
        }

        public void AddWarning(string warningMessage)
        {
            AddError(ElementId.InvalidElementId, "Warning", "Validation", warningMessage);
        }

        public void AddError(Element host, string rebarCategory, Exception ex)
        {
            string hostName = host?.Name ?? (host != null ? $"{host.Category?.Name} (ID {host.Id})" : "Unknown Host");
            AddError(host?.Id ?? ElementId.InvalidElementId, hostName, rebarCategory, ex?.Message ?? "Lỗi không xác định");
        }

        public void AddError(ElementId hostId, string hostName, string rebarCategory, string errorReason)
        {
            Errors.Add(new RebarGenerationError
            {
                HostId = hostId ?? ElementId.InvalidElementId,
                HostName = string.IsNullOrWhiteSpace(hostName) ? "Unknown Host" : hostName,
                RebarCategory = string.IsNullOrWhiteSpace(rebarCategory) ? "Rebar" : rebarCategory,
                ErrorReason = errorReason ?? "Unknown Error"
            });
        }

        public void Merge(RebarGenerationReport other)
        {
            if (other == null) return;
            SuccessBarCount += other.SuccessBarCount;
            Errors.AddRange(other.Errors);
        }
    }
}