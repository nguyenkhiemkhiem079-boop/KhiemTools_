using System;
using System.Collections.Generic;

namespace KhimTools.Tools.Updater.Models
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = "2.1.0";
        public string CurrentVersion { get; set; } = "2.0.0";
        public string ReleaseDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public string DownloadUrl { get; set; } = "";
        public List<string> Changelog { get; set; } = new List<string>();
        public bool IsMandatory { get; set; } = false;
    }
}
