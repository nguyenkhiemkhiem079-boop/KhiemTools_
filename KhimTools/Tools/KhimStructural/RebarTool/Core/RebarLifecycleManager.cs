using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Quản lý vòng đời (Lifecycle) của Rebar do KhimTools tạo ra:
    /// CREATE, UPDATE, SYNC, REBUILD.
    /// Ngăn chặn 100% tình trạng nhân đôi thép (duplicate rebars) khi người dùng chạy lại tool.
    /// </summary>
    public static class RebarLifecycleManager
    {
        public const string TagPrefix = "[KTools_Rebar]";

        /// <summary>
        /// Gắn Metadata quản lý vòng đời lên Rebar element
        /// </summary>
        public static void TagRebar(Rebar rebar, Element host, string moduleName, string roleName)
        {
            if (rebar == null || !rebar.IsValidObject) return;

            string hostIdStr = host != null && host.IsValidObject ? host.Id.ToString() : "0";
            string tag = $"{TagPrefix}|Module:{moduleName}|Host:{hostIdStr}|Role:{roleName}|v2.0";

            try
            {
                Parameter commentsParam = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && !commentsParam.IsReadOnly)
                {
                    commentsParam.Set(tag);
                }
            }
            catch { }
        }

        /// <summary>
        /// Gắn metadata hàng loạt cho danh sách Rebars
        /// </summary>
        public static void TagRebars(IEnumerable<Rebar> rebars, Element host, string moduleName, string roleName)
        {
            if (rebars == null) return;
            foreach (var r in rebars)
            {
                TagRebar(r, host, moduleName, roleName);
            }
        }

        /// <summary>
        /// Kiểm tra xem 1 Rebar có phải do KhimTools tạo ra và thuộc module chỉ định hay không
        /// </summary>
        public static bool IsKhimRebar(Rebar rebar, string moduleName = null)
        {
            if (rebar == null || !rebar.IsValidObject) return false;

            try
            {
                Parameter commentsParam = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && commentsParam.HasValue)
                {
                    string val = commentsParam.AsString() ?? "";
                    if (val.StartsWith(TagPrefix))
                    {
                        if (string.IsNullOrEmpty(moduleName)) return true;
                        return val.Contains($"Module:{moduleName}");
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Lấy tất cả Rebars do KhimTools tạo ra trên một cấu kiện Host
        /// </summary>
        public static List<Rebar> GetHostedKhimRebars(Document doc, Element host, string moduleName = null)
        {
            if (doc == null || host == null || !host.IsValidObject) return new List<Rebar>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(r => r.GetHostId() == host.Id && IsKhimRebar(r, moduleName))
                .ToList();
        }

        /// <summary>
        /// Xóa sạch toàn bộ Rebars cũ của module này trên Host trước khi vẽ lại (REBUILD / UPDATE).
        /// Ngăn chặn triệt để hiện tượng trùng lặp thép (duplicate rebars).
        /// </summary>
        public static int CleanPreviousRebars(Document doc, Element host, string moduleName = null)
        {
            if (doc == null || host == null || !host.IsValidObject) return 0;

            var existing = GetHostedKhimRebars(doc, host, moduleName);
            if (!existing.Any()) return 0;

            var idsToDelete = existing.Select(r => r.Id).ToList();
            try
            {
                doc.Delete(idsToDelete);
                return idsToDelete.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Thực thi chu trình Rebuild: Xóa thép cũ -> Chạy bộ sinh thép mới -> Gắn thẻ vòng đời mới
        /// </summary>
        public static List<Rebar> ExecuteRebuild(
            Document doc,
            Element host,
            string moduleName,
            string roleName,
            Func<List<Rebar>> generatorAction)
        {
            if (doc == null || host == null || generatorAction == null) return new List<Rebar>();

            // 1. Dọn dẹp thép cũ
            CleanPreviousRebars(doc, host, moduleName);

            // 2. Sinh thép mới
            var newBars = generatorAction();

            // 3. Gắn thẻ theo dõi
            TagRebars(newBars, host, moduleName, roleName);

            return newBars;
        }
    }
}
