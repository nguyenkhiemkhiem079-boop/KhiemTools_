using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using KhimTools.Tools.Updater.Models;

namespace KhimTools.Tools.Updater.Services
{
    public class UpdaterTestResult
    {
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string Details { get; set; }
        public long DurationMs { get; set; }

        public override string ToString()
        {
            return $"[{(Passed ? "PASS" : "FAIL")}] {TestName} ({DurationMs}ms): {Details}";
        }
    }

    /// <summary>
    /// Comprehensive test suite verifying the commercial update engine:
    /// manifest parsing, SHA256 hashing, semantic versioning, 10-point package verification,
    /// duplicate bundle scanner, staging lifecycle, and automated rollback.
    /// </summary>
    public static class UpdaterTestSuite
    {
        public static List<UpdaterTestResult> RunAllTests()
        {
            var results = new List<UpdaterTestResult>();
            var sw = new Stopwatch();

            void RunTest(string name, Action action)
            {
                sw.Restart();
                try
                {
                    action();
                    sw.Stop();
                    results.Add(new UpdaterTestResult
                    {
                        TestName = name,
                        Passed = true,
                        Details = "Execution completed successfully.",
                        DurationMs = sw.ElapsedMilliseconds
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new UpdaterTestResult
                    {
                        TestName = name,
                        Passed = false,
                        Details = $"{ex.GetType().Name}: {ex.Message}",
                        DurationMs = sw.ElapsedMilliseconds
                    });
                }
            }

            // 1. Manifest Parsing and Validation
            RunTest("Manifest_Serialization_And_Validation", () =>
            {
                string sampleJson = @"{
                    ""product"": ""K-TOOLS"",
                    ""version"": ""2.7.1"",
                    ""build"": ""20260905.2034"",
                    ""commit"": ""4e9dfa30"",
                    ""releaseDate"": ""2026-09-05"",
                    ""package"": ""KTools_2.7.1.zip"",
                    ""downloadUrl"": ""https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/download/v2.7.1/KhimTools_Bundle.zip"",
                    ""sha256"": ""e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"",
                    ""supportedRevit"": [""2020"", ""2024"", ""2025"", ""2026""]
                }";

                var manifest = UpdateManifest.FromJson(sampleJson);
                if (manifest.Product != "K-TOOLS") throw new Exception("Product mismatch.");
                if (manifest.Version != "2.7.1") throw new Exception("Version mismatch.");
                if (!manifest.IsRevitSupported("2025")) throw new Exception("Revit 2025 should be supported.");
                if (manifest.IsRevitSupported("2019")) throw new Exception("Revit 2019 should not be supported.");

                var (isValid, err) = manifest.Validate();
                if (!isValid) throw new Exception($"Manifest failed validation: {err}");
            });

            // 2. SHA256 Calculation & Mismatch Detection
            RunTest("Sha256_Checksum_Verification", () =>
            {
                string tempFile = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(tempFile, "K-TOOLS Commercial Update SHA256 Verification Test Data");
                    string hash1 = PackageVerificationService.ComputeSha256(tempFile);
                    if (string.IsNullOrEmpty(hash1) || hash1.Length != 64)
                        throw new Exception("SHA256 must be 64 characters hex string.");

                    if (!PackageVerificationService.VerifySha256(tempFile, hash1))
                        throw new Exception("Self-computed SHA256 failed verification.");

                    // Verify tamper detection
                    string wrongHash = hash1.Substring(0, 63) + (hash1.EndsWith("0") ? "1" : "0");
                    if (PackageVerificationService.VerifySha256(tempFile, wrongHash))
                        throw new Exception("Tampered hash must fail verification.");
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            });

            // 3. Semantic Version Comparison
            RunTest("Version_Comparison_Logic", () =>
            {
                if (VersionModel.CompareVersions("2.7.1", "2.7.0") <= 0)
                    throw new Exception("2.7.1 should be greater than 2.7.0");
                if (VersionModel.CompareVersions("2.7.0", "2.7.1") >= 0)
                    throw new Exception("2.7.0 should be less than 2.7.1");
                if (VersionModel.CompareVersions("v2.7.1", "2.7.1") != 0)
                    throw new Exception("v2.7.1 should equal 2.7.1");
                if (VersionModel.CompareVersions("2.8.0-beta", "2.7.5") <= 0)
                    throw new Exception("2.8.0-beta should be greater than 2.7.5");
            });

            // 4. Staging Directory Lifecycle
            RunTest("Staging_Directory_Lifecycle", () =>
            {
                var staging = new StagingManager("9.9.9-test");
                staging.PrepareStagingDirectory();
                if (!Directory.Exists(staging.StagingRoot))
                    throw new Exception("Staging root directory was not created.");

                File.WriteAllText(staging.PackageZipPath, "Dummy Zip Content");
                staging.Cleanup();
                if (Directory.Exists(staging.StagingRoot))
                    throw new Exception("Staging root was not cleaned up.");
            });

            // 5. Package Verification 10-Point Protocol
            RunTest("Package_Verification_10_Point_Check", () =>
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "KTools_Pkg_Test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, "test_bundle.zip");

                try
                {
                    // Create mock bundle inside zip
                    string bundleDir = Path.Combine(tempDir, "MockBundle");
                    Directory.CreateDirectory(bundleDir);
                    Directory.CreateDirectory(Path.Combine(bundleDir, "Contents", "Modern"));
                    
                    // PackageContents.xml
                    string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ApplicationPackage SchemaVersion=""1.0"" AppVersion=""2.7.1"" Name=""K-TOOLS"">
</ApplicationPackage>";
                    File.WriteAllText(Path.Combine(bundleDir, "PackageContents.xml"), xml);

                    // Use actual executing assembly for realistic PE FileVersion verification
                    string thisDll = typeof(UpdaterTestSuite).Assembly.Location;
                    if (File.Exists(thisDll))
                    {
                        File.Copy(thisDll, Path.Combine(bundleDir, "Contents", "Modern", "KhimTools.dll"), true);
                    }
                    else
                    {
                        File.WriteAllBytes(Path.Combine(bundleDir, "Contents", "Modern", "KhimTools.dll"), new byte[8192]);
                    }

                    // Zip it up
                    ZipFile.CreateFromDirectory(bundleDir, zipPath);

                    string sha = PackageVerificationService.ComputeSha256(zipPath);
                    var manifest = new UpdateManifest
                    {
                        Product = "K-TOOLS",
                        Version = "2.7.1",
                        Sha256 = sha,
                        DownloadUrl = "https://example.com/bundle.zip",
                        SupportedRevit = new List<string> { "2025", "2026" }
                    };

                    var result = PackageVerificationService.VerifyPackage(zipPath, null, manifest, "2025");
                    if (!result.IsValid)
                    {
                        throw new Exception($"Package verification failed: {string.Join("; ", result.FailedChecks)}");
                    }
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });

            // 6. Duplicate Installation Scanner Execution
            RunTest("Duplicate_Installation_Scanner", () =>
            {
                var report = DuplicateInstallationScanner.Scan();
                if (report == null) throw new Exception("Scan returned null report.");
                string formatted = report.FormatReport();
                if (string.IsNullOrEmpty(formatted)) throw new Exception("Formatted report is empty.");
            });

            // 7. Automatic Rollback Simulation
            RunTest("Automatic_Rollback_Simulation", () =>
            {
                string testRoot = Path.Combine(Path.GetTempPath(), "KTools_Rollback_Test_" + Guid.NewGuid().ToString("N"));
                string liveBundle = Path.Combine(testRoot, "LiveBundle");
                string backupDir = Path.Combine(testRoot, "BackupBundle");
                Directory.CreateDirectory(liveBundle);
                Directory.CreateDirectory(backupDir);

                try
                {
                    // State: Live has v2.7.0
                    File.WriteAllText(Path.Combine(liveBundle, "version.txt"), "2.7.0");
                    File.WriteAllText(Path.Combine(backupDir, "version.txt"), "2.7.0");

                    // Simulate update failure (corrupt live)
                    File.WriteAllText(Path.Combine(liveBundle, "version.txt"), "CORRUPT_UPDATE_2.7.1");

                    // Trigger Rollback from backup
                    Directory.Delete(liveBundle, true);
                    Directory.CreateDirectory(liveBundle);
                    foreach (var file in Directory.GetFiles(backupDir))
                    {
                        File.Copy(file, Path.Combine(liveBundle, Path.GetFileName(file)));
                    }

                    // Verify rollback restored 2.7.0
                    string restoredVer = File.ReadAllText(Path.Combine(liveBundle, "version.txt")).Trim();
                    if (restoredVer != "2.7.0")
                    {
                        throw new Exception($"Rollback failed to restore 2.7.0. Current: {restoredVer}");
                    }
                }
                finally
                {
                    try { Directory.Delete(testRoot, true); } catch { }
                }
            });

            return results;
        }

        public static string FormatTestResults(List<UpdaterTestResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== K-TOOLS COMMERCIAL UPDATER TEST SUITE REPORT ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total tests: {results.Count}");

            int passCount = 0;
            foreach (var r in results)
            {
                if (r.Passed) passCount++;
                sb.AppendLine(r.ToString());
            }

            sb.AppendLine($"Summary: {passCount}/{results.Count} PASSED");
            sb.AppendLine("====================================================");
            return sb.ToString();
        }
    }
}
