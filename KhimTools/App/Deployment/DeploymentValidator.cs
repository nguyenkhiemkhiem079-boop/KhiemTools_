using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Validates staging and installed bundles against structural, binary, and version requirements.
    /// Rejects incomplete packages, corrupted DLLs, and version mismatches.
    /// Supports SemVer 2.0 semantic versioning.
    /// </summary>
    public static class DeploymentValidator
    {
        /// <summary>
        /// Validates a bundle directory (staging or installed live bundle).
        /// Throws DeploymentValidationException if validation fails.
        /// </summary>
        /// <param name="bundleDirectory">Root path of the KhimTools.bundle directory.</param>
        /// <param name="expectedVersion">The expected release version string (e.g. "v2.8.0", "2.7.1-beta", "2.8.0").</param>
        public static void ValidateBundle(string bundleDirectory, string expectedVersion)
        {
            if (string.IsNullOrWhiteSpace(bundleDirectory) || !Directory.Exists(bundleDirectory))
            {
                throw new DeploymentValidationException(
                    string.Format("Bundle directory does not exist: {0}", bundleDirectory));
            }

            // 1. Verify PackageContents.xml exists and is valid XML
            string packageXmlPath = Path.Combine(bundleDirectory, "PackageContents.xml");
            if (!File.Exists(packageXmlPath))
            {
                throw new DeploymentValidationException(
                    string.Format("PackageContents.xml is missing from bundle: {0}", packageXmlPath));
            }

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(packageXmlPath);
                var root = xmlDoc.DocumentElement;
                if (root == null || !root.Name.Equals("ApplicationPackage", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DeploymentValidationException(
                        "PackageContents.xml root element is not <ApplicationPackage>.");
                }
            }
            catch (XmlException ex)
            {
                throw new DeploymentValidationException(
                    string.Format("PackageContents.xml is malformed or corrupted: {0}", ex.Message), ex);
            }

            // 2. Verify DLL binaries exist in at least one version folder (Legacy or Modern)
            string legacyDll = Path.Combine(bundleDirectory, "Contents", "Legacy", "KhimTools.dll");
            string modernDll = Path.Combine(bundleDirectory, "Contents", "Modern", "KhimTools.dll");

            bool legacyExists = File.Exists(legacyDll);
            bool modernExists = File.Exists(modernDll);

            if (!legacyExists && !modernExists)
            {
                throw new DeploymentValidationException(
                    string.Format("No KhimTools.dll found in bundle. Checked paths: '{0}' and '{1}'.", legacyDll, modernDll));
            }

            // 3. Strict version verification against expected version
            if (!string.IsNullOrWhiteSpace(expectedVersion))
            {
                SemanticVersion semVer;
                if (SemanticVersion.TryParse(expectedVersion, out semVer))
                {
                    bool verifiedAtLeastOne = false;

                    if (legacyExists)
                    {
                        VerifyDllVersion(legacyDll, semVer, expectedVersion);
                        verifiedAtLeastOne = true;
                    }

                    if (modernExists)
                    {
                        VerifyDllVersion(modernDll, semVer, expectedVersion);
                        verifiedAtLeastOne = true;
                    }

                    if (!verifiedAtLeastOne)
                    {
                        throw new DeploymentValidationException("No DLL was available to verify against expected version.");
                    }
                }
            }
        }

        /// <summary>
        /// Reads FileVersion and AssemblyVersion from the DLL and strictly asserts equality with expected semantic version.
        /// Throws DeploymentValidationException on mismatch.
        /// </summary>
        public static void VerifyDllVersion(string dllPath, SemanticVersion expected, string expectedRaw)
        {
            Version actualVer = GetDllVersion(dllPath);
            if (actualVer == null)
            {
                throw new DeploymentValidationException(
                    string.Format("Unable to read version information from DLL: {0}", dllPath));
            }

            var actual = new SemanticVersion(actualVer.Major, actualVer.Minor, Math.Max(0, actualVer.Build));

            // Compare Major, Minor, and Patch
            bool matches = (actual.Major == expected.Major &&
                            actual.Minor == expected.Minor &&
                            (expected.Patch == -1 || actual.Patch == expected.Patch));

            if (!matches)
            {
                throw new DeploymentValidationException(
                    string.Format("Post-install version mismatch in '{0}'! Expected version '{1}' ({2}) but found '{3}'. Deployment verification failed.",
                        Path.GetFileName(dllPath), expectedRaw, expected, actual),
                    expectedRaw,
                    actual.ToString());
            }
        }

        /// <summary>
        /// Backward-compatible overload for System.Version.
        /// </summary>
        public static void VerifyDllVersion(string dllPath, Version expected, string expectedRaw)
        {
            var semVer = new SemanticVersion(expected.Major, expected.Minor, Math.Max(0, expected.Build));
            VerifyDllVersion(dllPath, semVer, expectedRaw);
        }

        /// <summary>
        /// Reads version from DLL using FileVersionInfo, with Assembly reflection fallback.
        /// </summary>
        public static Version GetDllVersion(string dllPath)
        {
            if (!File.Exists(dllPath)) return null;

            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(dllPath);
                if (!string.IsNullOrEmpty(fvi.FileVersion) &&
                    fvi.FileVersion != "0.0.0.0" &&
                    fvi.FileVersion != "1.0.0.0")
                {
                    Version fv;
                    if (Version.TryParse(fvi.FileVersion, out fv))
                    {
                        return fv;
                    }
                }

                if (!string.IsNullOrEmpty(fvi.ProductVersion) &&
                    fvi.ProductVersion != "0.0.0.0" &&
                    fvi.ProductVersion != "1.0.0.0")
                {
                    Version pv;
                    if (Version.TryParse(fvi.ProductVersion, out pv))
                    {
                        return pv;
                    }
                }
            }
            catch { }

            try
            {
                AssemblyName asmName = AssemblyName.GetAssemblyName(dllPath);
                if (asmName.Version != null &&
                    asmName.Version.ToString() != "0.0.0.0" &&
                    asmName.Version.ToString() != "1.0.0.0")
                {
                    return asmName.Version;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Parses version string like "v2.8.0", "2.8.0", or "v2.8.0-beta" into System.Version.
        /// </summary>
        public static Version ParseNormalizedVersion(string versionStr)
        {
            SemanticVersion semVer;
            if (SemanticVersion.TryParse(versionStr, out semVer))
            {
                return new Version(semVer.Major, semVer.Minor, semVer.Patch);
            }
            return null;
        }
    }
}
