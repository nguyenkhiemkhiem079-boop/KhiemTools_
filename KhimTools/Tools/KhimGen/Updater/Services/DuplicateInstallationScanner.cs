using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KhimTools.Tools.Updater.Services
{
    public class DiscoveredInstallationItem
    {
        public string Path { get; set; }
        public string ItemType { get; set; } // "Bundle", "BackupBundle", "AddinFile", "LooseDll"
        public string FileVersion { get; set; } = "Unknown";
        public string AssemblyVersion { get; set; } = "Unknown";
        public string AppVersion { get; set; } = "Unknown";
        public DateTime LastWriteTime { get; set; }
        public bool IsActivePrimary { get; set; } = false;
        public string Warning { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{ItemType}] {Path} | FileVer: {FileVersion} | Time: {LastWriteTime:yyyy-MM-dd HH:mm:ss}" +
                   (!string.IsNullOrEmpty(Warning) ? $" [WARN: {Warning}]" : "");
        }
    }

    public class DuplicateScanReport
    {
        public List<DiscoveredInstallationItem> Items { get; } = new List<DiscoveredInstallationItem>();
        public List<string> Warnings { get; } = new List<string>();
        public bool HasDuplicateConflict => Warnings.Count > 0;
        public string PrimaryBundlePath { get; set; } = string.Empty;

        public string FormatReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== K-TOOLS INSTALLATION & DUPLICATE SCAN REPORT ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total items found: {Items.Count}");
            sb.AppendLine($"Primary bundle: {(string.IsNullOrEmpty(PrimaryBundlePath) ? "NOT DETECTED" : PrimaryBundlePath)}");

            foreach (var item in Items)
            {
                sb.AppendLine($" - {item}");
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine("--- DETECTED CONFLICTS / STALE INSTALLATIONS ---");
                foreach (var w in Warnings)
                {
                    sb.AppendLine($" [WARNING] {w}");
                }
            }
            else
            {
                sb.AppendLine("No duplicate or conflicting installations detected. Clean environment.");
            }

            sb.AppendLine("====================================================");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Scans Revit addin folders to detect duplicate bundles, stale DLLs, and rogue .addin files.
    /// Prevents Revit GUID collisions and silent loading of outdated code.
    /// </summary>
    public static class DuplicateInstallationScanner
    {
        public static DuplicateScanReport Scan()
        {
            var report = new DuplicateScanReport();

            string programDataPlugins = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Autodesk\ApplicationPlugins");

            string appDataPlugins = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Autodesk\ApplicationPlugins");

            string primaryBundle = Path.Combine(programDataPlugins, "KhimTools.bundle");
            report.PrimaryBundlePath = primaryBundle;

            // 1. Scan ProgramData\Autodesk\ApplicationPlugins
            ScanPluginsFolder(programDataPlugins, report, isProgramData: true);

            // 2. Scan AppData\Autodesk\ApplicationPlugins
            ScanPluginsFolder(appDataPlugins, report, isProgramData: false);

            // 3. Scan Revit versioned Addins folders (2020-2028)
            ScanRevitAddinsFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), report, "AppData");
            ScanRevitAddinsFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), report, "ProgramData");

            // 4. Evaluate duplicates and conflicts
            int bundleCount = 0;
            foreach (var item in report.Items)
            {
                if (item.ItemType == "Bundle" && !item.Path.EndsWith("_backup", StringComparison.OrdinalIgnoreCase))
                {
                    bundleCount++;
                }
            }

            if (bundleCount > 1)
            {
                report.Warnings.Add($"DUPLICATE BUNDLE COLLISION: Found {bundleCount} active KhimTools.bundle directories. Revit may load an arbitrary bundle or encounter GUID collision!");
            }

            return report;
        }

        private static void ScanPluginsFolder(string folder, DuplicateScanReport report, bool isProgramData)
        {
            if (!Directory.Exists(folder)) return;

            try
            {
                var dirs = Directory.GetDirectories(folder, "*KhimTools*", SearchOption.TopDirectoryOnly);
                foreach (var dir in dirs)
                {
                    string dirName = Path.GetFileName(dir);
                    bool isBackup = dirName.IndexOf("backup", StringComparison.OrdinalIgnoreCase) >= 0;

                    var item = new DiscoveredInstallationItem
                    {
                        Path = dir,
                        ItemType = isBackup ? "BackupBundle" : "Bundle",
                        LastWriteTime = Directory.GetLastWriteTime(dir),
                        IsActivePrimary = isProgramData && dirName.Equals("KhimTools.bundle", StringComparison.OrdinalIgnoreCase)
                    };

                    if (!isProgramData && !isBackup)
                    {
                        item.Warning = "Duplicate KhimTools.bundle in %APPDATA% conflicts with %PROGRAMDATA% installation.";
                        report.Warnings.Add($"Secondary bundle in AppData: '{dir}' can override ProgramData installation.");
                    }

                    // Inspect PackageContents.xml
                    string pkgXml = Path.Combine(dir, "PackageContents.xml");
                    if (File.Exists(pkgXml))
                    {
                        try
                        {
                            string content = File.ReadAllText(pkgXml);
                            var m = Regex.Match(content, @"AppVersion\s*=\s*""([^""]+)""");
                            if (m.Success) item.AppVersion = m.Groups[1].Value;
                        }
                        catch { }
                    }

                    // Inspect DLLs
                    InspectBundleDlls(dir, item);
                    report.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DuplicateInstallationScanner] Error scanning '{folder}': {ex.Message}");
            }
        }

        private static void InspectBundleDlls(string bundleDir, DiscoveredInstallationItem item)
        {
            string[] possiblePaths = new string[]
            {
                Path.Combine(bundleDir, "Contents", "Modern", "KhimTools.dll"),
                Path.Combine(bundleDir, "Contents", "Legacy", "KhimTools.dll"),
                Path.Combine(bundleDir, "Modern", "KhimTools.dll"),
                Path.Combine(bundleDir, "Legacy", "KhimTools.dll")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(path);
                        item.FileVersion = fvi.FileVersion ?? "Unknown";
                        item.LastWriteTime = File.GetLastWriteTime(path);
                        break;
                    }
                    catch { }
                }
            }
        }

        private static void ScanRevitAddinsFolder(string rootSpecialFolder, DuplicateScanReport report, string scopeName)
        {
            string baseAddins = Path.Combine(rootSpecialFolder, @"Autodesk\Revit\Addins");
            if (!Directory.Exists(baseAddins)) return;

            int[] years = new int[] { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028 };
            foreach (int year in years)
            {
                string yearFolder = Path.Combine(baseAddins, year.ToString());
                if (!Directory.Exists(yearFolder)) continue;

                string addinFile = Path.Combine(yearFolder, "KhimTools.addin");
                if (File.Exists(addinFile))
                {
                    var item = new DiscoveredInstallationItem
                    {
                        Path = addinFile,
                        ItemType = "AddinFile",
                        LastWriteTime = File.GetLastWriteTime(addinFile),
                        Warning = $"Legacy {addinFile} found in {scopeName} Revit {year}. May cause duplicate AddInId loading!"
                    };
                    report.Items.Add(item);
                    report.Warnings.Add($"Legacy addin file: '{addinFile}' should be purged to prevent collision with ApplicationPlugins bundle.");
                }

                string pluginFolder = Path.Combine(yearFolder, "KhimTools");
                if (Directory.Exists(pluginFolder))
                {
                    var item = new DiscoveredInstallationItem
                    {
                        Path = pluginFolder,
                        ItemType = "LooseDllFolder",
                        LastWriteTime = Directory.GetLastWriteTime(pluginFolder),
                        Warning = $"Stale KhimTools plugin directory in {scopeName} Revit {year}."
                    };
                    report.Items.Add(item);
                    report.Warnings.Add($"Stale plugin directory: '{pluginFolder}' found.");
                }
            }
        }
    }
}
