using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace KhimTools.Tools.Updater.Models
{
    /// <summary>
    /// Represents the complete version identity of a K-TOOLS build.
    /// Distinguishes Product Version, Build Timestamp ID, and Git Commit Hash.
    /// Enables exact package tracing from Source Commit -> Release -> Downloaded -> Installed -> Loaded.
    /// </summary>
    public class VersionModel
    {
        public string ProductVersion { get; set; } = "2.7.1";
        public string BuildId { get; set; } = "20260905.0001";
        public string GitCommit { get; set; } = "HEAD";
        public string AssemblyVersion { get; set; } = "2.7.1.0";
        public string FileVersion { get; set; } = "2.7.1.0";
        public string LoadedAssemblyPath { get; set; } = string.Empty;
        public string Sha256Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Reads the exact VersionModel from the currently executing assembly or a target DLL file.
        /// </summary>
        public static VersionModel FromAssembly(Assembly asm = null)
        {
            asm = asm ?? Assembly.GetExecutingAssembly();
            var model = new VersionModel();

            try
            {
                var asmVer = asm.GetName().Version;
                if (asmVer != null)
                {
                    model.AssemblyVersion = asmVer.ToString();
                    model.ProductVersion = $"{asmVer.Major}.{asmVer.Minor}.{Math.Max(0, asmVer.Build)}";
                }

                model.LoadedAssemblyPath = asm.Location;

                if (!string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
                {
                    var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                    if (!string.IsNullOrEmpty(fvi.FileVersion))
                    {
                        model.FileVersion = fvi.FileVersion;
                    }
                    if (!string.IsNullOrEmpty(fvi.ProductVersion))
                    {
                        model.ProductVersion = fvi.ProductVersion.TrimStart('v', 'V');
                    }

                    model.Sha256Checksum = ComputeFileSha256(asm.Location);
                }

                // Read InformationalVersion attribute (often holds commit hash & build metadata)
                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoAttr != null && !string.IsNullOrEmpty(infoAttr.InformationalVersion))
                {
                    string info = infoAttr.InformationalVersion;
                    // Format: "2.7.1+4e9dfa30" or "2.7.1.20260905+4e9dfa30"
                    if (info.Contains("+"))
                    {
                        var parts = info.Split('+');
                        model.GitCommit = parts[1];
                        if (parts[0].Contains("."))
                        {
                            model.BuildId = parts[0];
                        }
                    }
                    else
                    {
                        model.GitCommit = info;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[VersionModel] Error extracting version info: {ex.Message}");
            }

            return model;
        }

        /// <summary>
        /// Reads version model from an external DLL path.
        /// </summary>
        public static VersionModel FromDllPath(string dllPath)
        {
            var model = new VersionModel { LoadedAssemblyPath = dllPath };
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
                return model;

            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(dllPath);
                model.FileVersion = fvi.FileVersion ?? "0.0.0.0";
                model.ProductVersion = (fvi.ProductVersion ?? fvi.FileVersion ?? "0.0.0").TrimStart('v', 'V');
                model.Sha256Checksum = ComputeFileSha256(dllPath);

                var asm = Assembly.LoadFrom(dllPath);
                var asmVer = asm.GetName().Version;
                if (asmVer != null) model.AssemblyVersion = asmVer.ToString();

                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoAttr != null && !string.IsNullOrEmpty(infoAttr.InformationalVersion))
                {
                    string info = infoAttr.InformationalVersion;
                    if (info.Contains("+"))
                    {
                        var parts = info.Split('+');
                        model.GitCommit = parts[1];
                        model.BuildId = parts[0];
                    }
                    else
                    {
                        model.GitCommit = info;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[VersionModel] Error reading external DLL: {ex.Message}");
            }

            return model;
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

        /// <summary>
        /// Compares two version strings (e.g. "2.7.1" vs "2.7.0").
        /// Returns 1 if v1 > v2, -1 if v1 < v2, 0 if equal.
        /// </summary>
        public static int CompareVersions(string v1, string v2)
        {
            if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
            if (string.IsNullOrWhiteSpace(v1)) return -1;
            if (string.IsNullOrWhiteSpace(v2)) return 1;

            string clean1 = v1.Trim().TrimStart('v', 'V');
            string clean2 = v2.Trim().TrimStart('v', 'V');

            // Strip any build metadata suffix e.g. "-beta" or "+commit"
            if (clean1.Contains("-")) clean1 = clean1.Substring(0, clean1.IndexOf('-'));
            if (clean1.Contains("+")) clean1 = clean1.Substring(0, clean1.IndexOf('+'));
            if (clean2.Contains("-")) clean2 = clean2.Substring(0, clean2.IndexOf('-'));
            if (clean2.Contains("+")) clean2 = clean2.Substring(0, clean2.IndexOf('+'));

            if (Version.TryParse(clean1, out var ver1) && Version.TryParse(clean2, out var ver2))
            {
                return ver1.CompareTo(ver2);
            }

            // Fallback: segment comparison
            var segs1 = clean1.Split('.');
            var segs2 = clean2.Split('.');
            int maxLen = Math.Max(segs1.Length, segs2.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int val1 = i < segs1.Length && int.TryParse(segs1[i], out int p1) ? p1 : 0;
                int val2 = i < segs2.Length && int.TryParse(segs2[i], out int p2) ? p2 : 0;

                if (val1 > val2) return 1;
                if (val1 < val2) return -1;
            }

            return 0;
        }

        public override string ToString()
        {
            return $"Version: {ProductVersion} | Build: {BuildId} | Commit: {GitCommit} | FileVer: {FileVersion} | SHA: {(Sha256Checksum.Length >= 8 ? Sha256Checksum.Substring(0, 8) : "N/A")}";
        }
    }
}
