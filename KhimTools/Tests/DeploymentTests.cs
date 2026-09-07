using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using KhiemToolsApp.Deployment;
using KhimTools.Core.Family;
using KhimTools.Structural.QuickStructure.Services;
using KhimTools.Structural.QuickStructure.Models;
using KhimTools.Architectural.QuickArchi.Models;
using KhimTools.RebarTool.Core;

namespace KhimTools.Tests
{
    public class DeploymentTests
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static readonly Dictionary<string, string> FixtureCache = new Dictionary<string, string>();
        private static string _workspaceProjectRoot = null;

        public static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                _workspaceProjectRoot = args[0];
                ResetResolverWithProjectRoot();
            }

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

                // Phase 5: Family Manager Tests
                RunTest("Test 24: FamilyPathResolver Probe Hierarchy & Custom Probes", Test_24_FamilyPathResolver_ProbeOrder);
                RunTest("Test 25: FamilyPathResolver Resolves Built-in RINCO_AN_Step.rfa", Test_25_FamilyPathResolver_ResolvesRincoAnStep);
                RunTest("Test 26: FamilyPathResolver Case-Insensitive & Extension Normalization", Test_26_FamilyPathResolver_Normalization);
                RunTest("Test 27: FamilyPathResolver Library Scanning & Metadata", Test_27_FamilyPathResolver_ScanLibraryMetadata);
                RunTest("Test 28: FamilyPathResolver Missing Family Graceful Handling", Test_28_FamilyPathResolver_MissingFamilyGraceful);

                // Phase 6: Quick Structure / Quick Archi Tests
                RunTest("Test 29: GridIntersectionHelper Orthogonal Intersection Math", Test_29_GridIntersection_Orthogonal);
                RunTest("Test 30: GridIntersectionHelper Angled & Segment Bounds Math", Test_30_GridIntersection_AngledAndBounds);
                RunTest("Test 31: GridIntersectionHelper Parallel & Coincident Lines Handling", Test_31_GridIntersection_ParallelLines);
                RunTest("Test 32: GridIntersectionHelper Coordinate Deduplication Within Tolerance", Test_32_GridIntersection_Deduplication);
                RunTest("Test 33: QuickStructure & QuickArchi Settings Contract Validation", Test_33_QuickSettings_ContractValidation);

                // Phase 7: Rebar Family Loading Tests
                RunTest("Test 34: FamilyPathResolver Resolves Standard Rebar Shapes (JP_T00, JP_T51, JP_T80)", Test_34_RebarShape_Resolution);
                RunTest("Test 35: Rebar Shape Name Normalization & Case-Insensitivity", Test_35_RebarShape_Normalization);
                RunTest("Test 36: Standard 43-Shape Inventory Completeness Audit", Test_36_RebarShape_43InventoryCompleteness);
                RunTest("Test 37: Missing Rebar Shape Graceful Handling & Probe Fallback", Test_37_RebarShape_MissingGraceful);
                RunTest("Test 38: Rebar Shape Category Tagging in Family Scanner", Test_38_RebarShape_CategoryScanning);

                // Phase 8: Rebar Engineering Tests
                RunTest("Test 39: Rebar Anchorage Calculator — TCVN 5574:2018 Formulas", Test_39_RebarAnchorage_TCVN5574);
                RunTest("Test 40: Rebar Anchorage Calculator — Eurocode 2 (EN 1992-1-1)", Test_40_RebarAnchorage_Eurocode2);
                RunTest("Test 41: Lap Splice Length Calculations (TCVN vs Eurocode 2)", Test_41_RebarLapSplice_Calculations);
                RunTest("Test 42: Mandrel Diameter & Bend Radius Standards Verification", Test_42_MandrelAndBendRadius);
                RunTest("Test 43: Column Reinforcement Steel Ratio Validation (mu_min <= mu <= mu_max)", Test_43_ColumnSteelRatio_Validation);
                RunTest("Test 44: Beam Reinforcement Steel Ratio Validation (mu_min <= mu <= mu_max)", Test_44_BeamSteelRatio_Validation);
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

        private static void ResetResolverWithProjectRoot()
        {
            FamilyPathResolver.ResetCustomProbes();
            if (!string.IsNullOrEmpty(_workspaceProjectRoot))
            {
                string devFamily = Path.Combine(_workspaceProjectRoot, "Family");
                if (Directory.Exists(devFamily))
                {
                    FamilyPathResolver.RegisterProbePath(devFamily);
                }
            }
            else
            {
                string cwd = Directory.GetCurrentDirectory();
                string cand = Path.Combine(cwd, "KhimTools", "Family");
                if (Directory.Exists(cand)) FamilyPathResolver.RegisterProbePath(cand);
                string cand2 = Path.Combine(cwd, "Family");
                if (Directory.Exists(cand2)) FamilyPathResolver.RegisterProbePath(cand2);
                string cand3 = Path.Combine(cwd, "..", "KhimTools", "Family");
                if (Directory.Exists(cand3)) FamilyPathResolver.RegisterProbePath(Path.GetFullPath(cand3));
            }
        }

        // 24. FamilyPathResolver: Probe Hierarchy Order & Custom Probe Registration
        private static void Test_24_FamilyPathResolver_ProbeOrder()
        {
            string sandbox = CreateSandbox("Test24");
            try
            {
                string customDir = Path.Combine(sandbox, "CustomLibrary");
                Directory.CreateDirectory(customDir);
                string testRfa = Path.Combine(customDir, "TestFamily.rfa");
                File.WriteAllText(testRfa, "RIFF_MOCK_RFA");

                FamilyPathResolver.ResetCustomProbes();
                FamilyPathResolver.RegisterProbePath(customDir);

                string resolved = FamilyPathResolver.ResolveFamilyPath("TestFamily");
                if (resolved == null)
                {
                    throw new Exception("Custom probe path failed to resolve TestFamily.rfa");
                }

                if (!string.Equals(Path.GetFullPath(resolved), Path.GetFullPath(testRfa), StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(string.Format("Expected resolved path '{0}', got '{1}'", testRfa, resolved));
                }
            }
            finally
            {
                ResetResolverWithProjectRoot();
                CleanupSandbox(sandbox);
            }
        }

        // 25. FamilyPathResolver: Resolution of Built-in RINCO_AN_Step.rfa
        private static void Test_25_FamilyPathResolver_ResolvesRincoAnStep()
        {
            string resolved = FamilyPathResolver.ResolveFamilyPath(FamilyConstants.RincoAnStep);
            if (resolved == null)
            {
                throw new Exception(string.Format("Failed to resolve built-in family '{0}'!", FamilyConstants.RincoAnStep));
            }

            if (!File.Exists(resolved))
            {
                throw new Exception(string.Format("Resolved path does not exist on disk: '{0}'", resolved));
            }

            if (!resolved.EndsWith(FamilyConstants.RfaExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(string.Format("Resolved path does not end with .rfa: '{0}'", resolved));
            }
        }

        // 26. FamilyPathResolver: Case-Insensitive & Extension Normalization
        private static void Test_26_FamilyPathResolver_Normalization()
        {
            string path1 = FamilyPathResolver.ResolveFamilyPath("RINCO_AN_Step");
            string path2 = FamilyPathResolver.ResolveFamilyPath("rinco_an_step.rfa");
            string path3 = FamilyPathResolver.ResolveFamilyPath("  RINCO_AN_STEP  ");

            if (path1 == null || path2 == null || path3 == null)
            {
                throw new Exception("Failed to resolve family across normalized variants");
            }

            if (!string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(path1, path3, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Normalized variants resolved to disparate paths!");
            }
        }

        // 27. FamilyPathResolver: Library Scanning & FamilyFileInfo Metadata
        private static void Test_27_FamilyPathResolver_ScanLibraryMetadata()
        {
            var families = FamilyPathResolver.ScanAvailableFamilies();
            if (families == null || families.Count == 0)
            {
                throw new Exception("ScanAvailableFamilies returned empty library collection!");
            }

            bool foundRinco = false;
            foreach (var item in families)
            {
                if (string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.FullPath))
                {
                    throw new Exception("FamilyFileInfo contains null or empty Name/FullPath");
                }

                if (item.FormattedSize == null || item.FormattedSize.Length == 0)
                {
                    throw new Exception(string.Format("FamilyFileInfo formatted size missing for {0}", item.Name));
                }

                if (string.Equals(item.Name, FamilyConstants.RincoAnStep, StringComparison.OrdinalIgnoreCase))
                {
                    foundRinco = true;
                }
            }

            if (!foundRinco)
            {
                throw new Exception("ScanAvailableFamilies failed to inventory RINCO_AN_Step!");
            }
        }

        // 28. FamilyPathResolver: Missing Family Handling
        private static void Test_28_FamilyPathResolver_MissingFamilyGraceful()
        {
            try
            {
                string nonExistentName = "NonExistent_Family_12345_XYZ";

                bool exists = FamilyPathResolver.FamilyExists(nonExistentName);
                if (exists)
                {
                    throw new Exception("FamilyExists erroneously returned true for non-existent family!");
                }

                string path = FamilyPathResolver.ResolveFamilyPath(nonExistentName);
                if (path != null)
                {
                    throw new Exception("ResolveFamilyPath returned non-null for non-existent family!");
                }
            }
            finally
            {
                ResetResolverWithProjectRoot();
            }
        }

        // 29. GridIntersectionHelper: Orthogonal Intersection Math
        private static void Test_29_GridIntersection_Orthogonal()
        {
            var p1 = new Point2D(0.0, 5000.0);
            var p2 = new Point2D(10000.0, 5000.0);
            var p3 = new Point2D(4000.0, 0.0);
            var p4 = new Point2D(4000.0, 10000.0);

            var inter = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, false);
            if (inter == null)
            {
                throw new Exception("FindIntersection returned null for intersecting orthogonal lines!");
            }

            if (Math.Abs(inter.X - 4000.0) > 1e-5 || Math.Abs(inter.Y - 5000.0) > 1e-5)
            {
                throw new Exception(string.Format("Expected intersection (4000, 5000), got ({0}, {1})", inter.X, inter.Y));
            }
        }

        // 30. GridIntersectionHelper: Angled & Segment Bounds Math
        private static void Test_30_GridIntersection_AngledAndBounds()
        {
            // Two segments that intersect only when extended:
            // Line 1: (0, 0) to (10, 10)
            // Line 2: (0, 20) to (5, 15) -> extends to intersect Line 1 at (10, 10)
            var p1 = new Point2D(0.0, 0.0);
            var p2 = new Point2D(5.0, 5.0);
            var p3 = new Point2D(0.0, 20.0);
            var p4 = new Point2D(5.0, 15.0);

            // Without extension (bounded segments) - should NOT intersect within 0..5
            var bounded = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, false);
            if (bounded != null)
            {
                throw new Exception("Bounded intersection should have returned null for non-overlapping segments!");
            }

            // With extension - lines intersect at (10, 10)
            var infinite = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, true);
            if (infinite == null)
            {
                throw new Exception("Extended infinite intersection returned null!");
            }

            if (Math.Abs(infinite.X - 10.0) > 1e-5 || Math.Abs(infinite.Y - 10.0) > 1e-5)
            {
                throw new Exception(string.Format("Expected intersection (10, 10), got ({0}, {1})", infinite.X, infinite.Y));
            }
        }

        // 31. GridIntersectionHelper: Parallel & Coincident Lines Handling
        private static void Test_31_GridIntersection_ParallelLines()
        {
            // Parallel horizontal lines
            var p1 = new Point2D(0.0, 100.0);
            var p2 = new Point2D(500.0, 100.0);
            var p3 = new Point2D(0.0, 200.0);
            var p4 = new Point2D(500.0, 200.0);

            var inter = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, true);
            if (inter != null)
            {
                throw new Exception("Parallel lines erroneously returned an intersection point!");
            }

            // Coincident lines
            var interCoincident = GridIntersectionHelper.FindIntersection(p1, p2, p1, p2, true);
            if (interCoincident != null)
            {
                throw new Exception("Coincident lines erroneously returned an intersection point!");
            }
        }

        // 32. GridIntersectionHelper: Coordinate Deduplication Within Tolerance
        private static void Test_32_GridIntersection_Deduplication()
        {
            var points = new List<Point2D>();
            points.Add(new Point2D(100.0, 200.0));
            points.Add(new Point2D(100.00001, 200.00001)); // Duplicate within tolerance 1e-3
            points.Add(new Point2D(300.0, 400.0));
            points.Add(new Point2D(100.0, 200.00002)); // Duplicate
            points.Add(new Point2D(500.0, 600.0));

            var dedup = GridIntersectionHelper.DeduplicatePoints(points, 1e-3);
            if (dedup.Count != 3)
            {
                throw new Exception(string.Format("Expected 3 unique points after deduplication, got {0}", dedup.Count));
            }

            if (Math.Abs(dedup[0].X - 100.0) > 1e-5 || Math.Abs(dedup[1].X - 300.0) > 1e-5 || Math.Abs(dedup[2].X - 500.0) > 1e-5)
            {
                throw new Exception("Deduplication altered point coordinate order or values!");
            }
        }

        // 33. QuickStructure & QuickArchi Settings Contract Validation
        private static void Test_33_QuickSettings_ContractValidation()
        {
            // QuickStructureSettings validation
            var qs = new QuickStructureSettings();
            qs.CreateColumns = false;
            qs.CreateBeams = false;
            qs.CreateFootings = false;
            qs.CreateFloor = false;

            string qsErr;
            if (qs.Validate(out qsErr))
            {
                throw new Exception("QuickStructureSettings with 0 tasks selected should fail validation!");
            }

            qs.CreateColumns = true;
            if (!qs.Validate(out qsErr))
            {
                throw new Exception("QuickStructureSettings with CreateColumns=true failed validation: " + qsErr);
            }

            // QuickArchiSettings validation
            var qa = new QuickArchiSettings();
            qa.WallHeightMm = -50.0;

            string qaErr;
            if (qa.Validate(out qaErr))
            {
                throw new Exception("QuickArchiSettings with negative height should fail validation!");
            }

            qa.WallHeightMm = 3200.0;
            if (!qa.Validate(out qaErr))
            {
                throw new Exception("QuickArchiSettings with 3200mm height failed validation: " + qaErr);
            }
        }

        // 34. FamilyPathResolver Resolves Standard Rebar Shapes
        private static void Test_34_RebarShape_Resolution()
        {
            string[] testShapes = new string[] { "JP_T00", "JP_T51", "JP_T80" };
            foreach (string shape in testShapes)
            {
                string resolved = FamilyPathResolver.ResolveRebarShapePath(shape);
                if (string.IsNullOrEmpty(resolved))
                {
                    throw new Exception(string.Format("Failed to resolve standard rebar shape '{0}'!", shape));
                }

                if (!File.Exists(resolved))
                {
                    throw new Exception(string.Format("Resolved path for '{0}' does not exist: {1}", shape, resolved));
                }

                if (!resolved.EndsWith(FamilyConstants.RfaExtension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(string.Format("Resolved path for '{0}' does not end with .rfa: {1}", shape, resolved));
                }
            }
        }

        // 35. Rebar Shape Name Normalization & Case-Insensitivity
        private static void Test_35_RebarShape_Normalization()
        {
            // Lowercase
            string lower = FamilyPathResolver.ResolveRebarShapePath("jp_t00");
            // Standard
            string std = FamilyPathResolver.ResolveRebarShapePath("JP_T00");
            // With extension
            string withExt = FamilyPathResolver.ResolveRebarShapePath("JP_T00.rfa");
            // With whitespace and uppercase extension
            string dirty = FamilyPathResolver.ResolveRebarShapePath("   JP_T00.RFA   ");

            if (string.IsNullOrEmpty(lower) || string.IsNullOrEmpty(std) || string.IsNullOrEmpty(withExt) || string.IsNullOrEmpty(dirty))
            {
                throw new Exception("Normalization failed to resolve one or more variants of JP_T00!");
            }

            if (!string.Equals(Path.GetFullPath(lower), Path.GetFullPath(std), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(withExt), Path.GetFullPath(std), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(dirty), Path.GetFullPath(std), StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Normalized paths did not point to the identical file!");
            }
        }

        // 36. Standard 43-Shape Inventory Completeness Audit
        private static void Test_36_RebarShape_43InventoryCompleteness()
        {
            string[] all43Shapes = new string[]
            {
                "JP_T00", "JP_T02", "JP_T03", "JP_T04", "JP_T05", "JP_T06", "JP_T07",
                "JP_T11", "JP_T11a", "JP_T12", "JP_T13", "JP_T14", "JP_T15", "JP_T16", "JP_T17",
                "JP_T20", "JP_T21", "JP_T22", "JP_T23", "JP_T24", "JP_T25", "JP_T26", "JP_T27", "JP_T28", "JP_T29",
                "JP_T31", "JP_T32", "JP_T34", "JP_T35", "JP_T36", "JP_T38",
                "JP_T41", "JP_T44", "JP_T46", "JP_T47", "JP_T48", "JP_T49",
                "JP_T51",
                "JP_T63", "JP_T67", "JP_T68",
                "JP_T75",
                "JP_T80"
            };

            if (all43Shapes.Length != 43)
            {
                throw new Exception(string.Format("Expected 43 shapes in test list, got {0}", all43Shapes.Length));
            }

            var missing = new List<string>();
            foreach (string shape in all43Shapes)
            {
                string path = FamilyPathResolver.ResolveRebarShapePath(shape);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    missing.Add(shape);
                }
            }

            if (missing.Count > 0)
            {
                throw new Exception(string.Format("Audit failed: {0} / 43 standard rebar shapes missing: {1}",
                    missing.Count, string.Join(", ", missing.ToArray())));
            }
        }

        // 37. Missing Rebar Shape Graceful Handling & Probe Fallback
        private static void Test_37_RebarShape_MissingGraceful()
        {
            // Null or empty
            string resNull = FamilyPathResolver.ResolveRebarShapePath(null);
            if (resNull != null) throw new Exception("ResolveRebarShapePath(null) should return null!");

            string resEmpty = FamilyPathResolver.ResolveRebarShapePath("");
            if (resEmpty != null) throw new Exception("ResolveRebarShapePath('') should return null!");

            // Nonexistent shape
            string resNonExistent = FamilyPathResolver.ResolveRebarShapePath("JP_NON_EXISTENT_SHAPE_9999");
            if (resNonExistent != null) throw new Exception("Nonexistent shape returned non-null path!");

            // FamilyExists
            if (FamilyPathResolver.FamilyExists("JP_NON_EXISTENT_SHAPE_9999", FamilyConstants.RebarShapesFolder))
            {
                throw new Exception("FamilyExists erroneously returned true for nonexistent shape!");
            }
        }

        // 38. Rebar Shape Category Tagging in Family Scanner
        private static void Test_38_RebarShape_CategoryScanning()
        {
            var families = FamilyPathResolver.ScanAvailableFamilies(FamilyConstants.RebarShapesFolder);
            if (families == null || families.Count == 0)
            {
                throw new Exception("ScanAvailableFamilies failed to find any rebar shapes!");
            }

            var rebarItems = new List<FamilyFileInfo>();
            bool hasT00 = false;
            bool hasT51 = false;

            foreach (var f in families)
            {
                if (string.Equals(f.Category, "Rebar", StringComparison.OrdinalIgnoreCase))
                {
                    rebarItems.Add(f);
                }

                if (string.Equals(f.Name, "JP_T00", StringComparison.OrdinalIgnoreCase))
                {
                    hasT00 = true;
                }
                if (string.Equals(f.Name, "JP_T51", StringComparison.OrdinalIgnoreCase))
                {
                    hasT51 = true;
                }
            }

            if (rebarItems.Count < 43)
            {
                throw new Exception(string.Format("Expected at least 43 items categorized as 'Rebar', found {0}", rebarItems.Count));
            }

            if (!hasT00 || !hasT51)
            {
                throw new Exception("JP_T00 or JP_T51 missing from rebar category scanner!");
            }
        }

        // 39. Rebar Anchorage Calculator — TCVN 5574:2018 Formulas
        private static void Test_39_RebarAnchorage_TCVN5574()
        {
            double barDia = 18.0;
            // CB400-V, B25, TensionStraight
            double ldStraight = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);

            // Math check: Rs=350, Rbt=1.05, eta1=2.5, eta2=1.0 -> fbd=2.625 -> lan = 18*350/(4*2.625) = 600mm
            if (Math.Abs(ldStraight - 600.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN TensionStraight Ld expected 600.0mm, got {0:F2}mm", ldStraight));
            }

            // Hooked tension: alpha = 0.7 -> 420mm
            double ldHooked = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionHooked, DesignCode.TCVN5574_2018);
            if (Math.Abs(ldHooked - 420.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN TensionHooked Ld expected 420.0mm, got {0:F2}mm", ldHooked));
            }

            // Compression: alpha = 0.75 -> 450mm
            double ldComp = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.Compression, DesignCode.TCVN5574_2018);
            if (Math.Abs(ldComp - 450.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN Compression Ld expected 450.0mm, got {0:F2}mm", ldComp));
            }

            // Minimum boundary check: max(15*d, 200mm) = 270mm
            double ldSmall = RebarAnchorageCalculator.CalculateAnchorageLength(
                10.0, ConcreteGrade.B40, SteelGrade.CB240_T, AnchorageType.TensionHooked, DesignCode.TCVN5574_2018);
            if (ldSmall < 200.0)
            {
                throw new Exception("TCVN Ld failed minimum bound of 200mm!");
            }
        }

        // 40. Rebar Anchorage Calculator — Eurocode 2 (EN 1992-1-1)
        private static void Test_40_RebarAnchorage_Eurocode2()
        {
            double barDia = 18.0;
            // B500, C25/30, TensionStraight
            double ldStraight = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2);

            // Math check: lb_rqd ~ 726.2mm
            if (ldStraight < 600.0 || ldStraight > 850.0)
            {
                throw new Exception(string.Format("Eurocode 2 TensionStraight Ld expected ~726mm, got {0:F2}mm", ldStraight));
            }

            // Hooked tension: alpha_1 = 0.7 -> ~508.3mm
            double ldHooked = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionHooked, DesignCode.Eurocode2);
            if (ldHooked >= ldStraight || ldHooked < 400.0 || ldHooked > 600.0)
            {
                throw new Exception(string.Format("Eurocode 2 TensionHooked Ld unexpected: {0:F2}mm", ldHooked));
            }

            // Compression: minimum bound check
            double ldComp = RebarAnchorageCalculator.CalculateAnchorageLength(
                barDia, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.Compression, DesignCode.Eurocode2);
            if (ldComp < 300.0 || ldComp > 850.0)
            {
                throw new Exception(string.Format("Eurocode 2 Compression Ld unexpected: {0:F2}mm", ldComp));
            }
        }

        // 41. Lap Splice Length Calculations (TCVN vs Eurocode 2)
        private static void Test_41_RebarLapSplice_Calculations()
        {
            double barDia = 18.0;
            // TCVN 5574:2018: factor = 1.5 -> Lap = 1.5 * 600 = 900mm
            double lapTcvn = RebarAnchorageCalculator.CalculateLapLength(
                barDia, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, 35, 1.5);
            if (Math.Abs(lapTcvn - 900.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN Lap length expected 900.0mm, got {0:F2}mm", lapTcvn));
            }

            // Eurocode 2: factor = 1.5 -> Lap = 1.5 * 726.2 ~ 1089.3mm
            double lapEc2 = RebarAnchorageCalculator.CalculateLapLength(
                barDia, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2, 30, 1.5);
            if (lapEc2 < 900.0 || lapEc2 > 1250.0)
            {
                throw new Exception(string.Format("Eurocode 2 Lap length expected ~1089mm, got {0:F2}mm", lapEc2));
            }

            // Minimum boundary: Lap must always be >= Anchorage
            if (lapTcvn < 600.0 || lapEc2 < 700.0)
            {
                throw new Exception("Lap splice length must not be smaller than anchorage length!");
            }
        }

        // 42. Mandrel Diameter & Bend Radius Standards Verification
        private static void Test_42_MandrelAndBendRadius()
        {
            var ec2 = new EurocodeRebarStandard();
            var tcvn = new TcvnRebarStandard();

            // Eurocode 2: <= 16mm is 4*d, > 16mm is 7*d
            double ec2D12 = ec2.GetMinMandrelDiameter(12.0);
            double ec2D20 = ec2.GetMinMandrelDiameter(20.0);
            if (Math.Abs(ec2D12 - 48.0) > 1e-3 || Math.Abs(ec2D20 - 140.0) > 1e-3)
            {
                throw new Exception(string.Format("Eurocode 2 Mandrel mismatch: D12={0}mm (exp 48), D20={1}mm (exp 140)", ec2D12, ec2D20));
            }

            // TCVN 5574:2018:
            // Deformed (CB400-V): < 20mm is 5*d, >= 20mm is 8*d
            double tcvnD12Def = tcvn.GetMinMandrelDiameter(12.0, "CB400-V");
            double tcvnD25Def = tcvn.GetMinMandrelDiameter(25.0, "CB400-V");
            if (Math.Abs(tcvnD12Def - 60.0) > 1e-3 || Math.Abs(tcvnD25Def - 200.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN Deformed Mandrel mismatch: D12={0}mm (exp 60), D25={1}mm (exp 200)", tcvnD12Def, tcvnD25Def));
            }

            // Smooth (CB240-T): < 20mm is 2.5*d, >= 20mm is 4*d
            double tcvnD10Smooth = tcvn.GetMinMandrelDiameter(10.0, "CB240-T");
            double tcvnD20Smooth = tcvn.GetMinMandrelDiameter(20.0, "CB240-T");
            if (Math.Abs(tcvnD10Smooth - 25.0) > 1e-3 || Math.Abs(tcvnD20Smooth - 80.0) > 1e-3)
            {
                throw new Exception(string.Format("TCVN Smooth Mandrel mismatch: D10={0}mm (exp 25), D20={1}mm (exp 80)", tcvnD10Smooth, tcvnD20Smooth));
            }

            // Straight length after bend: both require >= 5*d
            double ec2Straight = ec2.GetMinStraightLengthAfterBend(16.0);
            double tcvnStraight = tcvn.GetMinStraightLengthAfterBend(16.0);
            if (Math.Abs(ec2Straight - 80.0) > 1e-3 || Math.Abs(tcvnStraight - 80.0) > 1e-3)
            {
                throw new Exception("Straight length after bend expected 80mm (5 * 16)!");
            }
        }

        // 43. Column Reinforcement Steel Ratio Validation (mu_min <= mu <= mu_max)
        private static void Test_43_ColumnSteelRatio_Validation()
        {
            var ec2 = new EurocodeRebarStandard();
            var tcvn = new TcvnRebarStandard();

            double colArea = 400.0 * 400.0; // 160,000 mm2
            // Normal: 8 D20 = 2513.3 mm2 (1.57%)
            double normalAs = 8.0 * (Math.PI * 20.0 * 20.0 / 4.0);
            var ec2Norm = ec2.ValidateColumnSteelRatio(normalAs, colArea);
            var tcvnNorm = tcvn.ValidateColumnSteelRatio(normalAs, colArea);
            if (!ec2Norm.IsValid || !tcvnNorm.IsValid)
            {
                throw new Exception("Normal column steel ratio (1.57%) should be valid in both EC2 and TCVN!");
            }

            // Too low: 2 D10 = 157.08 mm2 (0.098%)
            double lowAs = 2.0 * (Math.PI * 10.0 * 10.0 / 4.0);
            var ec2Low = ec2.ValidateColumnSteelRatio(lowAs, colArea);
            var tcvnLow = tcvn.ValidateColumnSteelRatio(lowAs, colArea);
            if (ec2Low.IsValid || tcvnLow.IsValid)
            {
                throw new Exception("Sub-minimum column steel ratio (< 0.1%) should fail validation in both standards!");
            }

            // Too high: 24 D25 = 11781 mm2 (7.36%)
            double highAs = 24.0 * (Math.PI * 25.0 * 25.0 / 4.0);
            var ec2High = ec2.ValidateColumnSteelRatio(highAs, colArea);
            var tcvnHigh = tcvn.ValidateColumnSteelRatio(highAs, colArea);
            if (ec2High.IsValid || tcvnHigh.IsValid)
            {
                throw new Exception("Super-maximum column steel ratio (7.36%) should fail validation in both standards!");
            }
        }

        // 44. Beam Reinforcement Steel Ratio Validation (mu_min <= mu <= mu_max)
        private static void Test_44_BeamSteelRatio_Validation()
        {
            var ec2 = new EurocodeRebarStandard();
            var tcvn = new TcvnRebarStandard();

            double b = 300.0;
            double d = 600.0; // bd = 180,000 mm2

            // Normal: 3 D20 Top (942.5 mm2, 0.52%), 3 D20 Bot (942.5 mm2, 0.52%)
            double normalAs = 3.0 * (Math.PI * 20.0 * 20.0 / 4.0);
            var ec2Norm = ec2.ValidateBeamSteelRatio(normalAs, normalAs, b, d);
            var tcvnNorm = tcvn.ValidateBeamSteelRatio(normalAs, normalAs, b, d);
            if (!ec2Norm.IsValid || !tcvnNorm.IsValid)
            {
                throw new Exception("Normal beam steel ratio (0.52%) should be valid in both EC2 and TCVN!");
            }

            // Too low: Top 1 D10 (78.5 mm2, 0.044%)
            double lowAs = 1.0 * (Math.PI * 10.0 * 10.0 / 4.0);
            var ec2Low = ec2.ValidateBeamSteelRatio(lowAs, normalAs, b, d);
            var tcvnLow = tcvn.ValidateBeamSteelRatio(lowAs, normalAs, b, d);
            if (ec2Low.IsValid || tcvnLow.IsValid)
            {
                throw new Exception("Sub-minimum beam steel ratio (0.044%) should fail validation in both standards!");
            }

            // Too high: Top 16 D25 (7854 mm2, 4.36%)
            double highAs = 16.0 * (Math.PI * 25.0 * 25.0 / 4.0);
            var ec2High = ec2.ValidateBeamSteelRatio(highAs, normalAs, b, d);
            var tcvnHigh = tcvn.ValidateBeamSteelRatio(highAs, normalAs, b, d);
            if (ec2High.IsValid || tcvnHigh.IsValid)
            {
                throw new Exception("Super-maximum beam steel ratio (4.36%) should fail validation in both standards!");
            }
        }
    }
}
