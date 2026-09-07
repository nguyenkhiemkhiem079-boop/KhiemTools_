using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Classifies installed Revit add-in bundles and manifest files.
    /// Prevents destructive deletion of user-managed or unknown configurations,
    /// and ensures verified legacy artifacts are preserved before removal.
    /// </summary>
    public static class InstallationClassifier
    {
        private static readonly int[] SupportedRevitYears = new int[]
        {
            2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028
        };

        /// <summary>
        /// Test simulation hook to override MSI detection in unit tests.
        /// </summary>
        public static Func<bool> MsiDetectionOverride { get; set; }

        /// <summary>
        /// Checks if K-TOOLS is currently deployed and managed via Windows Installer (MSI).
        /// Checks HKLM\SOFTWARE\K-TOOLS for InstalledVia == "MSI".
        /// </summary>
        public static bool IsMsiManaged()
        {
            if (MsiDetectionOverride != null)
            {
                return MsiDetectionOverride();
            }

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\K-TOOLS"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("InstalledVia") as string;
                        if (string.Equals(val, "MSI", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Classifies a bundle directory as Current, MsiManaged, Legacy, UserManaged, or Unknown.
        /// </summary>
        public static InstallationClassification ClassifyBundle(string bundlePath, string authoritativeBundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath))
            {
                return InstallationClassification.Unknown;
            }

            // 1. Authoritative installation match
            if (!string.IsNullOrEmpty(authoritativeBundlePath) &&
                string.Equals(Path.GetFullPath(bundlePath).TrimEnd('\\'),
                              Path.GetFullPath(authoritativeBundlePath).TrimEnd('\\'),
                              StringComparison.OrdinalIgnoreCase))
            {
                if (IsMsiManaged())
                {
                    return InstallationClassification.MsiManaged;
                }
                return InstallationClassification.Current;
            }

            // 2. Check if this is a user-managed bundle in AppData or elsewhere
            // Indications of UserManaged: presence of .git folder, .sln, source files, or non-matching publisher
            if (Directory.Exists(Path.Combine(bundlePath, ".git")) ||
                Directory.GetFiles(bundlePath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
                Directory.GetFiles(bundlePath, "*.csproj", SearchOption.AllDirectories).Length > 0)
            {
                return InstallationClassification.UserManaged;
            }

            string packageXml = Path.Combine(bundlePath, "PackageContents.xml");
            if (File.Exists(packageXml))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(packageXml);

                    // Check if publisher or package identity matches official K-TOOLS
                    string xmlText = File.ReadAllText(packageXml);
                    bool hasKhimToolsId = xmlText.IndexOf("KhimTools", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!hasKhimToolsId)
                    {
                        return InstallationClassification.Unknown;
                    }

                    // Check for custom user manifests or developer modifications
                    if (xmlText.IndexOf("Development", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        xmlText.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return InstallationClassification.UserManaged;
                    }

                    // Standard automated legacy bundle leftover in AppData
                    return InstallationClassification.Legacy;
                }
                catch
                {
                    return InstallationClassification.Unknown;
                }
            }

            return InstallationClassification.Unknown;
        }

        /// <summary>
        /// Classifies an individual .addin manifest file.
        /// Returns Legacy only if file verified to belong to K-TOOLS / KhimTools.
        /// </summary>
        public static InstallationClassification ClassifyAddinFile(string addinPath)
        {
            if (string.IsNullOrWhiteSpace(addinPath) || !File.Exists(addinPath))
            {
                return InstallationClassification.Unknown;
            }

            try
            {
                string content = File.ReadAllText(addinPath);
                bool hasKhimToolsAssembly = content.IndexOf("KhimTools.dll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            content.IndexOf("KhimTools", StringComparison.OrdinalIgnoreCase) >= 0;

                if (hasKhimToolsAssembly)
                {
                    // Check if pointing to a user workspace or debug build
                    if (content.IndexOf(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        content.IndexOf(@"\source\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return InstallationClassification.UserManaged;
                    }

                    return InstallationClassification.Legacy;
                }
            }
            catch
            {
                return InstallationClassification.Unknown;
            }

            return InstallationClassification.Unknown;
        }

        /// <summary>
        /// Identifies verified legacy K-TOOLS addin files and directories across all Revit years.
        /// Excludes Current and UserManaged installations.
        /// </summary>
        public static List<string> DiscoverLegacyArtifacts()
        {
            var legacyPaths = new List<string>();

            string[] baseAddinFolders = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Autodesk\Revit\Addins")
            };

            foreach (var baseDir in baseAddinFolders)
            {
                if (!Directory.Exists(baseDir)) continue;

                foreach (int year in SupportedRevitYears)
                {
                    string yearFolder = Path.Combine(baseDir, year.ToString());
                    if (!Directory.Exists(yearFolder)) continue;

                    string addinFile = Path.Combine(yearFolder, "KhimTools.addin");
                    if (File.Exists(addinFile) && ClassifyAddinFile(addinFile) == InstallationClassification.Legacy)
                    {
                        legacyPaths.Add(addinFile);
                    }

                    string pluginDir = Path.Combine(yearFolder, "KhimTools");
                    if (Directory.Exists(pluginDir))
                    {
                        // Check if contains KhimTools.dll
                        if (File.Exists(Path.Combine(pluginDir, "KhimTools.dll")))
                        {
                            legacyPaths.Add(pluginDir);
                        }
                    }
                }
            }

            return legacyPaths;
        }

        /// <summary>
        /// Preserves all classified legacy artifacts into backup directory before safely removing them.
        /// UserManaged and Unknown artifacts are untouched.
        /// </summary>
        public static void PreserveAndCleanLegacyArtifacts(string backupRoot, Action<string> logger)
        {
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                throw new ArgumentNullException("backupRoot");
            }

            string legacyBackupDir = Path.Combine(backupRoot, "Legacy_Artifacts");
            Directory.CreateDirectory(legacyBackupDir);

            var legacyItems = DiscoverLegacyArtifacts();
            foreach (var itemPath in legacyItems)
            {
                try
                {
                    if (File.Exists(itemPath))
                    {
                        string fileName = Path.GetFileName(itemPath);
                        string parentFolder = Path.GetFileName(Path.GetDirectoryName(itemPath));
                        string backupTarget = Path.Combine(legacyBackupDir, string.Format("{0}_{1}", parentFolder, fileName));
                        File.Copy(itemPath, backupTarget, true);
                        File.Delete(itemPath);
                        if (logger != null) logger(string.Format("Preserved and removed legacy addin: {0}", itemPath));
                    }
                    else if (Directory.Exists(itemPath))
                    {
                        string dirName = Path.GetFileName(itemPath);
                        string parentFolder = Path.GetFileName(Path.GetDirectoryName(itemPath));
                        string backupTarget = Path.Combine(legacyBackupDir, string.Format("{0}_{1}", parentFolder, dirName));
                        CopyDirectoryRecursive(itemPath, backupTarget);
                        Directory.Delete(itemPath, true);
                        if (logger != null) logger(string.Format("Preserved and removed legacy plugin directory: {0}", itemPath));
                    }
                }
                catch (Exception ex)
                {
                    if (logger != null) logger(string.Format("Warning: Could not preserve/remove legacy artifact '{0}': {1}", itemPath, ex.Message));
                }
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
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
