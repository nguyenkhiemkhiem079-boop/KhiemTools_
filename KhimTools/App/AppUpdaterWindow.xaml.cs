using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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

        // Thư mục cài đặt Revit Addin Bundle chuẩn của Autodesk (%ProgramData%)
        private readonly string _programDataBundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ApplicationPlugins\KhimTools.bundle");

        // Thư mục cài đặt Revit Addin Bundle cho User (%AppData%)
        private readonly string _appDataBundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Autodesk\ApplicationPlugins\KhimTools.bundle");

        public AppUpdaterWindow()
        {
            InitializeComponent();
            CheckCurrentLocalVersion();
            LoadRegistrySettings();
        }

        private string GetEffectiveBundlePath()
        {
            if (Directory.Exists(_programDataBundlePath)) return _programDataBundlePath;
            if (Directory.Exists(_appDataBundlePath)) return _appDataBundlePath;
            return _programDataBundlePath;
        }

        private void CheckCurrentLocalVersion()
        {
            try
            {
                string bundlePath = GetEffectiveBundlePath();
                if (!Directory.Exists(bundlePath))
                {
                    TxtLocalVersion.Text = "Chưa cài";
                    return;
                }

                // 1. Kiểm tra file update_info.json trong bundle (nếu có)
                string localInfoPath = Path.Combine(bundlePath, "update_info.json");
                if (File.Exists(localInfoPath))
                {
                    string infoJson = File.ReadAllText(localInfoPath);
                    Match mTag = Regex.Match(infoJson, "\"latest_version\"\\s*:\\s*\"([^\"]+)\"");
                    if (mTag.Success && !string.IsNullOrWhiteSpace(mTag.Groups[1].Value))
                    {
                        TxtLocalVersion.Text = mTag.Groups[1].Value;
                        return;
                    }
                }

                // 2. Kiểm tra file installed_version.txt trong bundle
                string versionTxtPath = Path.Combine(bundlePath, "installed_version.txt");
                if (File.Exists(versionTxtPath))
                {
                    string v = File.ReadAllText(versionTxtPath).Trim();
                    if (!string.IsNullOrEmpty(v))
                    {
                        TxtLocalVersion.Text = v;
                        return;
                    }
                }

                // 3. Kiểm tra PackageContents.xml trong bundle
                string packageXmlPath = Path.Combine(bundlePath, "PackageContents.xml");
                if (File.Exists(packageXmlPath))
                {
                    string xml = File.ReadAllText(packageXmlPath);
                    Match m = Regex.Match(xml, "AppVersion\\s*=\\s*\"([^\"]+)\"");
                    if (m.Success)
                    {
                        string ver = m.Groups[1].Value.Trim();
                        if (ver != "1.0.0" && ver != "1.0.0.0")
                        {
                            TxtLocalVersion.Text = ver.StartsWith("v") ? ver : "v" + ver;
                            return;
                        }
                    }
                }

                // 4. Kiểm tra DLL FileVersion
                string[] possibleDlls = new string[]
                {
                    Path.Combine(bundlePath, "Contents", "Legacy", "KhimTools.dll"),
                    Path.Combine(bundlePath, "Contents", "Modern", "KhimTools.dll"),
                    Path.Combine(bundlePath, "Legacy", "KhimTools.dll"),
                    Path.Combine(bundlePath, "Modern", "KhimTools.dll")
                };

                foreach (var dll in possibleDlls)
                {
                    if (File.Exists(dll))
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(dll);
                        if (!string.IsNullOrEmpty(fvi.FileVersion) && fvi.FileVersion != "1.0.0.0")
                        {
                            TxtLocalVersion.Text = "v" + fvi.FileVersion;
                            return;
                        }
                    }
                }

                TxtLocalVersion.Text = "Đã cài";
            }
            catch
            {
                TxtLocalVersion.Text = Directory.Exists(GetEffectiveBundlePath()) ? "Đã cài" : "Chưa cài";
            }
        }

        private void LoadRegistrySettings()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null)
                {
                    ChkAutoStart.IsChecked = (key.GetValue(RegistryKeyName) != null);
                }
            }
            catch { }
        }

        private bool EnsureRevitClosed()
        {
            var revitProcesses = Process.GetProcessesByName("Revit");
            if (revitProcesses.Length == 0) return true;

            var msgResult = MessageBox.Show(
                $"Phát hiện Autodesk Revit đang mở ({revitProcesses.Length} tiến trình).\n\n" +
                "Để cập nhật DLL mới trực tiếp vào Revit, bạn cần đóng Revit trước.\n\n" +
                "• Bấm 'Yes' để tự động đóng Revit (hãy chắc chắn bạn đã lưu bản vẽ).\n" +
                "• Bấm 'No' để tự đóng Revit bằng tay rồi thử lại.",
                "Đóng Revit trước khi cập nhật",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (msgResult == MessageBoxResult.Yes)
            {
                foreach (var proc in revitProcesses)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.CloseMainWindow();
                            if (!proc.WaitForExit(3000))
                            {
                                proc.Kill();
                                proc.WaitForExit(2000);
                            }
                        }
                    }
                    catch { }
                }

                // Chờ thêm 1 giây để OS nhả file lock
                System.Threading.Thread.Sleep(1000);
                return true;
            }

            return false;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtGithubVersion.Text = "Đang kiểm tra...";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/1.0");

                string latestTag = null;
                string downloadUrl = null;

                // Lớp 1: Đọc trực tiếp từ update_info.json trên GitHub master
                try
                {
                    string infoUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/master/update_info.json?t={DateTime.UtcNow.Ticks}";
                    string infoJson = await client.GetStringAsync(infoUrl);
                    Match mTag = Regex.Match(infoJson, "\"latest_version\"\\s*:\\s*\"([^\"]+)\"");
                    if (mTag.Success)
                    {
                        latestTag = mTag.Groups[1].Value;
                    }
                    Match mUrl = Regex.Match(infoJson, "\"download_url\"\\s*:\\s*\"([^\"]+)\"");
                    if (mUrl.Success)
                    {
                        downloadUrl = mUrl.Groups[1].Value;
                    }
                }
                catch { }

                // Lớp 2: Kiểm tra GitHub Releases API (fallback)
                if (string.IsNullOrEmpty(latestTag))
                {
                    try
                    {
                        string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                        string releaseJson = await client.GetStringAsync(apiUrl);
                        Match tagMatch = Regex.Match(releaseJson, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (tagMatch.Success)
                        {
                            latestTag = tagMatch.Groups[1].Value;
                        }

                        Match zipMatch = Regex.Match(releaseJson, "\"browser_download_url\"\\s*:\\s*\"([^\"]+KhimTools_Bundle\\.zip|[^\"]+\\.zip)\"");
                        if (zipMatch.Success)
                        {
                            downloadUrl = zipMatch.Groups[1].Value;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(latestTag))
                {
                    latestTag = "v2.5.4";
                }

                TxtGithubVersion.Text = latestTag;

                if (MessageBox.Show($"Tìm thấy phiên bản {latestTag} trên GitHub!\nBạn có muốn tự động cài đặt / cập nhật vào Revit ngay không?", 
                    "Cập nhật K-TOOLS", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (!EnsureRevitClosed())
                    {
                        TxtGithubVersion.Text = latestTag;
                        return;
                    }

                    TxtGithubVersion.Text = "Đang tải & cài đặt...";
                    await PerformInstallOrUpdateAsync(latestTag, downloadUrl);
                    MessageBox.Show("Cài đặt / Cập nhật hoàn tất!\nĐã nạp toàn bộ module mới vào tất cả phiên bản Revit trên máy.\nVui lòng mở Revit để sử dụng.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    CheckCurrentLocalVersion();
                    TxtGithubVersion.Text = latestTag;
                }
            }
            catch (Exception ex)
            {
                TxtGithubVersion.Text = "Lỗi cập nhật";
                MessageBox.Show($"Lỗi cập nhật: {ex.Message}\n\nChi tiết: {ex.StackTrace}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async Task PerformInstallOrUpdateAsync(string tag, string directZipUrl)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "KhimTools_Installer");
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            Directory.CreateDirectory(tempDir);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/1.0");

            bool downloaded = false;
            string bundleZipPath = Path.Combine(tempDir, "bundle.zip");

            // Cách 1: Tải trực tiếp từ directZipUrl
            if (!string.IsNullOrEmpty(directZipUrl))
            {
                try
                {
                    byte[] data = await client.GetByteArrayAsync(directZipUrl);
                    File.WriteAllBytes(bundleZipPath, data);
                    downloaded = true;
                }
                catch { }
            }

            // Cách 2: Thử tải link direct release theo tag
            if (!downloaded)
            {
                try
                {
                    string releaseUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tag}/KhimTools_Bundle.zip";
                    byte[] data = await client.GetByteArrayAsync(releaseUrl);
                    File.WriteAllBytes(bundleZipPath, data);
                    downloaded = true;
                }
                catch { }
            }

            // Cách 3: Fallback tải master repository zip
            if (!downloaded)
            {
                string masterZipUrl = $"https://github.com/{RepoOwner}/{RepoName}/archive/refs/heads/master.zip";
                byte[] repoData = await client.GetByteArrayAsync(masterZipUrl);
                string repoZipPath = Path.Combine(tempDir, "master.zip");
                File.WriteAllBytes(repoZipPath, repoData);

                string extractDir = Path.Combine(tempDir, "extracted");
                ExtractZipSafely(repoZipPath, extractDir);

                // Tìm thư mục Deploy trong repo
                string[] deployDirs = Directory.GetDirectories(extractDir, "Deploy", SearchOption.AllDirectories);
                if (deployDirs.Length > 0)
                {
                    string deploySrc = deployDirs[0];
                    DeployDirectoryToTargets(deploySrc, tag);
                    return;
                }
            }

            // Nếu tải được file KhimTools_Bundle.zip
            if (File.Exists(bundleZipPath))
            {
                DeployZipToTargets(bundleZipPath, tag);
            }
            else
            {
                throw new FileNotFoundException("Không thể tải bộ cài đặt K-TOOLS từ máy chủ.");
            }
        }

        private void DeployZipToTargets(string zipFilePath, string tag)
        {
            var targetPaths = new List<string>();

            // Target 1: ProgramData (All users)
            try
            {
                Directory.CreateDirectory(_programDataBundlePath);
                targetPaths.Add(_programDataBundlePath);
            }
            catch { }

            // Target 2: AppData (Current user fallback)
            try
            {
                Directory.CreateDirectory(_appDataBundlePath);
                targetPaths.Add(_appDataBundlePath);
            }
            catch { }

            if (targetPaths.Count == 0)
            {
                throw new UnauthorizedAccessException("Không có quyền ghi vào thư mục cài đặt Add-in.");
            }

            foreach (var target in targetPaths)
            {
                ExtractZipSafely(zipFilePath, target);
                try
                {
                    File.WriteAllText(Path.Combine(target, "installed_version.txt"), tag);
                }
                catch { }
            }

            // Cài đặt trực tiếp vào các thư mục Addin truyền thống của Revit (%APPDATA% và %PROGRAMDATA%)
            DeployToClassicAddinFolders(targetPaths[0], tag);
        }

        private void DeployDirectoryToTargets(string sourceDir, string tag)
        {
            var targetPaths = new List<string>();

            try
            {
                Directory.CreateDirectory(_programDataBundlePath);
                targetPaths.Add(_programDataBundlePath);
            }
            catch { }

            try
            {
                Directory.CreateDirectory(_appDataBundlePath);
                targetPaths.Add(_appDataBundlePath);
            }
            catch { }

            foreach (var target in targetPaths)
            {
                CopyDirectory(sourceDir, target);
                try
                {
                    File.WriteAllText(Path.Combine(target, "installed_version.txt"), tag);
                }
                catch { }
            }

            // Cài đặt trực tiếp vào các thư mục Addin truyền thống của Revit (%APPDATA% và %PROGRAMDATA%)
            DeployToClassicAddinFolders(targetPaths[0], tag);
        }

        private static void DeployToClassicAddinFolders(string bundleSourceRoot, string tag)
        {
            try
            {
                string legacySource = Path.Combine(bundleSourceRoot, "Contents", "Legacy");
                if (!Directory.Exists(legacySource))
                    legacySource = Path.Combine(bundleSourceRoot, "Legacy");

                string modernSource = Path.Combine(bundleSourceRoot, "Contents", "Modern");
                if (!Directory.Exists(modernSource))
                    modernSource = Path.Combine(bundleSourceRoot, "Modern");

                string[] baseAddinFolders = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\Revit\Addins")
                };

                // Revit 2020 - 2024 (.NET Framework 4.8)
                int[] legacyYears = new int[] { 2020, 2021, 2022, 2023, 2024 };
                // Revit 2025 - 2028 (.NET 8.0)
                int[] modernYears = new int[] { 2025, 2026, 2027, 2028 };

                string addinManifestContent =
@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>K-TOOLS</Name>
    <Assembly>KhimTools\KhimTools.dll</Assembly>
    <AddInId>4F1B2C3D-5E6F-4A7B-8C9D-0E1F2A3B4C5D</AddInId>
    <FullClassName>KhimTools.Core.App</FullClassName>
    <VendorId>SJTL</VendorId>
    <VendorDescription>K-TOOLS — Revit Automation Suite, support@example.com</VendorDescription>
  </AddIn>
</RevitAddIns>";

                foreach (var baseDir in baseAddinFolders)
                {
                    if (!Directory.Exists(baseDir))
                    {
                        try { Directory.CreateDirectory(baseDir); } catch { }
                    }

                    // Deploy Legacy (2020 - 2024)
                    if (Directory.Exists(legacySource))
                    {
                        foreach (int year in legacyYears)
                        {
                            string yearFolder = Path.Combine(baseDir, year.ToString());
                            try
                            {
                                Directory.CreateDirectory(yearFolder);
                                string targetPluginFolder = Path.Combine(yearFolder, "KhimTools");
                                Directory.CreateDirectory(targetPluginFolder);

                                CopyDirectory(legacySource, targetPluginFolder);

                                string addinFilePath = Path.Combine(yearFolder, "KhimTools.addin");
                                File.WriteAllText(addinFilePath, addinManifestContent);
                            }
                            catch { }
                        }
                    }

                    // Deploy Modern (2025 - 2028)
                    if (Directory.Exists(modernSource))
                    {
                        foreach (int year in modernYears)
                        {
                            string yearFolder = Path.Combine(baseDir, year.ToString());
                            try
                            {
                                Directory.CreateDirectory(yearFolder);
                                string targetPluginFolder = Path.Combine(yearFolder, "KhimTools");
                                Directory.CreateDirectory(targetPluginFolder);

                                CopyDirectory(modernSource, targetPluginFolder);

                                string addinFilePath = Path.Combine(yearFolder, "KhimTools.addin");
                                File.WriteAllText(addinFilePath, addinManifestContent);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static void ExtractZipSafely(string zipPath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    string subDir = Path.Combine(destinationDirectory, entry.FullName);
                    Directory.CreateDirectory(subDir);
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDirectory, entry.FullName);
                string parentDir = Path.GetDirectoryName(targetFilePath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                entry.ExtractToFile(targetFilePath, overwrite: true);
            }
        }

        private static void CleanLegacyAddinFiles()
        {
            try
            {
                // Dọn dẹp file .addin cũ trong %APPDATA%\Autodesk\Revit\Addins và %PROGRAMDATA%\Autodesk\Revit\Addins
                // để tránh lỗi Duplicate AddIn GUID khi Revit nạp từ KhimTools.bundle
                string[] baseAddinFolders = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\Revit\Addins")
                };

                int[] years = new int[] { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028 };

                foreach (var baseDir in baseAddinFolders)
                {
                    if (!Directory.Exists(baseDir)) continue;

                    foreach (int year in years)
                    {
                        string yearFolder = Path.Combine(baseDir, year.ToString());
                        if (!Directory.Exists(yearFolder)) continue;

                        string addinFile = Path.Combine(yearFolder, "KhimTools.addin");
                        if (File.Exists(addinFile))
                        {
                            try { File.Delete(addinFile); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestDir);
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
            catch { }
        }

        private void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn gỡ cài đặt K-TOOLS khỏi máy tính không?", 
                "Xác nhận gỡ bỏ", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    if (!EnsureRevitClosed()) return;

                    if (Directory.Exists(_programDataBundlePath))
                    {
                        Directory.Delete(_programDataBundlePath, true);
                    }
                    if (Directory.Exists(_appDataBundlePath))
                    {
                        Directory.Delete(_appDataBundlePath, true);
                    }

                    // Dọn dẹp cả file .addin và thư mục plugin trong %APPDATA% & %PROGRAMDATA%
                    string[] revitAddinsBases = new string[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\Revit\Addins")
                    };

                    int[] years = new int[] { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028 };
                    foreach (var revitAddinsBase in revitAddinsBases)
                    {
                        if (!Directory.Exists(revitAddinsBase)) continue;
                        foreach (int year in years)
                        {
                            string yearDir = Path.Combine(revitAddinsBase, year.ToString());
                            if (!Directory.Exists(yearDir)) continue;

                            string addinFile = Path.Combine(yearDir, "KhimTools.addin");
                            if (File.Exists(addinFile)) try { File.Delete(addinFile); } catch { }

                            string pluginDir = Path.Combine(yearDir, "KhimTools");
                            if (Directory.Exists(pluginDir)) try { Directory.Delete(pluginDir, true); } catch { }
                        }
                    }

                    TxtLocalVersion.Text = "Chưa cài";
                    MessageBox.Show("Đã gỡ cài đặt thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi gỡ cài đặt (hãy đảm bảo đã đóng Revit trước khi gỡ): " + ex.Message);
                }
            }
        }

        private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (ChkAutoStart.IsChecked == true)
                {
                    key.SetValue(RegistryKeyName, Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    key.DeleteValue(RegistryKeyName, false);
                }
            }
            catch { }
        }
    }
}
