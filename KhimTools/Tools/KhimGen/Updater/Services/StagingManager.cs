using System;
using System.IO;
using System.IO.Compression;

namespace KhimTools.Tools.Updater.Services
{
    /// <summary>
    /// Manages the isolated local staging environment for K-TOOLS updates.
    /// Location: %LocalAppData%\KTools\Updates\{version}\
    /// Ensures downloads never touch the active bundle until fully verified.
    /// </summary>
    public class StagingManager
    {
        public string Version { get; }
        public string StagingRoot { get; }
        public string PackageZipPath { get; }
        public string ExtractedBundlePath { get; }
        public string ManifestPath { get; }

        public StagingManager(string targetVersion)
        {
            if (string.IsNullOrWhiteSpace(targetVersion))
                targetVersion = "latest";

            Version = targetVersion.Trim().TrimStart('v', 'V');
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            StagingRoot = Path.Combine(localAppData, "KTools", "Updates", Version);
            PackageZipPath = Path.Combine(StagingRoot, "KhimTools_Bundle.zip");
            ExtractedBundlePath = Path.Combine(StagingRoot, "Bundle_Extracted");
            ManifestPath = Path.Combine(StagingRoot, "update_manifest.json");
        }

        /// <summary>
        /// Cleans previous partial downloads and ensures staging directory is ready.
        /// </summary>
        public void PrepareStagingDirectory()
        {
            if (Directory.Exists(StagingRoot))
            {
                try
                {
                    Directory.Delete(StagingRoot, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[StagingManager] Warning deleting existing staging dir: {ex.Message}");
                }
            }
            Directory.CreateDirectory(StagingRoot);
        }

        /// <summary>
        /// Extracts the downloaded ZIP into the isolated ExtractedBundlePath folder.
        /// </summary>
        public bool ExtractToStaging()
        {
            if (!File.Exists(PackageZipPath)) return false;

            try
            {
                if (Directory.Exists(ExtractedBundlePath))
                {
                    Directory.Delete(ExtractedBundlePath, true);
                }
                Directory.CreateDirectory(ExtractedBundlePath);

                ZipFile.ExtractToDirectory(PackageZipPath, ExtractedBundlePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[StagingManager] Extraction to staging failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if the package ZIP exists and has non-zero size.
        /// </summary>
        public bool IsPackageDownloaded()
        {
            return File.Exists(PackageZipPath) && new FileInfo(PackageZipPath).Length > 1024;
        }

        /// <summary>
        /// Cleans up the staging directory upon completion or cancellation.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(StagingRoot))
                {
                    Directory.Delete(StagingRoot, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[StagingManager] Error during staging cleanup: {ex.Message}");
            }
        }
    }
}
