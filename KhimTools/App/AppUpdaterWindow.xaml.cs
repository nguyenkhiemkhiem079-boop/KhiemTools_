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
        private const string RegistryKeyName = "KhimToolsUpdater";

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
            if (Directory.Exists(_revitBundlePath))
            {
                TxtLocalVersion.Text = "2.1.0";
            }
            else
            {
                TxtLocalVersion.Text = "Chưa cài";
            }
        }

        private void LoadRegistrySettings()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                object val = key?.GetValue(RegistryKeyName);
                ChkAutoStart.IsChecked = (val != null);
            }
            catch { }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtGithubVersion.Text = "Đang kiểm tra...";

            try
            {
                string latestTag = "v2.1.3";
                string downloadUrl = null;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/1.0");

                // Lớp 1: Đọc trực tiếp từ update_info.json trên nhánh master
                try
                {
                    string infoUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/master/update_info.json";
                    string infoJson = await client.GetStringAsync(infoUrl);
                    Match mTag = Regex.Match(infoJson, "\"latest_version\"\\s*:\\s*\"([^\"]+)\"");
                    if (mTag.Success)
                    {
                        latestTag = mTag.Groups[1].Value;
                    }
                }
                catch { }

                // Lớp 2: Kiểm tra GitHub Releases API (nếu có)
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

                TxtGithubVersion.Text = latestTag;

                if (MessageBox.Show($"Tìm thấy phiên bản {latestTag} trên GitHub!\nBạn có muốn tự động cài đặt / cập nhật vào Revit ngay không?", 
                    "Cập nhật KhimTools", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
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
            string tempDir = Path.Combine(Path.GetTempPath(), "KhiemTools_Installer");
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            Directory.CreateDirectory(tempDir);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KhiemToolsUpdater/1.0");

            bool downloaded = false;
            string bundleZipPath = Path.Combine(tempDir, "bundle.zip");

            // Cách 1: Tải file KhimTools_Bundle.zip từ Release nếu có link trực tiếp
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
            if (MessageBox.Show("Bạn có chắc chắn muốn gỡ cài đặt KhimTools khỏi máy tính không?", 
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
