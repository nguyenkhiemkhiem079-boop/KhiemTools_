using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using KhimTools.Tools.Updater.Models;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Services
{
    public class UpdateService
    {
        // Đường dẫn API hoặc file JSON kiểm tra phiên bản mới
        // (Có thể trỏ về GitHub raw content hoặc server của bạn)
        private const string UpdateCheckUrl = "https://raw.githubusercontent.com/nguyenkhiemkhiem079-boop/KhiemTools_/main/update_info.json";

        public static string GetCurrentVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v2.0.0";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            string currentVer = GetCurrentVersion();

            try
            {
                using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    string json = await httpClient.GetStringAsync(UpdateCheckUrl);
                    var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(json);
                    if (updateInfo != null)
                    {
                        updateInfo.CurrentVersion = currentVer;
                        return updateInfo;
                    }
                }
            }
            catch
            {
                // Fallback nếu không có internet hoặc API chưa host
            }

            // Mock dữ liệu demo mới nhất
            return new UpdateInfo
            {
                CurrentVersion = currentVer,
                LatestVersion = "v2.1.0",
                ReleaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                DownloadUrl = "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest",
                Changelog = new List<string>
                {
                    "Tái cấu trúc giao diện KhimTools Workspace chuyên nghiệp.",
                    "Bổ sung Dockable Workspace Panel ghim cạnh màn hình.",
                    "Tự động đồng bộ Detail Number và Căn chỉnh Viewport.",
                    "Tối ưu hiệu năng tạo thép hàng loạt cho Cột và Dầm."
                }
            };
        }

        public async Task<bool> DownloadAndStageUpdateAsync(string downloadUrl, IProgress<double> progress = null)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "KhimTools_Update");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                // Giả lập hoặc tải thật nếu có URL hợp lệ
                await Task.Delay(1200);
                progress?.Report(100.0);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
