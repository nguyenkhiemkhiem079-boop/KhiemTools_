using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Models
{
    public class UpdateInfo
    {
        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; } = "";

        [JsonProperty("current_version")]
        public string CurrentVersion { get; set; } = "";

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; } = "";

        [JsonIgnore]
        public bool DiscoverySucceeded { get; set; }

        [JsonIgnore]
        public string DiscoveryError { get; set; } = "";

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonProperty("download_url_msi")]
        public string DownloadUrlMsi { get; set; } = "";

        [JsonProperty("sha256_msi")]
        public string Sha256Msi { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("changelog")]
        public List<string> Changelog { get; set; } = new List<string>();

        [JsonProperty("is_mandatory")]
        public bool IsMandatory { get; set; } = false;
    }
}
