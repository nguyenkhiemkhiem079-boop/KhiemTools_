using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using KhimTools.Tools.Updater.Models;

namespace KhimTools.Tools.Updater.Services
{
    /// <summary>
    /// In-Revit commercial update service.
    /// Handles HTTPS manifest retrieval, background download to isolated staging,
    /// cryptographic SHA256 verification, and launching the out-of-process KToolsUpdater.exe.
    /// Never touches the active bundle directly from within Revit.
    /// </summary>
    public class UpdateService
    {
        public const string UpdateManifestUrl = "https://raw.githubusercontent.com/nguyenkhiemkhiem079-boop/KhiemTools_/master/update_info.json";

        public VersionModel CurrentVersionInfo { get; }
        public string TargetRevitVersion { get; set; } = string.Empty;
        public int RevitPid { get; set; } = 0;
        public string RevitExecutablePath { get; set; } = string.Empty;

        public UpdateService(string revitVersion = null)
        {
            CurrentVersionInfo = VersionModel.FromAssembly(Assembly.GetExecutingAssembly());
            TargetRevitVersion = revitVersion ?? string.Empty;

            try
            {
                var curProc = Process.GetCurrentProcess();
                RevitPid = curProc.Id;
                if (curProc.MainModule != null)
                {
                    RevitExecutablePath = curProc.MainModule.FileName;
                }
            }
            catch { }
        }

        public string GetCurrentVersion()
        {
            return CurrentVersionInfo.ProductVersion;
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            string currentVer = GetCurrentVersion();
            string cacheBustUrl = $"{UpdateManifestUrl}?t={DateTime.UtcNow.Ticks}";

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd($"K-TOOLS-Client/{currentVer}");
                    string json = await client.GetStringAsync(cacheBustUrl);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var manifest = UpdateManifest.FromJson(json);
                        var (isValid, errorMsg) = manifest.Validate();

                        if (!isValid)
                        {
                            return new UpdateInfo
                            {
                                Status = UpdateCheckStatus.Error,
                                CurrentVersion = currentVer,
                                StatusMessage = $"Tệp mô tả cập nhật không hợp lệ: {errorMsg}"
                            };
                        }

                        var updateInfo = UpdateInfo.FromManifest(manifest, currentVer);

                        // Verify Revit version compatibility if known
                        if (!string.IsNullOrEmpty(TargetRevitVersion) && !manifest.IsRevitSupported(TargetRevitVersion))
                        {
                            updateInfo.Status = UpdateCheckStatus.UpToDate;
                            updateInfo.StatusMessage = $"Bản cập nhật {manifest.Version} không hỗ trợ phiên bản Revit {TargetRevitVersion} đang chạy.";
                            return updateInfo;
                        }

                        return updateInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[K-TOOLS UpdateService] Exception fetching update manifest: {ex.Message}");
            }

            // Fallback: If server is unavailable, status MUST be UNKNOWN.
            // Under NO circumstance should we claim a hardcoded version is the latest!
            return new UpdateInfo
            {
                Status = UpdateCheckStatus.ServerUnavailable,
                CurrentVersion = currentVer,
                LatestVersion = "UNKNOWN",
                StatusMessage = "Không thể kết nối đến máy chủ cập nhật (GitHub). Vui lòng kiểm tra đường truyền Internet."
            };
        }

        public async Task<bool> DownloadAndStageUpdateAsync(UpdateInfo updateInfo, IProgress<double> progress = null)
        {
            if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                if (updateInfo != null) updateInfo.StatusMessage = "Đường dẫn tải gói cập nhật trống.";
                return false;
            }

            var staging = new StagingManager(updateInfo.LatestVersion);
            staging.PrepareStagingDirectory();

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd($"K-TOOLS-Updater/{GetCurrentVersion()}");

                    using (var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(staging.PackageZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes > 0 && progress != null)
                                {
                                    double pct = (double)totalRead / totalBytes * 100.0;
                                    progress.Report(pct);
                                }
                            }
                        }
                    }
                }

                // 1. Verify SHA256 cryptographic checksum
                string actualSha = PackageVerificationService.ComputeSha256(staging.PackageZipPath);
                if (!string.IsNullOrWhiteSpace(updateInfo.ExpectedSha256) &&
                    !string.Equals(actualSha, updateInfo.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    updateInfo.Status = UpdateCheckStatus.VerificationFailed;
                    updateInfo.StatusMessage = $"Lỗi kiểm tra toàn vẹn (SHA256 Mismatch)! Mong đợi: {updateInfo.ExpectedSha256.Substring(0, 10)}..., Thực tế: {actualSha.Substring(0, 10)}...";
                    staging.Cleanup();
                    return false;
                }

                // 2. Extract to staging directory
                if (!staging.ExtractToStaging())
                {
                    updateInfo.Status = UpdateCheckStatus.VerificationFailed;
                    updateInfo.StatusMessage = "Không thể giải nén gói cập nhật tại thư mục Staging.";
                    staging.Cleanup();
                    return false;
                }

                // 3. Complete 10-point package verification
                var manifest = new UpdateManifest
                {
                    Version = updateInfo.LatestVersion,
                    Sha256 = updateInfo.ExpectedSha256,
                    DownloadUrl = updateInfo.DownloadUrl,
                    SupportedRevit = updateInfo.SupportedRevit
                };

                var verifyResult = PackageVerificationService.VerifyPackage(
                    staging.PackageZipPath,
                    staging.ExtractedBundlePath,
                    manifest,
                    TargetRevitVersion);

                if (!verifyResult.IsValid)
                {
                    updateInfo.Status = UpdateCheckStatus.VerificationFailed;
                    updateInfo.StatusMessage = $"Xác thực gói thất bại: {string.Join("; ", verifyResult.FailedChecks)}";
                    staging.Cleanup();
                    return false;
                }

                updateInfo.Status = UpdateCheckStatus.ReadyToInstall;
                updateInfo.StagedZipPath = staging.PackageZipPath;
                updateInfo.StagedExtractPath = staging.ExtractedBundlePath;
                updateInfo.StatusMessage = "Gói cập nhật đã sẵn sàng cài đặt.";
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[K-TOOLS UpdateService] Download/Staging failed: {ex.Message}");
                updateInfo.Status = UpdateCheckStatus.Error;
                updateInfo.StatusMessage = $"Lỗi trong quá trình tải xuống: {ex.Message}";
                staging.Cleanup();
                return false;
            }
        }

        /// <summary>
        /// Finds the path to KToolsUpdater.exe (or KhimTools_Installer.exe).
        /// Searches bundle directory, AppData, and current assembly location.
        /// </summary>
        public string FindExternalUpdaterExecutable()
        {
            string asmLoc = Assembly.GetExecutingAssembly().Location;
            string asmDir = !string.IsNullOrEmpty(asmLoc) ? Path.GetDirectoryName(asmLoc) : string.Empty;

            string[] candidatePaths = new string[]
            {
                Path.Combine(asmDir ?? "", "KToolsUpdater.exe"),
                Path.Combine(asmDir ?? "", "..", "..", "KToolsUpdater.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\ApplicationPlugins\KhimTools.bundle\Contents\KToolsUpdater.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"KTools\KToolsUpdater.exe")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path)) return path;
            }

            return null;
        }

        /// <summary>
        /// Launches the external out-of-process updater (KToolsUpdater.exe) and signals Revit to exit.
        /// </summary>
        public bool LaunchExternalUpdater(UpdateInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.StagedExtractPath) || !Directory.Exists(info.StagedExtractPath))
            {
                return false;
            }

            string updaterExe = FindExternalUpdaterExecutable();
            if (string.IsNullOrEmpty(updaterExe) || !File.Exists(updaterExe))
            {
                Trace.WriteLine("[K-TOOLS UpdateService] Cannot find KToolsUpdater.exe on system.");
                return false;
            }

            string targetBundle = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Autodesk\ApplicationPlugins\KhimTools.bundle");

            string args = $"--action install --staging-dir \"{info.StagedExtractPath}\" --target-bundle \"{targetBundle}\" --revit-pid {RevitPid} --revit-path \"{RevitExecutablePath}\" --version \"{info.LatestVersion}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = updaterExe,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(updaterExe)
                };

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[K-TOOLS UpdateService] Error starting KToolsUpdater: {ex.Message}");
                return false;
            }
        }
    }
}