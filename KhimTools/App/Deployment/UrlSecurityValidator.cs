using System;
using System.Collections.Generic;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Enforces strict URL transport security and domain/path provenance verification.
    /// Ensures manifests and update packages originate only from official, authentic GitHub endpoints over HTTPS.
    /// </summary>
    public static class UrlSecurityValidator
    {
        public const string ExpectedOwner = "nguyenkhiemkhiem079-boop";
        public const string ExpectedRepo = "KhiemTools_";

        private static readonly HashSet<string> AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "github.com",
            "raw.githubusercontent.com",
            "api.github.com",
            "objects.githubusercontent.com" // GitHub Release asset CDN redirects
        };

        /// <summary>
        /// Validates that a manifest URL is HTTPS, targets an allowed GitHub domain, and belongs to the official repository.
        /// Throws DeploymentSecurityException if invalid.
        /// </summary>
        public static void ValidateManifestUrl(string url)
        {
            string reason;
            if (!IsSecureOfficialUrl(url, true, out reason))
            {
                throw new DeploymentSecurityException(
                    string.Format("Manifest URL security check failed: {0} (URL: {1})", reason, url));
            }
        }

        /// <summary>
        /// Validates that a package download URL is HTTPS and targets an approved GitHub release endpoint.
        /// Throws DeploymentSecurityException if invalid.
        /// </summary>
        public static void ValidateDownloadUrl(string url)
        {
            string reason;
            if (!IsSecureOfficialUrl(url, false, out reason))
            {
                throw new DeploymentSecurityException(
                    string.Format("Download URL security check failed: {0} (URL: {1})", reason, url));
            }
        }

        /// <summary>
        /// Evaluates whether a URL meets security standards.
        /// </summary>
        public static bool IsSecureOfficialUrl(string url, bool isManifest, out string failureReason)
        {
            failureReason = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                failureReason = "URL cannot be null or empty.";
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                failureReason = "Malformed URL format.";
                return false;
            }

            // 1. Enforce HTTPS only
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                failureReason = string.Format("Insecure protocol '{0}'. HTTPS is strictly required.", uri.Scheme);
                return false;
            }

            // 2. Enforce approved GitHub hosts
            if (!AllowedHosts.Contains(uri.Host))
            {
                failureReason = string.Format("Untrusted host '{0}'. Only official GitHub endpoints are permitted.", uri.Host);
                return false;
            }

            // 3. Verify official repo path provenance
            string path = uri.AbsolutePath.TrimStart('/');
            string expectedPrefix = string.Format("{0}/{1}/", ExpectedOwner, ExpectedRepo);

            // GitHub CDN objects.githubusercontent.com uses asset tokens, host is verified above
            if (uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                if (isManifest)
                {
                    failureReason = "Manifests cannot be served from objects.githubusercontent.com CDN.";
                    return false;
                }
                return true;
            }

            if (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                string expectedApiPrefix = string.Format("repos/{0}/{1}/", ExpectedOwner, ExpectedRepo);
                if (!path.StartsWith(expectedApiPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = string.Format("API path does not match official repository '{0}'.", expectedApiPrefix);
                    return false;
                }
                return true;
            }

            // For github.com and raw.githubusercontent.com
            if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = string.Format("URL path does not match official repository '{0}'.", expectedPrefix);
                return false;
            }

            return true;
        }
    }
}
