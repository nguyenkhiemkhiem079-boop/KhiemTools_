using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using KhimTools.Tools.Updater.Models;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Services
{
    public class UpdateService
    {
        private const string UpdateCheckUrl = "https://raw.githubusercontent.com/nguyenkhiemkhiem079-boop/KhiemTools_/master/update_info.json";
        private static readonly HttpClient UpdateHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;

        public UpdateService(HttpClient httpClient = null) { _httpClient = httpClient ?? UpdateHttpClient; }

        public static string GetCurrentVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "Unknown";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            string currentVer = GetCurrentVersion();
            string discoveryError = "Release metadata is unavailable.";

            try
            {
                string json = await _httpClient.GetStringAsync(new Uri(UpdateCheckUrl)).ConfigureAwait(false);
                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(json);
                if (updateInfo != null && !string.IsNullOrWhiteSpace(updateInfo.LatestVersion) &&
                    Version.TryParse(updateInfo.LatestVersion.TrimStart('v', 'V').Split('-')[0], out _))
                {
                    updateInfo.CurrentVersion = currentVer;
                    updateInfo.DiscoverySucceeded = true;
                    return updateInfo;
                }
                discoveryError = "Release metadata has no valid latest_version.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KhimTools.UpdateService] Error fetching update from '{UpdateCheckUrl}': {ex.Message}");
                discoveryError = "Release metadata could not be verified: " + ex.Message;
            }

            return new UpdateInfo
            {
                CurrentVersion = currentVer,
                DiscoverySucceeded = false,
                DiscoveryError = discoveryError
            };
        }

        /// <summary>
        /// Opens an allow-listed official GitHub release URL. This in-process updater
        /// never executes a local updater or writes into the MSI-owned bundle.
        /// </summary>
        public bool LaunchExternalUpdater(string releaseUrl = null)
        {
            try
            {
                string target = IsOfficialReleaseUrl(releaseUrl)
                    ? releaseUrl
                    : "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/latest";
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
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

        internal static bool IsOfficialReleaseUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;

            const string prefix = "/nguyenkhiemkhiem079-boop/KhiemTools_/releases/";
            return uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
