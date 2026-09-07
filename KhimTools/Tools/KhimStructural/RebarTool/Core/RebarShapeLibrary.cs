using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Family;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Trạng thái nạp family hình dạng thép.
    /// </summary>
    public enum RebarShapeLoadStatus
    {
        AlreadyLoaded,
        NewlyLoaded,
        MissingFile,
        Failed
    }

    /// <summary>
    /// Chi tiết kết quả nạp từng Rebar Shape.
    /// </summary>
    public class RebarShapeLoadDetail
    {
        public string ShapeName { get; set; }
        public RebarShapeLoadStatus Status { get; set; }
        public string Message { get; set; }

        public RebarShapeLoadDetail(string shapeName, RebarShapeLoadStatus status, string message)
        {
            ShapeName = shapeName;
            Status = status;
            Message = message;
        }
    }

    /// <summary>
    /// Báo cáo tổng kết nạp hàng loạt Rebar Shape vào dự án.
    /// </summary>
    public class RebarShapeLoadSummary
    {
        public int NewlyLoadedCount { get; set; }
        public int AlreadyLoadedCount { get; set; }
        public int MissingFileCount { get; set; }
        public int FailedCount { get; set; }
        public List<RebarShapeLoadDetail> Details { get; set; }

        public int TotalProcessed
        {
            get { return NewlyLoadedCount + AlreadyLoadedCount + MissingFileCount + FailedCount; }
        }

        public RebarShapeLoadSummary()
        {
            Details = new List<RebarShapeLoadDetail>();
        }
    }

    /// <summary>
    /// Nạp các family Rebar Shape tuỳ chỉnh (bộ JP_T## theo BS 8666:2005) từ thư viện K-TOOLS
    /// vào project hiện tại, rồi trả về RebarShape để dùng cho Rebar.CreateFromRebarShape
    /// thay vì để Revit tự sinh shape mặc định.
    ///
    /// Định vị file tự động thông qua FamilyPathResolver (MSI bundle, Assembly folder, Dev tree),
    /// hoàn toàn loại bỏ các đường dẫn hardcoded cá nhân.
    /// </summary>
    public static class RebarShapeLibrary
    {
        /// <summary>
        /// Danh mục toàn bộ 43 Rebar Shape chuẩn Nhật Bản / BS 8666:2005 được K-TOOLS hỗ trợ và đóng gói trong MSI.
        /// </summary>
        public static readonly string[] AllStandardShapes = new string[]
        {
            "JP_T00", "JP_T02", "JP_T03", "JP_T04", "JP_T05", "JP_T06", "JP_T07",
            "JP_T11", "JP_T11a", "JP_T12", "JP_T13", "JP_T14", "JP_T15", "JP_T16", "JP_T17",
            "JP_T20", "JP_T21", "JP_T22", "JP_T23", "JP_T24", "JP_T25", "JP_T26", "JP_T27", "JP_T28", "JP_T29",
            "JP_T31", "JP_T32", "JP_T34", "JP_T35", "JP_T36", "JP_T38",
            "JP_T41", "JP_T44", "JP_T46", "JP_T47", "JP_T48", "JP_T49",
            "JP_T51",
            "JP_T63", "JP_T67", "JP_T68",
            "JP_T75",
            "JP_T80"
        };

        /// <summary>
        /// Lấy RebarShape đã nạp trong project, hoặc nạp mới từ .rfa nếu chưa có.
        /// PHẢI gọi trong Transaction đang mở (doc.LoadFamily yêu cầu transaction).
        /// Trả về null nếu không tìm thấy .rfa hoặc nạp thất bại.
        /// </summary>
        public static RebarShape GetOrLoadShape(Document doc, string shapeFamilyName)
        {
            if (doc == null || string.IsNullOrEmpty(shapeFamilyName)) return null;

            // 1. Đã có sẵn trong project -> dùng luôn
            RebarShape existing = FindLoadedShape(doc, shapeFamilyName);
            if (existing != null) return existing;

            // 2. Tìm file .rfa theo cơ chế dò tìm đa tầng tự động
            string rfaPath = ResolveRfaPath(shapeFamilyName);
            if (rfaPath == null) return null;

            // 3. Nạp family vào project bằng KhimFamilyLoadOptions chuẩn Revit
            try
            {
                var loadOptions = new KhimFamilyLoadOptions(true, true);
                Family family;
                bool loaded = doc.LoadFamily(rfaPath, loadOptions, out family);
                if (!loaded && family == null)
                {
                    // Fallback thử overload cũ nếu Revit phiên bản đặc thù yêu cầu
                    doc.LoadFamily(rfaPath, out family);
                }
            }
            catch
            {
                return null;
            }

            return FindLoadedShape(doc, shapeFamilyName);
        }

        /// <summary>
        /// Kiểm tra .rfa có tồn tại trên disk không (không cần transaction, không load vào project).
        /// Dùng để validate trước khi chạy hoặc hiện UI cảnh báo.
        /// </summary>
        public static bool ShapeFileExists(string shapeFamilyName)
        {
            return ResolveRfaPath(shapeFamilyName) != null;
        }

        /// <summary>
        /// Trả về đường dẫn tuyệt đối đến .rfa, hoặc null nếu không tìm thấy.
        /// Sử dụng FamilyPathResolver với cơ chế dò tìm đa tầng (MSI bundle, Assembly dir, Dev source tree)
        /// hoàn toàn không chứa bất kỳ hardcoded path cá nhân nào.
        /// </summary>
        public static string ResolveRfaPath(string shapeFamilyName)
        {
            if (string.IsNullOrEmpty(shapeFamilyName)) return null;

            // 1. Dò tìm qua FamilyPathResolver chính thức trong subfolder RebarShapes
            string resolved = FamilyPathResolver.ResolveRebarShapePath(shapeFamilyName);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                return resolved;
            }

            // 2. Thử dò tổng quát qua toàn bộ probe folders
            resolved = FamilyPathResolver.ResolveFamilyPath(shapeFamilyName);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                return resolved;
            }

            return null;
        }

        /// <summary>
        /// Nạp sẵn các RebarShape tiêu chuẩn (JP_T00, JP_T02, JP_T11, JP_T12, JP_T21, JP_T27, JP_T51, JP_T68, JP_T75, JP_T80...) vào Document.
        /// Giúp Revit tự động gán đúng Family Shape khi tạo thanh thép từ Curve.
        /// </summary>
        public static void PreloadCommonShapes(Document doc)
        {
            if (doc == null || doc.IsReadOnly) return;

            string[] commonShapes = { "JP_T00", "JP_T02", "JP_T11", "JP_T12", "JP_T21", "JP_T27", "JP_T51", "JP_T68", "JP_T75", "JP_T80" };
            foreach (var shapeName in commonShapes)
            {
                try
                {
                    GetOrLoadShape(doc, shapeName);
                }
                catch { }
            }
        }

        /// <summary>
        /// Nạp toàn bộ 43 RebarShape tiêu chuẩn vào Document với báo cáo chi tiết.
        /// Thường gọi từ lệnh Batch Rebar Shape Loader.
        /// </summary>
        public static RebarShapeLoadSummary PreloadAllShapes(Document doc)
        {
            var summary = new RebarShapeLoadSummary();
            if (doc == null) return summary;

            foreach (var shapeName in AllStandardShapes)
            {
                var existing = FindLoadedShape(doc, shapeName);
                if (existing != null)
                {
                    summary.AlreadyLoadedCount++;
                    summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.AlreadyLoaded, "Đã có sẵn trong dự án"));
                    continue;
                }

                string path = ResolveRfaPath(shapeName);
                if (path == null)
                {
                    summary.MissingFileCount++;
                    summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.MissingFile, "Không tìm thấy file .rfa"));
                    continue;
                }

                try
                {
                    var shape = GetOrLoadShape(doc, shapeName);
                    if (shape != null)
                    {
                        summary.NewlyLoadedCount++;
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.NewlyLoaded, "Nạp thành công (" + Path.GetFileName(path) + ")"));
                    }
                    else
                    {
                        summary.FailedCount++;
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.Failed, "Revit không nạp được family"));
                    }
                }
                catch (Exception ex)
                {
                    summary.FailedCount++;
                    summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.Failed, ex.Message));
                }
            }

            return summary;
        }

        /// <summary>
        /// Lấy danh sách tên tất cả các RebarShape hiện đang nạp trong Document.
        /// </summary>
        public static List<string> GetLoadedShapeNames(Document doc)
        {
            if (doc == null) return new List<string>();
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .Select(s => s.Name)
                .OrderBy(n => n)
                .ToList();
        }

        /// <summary>
        /// Lấy danh sách các shape chuẩn chưa được nạp vào Document.
        /// </summary>
        public static List<string> GetMissingShapeNames(Document doc)
        {
            var loaded = GetLoadedShapeNames(doc);
            var missing = new List<string>();
            foreach (var shapeName in AllStandardShapes)
            {
                bool found = loaded.Any(l => l.Equals(shapeName, StringComparison.OrdinalIgnoreCase) ||
                                             l.StartsWith(shapeName + "_", StringComparison.OrdinalIgnoreCase));
                if (!found) missing.Add(shapeName);
            }
            return missing;
        }

        /// <summary>
        /// Gán các biến kích thước hình học (A, B, C, Angle1, L1, L2, L3, VNDC_L1, VNDC_L2...) vào Rebar
        /// để cây thép khớp chính xác kỹ thuật uốn bẻ và thống kê bảng biểu BBS.
        /// </summary>
        public static void ApplyShapeParameters(Rebar rebar, IDictionary<string, double> parameters)
        {
            if (rebar == null || parameters == null || parameters.Count == 0) return;

            foreach (var kvp in parameters)
            {
                string paramName = kvp.Key;
                double val = kvp.Value;

                // Thử gán tham số trực tiếp (A, B, C, VNDC_L1, L1, Angle1...)
                var param = rebar.LookupParameter(paramName) ??
                            rebar.LookupParameter("VNDC_" + paramName) ??
                            rebar.LookupParameter(paramName.ToUpper());

                if (param != null && !param.IsReadOnly)
                {
                    try
                    {
                        if (param.StorageType == StorageType.Double)
                        {
                            param.Set(val);
                        }
                    }
                    catch { }
                }
            }
        }

        // ─── Private helpers ────────────────────────────────────────────────────

        private static RebarShape FindLoadedShape(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .FirstOrDefault(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
