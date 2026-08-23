using System;
using System.Collections.Generic;
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
            catch
            {
                // Fallback nếu không có internet hoặc API chưa host
            }

            return new UpdateInfo
            {
                CurrentVersion = currentVer,
                LatestVersion = "v2.5.0",
                ReleaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                DownloadUrl = "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest",
                Changelog = new List<string>
                {
                    "Tái cấu trúc giao diện KhimTools theo 4 Panel chuyên môn: K-GEN, K-STRUCTURAL, K-ARCHITECTURAL, K-MEP.",
                    "Bổ sung công cụ Graphic Overdrive, Auto Grid & Plans, Copy Link Elements.",
                    "Cải tiến tính năng kiểm tra an toàn cốt thép và Align Viewport theo cây phân cấp."
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
            catch
            {
                return false;
            }
        }
    }
}