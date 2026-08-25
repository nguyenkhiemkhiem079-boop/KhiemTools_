using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Models
{
    public class UpdateInfo
    {
        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; } = "v2.7.0";

        [JsonProperty("current_version")]
        public string CurrentVersion { get; set; } = "v2.0.0";

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("changelog")]
        public List<string> Changelog { get; set; } = new List<string>();

        [JsonProperty("is_mandatory")]
        public bool IsMandatory { get; set; } = false;
    }
}
