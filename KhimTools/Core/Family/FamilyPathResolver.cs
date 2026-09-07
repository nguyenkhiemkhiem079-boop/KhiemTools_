using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace KhimTools.Core.Family
{
    /// <summary>
    /// Bộ giải quyết và định vị đường dẫn file Family (.rfa) tự động cho K-TOOLS.
    /// Hoạt động độc lập với Revit API, hỗ trợ dò tìm đa tầng từ MSI bundle, thư mục DLL,
    /// AppData và cây thư mục dự án khi đang ở môi trường phát triển (Dev).
    /// </summary>
    public static class FamilyPathResolver
    {
        private static readonly List<string> _customProbes = new List<string>();

        /// <summary>
        /// Đăng ký thêm thư mục ưu tiên dò tìm file family.
        /// </summary>
        public static void RegisterProbePath(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;
            lock (_customProbes)
            {
                if (!_customProbes.Contains(directoryPath))
                {
                    _customProbes.Insert(0, directoryPath);
                }
            }
        }

        /// <summary>
        /// Xóa danh sách thư mục custom đã đăng ký (thường dùng khi teardown unit test).
        /// </summary>
        public static void ResetCustomProbes()
        {
            lock (_customProbes)
            {
                _customProbes.Clear();
            }
        }

        /// <summary>
        /// Kiểm tra file family có tồn tại trên đĩa ở bất kỳ vị trí dò tìm nào không.
        /// </summary>
        public static bool FamilyExists(string familyName, string subfolder = null)
        {
            return ResolveFamilyPath(familyName, subfolder) != null;
        }

        /// <summary>
        /// Dò tìm và trả về đường dẫn tuyệt đối đến file family .rfa. Trả về null nếu không tìm thấy.
        /// </summary>
        public static string ResolveFamilyPath(string familyName, string subfolder = null)
        {
            if (string.IsNullOrEmpty(familyName)) return null;

            string cleanName = NormalizeFamilyName(familyName);
            string rfaFileName = cleanName + FamilyConstants.RfaExtension;

            var probeDirs = GetProbeDirectories(subfolder);
            foreach (var dir in probeDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                // 1. Thử file trực tiếp trong thư mục dir
                string candidate = Path.Combine(dir, rfaFileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                // 2. Thử trong subfolder nếu có truyền vào
                if (!string.IsNullOrEmpty(subfolder))
                {
                    candidate = Path.Combine(dir, subfolder, rfaFileName);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }

                // 3. Thử trong các subfolder chuẩn ("Standard", "Families", "Family")
                string candStandard = Path.Combine(dir, FamilyConstants.StandardSubfolder, rfaFileName);
                if (File.Exists(candStandard))
                {
                    return Path.GetFullPath(candStandard);
                }

                string candFamily = Path.Combine(dir, "Family", rfaFileName);
                if (File.Exists(candFamily))
                {
                    return Path.GetFullPath(candFamily);
                }

                string candFamilies = Path.Combine(dir, FamilyConstants.FamiliesFolder, rfaFileName);
                if (File.Exists(candFamilies))
                {
                    return Path.GetFullPath(candFamilies);
                }
            }

            return null;
        }

        /// <summary>
        /// Quét toàn bộ các thư mục thư viện để liệt kê các file Family (.rfa) sẵn có.
        /// </summary>
        public static List<FamilyFileInfo> ScanAvailableFamilies(string subfolder = null)
        {
            var result = new Dictionary<string, FamilyFileInfo>(StringComparer.OrdinalIgnoreCase);
            var probeDirs = GetProbeDirectories(subfolder);

            foreach (var dir in probeDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                try
                {
                    var rfaFiles = Directory.GetFiles(dir, "*" + FamilyConstants.RfaExtension, SearchOption.AllDirectories);
                    foreach (var file in rfaFiles)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (!result.ContainsKey(name))
                        {
                            string relCategory = DetermineCategory(dir, file);
                            result[name] = new FamilyFileInfo(file, relCategory);
                        }
                    }
                }
                catch
                {
                    // Tiếp tục duyệt các thư mục tiếp theo nếu 1 thư mục bị từ chối truy cập
                }
            }

            return new List<FamilyFileInfo>(result.Values);
        }

        /// <summary>
        /// Sinh danh sách các thư mục dò tìm theo đúng thứ tự ưu tiên kiến trúc K-TOOLS.
        /// </summary>
        public static List<string> GetProbeDirectories(string subfolder = null)
        {
            var list = new List<string>();

            // 0. Thư mục tuỳ chỉnh đăng ký runtime
            lock (_customProbes)
            {
                foreach (var cp in _customProbes)
                {
                    if (!list.Contains(cp)) list.Add(cp);
                }
            }

            // 1. Tier 1: MSI Authoritative ProgramData bundle
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string bundleBase = Path.Combine(programData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents");
            
            AddDirectoryCandidates(list, Path.Combine(bundleBase, FamilyConstants.FamiliesFolder));
            AddDirectoryCandidates(list, Path.Combine(bundleBase, FamilyConstants.FamiliesFolder, FamilyConstants.StandardSubfolder));
            AddDirectoryCandidates(list, Path.Combine(bundleBase, "Legacy"));
            AddDirectoryCandidates(list, Path.Combine(bundleBase, "Modern"));

            // 2. Tier 2: Thư mục Assembly đang chạy & AppDomain Base
            string asmDir = null;
            try
            {
                string asmLoc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(asmLoc))
                {
                    asmDir = Path.GetDirectoryName(asmLoc);
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(asmDir))
            {
                AddDirectoryCandidates(list, asmDir);
                AddDirectoryCandidates(list, Path.Combine(asmDir, FamilyConstants.FamiliesFolder));
                AddDirectoryCandidates(list, Path.Combine(asmDir, FamilyConstants.FamiliesFolder, FamilyConstants.StandardSubfolder));
                AddDirectoryCandidates(list, Path.Combine(asmDir, "Family"));
            }

            string appDomainDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appDomainDir) && !string.Equals(appDomainDir, asmDir, StringComparison.OrdinalIgnoreCase))
            {
                AddDirectoryCandidates(list, appDomainDir);
                AddDirectoryCandidates(list, Path.Combine(appDomainDir, FamilyConstants.FamiliesFolder));
                AddDirectoryCandidates(list, Path.Combine(appDomainDir, "Family"));
            }

            // 3. Tier 3: User AppData fallback
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataBundle = Path.Combine(appData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents");
            AddDirectoryCandidates(list, Path.Combine(appDataBundle, FamilyConstants.FamiliesFolder));
            AddDirectoryCandidates(list, Path.Combine(appDataBundle, FamilyConstants.FamiliesFolder, FamilyConstants.StandardSubfolder));

            // 4. Tier 4: Developer Solution tree discovery (tự động lần ngược tìm thư mục repo)
            var scanRoots = new List<string>();
            if (!string.IsNullOrEmpty(asmDir)) scanRoots.Add(asmDir);
            try
            {
                string cwd = Directory.GetCurrentDirectory();
                if (!string.IsNullOrEmpty(cwd) && !scanRoots.Contains(cwd))
                    scanRoots.Add(cwd);
            }
            catch { }
            if (!string.IsNullOrEmpty(appDomainDir) && !scanRoots.Contains(appDomainDir)) 
                scanRoots.Add(appDomainDir);

            foreach (var scanRoot in scanRoots)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(scanRoot);
                    for (int i = 0; i < 6 && dirInfo != null; i++)
                    {
                        string candidateDevFamily = Path.Combine(dirInfo.FullName, "KhimTools", "Family");
                        if (Directory.Exists(candidateDevFamily))
                        {
                            AddDirectoryCandidates(list, candidateDevFamily);
                        }

                        string directDevFamily = Path.Combine(dirInfo.FullName, "Family");
                        if (Directory.Exists(directDevFamily) && File.Exists(Path.Combine(dirInfo.FullName, "KhimTools.csproj")))
                        {
                            AddDirectoryCandidates(list, directDevFamily);
                        }

                        dirInfo = dirInfo.Parent;
                    }
                }
                catch { }
            }

            return list;
        }

        private static void AddDirectoryCandidates(List<string> list, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!list.Contains(dir))
            {
                list.Add(dir);
            }
        }

        private static string NormalizeFamilyName(string name)
        {
            string clean = name.Trim();
            if (clean.EndsWith(FamilyConstants.RfaExtension, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - FamilyConstants.RfaExtension.Length);
            }
            return clean;
        }

        private static string DetermineCategory(string baseDir, string fullPath)
        {
            string dirName = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dirName)) return "Standard";

            string lastFolder = Path.GetFileName(dirName);
            if (string.Equals(lastFolder, "RebarShapes", StringComparison.OrdinalIgnoreCase)) return "Rebar";
            if (string.Equals(lastFolder, "Standard", StringComparison.OrdinalIgnoreCase)) return "Standard";
            if (string.Equals(lastFolder, "Family", StringComparison.OrdinalIgnoreCase)) return "Standard";
            if (string.Equals(lastFolder, "Families", StringComparison.OrdinalIgnoreCase)) return "General";

            return lastFolder;
        }
    }
}
