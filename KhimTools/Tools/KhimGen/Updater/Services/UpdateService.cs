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

        /// <summary>
        /// Launches the external installer/updater process outside of Autodesk Revit.
        /// Live Revit DLLs must never be modified or replaced from inside the Revit process.
        /// </summary>
        public bool LaunchExternalUpdater()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string[] candidatePaths = new string[]
                {
                    Path.Combine(programData, @"Autodesk\ApplicationPlugins\KhimTools.bundle\KhimTools_Installer.exe"),
                    Path.Combine(programData, @"Autodesk\ApplicationPlugins\KhimTools.bundle\K-TOOLS_Installer.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\KhimTools\KhimTools_Installer.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KhimTools_Installer.exe")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                        return true;
                    }
                }

                // If local installer executable is not yet placed, open the official GitHub release page
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KhimTools.UpdateService] Failed to launch external updater: {ex.Message}");
                return false;
            }
        }
    }
}