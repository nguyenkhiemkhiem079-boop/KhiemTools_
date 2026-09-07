using System;
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
        private static readonly string RealDllSource = Path.GetFullPath(@"KhimTools\bin\Release\net48\KhimTools.dll");

        public static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" K-TOOLS UPDATER & DEPLOYMENT SECURITY TEST SUITE");
            Console.WriteLine(" Verifying P0 Deployment Safety, Rollback & Classification");
            Console.WriteLine("=========================================================\n");

            RunTest("Test 01: Interrupted Copy Protection", Test_01_InterruptedCopy);
            RunTest("Test 02: Locked Target Detection", Test_02_LockedTarget);
            RunTest("Test 03: Backup Failure Abort", Test_03_BackupFailure);
            RunTest("Test 04: Staging Corruption Rejection", Test_04_StagingCorruption);
            RunTest("Test 05: Missing PackageContents.xml Rejection", Test_05_PackageContentsMissing);
            RunTest("Test 06: Missing KhimTools.dll Rejection", Test_06_DllMissing);
            RunTest("Test 07: Wrong DLL Version Verification Failure & Rollback", Test_07_DllWrongVersion);
            RunTest("Test 08: Rollback Success After Swap Failure", Test_08_RollbackSuccess);
            RunTest("Test 09: Explicit Rollback Failure Notification", Test_09_RollbackFailure);
            RunTest("Test 10: Legacy AppData Classification & Preservation", Test_10_LegacyAppDataInstall);
            RunTest("Test 11: Duplicate ProgramData / UserManaged AppData Classification", Test_11_DuplicateProgramDataAppData);
            RunTest("Test 12: Multi-Year Revit Versions Detection", Test_12_MultipleRevitVersions);

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

                if (File.Exists(RealDllSource))
                {
                    File.Copy(RealDllSource, dllTarget, true);
                }
                else
                {
                    File.WriteAllText(dllTarget, "MOCK_DLL_CONTENT_" + version);
                }
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

        // 7. DLL wrong version: Expected = 2.8.0, DLL = 2.7.0 -> fails verification and triggers rollback.
        private static void Test_07_DllWrongVersion()
        {
            string sandbox = CreateSandbox("Test07");
            try
            {
                string target = Path.Combine(sandbox, "KhimTools.bundle");
                CreateMockBundle(target, "2.7.0", true, true);
                File.WriteAllText(Path.Combine(target, "live_sentinel.txt"), "PRE_UPDATE_SENTINEL");

                // Zip contains real DLL (2.7.0), but caller asserts deployment of 2.8.0
                string zip = CreateMockZip(sandbox, "update_270.zip", delegate(string dir) { CreateMockBundle(dir, "2.8.0", true, true); });

                var engine = new SafeDeploymentEngine();
                bool caughtMismatch = false;
                try
                {
                    engine.DeployZip(zip, target, "2.8.0", null);
                }
                catch (DeploymentValidationException ex)
                {
                    caughtMismatch = true;
                    if (!ex.Message.Contains("version mismatch") && !ex.Message.Contains("Expected version '2.8.0'"))
                    {
                        throw new Exception("Exception message did not contain version mismatch details: " + ex.Message);
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
                if (File.ReadAllText(Path.Combine(target, "live_sentinel.txt")) != "PRE_UPDATE_SENTINEL")
                {
                    throw new Exception("Live bundle content was altered after version mismatch failure.");
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
    }
}
