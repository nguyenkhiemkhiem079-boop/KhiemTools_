using System;
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

        // Thư mục cài đặt Revit Addin Bundle chuẩn của Autodesk
        private readonly string _revitBundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ApplicationPlugins\KhimTools.bundle");

        public AppUpdaterWindow()
        {
            InitializeComponent();
            CheckCurrentLocalVersion();
            LoadRegistrySettings();
        }

        private void CheckCurrentLocalVersion()
        {
            try
            {
                if (!Directory.Exists(_revitBundlePath))
                {
                    TxtLocalVersion.Text = "Chưa cài";
                    return;
                }

                // 1. Kiểm tra file update_info.json trong bundle (nếu có)
                string localInfoPath = Path.Combine(_revitBundlePath, "update_info.json");
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
                string versionTxtPath = Path.Combine(_revitBundlePath, "installed_version.txt");
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
                string packageXmlPath = Path.Combine(_revitBundlePath, "PackageContents.xml");
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
                    Path.Combine(_revitBundlePath, "Contents", "Legacy", "KhimTools.dll"),
                    Path.Combine(_revitBundlePath, "Contents", "Modern", "KhimTools.dll"),
                    Path.Combine(_revitBundlePath, "Legacy", "KhimTools.dll"),
                    Path.Combine(_revitBundlePath, "Modern", "KhimTools.dll")
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
                TxtLocalVersion.Text = Directory.Exists(_revitBundlePath) ? "Đã cài" : "Chưa cài";
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
                    latestTag = "v2.5.3";
                }

                TxtGithubVersion.Text = latestTag;

                if (MessageBox.Show($"Tìm thấy phiên bản {latestTag} trên GitHub!\nBạn có muốn tự động cài đặt / cập nhật vào Revit ngay không?", 
                    "Cập nhật K-TOOLS", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    TxtGithubVersion.Text = "Đang cài đặt...";
                    await PerformInstallOrUpdateAsync(latestTag, downloadUrl);
                    MessageBox.Show("Cài đặt / Cập nhật hoàn tất!\nVui lòng mở lại Revit để sử dụng các tính năng mới.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    CheckCurrentLocalVersion();
                    TxtGithubVersion.Text = latestTag;
                }
            }
            catch (Exception ex)
            {
                TxtGithubVersion.Text = "Lỗi kết nối";
                MessageBox.Show("Lỗi: " + ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                ZipFile.ExtractToDirectory(repoZipPath, extractDir);

                // Tìm thư mục Deploy trong repo
                string[] deployDirs = Directory.GetDirectories(extractDir, "Deploy", SearchOption.AllDirectories);
                if (deployDirs.Length > 0)
                {
                    string deploySrc = deployDirs[0];
                    if (Directory.Exists(_revitBundlePath))
                    {
                        try { Directory.Delete(_revitBundlePath, true); } catch { }
                    }
                    Directory.CreateDirectory(_revitBundlePath);

                    CopyDirectory(deploySrc, _revitBundlePath);

                    // Ghi version vào bundle
                    File.WriteAllText(Path.Combine(_revitBundlePath, "installed_version.txt"), tag);
                    return;
                }
            }

            // Nếu tải được file KhimTools_Bundle.zip
            if (File.Exists(bundleZipPath))
            {
                if (Directory.Exists(_revitBundlePath))
                {
                    try { Directory.Delete(_revitBundlePath, true); } catch { }
                }
                Directory.CreateDirectory(_revitBundlePath);
                ZipFile.ExtractToDirectory(bundleZipPath, _revitBundlePath);

                // Ghi version vào bundle
                File.WriteAllText(Path.Combine(_revitBundlePath, "installed_version.txt"), tag);
            }
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
                    if (Directory.Exists(_revitBundlePath))
                    {
                        Directory.Delete(_revitBundlePath, true);
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
