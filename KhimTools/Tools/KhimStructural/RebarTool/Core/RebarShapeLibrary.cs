using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        InvalidAsset,
        IdentityUnverified,
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
        public int InvalidAssetCount { get; set; }
        public int IdentityUnverifiedCount { get; set; }
        public int FailedCount { get; set; }
        public List<RebarShapeLoadDetail> Details { get; set; }

        public int TotalProcessed
        {
            get { return NewlyLoadedCount + AlreadyLoadedCount + MissingFileCount + InvalidAssetCount + IdentityUnverifiedCount + FailedCount; }
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
        /// Returns a loaded RebarShape only when its persistent identity matches the
        /// packaged asset manifest. Loading a new shape requires an open transaction.
        /// Returns null for missing, mismatched, legacy-unverified, or ambiguous shapes.
        /// </summary>
        public static RebarShape GetOrLoadShape(Document doc, string shapeFamilyName)
        {
            if (doc == null || string.IsNullOrEmpty(shapeFamilyName)) return null;

            RebarShapeIdentityInspection inspection = RebarShapeAssetIdentity.Inspect(doc, shapeFamilyName);
            if (inspection.Status == RebarShapeIdentityStatus.Verified) return inspection.Shape;
            if (inspection.Status != RebarShapeIdentityStatus.NotLoaded)
            {
                System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Refused " + shapeFamilyName + ": " + inspection.Message);
                return null;
            }

            if (doc.IsReadOnly || !doc.IsModifiable)
            {
                System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Loading " + shapeFamilyName + " requires an open modifiable transaction.");
                return null;
            }

            RebarShape loadedShape = null;
            using (var loadTransaction = new SubTransaction(doc))
            {
                if (loadTransaction.Start() != TransactionStatus.Started) return null;
                try
                {
                    var loadOptions = new KhimFamilyLoadOptions(true, true);
                    Family family;
                    bool loaded = doc.LoadFamily(inspection.AssetPath, loadOptions, out family);
                    if (!loaded && family == null)
                    {
                        // Fallback thử overload cũ nếu Revit phiên bản đặc thù yêu cầu
                        doc.LoadFamily(inspection.AssetPath, out family);
                    }

                    string postLoadAssetHash;
                    string postLoadManifestHash;
                    string postLoadValidationError;
                    if (!RebarShapeAssetIdentity.TryVerifyResource(shapeFamilyName, inspection.AssetPath,
                            out postLoadAssetHash, out postLoadManifestHash, out postLoadValidationError) ||
                        !postLoadAssetHash.Equals(inspection.AssetSha256, StringComparison.OrdinalIgnoreCase) ||
                        !postLoadManifestHash.Equals(inspection.ManifestSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(postLoadValidationError ??
                            "The Rebar Shape asset changed while Revit was loading it.");
                    }

                    RebarShape[] matches = FindLoadedShapes(doc, shapeFamilyName);
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Revit did not create exactly one Rebar Shape named " + shapeFamilyName + ".");

                    loadedShape = matches[0];
                    string stampError;
                    if (!RebarShapeAssetIdentity.Stamp(loadedShape, inspection, out stampError))
                        throw new InvalidOperationException(stampError ?? "Could not persist the Rebar Shape asset identity.");

                    if (loadTransaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit did not commit the Rebar Shape load transaction.");
                }
                catch (Exception ex)
                {
                    if (loadTransaction.GetStatus() == TransactionStatus.Started)
                    {
                        try { loadTransaction.RollBack(); }
                        catch (Exception rollbackException)
                        {
                            System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Rollback failed for " + shapeFamilyName + ": " + rollbackException);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Could not load " + shapeFamilyName + ": " + ex);
                    return null;
                }
            }

            // Re-read the document state after commit. The returned object must be
            // the exact element carrying the matching persistent asset identity.
            RebarShapeIdentityInspection committed = RebarShapeAssetIdentity.Inspect(doc, shapeFamilyName);
            if (committed.Status != RebarShapeIdentityStatus.Verified)
            {
                System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Post-load identity check failed for " + shapeFamilyName + ": " + committed.Message);
                return null;
            }

            return loadedShape != null && loadedShape.IsValidObject &&
                loadedShape.UniqueId.Equals(committed.Shape.UniqueId, StringComparison.Ordinal)
                ? committed.Shape
                : null;
        }

        /// <summary>
        /// Returns the packaged rectangular tie shape only when it is a usable planar,
        /// four-segment Stirrup/Tie shape for the requested bar type.  A rectangular
        /// column tie must be driven by this known shape; callers must not substitute an
        /// arbitrary curve loop when the shape cannot be used.
        /// </summary>
        public static RebarShape GetOrLoadRectangularTieShape(Document doc, RebarBarType barType,
            out string failureReason)
        {
            failureReason = null;
            if (doc == null)
            {
                failureReason = "Document is required to resolve the rectangular tie shape.";
                return null;
            }

            if (barType == null)
            {
                failureReason = "A RebarBarType is required to resolve the rectangular tie shape.";
                return null;
            }

            RebarShape shape = GetOrLoadShape(doc, "JP_T51");
            if (shape == null)
            {
                RebarShapeIdentityInspection inspection = RebarShapeAssetIdentity.Inspect(doc, "JP_T51");
                failureReason = inspection.Status == RebarShapeIdentityStatus.NotLoaded && !doc.IsModifiable
                    ? "Loading JP_T51 requires an open modifiable transaction."
                    : inspection.Message;
                return null;
            }

            if (shape.RebarStyle != RebarStyle.StirrupTie)
            {
                failureReason = "Loaded shape " + shape.Name + " is not a Stirrup/Tie shape.";
                return null;
            }

            RebarShapeDefinitionBySegments definition = shape.GetRebarShapeDefinition() as RebarShapeDefinitionBySegments;
            if (definition == null || !definition.IsValidObject || !definition.IsPlanar ||
                !definition.Complete || definition.NumberOfSegments != 4)
            {
                failureReason = "Loaded shape " + shape.Name + " is not a complete planar four-segment rectangular tie shape.";
                return null;
            }

            try
            {
                if (!shape.GetAllowed(barType))
                {
                    failureReason = "Shape " + shape.Name + " does not allow bar type " + barType.Name + ".";
                    return null;
                }
            }
            catch (Exception ex)
            {
                failureReason = "Could not verify that shape " + shape.Name + " supports bar type " + barType.Name + ": " + ex.Message;
                return null;
            }

            return shape;
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
            if (!AllStandardShapes.Contains(shapeFamilyName, StringComparer.OrdinalIgnoreCase)) return null;

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
                RebarShapeIdentityInspection inspection = RebarShapeAssetIdentity.Inspect(doc, shapeName);
                if (inspection.Status == RebarShapeIdentityStatus.Verified)
                {
                    summary.AlreadyLoadedCount++;
                    summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.AlreadyLoaded, inspection.Message));
                    continue;
                }

                if (inspection.Status == RebarShapeIdentityStatus.Unverified ||
                    inspection.Status == RebarShapeIdentityStatus.Ambiguous)
                {
                    summary.IdentityUnverifiedCount++;
                    summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.IdentityUnverified, inspection.Message));
                    continue;
                }

                if (inspection.Status == RebarShapeIdentityStatus.AssetUnavailable)
                {
                    if (inspection.AssetPath == null)
                    {
                        summary.MissingFileCount++;
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.MissingFile, inspection.Message));
                    }
                    else
                    {
                        summary.InvalidAssetCount++;
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.InvalidAsset, inspection.Message));
                    }
                    continue;
                }

                try
                {
                    var shape = GetOrLoadShape(doc, shapeName);
                    if (shape != null)
                    {
                        summary.NewlyLoadedCount++;
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.NewlyLoaded,
                            "Loaded and identity-stamped (" + Path.GetFileName(inspection.AssetPath) + ")"));
                    }
                    else
                    {
                        summary.FailedCount++;
                        RebarShapeIdentityInspection afterAttempt = RebarShapeAssetIdentity.Inspect(doc, shapeName);
                        summary.Details.Add(new RebarShapeLoadDetail(shapeName, RebarShapeLoadStatus.Failed, afterAttempt.Message));
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
        /// Lấy danh sách các shape chuẩn chưa được nạp hoặc chưa xác minh identity.
        /// </summary>
        public static List<string> GetMissingShapeNames(Document doc)
        {
            var missing = new List<string>();
            foreach (var shapeName in AllStandardShapes)
            {
                if (RebarShapeAssetIdentity.Inspect(doc, shapeName).Status != RebarShapeIdentityStatus.Verified)
                    missing.Add(shapeName);
            }
            return missing;
        }

        /// <summary>
        /// Returns only the shared shape parameters explicitly declared by the rebar's
        /// current RebarShape definition.  This intentionally excludes similarly named
        /// instance or family parameters.
        /// </summary>
        public static List<string> GetSupportedShapeParameterNames(Rebar rebar)
        {
            var names = new List<string>();
            foreach (var item in GetSupportedShapeParameters(rebar))
            {
                names.Add(item.Key);
            }
            return names;
        }

        /// <summary>
        /// Gán các biến kích thước hình học chỉ khi chúng được khai báo trực tiếp bởi
        /// RebarShape hiện tại. Không suy diễn alias (ví dụ VNDC_) và không đụng đến
        /// parameter instance/family không thuộc shape.
        /// </summary>
        public static void ApplyShapeParameters(Rebar rebar, IDictionary<string, double> parameters)
        {
            if (rebar == null || parameters == null || parameters.Count == 0) return;

            var supported = GetSupportedShapeParameters(rebar);
            if (supported.Count == 0) return;

            foreach (var kvp in parameters)
            {
                InternalDefinition definition;
                if (!supported.TryGetValue(kvp.Key, out definition))
                {
                    System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Skipped unsupported shape parameter '" + kvp.Key + "'.");
                    continue;
                }

                Parameter param = rebar.get_Parameter(definition);
                if (param == null || param.IsReadOnly || param.StorageType != StorageType.Double)
                {
                    System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Skipped non-writable shape parameter '" + kvp.Key + "'.");
                    continue;
                }

                try
                {
                    param.Set(kvp.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Could not set shape parameter '" + kvp.Key + "': " + ex.Message);
                }
            }
        }

        // ─── Private helpers ────────────────────────────────────────────────────

        private static RebarShape[] FindLoadedShapes(Document doc, string name)
        {
            if (doc == null || string.IsNullOrWhiteSpace(name)) return new RebarShape[0];

            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
        }

        /// <summary>
        /// Finds a loaded, complete, planar shape with the requested name and style.
        /// A Standard shape is never returned for a StirrupTie request.
        /// </summary>
        public static RebarShape FindCompatibleShape(Document doc, string preferredName, RebarStyle expectedStyle)
        {
            if (doc == null || string.IsNullOrWhiteSpace(preferredName)) return null;
            RebarShapeIdentityInspection inspection = RebarShapeAssetIdentity.Inspect(doc, preferredName);
            if (inspection.Status != RebarShapeIdentityStatus.Verified || inspection.Shape.RebarStyle != expectedStyle)
                return null;

            RebarShape shape = inspection.Shape;
            try
            {
                RebarShapeDefinition definition = shape.GetRebarShapeDefinition();
                if (definition != null && definition.IsPlanar && definition.Complete)
                    return shape;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][RebarShape] Ignoring invalid shape " + shape.Name + ": " + ex.Message);
            }
            return null;
        }

        private static Dictionary<string, InternalDefinition> GetSupportedShapeParameters(Rebar rebar)
        {
            var supported = new Dictionary<string, InternalDefinition>(StringComparer.Ordinal);
            if (rebar == null || !rebar.IsValidObject || rebar.Document == null) return supported;

            try
            {
                RebarShape shape = rebar.Document.GetElement(rebar.GetShapeId()) as RebarShape;
                RebarShapeDefinition definition = shape != null ? shape.GetRebarShapeDefinition() : null;
                if (definition == null) return supported;

                foreach (ElementId parameterId in definition.GetParameters())
                {
                    ParameterElement parameterElement = rebar.Document.GetElement(parameterId) as ParameterElement;
                    InternalDefinition parameterDefinition = parameterElement != null ? parameterElement.GetDefinition() : null;
                    if (parameterDefinition != null && !string.IsNullOrEmpty(parameterDefinition.Name))
                    {
                        supported[parameterDefinition.Name] = parameterDefinition;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RebarShapeLibrary] Could not inspect rebar shape parameters: " + ex.Message);
            }

            return supported;
        }
    }
}
