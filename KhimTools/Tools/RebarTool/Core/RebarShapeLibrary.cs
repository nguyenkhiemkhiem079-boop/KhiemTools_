using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Nạp các family Rebar Shape tuỳ chỉnh (bộ JP_T## theo BS 8666:2005) từ thư mục
    /// "RebarShapes" cạnh DLL add-in vào project hiện tại, rồi trả về RebarShape để dùng
    /// cho Rebar.CreateFromRebarShape thay vì để Revit tự sinh shape mặc định.
    ///
    /// Thứ tự tìm .rfa:
    ///   1. &lt;DLL dir&gt;\RebarShapes\&lt;name&gt;.rfa  (bundle chính thức)
    ///   2. &lt;DLL dir&gt;\Tools\&lt;name&gt;.rfa         (bundle cũ, backward-compat)
    ///   3. &lt;DLL dir&gt;\&lt;name&gt;.rfa               (cạnh DLL, dev mode)
    /// </summary>
    public static class RebarShapeLibrary
    {
        // Thứ tự tìm thư mục chứa .rfa — khớp với post-build target trong .csproj
        private static readonly string[] FolderCandidates = { "RebarShapes", "Tools", "" };

        /// <summary>
        /// Lấy RebarShape đã nạp trong project, hoặc nạp mới từ .rfa nếu chưa có.
        /// PHẢI gọi trong Transaction đang mở (doc.LoadFamily yêu cầu transaction).
        /// Trả về null nếu không tìm thấy .rfa hoặc nạp thất bại.
        /// </summary>
        public static RebarShape GetOrLoadShape(Document doc, string shapeFamilyName)
        {
            // 1. Đã có sẵn trong project -> dùng luôn
            RebarShape existing = FindLoadedShape(doc, shapeFamilyName);
            if (existing != null) return existing;

            // 2. Tìm file .rfa theo các thư mục candidate
            string rfaPath = ResolveRfaPath(shapeFamilyName);
            if (rfaPath == null) return null;

            // 3. Nạp family vào project
            try
            {
                bool loaded = doc.LoadFamily(rfaPath, out Family family);
                if (!loaded && family == null) return null; // false = đã nạp rồi (không báo lỗi)
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
            => ResolveRfaPath(shapeFamilyName) != null;

        /// <summary>
        /// Trả về đường dẫn tuyệt đối đến .rfa, hoặc null nếu không tìm thấy.
        /// </summary>
        public static string ResolveRfaPath(string shapeFamilyName)
        {
            var baseDirs = new List<string>();

            try
            {
                string asmLoc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(asmLoc))
                {
                    string dir = Path.GetDirectoryName(asmLoc);
                    if (!string.IsNullOrEmpty(dir)) baseDirs.Add(dir);
                }
            }
            catch { }

            // Thư mục AppDomain
            if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory))
                baseDirs.Add(AppDomain.CurrentDomain.BaseDirectory);

            // Thư mục Add-in Bundle chính thức của Autodesk
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            baseDirs.Add(Path.Combine(programData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents", "Legacy"));
            baseDirs.Add(Path.Combine(programData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents", "Modern"));

            // Thư mục mã nguồn Workspace của KhimTools
            baseDirs.Add(@"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\Tools\RebarTool\RebarShapes");
            baseDirs.Add(@"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\bin\Debug\net48");
            baseDirs.Add(@"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\bin\Debug\net8.0-windows");

            foreach (var baseDir in baseDirs)
            {
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) continue;

                foreach (var folder in FolderCandidates)
                {
                    string dir = string.IsNullOrEmpty(folder) ? baseDir : Path.Combine(baseDir, folder);
                    string file = Path.Combine(dir, shapeFamilyName + ".rfa");
                    if (File.Exists(file)) return file;
                }
            }

            return null;
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
