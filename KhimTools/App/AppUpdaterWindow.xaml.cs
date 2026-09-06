using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace KhiemToolsApp
{
    public partial class AppUpdaterWindow : Window
    {
        private const string RepoOwner = "nguyenkhiemkhiem079-boop";
        private const string RepoName = "KhiemTools_";
        private const string RegistryKeyName = "KhiemToolsUpdater";
        private const long MinimumBundleBytes = 64 * 1024;

        private readonly string _programDataBundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ApplicationPlugins\KhimTools.bundle");

        private readonly string _appDataBundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Autodesk\ApplicationPlugins\KhimTools.bundle");

        public AppUpdaterWindow()
        {
            InitializeComponent();
            InitializeUpdaterVersion();
            CheckCurrentLocalVersion();
            LoadRegistrySettings();
        }

        private void InitializeUpdaterVersion()
        {
            try
            {
                Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null)
                {
                    TxtUpdaterVersion.Text = $"K-TOOLS Updater v{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch (Exception ex)
            {
                LogInfo($"Unable to read updater version: {ex.Message}");
            }
        }

        private static void LogInfo(string message)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KhimTools");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "update_log.txt"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never break the updater.
            }
        }

        private string GetEffectiveBundlePath()
        {
            return _programDataBundlePath;
        }

        private void CheckCurrentLocalVersion()
        {
            string localTag = GetLocalVersionTag();
            TxtLocalVersion.Text = localTag ?? (Directory.Exists(GetEffectiveBundlePath()) ? "Đã cài" : "Chưa cài");
        }

        private string GetLocalVersionTag()
        {
            string bundlePath = GetEffectiveBundlePath();
            if (!Directory.Exists(bundlePath))
            {
                return null;
            }

            string installedVersionPath = Path.Combine(bundlePath, "installed_version.txt");
            if (File.Exists(installedVersionPath))
            {
                try
                {
                    string value = NormalizeVersionTag(File.ReadAllText(installedVersionPath));
                    if (TryParseVersionTag(value, out _))
                    {
                        return value;
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"Read installed_version.txt failed: {ex.Message}");
                }
            }

            string packageXmlPath = Path.Combine(bundlePath, "PackageContents.xml");
            if (File.Exists(packageXmlPath))
            {
                try
                {
                    string xml = File.ReadAllText(packageXmlPath);
                    Match match = Regex.Match(xml, "AppVersion\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string value = NormalizeVersionTag(match.Groups[1].Value);
                        if (TryParseVersionTag(value, out _))
                        {
                            return value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"Read PackageContents.xml failed: {ex.Message}");
                }
            }

            string[] possibleDlls =
            {
                Path.Combine(bundlePath, "Contents", "Legacy", "KhimTools.dll"),
                Path.Combine(bundlePath, "Contents", "Modern", "KhimTools.dll"),
                Path.Combine(bundlePath, "Legacy", "KhimTools.dll"),
                Path.Combine(bundlePath, "Modern", "KhimTools.dll")
            };

            foreach (string dll in possibleDlls)
            {
                if (!File.Exists(dll))
                {
                    continue;
                }

                try
                {
                    FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(dll);
                    string value = NormalizeVersionTag(fileVersion.FileVersion);
                    if (TryParseVersionTag(value, out _))
                    {
                        return value;
                    }

                    Version assemblyVersion = AssemblyName.GetAssemblyName(dll).Version;
                    if (assemblyVersion != null)
                    {
                        value = NormalizeVersionTag(assemblyVersion.ToString(3));
                        if (TryParseVersionTag(value, out _))
                        {
                            return value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"Read DLL version failed for {dll}: {ex.Message}");
                }
            }

            string localInfoPath = Path.Combine(bundlePath, "update_info.json");
            if (File.Exists(localInfoPath))
            {
                try
                {
                    string infoJson = File.ReadAllText(localInfoPath);
                    Match tagMatch = Regex.Match(
                        infoJson,
                        "\"latest_version\"\\s*:\\s*\"([^\"]+)\"",
                        RegexOptions.IgnoreCase);

                    if (tagMatch.Success)
                    {
                        string value = NormalizeVersionTag(tagMatch.Groups[1].Value);
                        if (TryParseVersionTag(value, out _))
                        {
                            return value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"Read local update_info.json failed: {ex.Message}");
                }
            }

            return null;
        }

        private static string NormalizeVersionTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            return "v" + normalized;
        }

        private static bool TryParseVersionTag(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            int suffixIndex = normalized.IndexOf('-');
            if (suffixIndex >= 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return Version.TryParse(normalized, out version);
        }

        private void LoadRegistrySettings()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    false);

                ChkAutoStart.IsChecked = key?.GetValue(RegistryKeyName) != null;
            }
            catch (Exception ex)
            {
                LogInfo($"Load auto-start setting failed: {ex.Message}");
            }
        }

        private bool EnsureRevitClosed()
        {
            Process[] revitProcesses = Process.GetProcessesByName("Revit");
            if (revitProcesses.Length == 0)
            {
                return true;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Phát hiện Autodesk Revit đang mở ({revitProcesses.Length} tiến trình).\n\n" +
                "K-TOOLS chỉ cập nhật khi Revit đã đóng để tránh file DLL bị khóa.\n\n" +
                "Bấm Yes để gửi yêu cầu đóng Revit. Hãy lưu toàn bộ bản vẽ trước khi tiếp tục.\n" +
                "Updater sẽ KHÔNG ép tắt Revit nếu Revit chưa đóng an toàn.",
                "Đóng Revit trước khi cập nhật",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return false;
            }

            foreach (Process process in revitProcesses)
            {
                try
                {
                    process.CloseMainWindow();
                }
                catch (Exception ex)
                {
                    LogInfo($"Could not request Revit close (PID {process.Id}): {ex.Message}");
                }
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                bool anyRunning = false;
                foreach (Process process in Process.GetProcessesByName("Revit"))
                {
                    anyRunning = true;
                    process.Dispose();
                }

                if (!anyRunning)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            MessageBox.Show(
                "Revit vẫn đang chạy nên K-TOOLS chưa thể cập nhật.\n\n" +
                "Vui lòng đóng Revit thủ công sau khi lưu công việc rồi thử lại.",
                "Chưa thể cập nhật",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return false;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtGithubVersion.Text = "Đang kiểm tra...";
            LogInfo("=== Begin update check ===");

            try
            {
                UpdateDescriptor descriptor = await GetLatestReleaseAsync();
                TxtGithubVersion.Text = descriptor.Tag;

                string localTag = GetLocalVersionTag();
                if (!TryParseVersionTag(descriptor.Tag, out Version remoteVersion))
                {
                    throw new InvalidDataException($"Phiên bản trên GitHub không hợp lệ: {descriptor.Tag}");
                }

                if (TryParseVersionTag(localTag, out Version localVersion))
                {
                    int comparison = localVersion.CompareTo(remoteVersion);
                    if (comparison == 0)
                    {
                        MessageBox.Show(
                            $"Bạn đang dùng phiên bản mới nhất ({descriptor.Tag}).",
                            "K-TOOLS đã cập nhật",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    if (comparison > 0)
                    {
                        MessageBox.Show(
                            $"Phiên bản đang cài ({localTag}) mới hơn phiên bản public trên GitHub ({descriptor.Tag}).\n\n" +
                            "Updater sẽ không tự động hạ phiên bản.",
                            "Không cần cập nhật",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }

                string action = string.IsNullOrWhiteSpace(localTag) ? "cài đặt" : "cập nhật";
                MessageBoxResult installResult = MessageBox.Show(
                    $"Phiên bản mới nhất: {descriptor.Tag}\n" +
                    $"Phiên bản hiện tại: {localTag ?? "không xác định"}\n\n" +
                    $"Bạn có muốn {action} K-TOOLS ngay không?",
                    "Cập nhật K-TOOLS",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (installResult != MessageBoxResult.Yes)
                {
                    return;
                }

                if (!EnsureRevitClosed())
                {
                    return;
                }

                TxtGithubVersion.Text = "Đang tải & cài đặt...";
                await PerformInstallOrUpdateAsync(descriptor);

                CheckCurrentLocalVersion();
                TxtGithubVersion.Text = descriptor.Tag;

                MessageBox.Show(
                    $"K-TOOLS {descriptor.Tag} đã được cài đặt thành công.\n\n" +
                    "Bạn có thể mở lại Revit để sử dụng.",
                    "Cập nhật hoàn tất",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogInfo($"Permission error: {ex}");
                TxtGithubVersion.Text = "Thiếu quyền";
                MessageBox.Show(
                    "Windows không cho phép ghi vào thư mục cài đặt K-TOOLS.\n\n" +
                    "Hãy đóng Updater, chạy K-TOOLS Updater bằng quyền Administrator rồi thử lại.",
                    "Thiếu quyền cài đặt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LogInfo($"Update failed: {ex}");
                TxtGithubVersion.Text = "Lỗi cập nhật";
                MessageBox.Show(
                    $"Không thể cập nhật K-TOOLS.\n\n{ex.Message}\n\n" +
                    "Phiên bản hiện tại được giữ nguyên nếu quá trình cài đặt chưa hoàn tất.\n" +
                    "Chi tiết kỹ thuật đã được ghi vào update_log.txt.",
                    "Lỗi cập nhật",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async Task<UpdateDescriptor> GetLatestReleaseAsync()
        {
            using var client = CreateHttpClient(TimeSpan.FromSeconds(15));
            Exception releaseApiError = null;

            try
            {
                string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string releaseJson = await client.GetStringAsync(apiUrl);

                Match tagMatch = Regex.Match(
                    releaseJson,
                    "\"tag_name\"\\s*:\\s*\"([^\"]+)\"",
                    RegexOptions.IgnoreCase);

                Match assetMatch = Regex.Match(
                    releaseJson,
                    "\"browser_download_url\"\\s*:\\s*\"([^\"]+(?:KhimTools_Bundle|K-TOOLS_Bundle)\\.zip)\"",
                    RegexOptions.IgnoreCase);

                if (tagMatch.Success)
                {
                    string tag = NormalizeVersionTag(tagMatch.Groups[1].Value);
                    string url = assetMatch.Success ? assetMatch.Groups[1].Value : null;
                    ValidateRemoteDescriptor(tag, url, allowMissingUrl: true);
                    LogInfo($"Latest release from GitHub API: {tag}");
                    return new UpdateDescriptor(tag, url);
                }

                releaseApiError = new InvalidDataException("GitHub Releases API không trả về tag_name hợp lệ.");
            }
            catch (Exception ex)
            {
                releaseApiError = ex;
                LogInfo($"GitHub Releases API failed: {ex.GetType().Name} - {ex.Message}");
            }

            try
            {
                string infoUrl =
                    $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/master/update_info.json?t={DateTime.UtcNow.Ticks}";
                string infoJson = await client.GetStringAsync(infoUrl);

                Match tagMatch = Regex.Match(
                    infoJson,
                    "\"latest_version\"\\s*:\\s*\"([^\"]+)\"",
                    RegexOptions.IgnoreCase);
                Match urlMatch = Regex.Match(
                    infoJson,
                    "\"download_url\"\\s*:\\s*\"([^\"]+)\"",
                    RegexOptions.IgnoreCase);

                if (!tagMatch.Success)
                {
                    throw new InvalidDataException("update_info.json không có latest_version hợp lệ.");
                }

                string tag = NormalizeVersionTag(tagMatch.Groups[1].Value);
                string url = urlMatch.Success ? urlMatch.Groups[1].Value : null;
                ValidateRemoteDescriptor(tag, url, allowMissingUrl: true);
                LogInfo($"Latest release from update_info.json fallback: {tag}");
                return new UpdateDescriptor(tag, url);
            }
            catch (Exception infoError)
            {
                LogInfo($"update_info.json fallback failed: {infoError.GetType().Name} - {infoError.Message}");
                throw new InvalidOperationException(
                    "Không thể kiểm tra phiên bản mới từ GitHub. " +
                    "Vui lòng kiểm tra kết nối Internet và thử lại.",
                    new AggregateException(releaseApiError, infoError));
            }
        }

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/2.7.1");
            client.DefaultRequestHeaders.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return client;
        }

        private static void ValidateRemoteDescriptor(string tag, string url, bool allowMissingUrl)
        {
            if (!TryParseVersionTag(tag, out _))
            {
                throw new InvalidDataException($"Remote version tag không hợp lệ: {tag}");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                if (allowMissingUrl)
                {
                    return;
                }

                throw new InvalidDataException("GitHub release không có file K-TOOLS bundle.");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith(
                    $"/{RepoOwner}/{RepoName}/releases/download/",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Download URL không thuộc GitHub Release chính thức của K-TOOLS.");
            }
        }

        private async Task PerformInstallOrUpdateAsync(UpdateDescriptor descriptor)
        {
            string tempDir = Path.Combine(
                Path.GetTempPath(),
                "KhimTools_Installer_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string bundleZipPath = Path.Combine(tempDir, "bundle.zip");
                await DownloadBundleAsync(descriptor, bundleZipPath);
                ValidateZipFile(bundleZipPath);
                DeployZipToTargets(bundleZipPath, descriptor.Tag);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private async Task DownloadBundleAsync(UpdateDescriptor descriptor, string destinationPath)
        {
            string[] candidateUrls =
            {
                descriptor.DownloadUrl,
                $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{descriptor.Tag}/KhimTools_Bundle.zip",
                $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{descriptor.Tag}/K-TOOLS_Bundle.zip"
            };

            Exception lastError = null;

            foreach (string url in candidateUrls)
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                try
                {
                    ValidateRemoteDescriptor(descriptor.Tag, url, allowMissingUrl: false);
                    LogInfo($"Downloading bundle from {url}");

                    using var client = CreateHttpClient(TimeSpan.FromMinutes(3));
                    using HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    if (data.LongLength < MinimumBundleBytes)
                    {
                        throw new InvalidDataException(
                            $"File tải về quá nhỏ ({data.LongLength} bytes), có thể không phải bundle hợp lệ.");
                    }

                    File.WriteAllBytes(destinationPath, data);
                    ValidateZipFile(destinationPath);
                    LogInfo($"Bundle download OK: {data.LongLength} bytes");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LogInfo($"Download failed from {url}: {ex.GetType().Name} - {ex.Message}");
                    TryDeleteFile(destinationPath);
                }
            }

            throw new InvalidOperationException(
                $"Không thể tải K-TOOLS {descriptor.Tag} từ GitHub Release.",
                lastError);
        }

        private static void ValidateZipFile(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Không tìm thấy file bundle vừa tải.", zipFilePath);
            }

            var fileInfo = new FileInfo(zipFilePath);
            if (fileInfo.Length < MinimumBundleBytes)
            {
                throw new InvalidDataException("Bundle tải về không hợp lệ hoặc bị thiếu dữ liệu.");
            }

            using FileStream stream = File.OpenRead(zipFilePath);
            if (stream.Length < 4 ||
                stream.ReadByte() != 0x50 ||
                stream.ReadByte() != 0x4B)
            {
                throw new InvalidDataException("File tải về không phải ZIP hợp lệ.");
            }

            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count == 0)
            {
                throw new InvalidDataException("Bundle ZIP không chứa dữ liệu.");
            }
        }

        private void DeployZipToTargets(string zipFilePath, string tag)
        {
            string target = _programDataBundlePath;
            string parent = Directory.GetParent(target)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException("Không xác định được thư mục cài đặt K-TOOLS.");
            }

            Directory.CreateDirectory(parent);

            string operationId = Guid.NewGuid().ToString("N");
            string stagingRoot = Path.Combine(parent, $"KhimTools.bundle.__staging_{operationId}");
            string backupRoot = Path.Combine(parent, $"KhimTools.bundle.__backup_{operationId}");

            bool oldInstallMoved = false;
            bool newInstallMoved = false;
            bool deploymentSucceeded = false;

            try
            {
                ExtractZipSafely(zipFilePath, stagingRoot);
                string packageRoot = ResolvePackageRoot(stagingRoot);
                ValidateExtractedBundle(packageRoot);

                File.WriteAllText(Path.Combine(packageRoot, "installed_version.txt"), tag);

                if (Directory.Exists(target))
                {
                    Directory.Move(target, backupRoot);
                    oldInstallMoved = true;
                }

                Directory.Move(packageRoot, target);
                newInstallMoved = true;

                ValidateExtractedBundle(target);
                string installedTag = NormalizeVersionTag(
                    File.ReadAllText(Path.Combine(target, "installed_version.txt")));
                if (!string.Equals(installedTag, NormalizeVersionTag(tag), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Xác minh phiên bản sau cài đặt thất bại.");
                }

                CleanLegacyAddinFiles();

                if (oldInstallMoved)
                {
                    TryDeleteDirectory(backupRoot);
                }

                deploymentSucceeded = true;
                LogInfo($"Deployment successful: {tag}");
            }
            catch (Exception deploymentError)
            {
                LogInfo($"Deployment failed: {deploymentError}");

                Exception rollbackError = null;
                try
                {
                    if (newInstallMoved && Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }

                    if (oldInstallMoved && Directory.Exists(backupRoot))
                    {
                        Directory.Move(backupRoot, target);
                    }
                }
                catch (Exception ex)
                {
                    rollbackError = ex;
                    LogInfo($"Rollback failed: {ex}");
                }

                if (rollbackError != null)
                {
                    throw new InvalidOperationException(
                        "Cập nhật thất bại và quá trình khôi phục phiên bản cũ cũng gặp lỗi. " +
                        "Vui lòng kiểm tra update_log.txt trước khi mở Revit.",
                        new AggregateException(deploymentError, rollbackError));
                }

                throw;
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
                if (deploymentSucceeded)
                {
                    TryDeleteDirectory(backupRoot);
                }
            }
        }

        private static string ResolvePackageRoot(string stagingRoot)
        {
            if (IsBundleRoot(stagingRoot))
            {
                return stagingRoot;
            }

            string nestedBundle = Path.Combine(stagingRoot, "KhimTools.bundle");
            if (Directory.Exists(nestedBundle) && IsBundleRoot(nestedBundle))
            {
                return nestedBundle;
            }

            throw new InvalidDataException(
                "Cấu trúc bundle không hợp lệ: không tìm thấy PackageContents.xml/KhimTools.dll ở vị trí mong đợi.");
        }

        private static bool IsBundleRoot(string root)
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            bool hasPackageXml = File.Exists(Path.Combine(root, "PackageContents.xml"));
            bool hasDll =
                File.Exists(Path.Combine(root, "Contents", "Legacy", "KhimTools.dll")) ||
                File.Exists(Path.Combine(root, "Contents", "Modern", "KhimTools.dll")) ||
                File.Exists(Path.Combine(root, "Legacy", "KhimTools.dll")) ||
                File.Exists(Path.Combine(root, "Modern", "KhimTools.dll"));

            return hasPackageXml && hasDll;
        }

        private static void ValidateExtractedBundle(string root)
        {
            if (!IsBundleRoot(root))
            {
                throw new InvalidDataException(
                    "Bundle thiếu PackageContents.xml hoặc KhimTools.dll. Không thay đổi bản cài hiện tại.");
            }
        }

        private static void ExtractZipSafely(string zipPath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            string destinationRoot = Path.GetFullPath(destinationDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string fullPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Bundle chứa đường dẫn không an toàn: {entry.FullName}");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullPath);
                    continue;
                }

                string parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                entry.ExtractToFile(fullPath, overwrite: true);
            }
        }

        private static void CleanLegacyAddinFiles()
        {
            string appDataBundle = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Autodesk\ApplicationPlugins\KhimTools.bundle");
            TryDeleteDirectory(appDataBundle);

            string[] baseAddinFolders =
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Autodesk\Revit\Addins"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Autodesk\Revit\Addins")
            };

            int[] years = { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028 };

            foreach (string baseDir in baseAddinFolders)
            {
                if (!Directory.Exists(baseDir))
                {
                    continue;
                }

                foreach (int year in years)
                {
                    string yearFolder = Path.Combine(baseDir, year.ToString());
                    if (!Directory.Exists(yearFolder))
                    {
                        continue;
                    }

                    TryDeleteFile(Path.Combine(yearFolder, "KhimTools.addin"));
                    TryDeleteDirectory(Path.Combine(yearFolder, "KhimTools"));
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                LogInfo($"Cleanup file failed ({path}): {ex.Message}");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                LogInfo($"Cleanup directory failed ({path}): {ex.Message}");
            }
        }

        private void BtnFeedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://github.com/{RepoOwner}/{RepoName}/issues",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogInfo($"Open feedback URL failed: {ex.Message}");
            }
        }

        private void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Bạn có chắc chắn muốn gỡ cài đặt K-TOOLS khỏi máy tính không?",
                    "Xác nhận gỡ bỏ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (!EnsureRevitClosed())
                {
                    return;
                }

                if (Directory.Exists(_programDataBundlePath))
                {
                    Directory.Delete(_programDataBundlePath, true);
                }

                if (Directory.Exists(_appDataBundlePath))
                {
                    Directory.Delete(_appDataBundlePath, true);
                }

                CleanLegacyAddinFiles();
                TxtLocalVersion.Text = "Chưa cài";

                MessageBox.Show(
                    "Đã gỡ cài đặt K-TOOLS thành công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogInfo($"Uninstall permission error: {ex}");
                MessageBox.Show(
                    "Không đủ quyền để gỡ K-TOOLS. Hãy chạy Updater bằng quyền Administrator.",
                    "Thiếu quyền",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LogInfo($"Uninstall failed: {ex}");
                MessageBox.Show(
                    "Không thể gỡ K-TOOLS: " + ex.Message,
                    "Lỗi gỡ cài đặt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    true);
                if (key == null)
                {
                    return;
                }

                if (ChkAutoStart.IsChecked == true)
                {
                    string executable = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(executable))
                    {
                        key.SetValue(RegistryKeyName, $"\"{executable}\"");
                    }
                }
                else
                {
                    key.DeleteValue(RegistryKeyName, false);
                }
            }
            catch (Exception ex)
            {
                LogInfo($"Save auto-start setting failed: {ex.Message}");
            }
        }

        private sealed class UpdateDescriptor
        {
            public UpdateDescriptor(string tag, string downloadUrl)
            {
                Tag = tag;
                DownloadUrl = downloadUrl;
            }

            public string Tag { get; }
            public string DownloadUrl { get; }
        }
    }
}
