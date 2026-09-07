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
using KhiemToolsApp.Deployment;

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
            InitializeUpdaterVersion();
            CheckCurrentLocalVersion();
            LoadRegistrySettings();
        }

        private void InitializeUpdaterVersion()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null)
                {
                    TxtUpdaterVersion.Text = $"K-TOOLS Updater v{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch { }
        }

        private static void LogInfo(string message)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhimTools");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "update_log.txt");
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logLine);
            }
            catch { }
        }

        private string GetEffectiveBundlePath()
        {
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

                // 4. Kiểm tra DLL FileVersion & AssemblyVersion
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
                        try
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(dll);
                            if (!string.IsNullOrEmpty(fvi.FileVersion) && fvi.FileVersion != "1.0.0.0" && fvi.FileVersion != "0.0.0.0")
                            {
                                TxtLocalVersion.Text = "v" + fvi.FileVersion;
                                return;
                            }

                            // Fallback: Read assembly version via reflection if FileVersion is stripped/invalid
                            var asm = System.Reflection.Assembly.LoadFrom(dll);
                            var asmVer = asm.GetName().Version;
                            if (asmVer != null && asmVer.ToString() != "0.0.0.0" && asmVer.ToString() != "1.0.0.0")
                            {
                                TxtLocalVersion.Text = "v" + asmVer.ToString(3);
                                return;
                            }
                        }
                        catch { }
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
                foreach (var p in revitProcesses)
                {
                    try
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(3000))
                        {
                            p.Kill();
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
            LogInfo("=== Bắt đầu kiểm tra cập nhật ===");

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/1.0");

                string latestTag = null;
                string downloadUrl = null;
                string downloadUrlMsi = null;
                string sha256Msi = null;

                // Lớp 1: Đọc trực tiếp từ update_info.json trên GitHub master
                string infoUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/master/update_info.json?t={DateTime.UtcNow.Ticks}";
                UrlSecurityValidator.ValidateManifestUrl(infoUrl);
                LogInfo($"Layer 1 - Fetching update_info: {infoUrl}");
                try
                {
                    string infoJson = await client.GetStringAsync(infoUrl);
                    LogInfo($"Layer 1 - Response body: {infoJson}");
                    Match mTag = Regex.Match(infoJson, "\"latest_version\"\\s*:\\s*\"([^\"]+)\"");
                    if (mTag.Success)
                    {
                        latestTag = mTag.Groups[1].Value;
                        LogInfo($"Layer 1 - Parsed tag: {latestTag}");
                    }
                    Match mUrl = Regex.Match(infoJson, "\"download_url\"\\s*:\\s*\"([^\"]+)\"");
                    if (mUrl.Success)
                    {
                        string candidateUrl = mUrl.Groups[1].Value;
                        if (UrlSecurityValidator.IsSecureOfficialUrl(candidateUrl, false, out string reason))
                        {
                            downloadUrl = candidateUrl;
                            LogInfo($"Layer 1 - Parsed valid download_url: {downloadUrl}");
                        }
                        else
                        {
                            LogInfo($"Layer 1 - Rejected download_url: {reason} ({candidateUrl})");
                        }
                    }
                    Match mMsi = Regex.Match(infoJson, "\"download_url_msi\"\\s*:\\s*\"([^\"]+)\"");
                    if (mMsi.Success)
                    {
                        string candidateMsi = mMsi.Groups[1].Value;
                        if (UrlSecurityValidator.IsSecureOfficialUrl(candidateMsi, false, out string reasonMsi))
                        {
                            downloadUrlMsi = candidateMsi;
                            LogInfo($"Layer 1 - Parsed valid download_url_msi: {downloadUrlMsi}");
                        }
                    }
                    Match mSha = Regex.Match(infoJson, "\"sha256_msi\"\\s*:\\s*\"([^\"]+)\"");
                    if (mSha.Success)
                    {
                        sha256Msi = mSha.Groups[1].Value;
                        LogInfo($"Layer 1 - Parsed sha256_msi: {sha256Msi}");
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"Layer 1 - Error: {ex.GetType().Name} - {ex.Message}");
                }

                // Lớp 2: Kiểm tra GitHub Releases API (fallback)
                if (string.IsNullOrEmpty(latestTag))
                {
                    string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                    UrlSecurityValidator.ValidateManifestUrl(apiUrl);
                    LogInfo($"Layer 2 - Fetching API releases/latest: {apiUrl}");
                    try
                    {
                        string releaseJson = await client.GetStringAsync(apiUrl);
                        LogInfo($"Layer 2 - Response body: {releaseJson}");
                        Match tagMatch = Regex.Match(releaseJson, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (tagMatch.Success)
                        {
                            latestTag = tagMatch.Groups[1].Value;
                            LogInfo($"Layer 2 - Parsed tag: {latestTag}");
                        }

                        Match zipMatch = Regex.Match(releaseJson, "\"browser_download_url\"\\s*:\\s*\"([^\"]+KhimTools_Bundle\\.zip|[^\"]+\\.zip)\"");
                        if (zipMatch.Success)
                        {
                            string candidateUrl = zipMatch.Groups[1].Value;
                            if (UrlSecurityValidator.IsSecureOfficialUrl(candidateUrl, false, out string reason))
                            {
                                downloadUrl = candidateUrl;
                                LogInfo($"Layer 2 - Parsed valid download_url: {downloadUrl}");
                            }
                            else
                            {
                                LogInfo($"Layer 2 - Rejected download_url: {reason} ({candidateUrl})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogInfo($"Layer 2 - Error: {ex.GetType().Name} - {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(latestTag))
                {
                    latestTag = "v2.7.0";
                    LogInfo($"Using ultimate fallback tag: {latestTag}");
                }

                TxtGithubVersion.Text = latestTag;

                if (MessageBox.Show($"Tìm thấy phiên bản {latestTag} trên GitHub!\nBạn có muốn tự động cài đặt / cập nhật vào Revit ngay không?", 
                    "Cập nhật K-TOOLS", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (!EnsureRevitClosed())
                    {
                        TxtGithubVersion.Text = latestTag;
                        LogInfo("Revit is still running. Aborting install.");
                        return;
                    }

                    TxtGithubVersion.Text = "Đang tải & cài đặt...";
                    await PerformInstallOrUpdateAsync(latestTag, downloadUrl, downloadUrlMsi, sha256Msi);
                    MessageBox.Show("Cài đặt / Cập nhật hoàn tất!\nĐã nạp toàn bộ module mới an toàn vào tất cả phiên bản Revit trên máy.\nVui lòng mở Revit để sử dụng.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    CheckCurrentLocalVersion();
                    TxtGithubVersion.Text = latestTag;
                }
            }
            catch (DeploymentLockException ex)
            {
                LogInfo($"Lock Error: {ex.Message}");
                TxtGithubVersion.Text = "Revit đang mở";
                MessageBox.Show($"Không thể cập nhật vì tập tin đang bị khóa bởi Revit:\n{ex.Message}\n\nVui lòng đóng Revit hoàn toàn rồi thử lại.", "Tập tin bị khóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DeploymentValidationException ex)
            {
                LogInfo($"Validation Error: {ex.Message}");
                TxtGithubVersion.Text = "Lỗi xác thực gói";
                MessageBox.Show($"Lỗi kiểm tra tính toàn vẹn hoặc phiên bản:\n{ex.Message}\n\nĐã tự động khôi phục an toàn phiên bản trước đó.", "Xác thực thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (RollbackFailedException ex)
            {
                LogInfo($"CRITICAL ROLLBACK FAILURE: {ex.Message}");
                TxtGithubVersion.Text = "Lỗi khôi phục";
                MessageBox.Show($"CẢNH BÁO NGUY HIỂM:\nQuá trình khôi phục bản cũ gặp sự cố!\n{ex.Message}\n\nThư mục sao lưu an toàn tại:\n{ex.BackupPath}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (DeploymentSecurityException ex)
            {
                LogInfo($"Security Error: {ex.Message}");
                TxtGithubVersion.Text = "Lỗi bảo mật URL";
                MessageBox.Show($"Cảnh báo bảo mật:\n{ex.Message}", "Bảo mật", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LogInfo($"General Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                TxtGithubVersion.Text = "Lỗi cập nhật";
                MessageBox.Show($"Lỗi cập nhật: {ex.Message}\n\nChi tiết: {ex.StackTrace}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async Task PerformInstallOrUpdateAsync(string tag, string directZipUrl, string msiUrl = null, string expectedMsiSha256 = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "KhimTools_Download_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KhimToolsUpdater/1.0");

            // Priority 1: If MSI installer is available or target is MSI-managed, deploy via authoritative Windows Installer
            if (!string.IsNullOrEmpty(msiUrl) || InstallationClassifier.IsMsiManaged())
            {
                string targetMsiUrl = msiUrl;
                if (string.IsNullOrEmpty(targetMsiUrl))
                {
                    targetMsiUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tag}/K-TOOLS.msi";
                }

                if (UrlSecurityValidator.IsSecureOfficialUrl(targetMsiUrl, false, out string msiRejectReason))
                {
                    LogInfo($"MSI Deploy - Starting download from: {targetMsiUrl}");
                    string msiLocalPath = Path.Combine(tempDir, "K-TOOLS.msi");
                    try
                    {
                        byte[] msiData = await client.GetByteArrayAsync(targetMsiUrl);
                        File.WriteAllBytes(msiLocalPath, msiData);
                        LogInfo("MSI Deploy - Download completed. Executing authoritative MSI deployment...");

                        var engine = new SafeDeploymentEngine();
                        engine.DeployMsi(msiLocalPath, expectedMsiSha256, LogInfo);
                        LogInfo("MSI Deploy - Authoritative deployment completed successfully.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogInfo($"MSI Deploy - Execution encountered error: {ex.Message}");
                        // If machine is already MSI-managed, we cannot fall back to raw ZIP
                        if (InstallationClassifier.IsMsiManaged())
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    LogInfo($"MSI Deploy - URL rejected by security validator: {msiRejectReason}");
                }
            }

            // Priority 2: Staged ZIP deployment for legacy non-MSI installations
            bool downloaded = false;
            string bundleZipPath = Path.Combine(tempDir, "bundle.zip");

            // Cách 1: Tải trực tiếp từ directZipUrl
            if (!string.IsNullOrEmpty(directZipUrl))
            {
                if (UrlSecurityValidator.IsSecureOfficialUrl(directZipUrl, false, out string reason))
                {
                    LogInfo($"Download Method 1 - Starting download from directZipUrl: {directZipUrl}");
                    try
                    {
                        byte[] data = await client.GetByteArrayAsync(directZipUrl);
                        File.WriteAllBytes(bundleZipPath, data);
                        downloaded = true;
                        LogInfo("Download Method 1 - Completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        LogInfo($"Download Method 1 - Failed: {ex.GetType().Name} - {ex.Message}");
                    }
                }
                else
                {
                    LogInfo($"Download Method 1 - Rejected by security validator: {reason}");
                }
            }

            // Cách 2: Thử tải link direct release theo tag
            if (!downloaded)
            {
                string releaseUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tag}/KhimTools_Bundle.zip";
                UrlSecurityValidator.ValidateDownloadUrl(releaseUrl);
                LogInfo($"Download Method 2 - Starting download from fallback releaseUrl: {releaseUrl}");
                try
                {
                    byte[] data = await client.GetByteArrayAsync(releaseUrl);
                    File.WriteAllBytes(bundleZipPath, data);
                    downloaded = true;
                    LogInfo("Download Method 2 - Completed successfully.");
                }
                catch (Exception ex)
                {
                    LogInfo($"Download Method 2 - Failed: {ex.GetType().Name} - {ex.Message}");
                }
            }

            if (File.Exists(bundleZipPath) && downloaded)
            {
                LogInfo($"Executing safe staged deployment for tag {tag}...");
                var engine = new SafeDeploymentEngine();
                engine.DeployZip(bundleZipPath, _programDataBundlePath, tag, LogInfo);
                LogInfo("Safe deployment completed successfully.");
            }
            else
            {
                LogInfo("Download failed (both methods failed). Raising exception.");
                throw new FileNotFoundException($"Không thể tải bộ cài đặt K-TOOLS ({tag}) từ GitHub server. Vui lòng kiểm tra lại kết nối mạng.");
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

                    // If installation is managed by MSI, perform authoritative Windows Installer uninstall
                    if (InstallationClassifier.IsMsiManaged())
                    {
                        LogInfo("Uninstall: Detected MSI-managed installation. Invoking msiexec /x...");
                        var psi = new ProcessStartInfo
                        {
                            FileName = "msiexec.exe",
                            Arguments = "/x {B73A7490-6831-4F58-9D26-C18244B27DF1} /passive /norestart",
                            UseShellExecute = true
                        };
                        using (var p = Process.Start(psi))
                        {
                            p.WaitForExit();
                        }
                        LogInfo("Uninstall: Windows Installer process completed.");
                        TxtLocalVersion.Text = "Chưa cài";
                        MessageBox.Show("Đã gỡ cài đặt K-TOOLS an toàn qua Windows Installer!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    if (Directory.Exists(_programDataBundlePath))
                    {
                        Directory.Delete(_programDataBundlePath, true);
                    }
                    if (Directory.Exists(_appDataBundlePath))
                    {
                        var classification = InstallationClassifier.ClassifyBundle(_appDataBundlePath, _programDataBundlePath);
                        if (classification == InstallationClassification.UserManaged)
                        {
                            LogInfo($"Uninstall: Preserved user-managed bundle at '{_appDataBundlePath}'.");
                        }
                        else
                        {
                            Directory.Delete(_appDataBundlePath, true);
                        }
                    }

                    // Dọn dẹp có phân loại và sao lưu trước cho các tệp legacy
                    string uninstallBackup = Path.Combine(Path.GetTempPath(), "KhimTools_Uninstall_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    InstallationClassifier.PreserveAndCleanLegacyArtifacts(uninstallBackup, LogInfo);

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