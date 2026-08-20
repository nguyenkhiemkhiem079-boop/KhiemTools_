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
            TxtGithubVersion.Text = "Đang check...";

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("KhiemToolsUpdater");

                string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string json = await client.GetStringAsync(apiUrl);

                // Trích xuất tag_name
                Match tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                string latestTag = tagMatch.Success ? tagMatch.Groups[1].Value : "v2.1.0";
                TxtGithubVersion.Text = latestTag;

                // Trích xuất browser_download_url của file bundle .zip
                Match zipMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+KhimTools_Bundle\\.zip|[^\"]+\\.zip)\"");
                string downloadUrl = zipMatch.Success ? zipMatch.Groups[1].Value : null;

                if (MessageBox.Show($"Tìm thấy phiên bản {latestTag} trên GitHub! Bạn có muốn cài đặt/cập nhật ngay không?", 
                    "Cập nhật KhiemTools", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    await PerformInstallOrUpdateAsync(downloadUrl);
                    MessageBox.Show("Cài đặt/Cập nhật hoàn tất! Vui lòng mở lại Revit để áp dụng.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    CheckCurrentLocalVersion();
                }
            }
            catch (Exception ex)
            {
                TxtGithubVersion.Text = "Lỗi kết nối";
                MessageBox.Show("Không thể kiểm tra cập nhật: " + ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async Task PerformInstallOrUpdateAsync(string downloadUrl)
        {
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                string tempZip = Path.Combine(Path.GetTempPath(), "KhiemTools_latest.zip");
                using (var client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(downloadUrl);
                    File.WriteAllBytes(tempZip, bytes);
                }

                if (Directory.Exists(_revitBundlePath))
                {
                    Directory.Delete(_revitBundlePath, true);
                }

                Directory.CreateDirectory(_revitBundlePath);
                ZipFile.ExtractToDirectory(tempZip, _revitBundlePath);
            }
            else
            {
                // Nếu chưa có link file zip, tạo thư mục bundle mặc định
                Directory.CreateDirectory(_revitBundlePath);
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
            if (MessageBox.Show("Bạn có chắc chắn muốn gỡ cài đặt KhiemTools khỏi máy tính không?", 
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
