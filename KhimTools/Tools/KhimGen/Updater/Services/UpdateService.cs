using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using KhimTools.Tools.Updater.Models;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Services
{
    public class UpdateService
    {
        private const string UpdateCheckUrl = "https://raw.githubusercontent.com/nguyenkhiemkhiem079-boop/KhiemTools_/master/update_info.json";

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
                using (var wc = new WebClient())
                {
                    string json = await wc.DownloadStringTaskAsync(new Uri(UpdateCheckUrl));
                    var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(json);
                    if (updateInfo != null)
                    {
                        updateInfo.CurrentVersion = currentVer;
                        return updateInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KhimTools.UpdateService] Error fetching update from '{UpdateCheckUrl}': {ex.Message}");
                // Fallback nếu không có internet hoặc API chưa host
            }

            return new UpdateInfo
            {
                CurrentVersion = currentVer,
                LatestVersion = "v2.7.0",
                ReleaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                DownloadUrl = "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest",
                Changelog = new List<string>
                {
                    "Slab Step Generator - Tự động tạo và đặt Family giật cấp sàn theo ranh giới chọn lọc.",
                    "Layout Pulldown - Gom nhóm các công cụ SheetGen, ViewportAlign, DetailNumberUpdater và TextAligns.",
                    "Graphic Overdrive, Auto Grid & Plans, Copy Link Elements.",
                    "Hỗ trợ bộ cài đặt Bundle tự động đa phiên bản Revit (2020-2028+)."
                }
            };
        }

        public async Task<bool> DownloadAndStageUpdateAsync(string downloadUrl, IProgress<double> progress = null)
        {
            try
            {
                string tempZip = Path.Combine(Path.GetTempPath(), "KhimTools_Update.zip");
                using (var wc = new WebClient())
                {
                    wc.DownloadProgressChanged += (s, e) => progress?.Report((double)e.ProgressPercentage);
                    await wc.DownloadFileTaskAsync(new Uri(downloadUrl), tempZip);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KhimTools.UpdateService] Lỗi khi tải gói cập nhật từ '{downloadUrl}': {ex.Message}");
                Trace.WriteLine($"[KhimTools.UpdateService] Lỗi ngoại lệ trong DownloadAndStageUpdateAsync: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }
    }
}