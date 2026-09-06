using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KhimTools.FamilyManager.Models;

namespace KhimTools.FamilyManager.Services
{
    /// <summary>
    /// Discovers and resolves family source directories dynamically.
    /// Eliminates hardcoded developer paths and seamlessly supports deployed vs dev environments.
    /// </summary>
    public class FamilySourceResolver
    {
        public static List<FamilyLibrarySource> ResolveAllSources(FamilyManagerSettings settings = null)
        {
            var sources = new List<FamilyLibrarySource>();

            // 1. User-configured sources from settings
            if (settings?.Sources != null)
            {
                foreach (var s in settings.Sources)
                {
                    if (s.IsEnabled && s.RootPaths != null)
                    {
                        sources.Add(s);
                    }
                }
            }

            // 2. Discover default installation / build / repo directories
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyPath);

            var candidateRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(assemblyDir) && Directory.Exists(assemblyDir))
            {
                candidateRoots.Add(assemblyDir);

                // Check parent directories (up to 3 levels, e.g. Modern -> Contents -> KhimTools.bundle)
                var current = new DirectoryInfo(assemblyDir);
                for (int i = 0; i < 3 && current != null; i++)
                {
                    candidateRoots.Add(current.FullName);
                    current = current.Parent;
                }
            }

            // Production bundle paths in ProgramData and AppData
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(commonAppData))
            {
                string bundleContents = Path.Combine(commonAppData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents");
                if (Directory.Exists(bundleContents)) candidateRoots.Add(bundleContents);
                string bundleRoot = Path.Combine(commonAppData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle");
                if (Directory.Exists(bundleRoot)) candidateRoots.Add(bundleRoot);
            }

            string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(userAppData))
            {
                string userBundleContents = Path.Combine(userAppData, "Autodesk", "ApplicationPlugins", "KhimTools.bundle", "Contents");
                if (Directory.Exists(userBundleContents)) candidateRoots.Add(userBundleContents);
            }

            // Also check AppData KhimTools location
            var appDataDir = Path.Combine(userAppData, "KhimTools", "Families");
            if (Directory.Exists(appDataDir))
            {
                candidateRoots.Add(appDataDir);
            }

            // Resolve Structure directories
            var structurePaths = FindSubdirectories(candidateRoots, new[] { "Family/STRUCTURE", "Families/STRUCTURE", "Structure", "KhimTools/Family/STRUCTURE" });
            if (structurePaths.Count > 0)
            {
                sources.Add(new FamilyLibrarySource
                {
                    Id = "default_structure",
                    DisplayName = "Structure Library (Built-in)",
                    LogicalGroup = FamilyGroupType.Structure,
                    RootPaths = structurePaths,
                    Priority = 100,
                    IsEnabled = true
                });
            }

            // Resolve Rebar Shape directories
            var rebarPaths = FindSubdirectories(candidateRoots, new[] {
                "Tools/KhimStructural/RebarTool/RebarShapes",
                "KhimTools/Tools/KhimStructural/RebarTool/RebarShapes",
                "Family/REBAR",
                "Families/REBAR",
                "RebarShapes",
                "Docs/2.1_Rebar Shape"
            });
            if (rebarPaths.Count > 0)
            {
                sources.Add(new FamilyLibrarySource
                {
                    Id = "default_rebar",
                    DisplayName = "Rebar Library (Built-in)",
                    LogicalGroup = FamilyGroupType.Rebar,
                    RootPaths = rebarPaths,
                    Priority = 100,
                    IsEnabled = true
                });
            }

            // Resolve Annotation directories
            var annoPaths = FindSubdirectories(candidateRoots, new[] { "Family/ANNO", "Families/ANNO", "Annotation", "KhimTools/Family/ANNO" });
            if (annoPaths.Count > 0)
            {
                sources.Add(new FamilyLibrarySource
                {
                    Id = "default_anno",
                    DisplayName = "Annotation Library (Built-in)",
                    LogicalGroup = FamilyGroupType.Annotation,
                    RootPaths = annoPaths,
                    Priority = 100,
                    IsEnabled = true
                });
            }

            // Sort by priority descending
            sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return sources;
        }

        private static List<string> FindSubdirectories(IEnumerable<string> baseDirs, string[] candidateRelativePaths)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var baseDir in baseDirs)
            {
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) continue;

                foreach (var relPath in candidateRelativePaths)
                {
                    var normalized = relPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(baseDir, normalized);
                    if (Directory.Exists(fullPath) && seen.Add(fullPath))
                    {
                        results.Add(fullPath);
                    }
                }
            }

            return results;
        }
    }
}
