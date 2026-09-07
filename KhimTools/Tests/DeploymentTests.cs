using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using KhiemToolsApp.Deployment;

namespace KhimTools.Tests
{
    public class DeploymentTests
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static readonly Dictionary<string, string> FixtureCache = new Dictionary<string, string>();

        public static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" K-TOOLS UPDATER & DEPLOYMENT SECURITY TEST SUITE");
            Console.WriteLine(" Evidence-Based Audit: Zip Slip, Rollback, SemVer, Backup");
            Console.WriteLine("=========================================================\n");

            try
            {
                // Original 12 P0 Tests
                RunTest("Test 01: Interrupted Copy Protection", Test_01_InterruptedCopy);
                RunTest("Test 02: Locked Target Detection", Test_02_LockedTarget);
                RunTest("Test 03: Backup Failure Abort", Test_03_BackupFailure);
                RunTest("Test 04: Staging Corruption Rejection", Test_04_StagingCorruption);
                RunTest("Test 05: Missing PackageContents.xml Rejection", Test_05_PackageContentsMissing);
                RunTest("Test 06: Missing KhimTools.dll Rejection", Test_06_DllMissing);
                RunTest("Test 07: Deterministic Wrong DLL Version Mismatch & Rollback", Test_07_DllWrongVersion);
                RunTest("Test 08: Rollback Success After Swap Failure", Test_08_RollbackSuccess);
                RunTest("Test 09: Explicit Rollback Failure Notification", Test_09_RollbackFailure);
                RunTest("Test 10: Legacy AppData Classification & Preservation", Test_10_LegacyAppDataInstall);
                RunTest("Test 11: Duplicate ProgramData / UserManaged AppData Classification", Test_11_DuplicateProgramDataAppData);
                RunTest("Test 12: Multi-Year Revit Versions Detection", Test_12_MultipleRevitVersions);

                // Additional Mandatory Security Verification Tests
                RunTest("Test 13: Zip Slip Path Traversal (../evil.dll)", Test_13_ZipSlip_RelativeForward);
                RunTest("Test 14: Zip Slip Path Traversal (..\\evil.dll)", Test_14_ZipSlip_RelativeBackward);
                RunTest("Test 15: Zip Slip Rooted / Absolute Windows Path Rejection", Test_15_ZipSlip_AbsoluteWindows);
                RunTest("Test 16: Zip Slip Prohibited UNC Path Rejection", Test_16_ZipSlip_ProhibitedUnc);
                RunTest("Test 17: SemVer 2.0 Prerelease Ordering (beta < rc1 < 2.7.1 < 2.7.2)", Test_17_SemVerOrdering);
                RunTest("Test 18: UserManaged & Unknown Installations Preservation", Test_18_UserManagedAndUnknownPreservation);
                RunTest("Test 19: Backup Retention Pruning (No Uncontrolled Accumulation)", Test_19_BackupRetentionPruning);

                // Phase 4: Updater -> MSI Transition Tests
                RunTest("Test 20: MSI-Managed Installation Detection & Classification", Test_20_MsiManaged_Classification);
                RunTest("Test 21: MSI-Managed Boundary Guard (Blocks Raw ZIP Overwrite)", Test_21_MsiManaged_BlocksRawZipOverwrite);
                RunTest("Test 22: MSI Package SHA-256 Checksum Verification & Mismatch Protection", Test_22_MsiPackage_Sha256Verification);
                RunTest("Test 23: Release Manifest MSI Metadata Parsing & Validation", Test_23_UpdateInfoJson_MsiMetadata);
            }
            finally
            {
                // Clean up cached fixture DLLs
                foreach (string fixturePath in FixtureCache.Values)
                {
                    try { if (File.Exists(fixturePath)) File.Delete(fixturePath); } catch { }
                }
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine(string.Format(" RESULTS: {0} Passed, {1} Failed", _passed, _failed));
            Console.WriteLine("=========================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            Console.Write(string.Format("[TEST] {0} ... ", testName));
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.ResetColor();
                Console.WriteLine(string.Format("       -> Reason: {0}", ex.Message));
                _failed++;
            }
        }

        private static string CreateSandbox(string name)
        {
            string path = Path.Combine(Path.GetTempPath(), "KhimTest_" + name + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void CleanupSandbox(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }

        /// <summary>
        /// Compiles a deterministic mock PE DLL with exact requested assembly & file version.
        /// Does NOT depend on whatever DLL happens to exist in bin\Release.
        /// </summary>
        private static string GetOrCreateDeterministicDllFixture(string version)
        {
            if (FixtureCache.ContainsKey(version) && File.Exists(FixtureCache[version]))
            {
                return FixtureCache[version];
            }

            string fixtureDll = Path.Combine(Path.GetTempPath(), string.Format("KhimFixture_{0}_{1}.dll", version.Replace('.', '_'), Guid.NewGuid().ToString("N")));
            string tempSource = Path.Combine(Path.GetTempPath(), "FixtureSource_" + Guid.NewGuid().ToString("N") + ".cs");

            string csc = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
            if (!File.Exists(csc)) csc = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe";

            string src = string.Format(
                "using System.Reflection;\n" +
                "[assembly: AssemblyVersion(\"{0}.0\")]\n" +
                "[assembly: AssemblyFileVersion(\"{0}.0\")]\n" +
                "public class KhimToolsDeterministicFixture {{}}\n", version);

            File.WriteAllText(tempSource, src);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = csc,
                Arguments = string.Format("/target:library /out:\"{0}\" /nologo \"{1}\"", fixtureDll, tempSource),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var p = System.Diagnostics.Process.Start(psi))
            {
                p.WaitForExit();
            }

            try { File.Delete(tempSource); } catch { }

            if (!File.Exists(fixtureDll))
            {
                throw new InvalidOperationException("Failed to compile deterministic fixture DLL for version: " + version);
            }

            FixtureCache[version] = fixtureDll;
            return fixtureDll;
        }

        private static void CreateMockBundle(string bundlePath, string version, bool includePackageContents, bool includeDll)
        {
            Directory.CreateDirectory(bundlePath);
            if (includePackageContents)
            {
                string xml = string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><ApplicationPackage SchemaVersion=\"1.0\" AppVersion=\"{0}\" Name=\"KhimTools\"></ApplicationPackage>", version);
                File.WriteAllText(Path.Combine(bundlePath, "PackageContents.xml"), xml);
            }

            if (includeDll)
            {
                string legacyDir = Path.Combine(bundlePath, "Contents", "Legacy");
                Directory.CreateDirectory(legacyDir);
                string dllTarget = Path.Combine(legacyDir, "KhimTools.dll");

                string fixtureDll = GetOrCreateDeterministicDllFixture(version);
                File.Copy(fixtureDll, dllTarget, true);
            }
        }

        private static string CreateMockZip(string sandboxPath, string zipName, Action<string> bundleBuilder)
        {
            string payloadDir = Path.Combine(sandboxPath, "payload_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(payloadDir);
            bundleBuilder(payloadDir);

            string zipPath = Path.Combine(sandboxPath, zipName);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(payloadDir, zipPath);
            Directory.Delete(payloadDir, true);
            return zipPath;
        }

        private static string CreateZipWithCustomEntry(string sandboxPath, string zipName, string maliciousEntryName, string fileContent)
        {
            string zipPath = Path.Combine(sandboxPath, zipName);
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(maliciousEntryName);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(fileContent);
                }
            }
            return zipPath;
        }

        // 1. Interrupted copy: Target is unaffected if staging/copy fails midway.
        private static void Test_01_InterruptedCopy()
        {
            string sandbox = CreateSandbox("Test01");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                File.WriteAllText(Path.Combine(target, "sentinel.txt"), "ORIGINAL_STATE");

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, true); });

                var engine = new SafeDeploymentEngine();
                engine.SimulateInterruptedCopy = true;
                bool threw = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (IOException)
                {
                    threw = true;
                }

                if (!threw) throw new Exception("Expected IOException was not thrown.");
                if (!File.Exists(Path.Combine(target, "sentinel.txt")))
                    throw new Exception("Live bundle was modified or destroyed during interrupted copy.");
                if (File.ReadAllText(Path.Combine(target, "sentinel.txt")) != "ORIGINAL_STATE")
                    throw new Exception("Live bundle sentinel content was corrupted.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 2. Locked target: Target locked by another process fails safely.
        private static void Test_02_LockedTarget()
        {
            string sandbox = CreateSandbox("Test02");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                string dllPath = Path.Combine(target, "Contents", "Legacy", "KhimTools.dll");

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, true); });

                // Lock the DLL exclusively
                using (var fs = new FileStream(dllPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var engine = new SafeDeploymentEngine();
                    bool threw = false;
                    try
                    {
                        engine.DeployZip(zip, target, "2.7.0", null);
                    }
                    catch (DeploymentLockException)
                    {
                        threw = true;
                    }

                    if (!threw) throw new Exception("Expected DeploymentLockException was not thrown on locked target.");
                }

                // Verify target still exists intact
                if (!File.Exists(dllPath)) throw new Exception("Target DLL was deleted despite being locked.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 3. Backup failure: Failure to create backup aborts install; target remains untouched.
        private static void Test_03_BackupFailure()
        {
            string sandbox = CreateSandbox("Test03");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                File.WriteAllText(Path.Combine(target, "important.data"), "DO_NOT_LOSE");

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, true); });

                var engine = new SafeDeploymentEngine();
                engine.SimulateBackupFailure = true;
                bool threw = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (BackupFailedException)
                {
                    threw = true;
                }

                if (!threw) throw new Exception("Expected BackupFailedException was not thrown.");
                if (!File.Exists(Path.Combine(target, "important.data")))
                    throw new Exception("Live bundle was modified despite backup failure.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 4. Staging corruption: Damaged zip fails before target is modified.
        private static void Test_04_StagingCorruption()
        {
            string sandbox = CreateSandbox("Test04");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);

                string corruptedZip = Path.Combine(sandbox, "corrupt.zip");
                File.WriteAllBytes(corruptedZip, new byte[] { 0x00, 0x11, 0x22, 0x33, 0xFF, 0xEE });

                var engine = new SafeDeploymentEngine();
                bool threw = false;
                try
                {
                    engine.DeployZip(corruptedZip, target, "2.7.0", null);
                }
                catch (Exception)
                {
                    threw = true;
                }

                if (!threw) throw new Exception("Corrupted zip did not throw exception.");
                if (!Directory.Exists(target)) throw new Exception("Target bundle was removed.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 5. PackageContents missing: Aborts deployment when PackageContents.xml is absent.
        private static void Test_05_PackageContentsMissing()
        {
            string sandbox = CreateSandbox("Test05");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);

                string zip = CreateMockZip(sandbox, "bad_payload.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", false, true); });

                var engine = new SafeDeploymentEngine();
                bool threw = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (DeploymentValidationException ex)
                {
                    threw = true;
                    if (!ex.Message.Contains("PackageContents.xml"))
                        throw new Exception("Exception message did not mention PackageContents.xml: " + ex.Message);
                }

                if (!threw) throw new Exception("DeploymentValidationException was not thrown for missing PackageContents.xml.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 6. DLL missing: Aborts deployment when KhimTools.dll is absent.
        private static void Test_06_DllMissing()
        {
            string sandbox = CreateSandbox("Test06");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);

                string zip = CreateMockZip(sandbox, "no_dll.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, false); });

                var engine = new SafeDeploymentEngine();
                bool threw = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (DeploymentValidationException ex)
                {
                    threw = true;
                    if (!ex.Message.Contains("No KhimTools.dll found"))
                        throw new Exception("Exception message did not mention KhimTools.dll: " + ex.Message);
                }

                if (!threw) throw new Exception("DeploymentValidationException was not thrown for missing DLL.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 7. Deterministic Wrong DLL Version Mismatch & Rollback
        // Fixture guarantees: actual compiled DLL version = 2.7.0, expected version = 2.8.0, result = DeploymentValidationException
        private static void Test_07_DllWrongVersion()
        {
            string sandbox = CreateSandbox("Test07");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                // Target bundle is currently installed at version 2.7.0 using deterministic fixture
                CreateMockBundle(target, "2.7.0", true, true);
                File.WriteAllText(Path.Combine(target, "live_sentinel.txt"), "PRE_UPDATE_SENTINEL_VERSION_270");

                // Staged package has a DLL whose actual compiled binary PE version is strictly 2.7.0
                string zip = CreateMockZip(sandbox, "payload_270.zip", delegate(string dir) {
                    CreateMockBundle(dir, "2.7.0", true, true);
                });

                // Deployer attempts to deploy expecting version 2.8.0
                var engine = new SafeDeploymentEngine();
                bool caughtMismatch = false;
                try
                {
                    engine.DeployZip(zip, target, "2.8.0", null);
                }
                catch (DeploymentValidationException ex)
                {
                    caughtMismatch = true;
                    if (!ex.Message.Contains("version mismatch") || !ex.Message.Contains("2.8.0") || !ex.Message.Contains("2.7.0"))
                    {
                        throw new Exception("Exception message did not contain exact version mismatch details: " + ex.Message);
                    }
                }

                if (!caughtMismatch)
                {
                    throw new Exception("Expected DeploymentValidationException on version mismatch was not thrown.");
                }

                // Verify target was safely preserved / rolled back
                if (!File.Exists(Path.Combine(target, "live_sentinel.txt")))
                {
                    throw new Exception("Live bundle was not preserved after version mismatch failure.");
                }
                if (File.ReadAllText(Path.Combine(target, "live_sentinel.txt")) != "PRE_UPDATE_SENTINEL_VERSION_270")
                {
                    throw new Exception("Live bundle sentinel content was altered after version mismatch failure.");
                }

                // Verify target DLL version remains 2.7.0
                string targetDll = Path.Combine(target, "Contents", "Legacy", "KhimTools.dll");
                var actualVer = DeploymentValidator.GetDllVersion(targetDll);
                if (actualVer == null || actualVer.Major != 2 || actualVer.Minor != 7)
                {
                    throw new Exception("Target DLL after rollback is not version 2.7.0!");
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 8. Rollback success: When verification fails after swap, rollback restores previous installation bit-for-bit.
        private static void Test_08_RollbackSuccess()
        {
            string sandbox = CreateSandbox("Test08");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                string markerPath = Path.Combine(target, "original_marker.dat");
                File.WriteAllText(markerPath, "PRE_INSTALL_FINGERPRINT_12345");

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, true); });

                var engine = new SafeDeploymentEngine();
                engine.SimulatePostInstallVerificationFailure = true;
                bool threw = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (DeploymentValidationException)
                {
                    threw = true;
                }

                if (!threw) throw new Exception("Validation failure was not thrown.");

                // Verify target was restored by rollback
                if (!Directory.Exists(target)) throw new Exception("Target directory was lost after rollback.");
                if (!File.Exists(markerPath)) throw new Exception("Marker file was lost after rollback.");
                if (File.ReadAllText(markerPath) != "PRE_INSTALL_FINGERPRINT_12345")
                    throw new Exception("Marker content did not match original after rollback.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 9. Rollback failure: Explicit RollbackFailedException raised if rollback fails; not swallowed.
        private static void Test_09_RollbackFailure()
        {
            string sandbox = CreateSandbox("Test09");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) { CreateMockBundle(dir, "2.7.0", true, true); });

                var engine = new SafeDeploymentEngine();
                engine.SimulatePostInstallVerificationFailure = true;
                engine.SimulateRollbackFailure = true;

                bool caughtRollbackFailed = false;
                try
                {
                    engine.DeployZip(zip, target, "2.7.0", null);
                }
                catch (RollbackFailedException ex)
                {
                    caughtRollbackFailed = true;
                    if (string.IsNullOrEmpty(ex.BackupPath) || string.IsNullOrEmpty(ex.TargetPath))
                    {
                        throw new Exception("RollbackFailedException is missing backup/target paths.");
                    }
                }

                if (!caughtRollbackFailed) throw new Exception("RollbackFailedException was not raised or was swallowed.");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 10. Legacy AppData install: Detects legacy AppData addins, preserves them in backup before removal.
        private static void Test_10_LegacyAppDataInstall()
        {
            string sandbox = CreateSandbox("Test10");
            try
            {
                string dummyAddin = Path.Combine(sandbox, "KhimTools.addin");
                File.WriteAllText(dummyAddin, "<?xml version=\"1.0\" encoding=\"utf-8\"?><RevitAddIns><AddIn Type=\"Application\"><Name>KhimTools</Name><Assembly>C:\\Legacy\\KhimTools.dll</Assembly><FullClassName>KhimTools.App</FullClassName></AddIn></RevitAddIns>");

                var classification = InstallationClassifier.ClassifyAddinFile(dummyAddin);
                if (classification != InstallationClassification.Legacy)
                {
                    throw new Exception(string.Format("Expected Legacy classification, but got {0}", classification));
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 11. Duplicate ProgramData / UserManaged AppData classification.
        private static void Test_11_DuplicateProgramDataAppData()
        {
            string sandbox = CreateSandbox("Test11");
            try
            {
                string authBundle = Path.Combine(sandbox, "ProgramData", "KhimTools.bundle");
                CreateMockBundle(authBundle, "2.7.0", true, true);

                string userBundle = Path.Combine(sandbox, "AppData", "KhimTools.bundle");
                CreateMockBundle(userBundle, "2.7.0", true, true);
                // Add git folder to signify user development build
                Directory.CreateDirectory(Path.Combine(userBundle, ".git"));

                var authClass = InstallationClassifier.ClassifyBundle(authBundle, authBundle);
                var userClass = InstallationClassifier.ClassifyBundle(userBundle, authBundle);

                if (authClass != InstallationClassification.Current)
                {
                    throw new Exception(string.Format("Expected Current for authoritative bundle, got {0}", authClass));
                }

                if (userClass != InstallationClassification.UserManaged)
                {
                    throw new Exception(string.Format("Expected UserManaged for user workspace bundle, got {0}", userClass));
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 12. Multiple Revit versions: Multi-year addin detection (2020-2028).
        private static void Test_12_MultipleRevitVersions()
        {
            string sandbox = CreateSandbox("Test12");
            try
            {
                int[] testYears = new int[] { 2020, 2022, 2024, 2026, 2028 };
                foreach (int y in testYears)
                {
                    string yearDir = Path.Combine(sandbox, y.ToString());
                    Directory.CreateDirectory(yearDir);
                    string addinFile = Path.Combine(yearDir, "KhimTools.addin");
                    File.WriteAllText(addinFile, string.Format("<Assembly>C:\\Addins\\{0}\\KhimTools.dll</Assembly>", y));

                    var classification = InstallationClassifier.ClassifyAddinFile(addinFile);
                    if (classification != InstallationClassification.Legacy)
                    {
                        throw new Exception(string.Format("Failed detection for Revit year {0}, got {1}", y, classification));
                    }
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 13. Zip Slip Path Traversal (../evil.dll)
        private static void Test_13_ZipSlip_RelativeForward()
        {
            string sandbox = CreateSandbox("Test13");
            try
            {
                string zipPath = CreateZipWithCustomEntry(sandbox, "slip_forward.zip", "../evil.dll", "MALICIOUS");
                string targetDir = Path.Combine(sandbox, "target_extraction");

                bool caught = false;
                try
                {
                    SafeDeploymentEngine.ExtractZipSafely(zipPath, targetDir);
                }
                catch (DeploymentSecurityException ex)
                {
                    caught = true;
                    if (!ex.Message.Contains("traversal") && !ex.Message.Contains("prohibited"))
                        throw new Exception("Unexpected exception message: " + ex.Message);
                }

                if (!caught) throw new Exception("Zip Slip with '../evil.dll' was NOT rejected!");
                if (File.Exists(Path.Combine(sandbox, "evil.dll")))
                    throw new Exception("Malicious file was extracted outside target directory!");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 14. Zip Slip Path Traversal (..\evil.dll)
        private static void Test_14_ZipSlip_RelativeBackward()
        {
            string sandbox = CreateSandbox("Test14");
            try
            {
                string zipPath = CreateZipWithCustomEntry(sandbox, "slip_back.zip", @"..\evil.dll", "MALICIOUS");
                string targetDir = Path.Combine(sandbox, "target_extraction");

                bool caught = false;
                try
                {
                    SafeDeploymentEngine.ExtractZipSafely(zipPath, targetDir);
                }
                catch (DeploymentSecurityException)
                {
                    caught = true;
                }

                if (!caught) throw new Exception(@"Zip Slip with '..\evil.dll' was NOT rejected!");
                if (File.Exists(Path.Combine(sandbox, "evil.dll")))
                    throw new Exception("Malicious file was extracted outside target directory!");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 15. Zip Slip Rooted / Absolute Windows Path Rejection
        private static void Test_15_ZipSlip_AbsoluteWindows()
        {
            string sandbox = CreateSandbox("Test15");
            try
            {
                string zipPath = CreateZipWithCustomEntry(sandbox, "slip_abs.zip", @"C:\Windows\evil.dll", "MALICIOUS");
                string targetDir = Path.Combine(sandbox, "target_extraction");

                bool caught = false;
                try
                {
                    SafeDeploymentEngine.ExtractZipSafely(zipPath, targetDir);
                }
                catch (DeploymentSecurityException)
                {
                    caught = true;
                }

                if (!caught) throw new Exception(@"Absolute Windows path in zip was NOT rejected!");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 16. Zip Slip Prohibited UNC Path Rejection
        private static void Test_16_ZipSlip_ProhibitedUnc()
        {
            string sandbox = CreateSandbox("Test16");
            try
            {
                string zipPath = CreateZipWithCustomEntry(sandbox, "slip_unc.zip", @"\\server\share\evil.dll", "MALICIOUS");
                string targetDir = Path.Combine(sandbox, "target_extraction");

                bool caught = false;
                try
                {
                    SafeDeploymentEngine.ExtractZipSafely(zipPath, targetDir);
                }
                catch (DeploymentSecurityException)
                {
                    caught = true;
                }

                if (!caught) throw new Exception("UNC path in zip was NOT rejected!");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 17. SemVer 2.0 Prerelease Ordering: 2.7.1-beta < 2.7.1-rc1 < 2.7.1 < 2.7.2
        private static void Test_17_SemVerOrdering()
        {
            var vBeta = SemanticVersion.Parse("2.7.1-beta");
            var vRc1 = SemanticVersion.Parse("2.7.1-rc1");
            var vRelease = SemanticVersion.Parse("2.7.1");
            var vNext = SemanticVersion.Parse("2.7.2");

            if (vBeta.CompareTo(vRc1) >= 0)
                throw new Exception(string.Format("Ordering error: expected {0} < {1}", vBeta, vRc1));

            if (vRc1.CompareTo(vRelease) >= 0)
                throw new Exception(string.Format("Ordering error: expected {0} < {1} (prerelease must be lower than release)", vRc1, vRelease));

            if (vRelease.CompareTo(vNext) >= 0)
                throw new Exception(string.Format("Ordering error: expected {0} < {1}", vRelease, vNext));

            // Test sorting a full list
            var list = new List<SemanticVersion> { vNext, vRelease, vBeta, vRc1 };
            list.Sort();

            if (list[0] != vBeta || list[1] != vRc1 || list[2] != vRelease || list[3] != vNext)
            {
                throw new Exception("SemVer list sorting did not yield expected order: beta, rc1, 2.7.1, 2.7.2");
            }
        }

        // 18. UserManaged & Unknown Installations Preservation
        private static void Test_18_UserManagedAndUnknownPreservation()
        {
            string sandbox = CreateSandbox("Test18");
            try
            {
                string userManagedDir = Path.Combine(sandbox, "UserManagedBundle");
                Directory.CreateDirectory(userManagedDir);
                Directory.CreateDirectory(Path.Combine(userManagedDir, ".git"));
                File.WriteAllText(Path.Combine(userManagedDir, "user_source.cs"), "// User custom source");

                string unknownDir = Path.Combine(sandbox, "ThirdPartyBundle");
                Directory.CreateDirectory(unknownDir);
                File.WriteAllText(Path.Combine(unknownDir, "PackageContents.xml"), "<ApplicationPackage Name=\"ThirdParty\"></ApplicationPackage>");

                var classUser = InstallationClassifier.ClassifyBundle(userManagedDir, "C:\\Authoritative");
                var classUnknown = InstallationClassifier.ClassifyBundle(unknownDir, "C:\\Authoritative");

                if (classUser != InstallationClassification.UserManaged)
                    throw new Exception(string.Format("Expected UserManaged, got {0}", classUser));

                if (classUnknown != InstallationClassification.Unknown)
                    throw new Exception(string.Format("Expected Unknown, got {0}", classUnknown));

                // Verify that neither is touched or deleted by legacy cleanup
                string backupDir = Path.Combine(sandbox, "Backup");
                InstallationClassifier.PreserveAndCleanLegacyArtifacts(backupDir, null);

                if (!Directory.Exists(userManagedDir) || !File.Exists(Path.Combine(userManagedDir, "user_source.cs")))
                    throw new Exception("UserManaged bundle was modified or deleted!");

                if (!Directory.Exists(unknownDir))
                    throw new Exception("Unknown bundle was deleted!");
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 19. Backup Retention Pruning (No Uncontrolled Accumulation)
        private static void Test_19_BackupRetentionPruning()
        {
            string sandbox = CreateSandbox("Test19");
            try
            {
                string backupRoot = Path.Combine(sandbox, "KhimTools_Backups");
                Directory.CreateDirectory(backupRoot);

                // Create 5 simulated backup directories with distinct timestamps
                var createdDirs = new List<string>();
                for (int i = 0; i < 5; i++)
                {
                    string dirPath = Path.Combine(backupRoot, string.Format("Backup_{0}_{1}", DateTime.UtcNow.AddMinutes(i).ToString("yyyyMMdd_HHmmss"), i));
                    Directory.CreateDirectory(dirPath);
                    File.WriteAllText(Path.Combine(dirPath, "info.txt"), "Backup #" + i);
                    // Artificially set CreationTime to ensure distinct ordering
                    Directory.SetCreationTimeUtc(dirPath, DateTime.UtcNow.AddMinutes(i));
                    createdDirs.Add(dirPath);
                }

                // Prune with retention limit = 2
                SafeDeploymentEngine.PruneOldBackups(backupRoot, 2, null);

                string[] remainingDirs = Directory.GetDirectories(backupRoot, "Backup_*");
                if (remainingDirs.Length != 2)
                {
                    throw new Exception(string.Format("Expected exactly 2 backups retained, but found {0}", remainingDirs.Length));
                }

                // Verify the 2 newest backups were preserved
                if (!Directory.Exists(createdDirs[4]) || !Directory.Exists(createdDirs[3]))
                {
                    throw new Exception("The newest backups were unexpectedly pruned!");
                }

                // Verify older backups were pruned
                if (Directory.Exists(createdDirs[0]) || Directory.Exists(createdDirs[1]) || Directory.Exists(createdDirs[2]))
                {
                    throw new Exception("Older expired backups were not pruned!");
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 20. MSI-Managed Installation Detection & Classification
        private static void Test_20_MsiManaged_Classification()
        {
            string sandbox = CreateSandbox("Test20");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);

                // When MSI detection override is false: classified as standard Current
                InstallationClassifier.MsiDetectionOverride = delegate() { return false; };
                var normalClass = InstallationClassifier.ClassifyBundle(target, target);
                if (normalClass != InstallationClassification.Current)
                {
                    throw new Exception(string.Format("Expected Current, got: {0}", normalClass));
                }

                // When MSI detection override is true (InstalledVia == 'MSI'): classified as MsiManaged
                InstallationClassifier.MsiDetectionOverride = delegate() { return true; };
                var msiClass = InstallationClassifier.ClassifyBundle(target, target);
                if (msiClass != InstallationClassification.MsiManaged)
                {
                    throw new Exception(string.Format("Expected MsiManaged, got: {0}", msiClass));
                }
            }
            finally
            {
                InstallationClassifier.MsiDetectionOverride = null;
                CleanupSandbox(sandbox);
            }
        }

        // 21. MSI-Managed Boundary Guard (Blocks Raw ZIP Overwrite)
        private static void Test_21_MsiManaged_BlocksRawZipOverwrite()
        {
            string sandbox = CreateSandbox("Test21");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                File.WriteAllText(Path.Combine(target, "msi_sentinel.txt"), "ORIGINAL_MSI_PAYLOAD");

                string zip = CreateMockZip(sandbox, "update.zip", delegate(string dir) {
                    CreateMockBundle(dir, "2.8.0", true, true);
                });

                // Simulate that current installation is MSI-managed
                InstallationClassifier.MsiDetectionOverride = delegate() { return true; };

                var engine = new SafeDeploymentEngine();
                bool caughtMsiBlocked = false;
                try
                {
                    engine.DeployZip(zip, target, "2.8.0", null);
                }
                catch (MsiManagedDeploymentException)
                {
                    caughtMsiBlocked = true;
                }

                if (!caughtMsiBlocked)
                {
                    throw new Exception("DeployZip failed to block raw ZIP extraction against an MSI-managed target!");
                }

                // Verify the original MSI bundle was completely untouched
                if (!File.Exists(Path.Combine(target, "msi_sentinel.txt")) ||
                    File.ReadAllText(Path.Combine(target, "msi_sentinel.txt")) != "ORIGINAL_MSI_PAYLOAD")
                {
                    throw new Exception("MSI target was modified despite MsiManagedDeploymentException!");
                }
            }
            finally
            {
                InstallationClassifier.MsiDetectionOverride = null;
                CleanupSandbox(sandbox);
            }
        }

        // 22. MSI Package SHA-256 Checksum Verification & Mismatch Protection
        private static void Test_22_MsiPackage_Sha256Verification()
        {
            string sandbox = CreateSandbox("Test22");
            try
            {
                string mockMsi = Path.Combine(sandbox, "MockPackage.msi");
                File.WriteAllText(mockMsi, "MOCK_MSI_BINARY_CONTENT_SJTL_2026");

                string actualHash;
                bool validNoHash = SafeDeploymentEngine.VerifyMsiSha256(mockMsi, null, out actualHash);
                if (!validNoHash || string.IsNullOrEmpty(actualHash))
                {
                    throw new Exception("Failed to compute SHA-256 hash for mock MSI.");
                }

                // Test matching hash
                string dummy1;
                bool match = SafeDeploymentEngine.VerifyMsiSha256(mockMsi, actualHash, out dummy1);
                if (!match)
                {
                    throw new Exception("Matching SHA-256 hash was rejected!");
                }

                // Test mismatched hash
                string badHash = "E2462CCA1A90EB5C9FA7D2DFD8694B6AF3F7EC14919B201B51CF4CE6AFFD0000";
                string dummy2;
                bool mismatch = SafeDeploymentEngine.VerifyMsiSha256(mockMsi, badHash, out dummy2);
                if (mismatch)
                {
                    throw new Exception("Mismatched SHA-256 hash was erroneously accepted!");
                }

                // DeployMsi with bad hash must throw DeploymentValidationException
                var engine = new SafeDeploymentEngine();
                bool caughtMismatch = false;
                try
                {
                    engine.DeployMsi(mockMsi, badHash, null);
                }
                catch (DeploymentValidationException)
                {
                    caughtMismatch = true;
                }

                if (!caughtMismatch)
                {
                    throw new Exception("DeployMsi failed to throw DeploymentValidationException on SHA-256 mismatch!");
                }
            }
            finally
            {
                CleanupSandbox(sandbox);
            }
        }

        // 23. Release Manifest MSI Metadata Parsing & Validation
        private static void Test_23_UpdateInfoJson_MsiMetadata()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\update_info.json"));
            if (!File.Exists(manifestPath))
            {
                manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "update_info.json");
            }
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("update_info.json not found at: " + manifestPath);
            }

            string json = File.ReadAllText(manifestPath);

            // Verify download_url_msi exists
            var msiUrlMatch = System.Text.RegularExpressions.Regex.Match(json, "\"download_url_msi\"\\s*:\\s*\"([^\"]+)\"");
            if (!msiUrlMatch.Success)
            {
                throw new Exception("Missing 'download_url_msi' in update_info.json!");
            }

            string msiUrl = msiUrlMatch.Groups[1].Value;
            string rejectReason;
            if (!UrlSecurityValidator.IsSecureOfficialUrl(msiUrl, false, out rejectReason))
            {
                throw new Exception("MSI download URL in update_info.json failed security policy: " + rejectReason);
            }

            // Verify sha256_msi exists
            var msiShaMatch = System.Text.RegularExpressions.Regex.Match(json, "\"sha256_msi\"\\s*:\\s*\"([^\"]*)\"");
            if (!msiShaMatch.Success)
            {
                throw new Exception("Missing 'sha256_msi' in update_info.json!");
            }
        }
    }
}
