using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Models
{
    public enum UpdateCheckStatus
    {
        Unknown,
        Checking,
        UpToDate,
        UpdateAvailable,
        ReadyToInstall,
        ServerUnavailable,
        VerificationFailed,
        Error
    }

    /// <summary>
    /// Model holding active update status and metadata for UI presentation.
    /// Eliminates hardcoded version fallbacks and enforces strict status tracking.
    /// </summary>
    public class UpdateInfo
    {
        public UpdateCheckStatus Status { get; set; } = UpdateCheckStatus.Unknown;

        [JsonProperty("product")]
        public string Product { get; set; } = "K-TOOLS";

        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; } = string.Empty;

        [JsonProperty("current_version")]
        public string CurrentVersion { get; set; } = string.Empty;

        [JsonProperty("build")]
        public string BuildId { get; set; } = string.Empty;

        [JsonProperty("commit")]
        public string GitCommit { get; set; } = string.Empty;

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonProperty("sha256")]
        public string ExpectedSha256 { get; set; } = string.Empty;

        [JsonProperty("package")]
        public string PackageFileName { get; set; } = string.Empty;

        [JsonProperty("supportedRevit")]
        public List<string> SupportedRevit { get; set; } = new List<string>();

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("changelog")]
        public List<string> Changelog { get; set; } = new List<string>();

        [JsonProperty("is_mandatory")]
        public bool IsMandatory { get; set; } = false;

        public string StagedZipPath { get; set; } = string.Empty;
        public string StagedExtractPath { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Populates UpdateInfo from an official UpdateManifest.
        /// </summary>
        public static UpdateInfo FromManifest(UpdateManifest manifest, string currentVersion)
        {
            if (manifest == null) return new UpdateInfo { Status = UpdateCheckStatus.Unknown };

            bool hasUpdate = VersionModel.CompareVersions(manifest.Version, currentVersion) > 0;

            return new UpdateInfo
            {
                Status = hasUpdate ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                Product = manifest.Product ?? "K-TOOLS",
                LatestVersion = manifest.Version ?? string.Empty,
                CurrentVersion = currentVersion ?? string.Empty,
                BuildId = manifest.Build ?? string.Empty,
                GitCommit = manifest.Commit ?? string.Empty,
                ReleaseDate = manifest.ReleaseDate ?? string.Empty,
                DownloadUrl = manifest.DownloadUrl ?? string.Empty,
                ExpectedSha256 = manifest.Sha256 ?? string.Empty,
                PackageFileName = manifest.Package ?? string.Empty,
                SupportedRevit = manifest.SupportedRevit ?? new List<string>(),
                Changelog = manifest.Changelog ?? new List<string>(),
                IsMandatory = manifest.IsMandatory,
                StatusMessage = hasUpdate ? "Có phiên bản mới sẵn sàng." : "K-TOOLS đang ở phiên bản mới nhất."
            };
        }
    }
}
