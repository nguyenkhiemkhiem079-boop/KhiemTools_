using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace KhimTools.Tools.Updater.Models
{
    /// <summary>
    /// K-TOOLS Production-Grade Update Manifest.
    /// Acts as the single source of truth for commercial update packages,
    /// cryptographic integrity (SHA256), exact versioning, and Revit compatibility.
    /// </summary>
    public class UpdateManifest
    {
        [JsonProperty("product")]
        public string Product { get; set; } = "K-TOOLS";

        [JsonProperty("version")]
        public string Version { get; set; } = "";

        [JsonProperty("updater_version")]
        public string UpdaterVersion { get; set; } = "";

        [JsonProperty("build")]
        public string Build { get; set; } = "";

        [JsonProperty("commit")]
        public string Commit { get; set; } = "";

        [JsonProperty("releaseDate")]
        public string ReleaseDate { get; set; } = "";

        [JsonProperty("package")]
        public string Package { get; set; } = "";

        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonProperty("supportedRevit")]
        public List<string> SupportedRevit { get; set; } = new List<string>();

        [JsonProperty("changelog")]
        public List<string> Changelog { get; set; } = new List<string>();

        [JsonProperty("isMandatory")]
        public bool IsMandatory { get; set; } = false;

        [JsonProperty("minRequiredVersion")]
        public string MinRequiredVersion { get; set; } = "";

        /// <summary>
        /// Parses a manifest JSON string. Throws if json is null or invalid.
        /// </summary>
        public static UpdateManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Update manifest JSON content cannot be empty.", nameof(json));

            var manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
            if (manifest == null)
                throw new InvalidDataException("Failed to deserialize UpdateManifest from JSON.");

            return manifest;
        }

        /// <summary>
        /// Serializes this manifest to a formatted JSON string.
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Validates that all required fields are present and well-formed.
        /// </summary>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (string.IsNullOrWhiteSpace(Product))
                return (false, "Manifest 'product' field is missing or empty.");

            if (string.IsNullOrWhiteSpace(Version))
                return (false, "Manifest 'version' field is missing or empty.");

            if (string.IsNullOrWhiteSpace(Sha256))
                return (false, "Manifest 'sha256' cryptographic hash is missing.");

            if (Sha256.Length != 64)
                return (false, $"Manifest 'sha256' must be a 64-character hex string (actual length: {Sha256.Length}).");

            if (string.IsNullOrWhiteSpace(DownloadUrl))
                return (false, "Manifest 'downloadUrl' is missing or empty.");

            if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return (false, $"Manifest 'downloadUrl' '{DownloadUrl}' is not a valid HTTP/HTTPS URL.");

            if (SupportedRevit == null || SupportedRevit.Count == 0)
                return (false, "Manifest 'supportedRevit' list must specify at least one supported Revit version.");

            return (true, string.Empty);
        }

        /// <summary>
        /// Checks if a given Revit year (e.g. "2025" or 2025) is supported by this package.
        /// </summary>
        public bool IsRevitSupported(string revitVersion)
        {
            if (string.IsNullOrWhiteSpace(revitVersion) || SupportedRevit == null) return false;
            string clean = revitVersion.Trim();
            foreach (var sup in SupportedRevit)
            {
                if (string.Equals(sup.Trim(), clean, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
