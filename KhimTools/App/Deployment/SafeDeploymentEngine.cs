using System;
using System.IO;
using System.IO.Compression;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Safe deployment coordinator implementing a two-phase staged deployment strategy with rollback.
    /// Guarantees that the live Revit bundle is never left in a partially updated or corrupted state.
    /// Implements canonical containment validation against Zip Slip path traversal.
    /// Implements explicit backup retention management to prevent uncontrolled accumulation.
    /// </summary>
    public class SafeDeploymentEngine
    {
        public const int DefaultMaxRetainedBackups = 2;

        // Test simulation hooks to verify edge-case failure handling
        public bool SimulateInterruptedCopy { get; set; }
        public bool SimulateBackupFailure { get; set; }
        public bool SimulateSwapFailure { get; set; }
        public bool SimulatePostInstallVerificationFailure { get; set; }
        public bool SimulateRollbackFailure { get; set; }
        public int MaxRetainedBackups { get; set; }

        public SafeDeploymentEngine()
        {
            SimulateInterruptedCopy = false;
            SimulateBackupFailure = false;
            SimulateSwapFailure = false;
            SimulatePostInstallVerificationFailure = false;
            SimulateRollbackFailure = false;
            MaxRetainedBackups = DefaultMaxRetainedBackups;
        }

        /// <summary>
        /// Deploys an update zip payload into the authoritative Revit bundle location using a staged, reversible pipeline.
        /// </summary>
        /// <param name="zipFilePath">Path to downloaded update zip file.</param>
        /// <param name="targetBundlePath">Authoritative installation directory (e.g. %ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle).</param>
        /// <param name="expectedVersion">Expected release version (e.g. "v2.8.0").</param>
        /// <param name="logger">Optional diagnostic logger action.</param>
        public void DeployZip(string zipFilePath, string targetBundlePath, string expectedVersion, Action<string> logger)
        {
            if (string.IsNullOrWhiteSpace(zipFilePath) || !File.Exists(zipFilePath))
            {
                throw new FileNotFoundException(string.Format("Update zip package not found at '{0}'.", zipFilePath));
            }

            if (string.IsNullOrWhiteSpace(targetBundlePath))
            {
                throw new ArgumentNullException("targetBundlePath");
            }

            if (logger != null) logger(string.Format("Starting safe staged deployment for version '{0}' -> '{1}'", expectedVersion, targetBundlePath));

            // 1. Check for locked target files (e.g. Revit running)
            AssertTargetNotLocked(targetBundlePath);

            // 2. Stage: Extract to an isolated temporary staging directory with canonical path traversal validation
            string stagingDir = Path.Combine(Path.GetTempPath(), "KhimTools_Staging_" + Guid.NewGuid().ToString("N"));
            string backupDir = null;
            string backupRoot = Path.Combine(Path.GetTempPath(), "KhimTools_Backups");
            string tempRetiredPath = null;

            try
            {
                if (logger != null) logger(string.Format("Extracting payload to isolated staging: {0}", stagingDir));
                ExtractZipSafely(zipFilePath, stagingDir);

                if (SimulateInterruptedCopy)
                {
                    throw new IOException("SIMULATED ERROR: Interrupted copy during staging extraction.");
                }

                // 3. Pre-Validate staging completely BEFORE touching the live installation
                if (logger != null) logger("Pre-validating staging directory structure and binaries...");
                DeploymentValidator.ValidateBundle(stagingDir, expectedVersion);
                if (logger != null) logger("Staging pre-validation passed successfully.");

                // 4. Preserve current installation (Backup)
                if (Directory.Exists(targetBundlePath))
                {
                    Directory.CreateDirectory(backupRoot);
                    backupDir = Path.Combine(backupRoot, "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N"));
                    if (logger != null) logger(string.Format("Creating full backup of current installation: {0}", backupDir));

                    try
                    {
                        if (SimulateBackupFailure)
                        {
                            throw new UnauthorizedAccessException("SIMULATED ERROR: Access denied creating backup.");
                        }

                        CopyDirectoryRecursive(targetBundlePath, backupDir);
                    }
                    catch (Exception ex)
                    {
                        throw new BackupFailedException(
                            string.Format("Failed to preserve current installation to backup directory '{0}'. Deployment aborted without modifying live installation.", backupDir), ex);
                    }

                    if (logger != null) logger("Current installation successfully preserved.");
                }

                // 5. Controlled Switch
                if (logger != null) logger("Initiating controlled installation switch...");
                if (Directory.Exists(targetBundlePath))
                {
                    tempRetiredPath = targetBundlePath + "_retiring_" + Guid.NewGuid().ToString("N");
                    try
                    {
                        Directory.Move(targetBundlePath, tempRetiredPath);
                    }
                    catch (Exception ex)
                    {
                        throw new DeploymentLockException(
                            string.Format("Failed to retire current installation folder '{0}'. A process (such as Revit) may have files open.", targetBundlePath), ex, targetBundlePath);
                    }
                }

                if (SimulateSwapFailure)
                {
                    // Rollback phase 1
                    if (tempRetiredPath != null && Directory.Exists(tempRetiredPath))
                    {
                        Directory.Move(tempRetiredPath, targetBundlePath);
                    }
                    throw new IOException("SIMULATED ERROR: Filesystem error during staging activation.");
                }

                try
                {
                    // Move staging into active target location
                    Directory.Move(stagingDir, targetBundlePath);
                }
                catch (Exception ex)
                {
                    if (logger != null) logger(string.Format("Staging activation failed: {0}. Restoring previous installation...", ex.Message));
                    // Restore retired folder if available
                    if (tempRetiredPath != null && Directory.Exists(tempRetiredPath) && !Directory.Exists(targetBundlePath))
                    {
                        Directory.Move(tempRetiredPath, targetBundlePath);
                    }
                    throw new DeploymentException(string.Format("Failed to activate staged installation to '{0}'. Previous installation restored.", targetBundlePath), ex);
                }

                // 6. Post-Installation Verification
                if (logger != null) logger("Running post-install verification on live installation...");
                try
                {
                    if (SimulatePostInstallVerificationFailure)
                    {
                        throw new DeploymentValidationException("SIMULATED ERROR: Post-install validation forced failure.");
                    }

                    DeploymentValidator.ValidateBundle(targetBundlePath, expectedVersion);
                    if (logger != null) logger("Post-install verification passed.");
                }
                catch (Exception verifyEx)
                {
                    if (logger != null) logger(string.Format("POST-INSTALL VERIFICATION FAILED: {0}. Commencing emergency rollback...", verifyEx.Message));
                    ExecuteRollback(targetBundlePath, tempRetiredPath, backupDir, logger);
                    throw new DeploymentValidationException(
                        string.Format("Post-install verification failed: {0}. Live installation was safely rolled back to original state.", verifyEx.Message), verifyEx);
                }

                // 7. Write version tracking file
                try
                {
                    File.WriteAllText(Path.Combine(targetBundlePath, "installed_version.txt"), expectedVersion ?? "unknown");
                }
                catch (Exception ex)
                {
                    if (logger != null) logger(string.Format("Warning: Could not write installed_version.txt: {0}", ex.Message));
                }

                // 8. Clean Legacy Conflicts ONLY AFTER verified deployment and backup
                if (!string.IsNullOrEmpty(backupDir))
                {
                    if (logger != null) logger("Preserving and migrating known legacy add-in conflicts...");
                    InstallationClassifier.PreserveAndCleanLegacyArtifacts(backupDir, logger);
                }

                // 9. Clean up temporary retirement directory
                if (tempRetiredPath != null && Directory.Exists(tempRetiredPath))
                {
                    try { Directory.Delete(tempRetiredPath, true); } catch { }
                }

                // 10. Explicit Backup Retention Management: Prune older backups to prevent uncontrolled accumulation
                PruneOldBackups(backupRoot, MaxRetainedBackups, logger);

                if (logger != null) logger("Deployment successfully finalized.");
            }
            finally
            {
                // Clean up staging directory if still remaining
                if (Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, true); } catch { }
                }
            }
        }

        private void ExecuteRollback(string targetPath, string retiredPath, string backupPath, Action<string> logger)
        {
            if (SimulateRollbackFailure)
            {
                throw new RollbackFailedException(
                    "SIMULATED CRITICAL ERROR: Rollback failed due to simulated catastrophic I/O failure.",
                    backupPath, targetPath, new IOException("Access denied during rollback."));
            }

            try
            {
                // Delete invalid or corrupted target if it exists
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }

                // Restore from retired folder or backup
                if (retiredPath != null && Directory.Exists(retiredPath))
                {
                    Directory.Move(retiredPath, targetPath);
                    if (logger != null) logger("Rollback completed successfully from retired cache.");
                }
                else if (backupPath != null && Directory.Exists(backupPath))
                {
                    CopyDirectoryRecursive(backupPath, targetPath);
                    if (logger != null) logger("Rollback completed successfully from backup directory.");
                }
            }
            catch (Exception rollbackEx)
            {
                string msg = string.Format("FATAL: Rollback failed while restoring '{0}'. Original backup is preserved at '{1}'. Error: {2}", targetPath, backupPath, rollbackEx.Message);
                if (logger != null) logger(msg);
                throw new RollbackFailedException(msg, backupPath, targetPath, rollbackEx);
            }
        }

        public static void AssertTargetNotLocked(string targetBundlePath)
        {
            if (!Directory.Exists(targetBundlePath)) return;

            string[] dlls = Directory.GetFiles(targetBundlePath, "*.dll", SearchOption.AllDirectories);
            foreach (string dll in dlls)
            {
                try
                {
                    using (var fs = new FileStream(dll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // File is not locked
                    }
                }
                catch (IOException ex)
                {
                    throw new DeploymentLockException(
                        string.Format("File '{0}' is locked by another process (likely Autodesk Revit). Please close Revit completely before continuing.", Path.GetFileName(dll)),
                        ex, dll);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new DeploymentLockException(
                        string.Format("Access denied accessing '{0}'. Please run updater with appropriate permissions.", Path.GetFileName(dll)),
                        ex, dll);
                }
            }
        }

        /// <summary>
        /// Extracts a zip archive safely with canonical path containment verification.
        /// Rejects Zip Slip path traversal (../, ..\, rooted, absolute, and UNC paths).
        /// </summary>
        public static void ExtractZipSafely(string zipPath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            string canonicalDest = Path.GetFullPath(destinationDirectory);
            if (!canonicalDest.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                canonicalDest += Path.DirectorySeparatorChar;
            }

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string entryName = entry.FullName;

                    // 1. Block UNC paths
                    if (entryName.StartsWith("\\\\") || entryName.StartsWith("//"))
                    {
                        throw new DeploymentSecurityException("Zip entry contains prohibited UNC path: " + entryName);
                    }

                    // 2. Block rooted or absolute paths (e.g. C:\Windows or \foo)
                    if (Path.IsPathRooted(entryName) || entryName.StartsWith("/") || entryName.StartsWith("\\"))
                    {
                        throw new DeploymentSecurityException("Zip entry contains prohibited rooted/absolute path: " + entryName);
                    }

                    // 3. Normalize directory separators to OS standard
                    string normalizedRel = entryName.Replace('/', Path.DirectorySeparatorChar);

                    // 4. Resolve canonical absolute path
                    string combinedPath = Path.Combine(canonicalDest, normalizedRel);
                    string canonicalTarget = Path.GetFullPath(combinedPath);

                    // 5. Canonical containment validation: target MUST start with canonical destination directory
                    if (!canonicalTarget.StartsWith(canonicalDest, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DeploymentSecurityException("Zip path traversal attempt detected! Entry escapes destination: " + entryName);
                    }

                    // Handle directory entry
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(canonicalTarget);
                        continue;
                    }

                    string parentDir = Path.GetDirectoryName(canonicalTarget);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.ExtractToFile(canonicalTarget, true);
                }
            }
        }

        /// <summary>
        /// Prunes older backups in the backup root directory, keeping only the most recent N backups.
        /// Prevents uncontrolled disk accumulation.
        /// </summary>
        public static void PruneOldBackups(string backupRoot, int maxRetained, Action<string> logger)
        {
            if (string.IsNullOrWhiteSpace(backupRoot) || !Directory.Exists(backupRoot) || maxRetained <= 0)
            {
                return;
            }

            try
            {
                var dirInfo = new DirectoryInfo(backupRoot);
                DirectoryInfo[] backupDirs = dirInfo.GetDirectories("Backup_*");

                // Sort newest first
                Array.Sort(backupDirs, delegate(DirectoryInfo a, DirectoryInfo b)
                {
                    return b.CreationTimeUtc.CompareTo(a.CreationTimeUtc);
                });

                if (backupDirs.Length > maxRetained)
                {
                    for (int i = maxRetained; i < backupDirs.Length; i++)
                    {
                        try
                        {
                            if (logger != null) logger(string.Format("Pruning expired backup: {0}", backupDirs[i].FullName));
                            backupDirs[i].Delete(true);
                        }
                        catch (Exception ex)
                        {
                            if (logger != null) logger(string.Format("Warning: Could not prune backup '{0}': {1}", backupDirs[i].FullName, ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null) logger(string.Format("Warning: Backup pruning encountered error: {0}", ex.Message));
            }
        }

        public static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }
    }
}
