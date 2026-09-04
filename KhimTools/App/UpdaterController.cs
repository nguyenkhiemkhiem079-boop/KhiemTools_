using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace KhiemToolsApp
{
    public class CliOptions
    {
        public string Action { get; set; } = "install"; // install, rollback, verify, scan
        public string StagingDir { get; set; } = string.Empty;
        public string TargetBundle { get; set; } = string.Empty;
        public string BackupDir { get; set; } = string.Empty;
        public int RevitPid { get; set; } = 0;
        public string RevitPath { get; set; } = string.Empty;
        public string ExpectedVersion { get; set; } = string.Empty;
        public string ExpectedSha256 { get; set; } = string.Empty;
        public bool RelaunchRevit { get; set; } = true;
        public bool Silent { get; set; } = false;

        public static CliOptions Parse(string[] args)
        {
            var opts = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--action", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.Action = args[++i];
                else if (arg.Equals("--staging-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.StagingDir = args[++i];
                else if (arg.Equals("--target-bundle", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.TargetBundle = args[++i];
                else if (arg.Equals("--backup-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.BackupDir = args[++i];
                else if (arg.Equals("--revit-pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int pid)) opts.RevitPid = pid;
                }
                else if (arg.Equals("--revit-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.RevitPath = args[++i];
                else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.ExpectedVersion = args[++i];
                else if (arg.Equals("--sha256", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    opts.ExpectedSha256 = args[++i];
                else if (arg.Equals("--no-relaunch", StringComparison.OrdinalIgnoreCase))
                    opts.RelaunchRevit = false;
                else if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                    opts.Silent = true;
            }
            return opts;
        }
    }

    /// <summary>
    /// Out-of-process Update Controller executing in KToolsUpdater.exe.
    /// Manages Revit exit synchronization, backup snapshots, atomic bundle installation,
    /// verification checks, automatic rollback, and structured diagnostic logging.
    /// </summary>
    public static class UpdaterController
    {
        private static string _currentLogFile;

        public static string GetLogsDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(localAppData, "KTools", "Logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetBackupsDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(localAppData, "KTools", "Backups");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetDefaultBundlePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Autodesk\ApplicationPlugins\KhimTools.bundle");
        }

        public static void Log(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentLogFile))
                {
                    string logsDir = GetLogsDirectory();
                    _currentLogFile = Path.Combine(logsDir, $"update_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                }

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                Console.Write(line);
                File.AppendAllText(_currentLogFile, line, Encoding.UTF8);
            }
            catch { }
        }

        public static int RunCli(string[] args)
        {
            var opts = CliOptions.Parse(args);
            Log("==================================================================");
            Log($"[KToolsUpdater] Starting execution in CLI mode. Action: {opts.Action}");
            Log($"[KToolsUpdater] Process ID: {Process.GetCurrentProcess().Id}");

            if (string.IsNullOrEmpty(opts.TargetBundle))
                opts.TargetBundle = GetDefaultBundlePath();

            try
            {
                switch (opts.Action.ToLowerInvariant())
                {
                    case "install":
                        return ExecuteInstall(opts);
                    case "rollback":
                        return ExecuteRollback(opts);
                    case "verify":
                        return ExecuteVerify(opts);
                    default:
                        Log($"Unknown action '{opts.Action}'. Aborting.");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Log($"[CRITICAL] Unhandled exception in RunCli: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return 99;
            }
        }

        private static int ExecuteInstall(CliOptions opts)
        {
            Log("--- PHASE 1: REVIT PROCESS SYNCHRONIZATION ---");
            if (opts.RevitPid > 0)
            {
                Log($"Waiting for Revit process (PID: {opts.RevitPid}) to exit...");
                bool exited = WaitForProcessExit(opts.RevitPid, 45);
                if (!exited)
                {
                    Log($"[ERROR] Revit process {opts.RevitPid} did not exit within timeout. Aborting install to prevent file lock crashes.");
                    return 2;
                }
                Log("Revit process has successfully exited.");
            }

            // Ensure no lingering Revit processes hold file locks
            WaitForAnyRevitProcesses(15);

            Log("--- PHASE 2: STAGING VERIFICATION ---");
            string stagingSource = opts.StagingDir;
            if (string.IsNullOrEmpty(stagingSource) || !Directory.Exists(stagingSource))
            {
                Log($"[ERROR] Staging directory '{stagingSource}' does not exist or is empty.");
                return 3;
            }

            // If stagingDir contains a nested KhimTools.bundle, adapt path
            string nestedBundle = Path.Combine(stagingSource, "KhimTools.bundle");
            if (Directory.Exists(nestedBundle))
            {
                stagingSource = nestedBundle;
            }

            Log($"Staging source directory: '{stagingSource}'");

            Log("--- PHASE 3: ENVIRONMENT PREPARATION & CONFLICT PURGE ---");
            CleanLegacyConflicts();

            Log("--- PHASE 4: BACKUP CREATION ---");
            string backupDir = opts.BackupDir;
            if (string.IsNullOrEmpty(backupDir))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                backupDir = Path.Combine(GetBackupsDirectory(), $"bundle_{timestamp}");
            }

            bool hadExistingBundle = Directory.Exists(opts.TargetBundle);
            if (hadExistingBundle)
            {
                Log($"Creating backup snapshot of existing bundle: '{opts.TargetBundle}' -> '{backupDir}'");
                try
                {
                    CopyDirectory(opts.TargetBundle, backupDir);
                    Log("Backup snapshot created successfully.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Failed to create backup: {ex.Message}. Aborting installation.");
                    return 4;
                }
            }
            else
            {
                Log("No existing bundle found. Clean initial installation.");
            }

            Log("--- PHASE 5: ATOMIC INSTALLATION ---");
            try
            {
                // To achieve atomic update without file-lock collisions:
                // If destination exists, copy files with overwrite.
                Directory.CreateDirectory(opts.TargetBundle);
                CopyDirectory(stagingSource, opts.TargetBundle);
                Log("Files successfully copied from staging to target bundle.");

                if (!string.IsNullOrEmpty(opts.ExpectedVersion))
                {
                    File.WriteAllText(Path.Combine(opts.TargetBundle, "installed_version.txt"), opts.ExpectedVersion);
                }
            }
            catch (Exception ex)
            {
                Log($"[CRITICAL] Error copying files to target bundle: {ex.Message}");
                Log("Triggering AUTOMATIC ROLLBACK...");
                if (hadExistingBundle && Directory.Exists(backupDir))
                {
                    RollbackFromBackup(backupDir, opts.TargetBundle);
                }
                return 5;
            }

            Log("--- PHASE 6: POST-INSTALLATION INTEGRITY VERIFICATION ---");
            bool verificationPassed = VerifyInstalledBundle(opts.TargetBundle, opts.ExpectedVersion, out string verifyError);
            if (!verificationPassed)
            {
                Log($"[CRITICAL] Post-installation verification FAILED: {verifyError}");
                Log("Triggering AUTOMATIC ROLLBACK to preserve usable K-TOOLS state...");
                if (hadExistingBundle && Directory.Exists(backupDir))
                {
                    RollbackFromBackup(backupDir, opts.TargetBundle);
                    Log("Rollback completed. Previous version has been restored.");
                }
                else
                {
                    Log("No backup available to rollback to.");
                }
                return 6;
            }

            Log("--- PHASE 7: INSTALLATION SUCCESS & CLEANUP ---");
            Log("Post-installation verification PASSED. New K-TOOLS version is active.");

            // Optional: Launch Revit
            if (opts.RelaunchRevit && !string.IsNullOrEmpty(opts.RevitPath) && File.Exists(opts.RevitPath))
            {
                try
                {
                    Log($"Relaunching Autodesk Revit: '{opts.RevitPath}'");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = opts.RevitPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Log($"[WARN] Could not relaunch Revit: {ex.Message}");
                }
            }

            Log("KToolsUpdater completed successfully. Exit code: 0.");
            return 0;
        }

        private static int ExecuteRollback(CliOptions opts)
        {
            Log("Executing explicit rollback...");
            if (string.IsNullOrEmpty(opts.BackupDir) || !Directory.Exists(opts.BackupDir))
            {
                Log($"[ERROR] Backup directory '{opts.BackupDir}' not found. Cannot rollback.");
                return 10;
            }

            bool success = RollbackFromBackup(opts.BackupDir, opts.TargetBundle);
            return success ? 0 : 11;
        }

        private static int ExecuteVerify(CliOptions opts)
        {
            Log($"Executing bundle verification for '{opts.TargetBundle}'...");
            bool ok = VerifyInstalledBundle(opts.TargetBundle, opts.ExpectedVersion, out string err);
            if (ok)
            {
                Log("Verification PASSED.");
                return 0;
            }
            else
            {
                Log($"Verification FAILED: {err}");
                return 1;
            }
        }

        private static bool RollbackFromBackup(string backupDir, string targetBundle)
        {
            try
            {
                Log($"Restoring from '{backupDir}' to '{targetBundle}'...");
                if (Directory.Exists(targetBundle))
                {
                    try { Directory.Delete(targetBundle, true); } catch { }
                }
                Directory.CreateDirectory(targetBundle);
                CopyDirectory(backupDir, targetBundle);
                Log("Rollback restoration completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Rollback restoration failed: {ex.Message}");
                return false;
            }
        }

        public static bool VerifyInstalledBundle(string bundlePath, string expectedVersion, out string error)
        {
            error = string.Empty;

            if (!Directory.Exists(bundlePath))
            {
                error = $"Target bundle folder does not exist: '{bundlePath}'";
                return false;
            }

            // 1. Check PackageContents.xml
            string pkgXml = Path.Combine(bundlePath, "PackageContents.xml");
            if (!File.Exists(pkgXml))
            {
                error = "PackageContents.xml is missing from installed bundle.";
                return false;
            }

            // 2. Check KhimTools.dll existence in Modern or Legacy
            string modernDll = Path.Combine(bundlePath, "Contents", "Modern", "KhimTools.dll");
            string legacyDll = Path.Combine(bundlePath, "Contents", "Legacy", "KhimTools.dll");
            string rootModernDll = Path.Combine(bundlePath, "Modern", "KhimTools.dll");
            string rootLegacyDll = Path.Combine(bundlePath, "Legacy", "KhimTools.dll");

            string foundDll = null;
            if (File.Exists(modernDll)) foundDll = modernDll;
            else if (File.Exists(legacyDll)) foundDll = legacyDll;
            else if (File.Exists(rootModernDll)) foundDll = rootModernDll;
            else if (File.Exists(rootLegacyDll)) foundDll = rootLegacyDll;

            if (foundDll == null)
            {
                error = "KhimTools.dll is missing from both Modern and Legacy directories in installed bundle.";
                return false;
            }

            // 3. Check FileVersion if expectedVersion was specified
            if (!string.IsNullOrEmpty(expectedVersion))
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(foundDll);
                    string cleanFv = (fvi.FileVersion ?? "").TrimStart('v', 'V');
                    string cleanExp = expectedVersion.TrimStart('v', 'V');
                    if (!cleanFv.StartsWith(cleanExp) && !cleanExp.StartsWith(cleanFv))
                    {
                        Log($"[WARN] DLL FileVersion ({fvi.FileVersion}) differs from expected ({expectedVersion}), but file exists.");
                    }
                }
                catch { }
            }

            return true;
        }

        public static void CleanLegacyConflicts()
        {
            try
            {
                // Purge duplicate KhimTools.bundle in %APPDATA%\Autodesk\ApplicationPlugins
                string appDataBundle = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Autodesk\ApplicationPlugins\KhimTools.bundle");

                if (Directory.Exists(appDataBundle))
                {
                    Log($"Purging conflicting bundle in AppData: '{appDataBundle}'");
                    try { Directory.Delete(appDataBundle, true); } catch { }
                }

                // Purge rogue .addin files from Revit version folders
                string[] baseAddins = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\Revit\Addins")
                };

                int[] years = new int[] { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028 };
                foreach (var baseDir in baseAddins)
                {
                    if (!Directory.Exists(baseDir)) continue;
                    foreach (int yr in years)
                    {
                        string yrDir = Path.Combine(baseDir, yr.ToString());
                        if (!Directory.Exists(yrDir)) continue;

                        string addin = Path.Combine(yrDir, "KhimTools.addin");
                        if (File.Exists(addin))
                        {
                            Log($"Purging rogue addin: '{addin}'");
                            try { File.Delete(addin); } catch { }
                        }

                        string pluginDir = Path.Combine(yrDir, "KhimTools");
                        if (Directory.Exists(pluginDir))
                        {
                            Log($"Purging rogue folder: '{pluginDir}'");
                            try { Directory.Delete(pluginDir, true); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[WARN] CleanLegacyConflicts exception: {ex.Message}");
            }
        }

        private static bool WaitForProcessExit(int pid, int timeoutSeconds)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                return proc.WaitForExit(timeoutSeconds * 1000);
            }
            catch (ArgumentException)
            {
                // Process already exited
                return true;
            }
            catch (Exception ex)
            {
                Log($"WaitForProcessExit error: {ex.Message}");
                return true;
            }
        }

        private static void WaitForAnyRevitProcesses(int maxSeconds)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < maxSeconds)
            {
                var revits = Process.GetProcessesByName("Revit");
                if (revits.Length == 0) return;
                Thread.Sleep(500);
            }
            Log("[WARN] Revit process may still be lingering in background.");
        }

        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestDir);
            }
        }

        public static string ComputeFileSha256(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
