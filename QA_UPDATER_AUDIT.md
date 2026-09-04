# QA UPDATER AUDIT — K-TOOLS COMMERCIAL UPDATE SYSTEM
**Document ID:** QA-UPDATER-AUDIT-001  
**Target Solution:** `KhimTools.sln` / `K-TOOLS`  
**Date:** 2026-09-05  
**Auditor:** Senior C# / Revit API / Systems Architect  
**Status:** FORENSIC AUDIT COMPLETED — P0 REFACTOR MANDATE  

---

## 1. EXECUTIVE SUMMARY

K-TOOLS is transitioning to a **commercial production-grade product**. As part of this transition, the update system must guarantee **zero corrupted installations, zero lost plugins ("K-TOOLS disappeared"), zero stale DLLs, cryptographic integrity (SHA256), atomic installations, and guaranteed automatic rollback**.

A comprehensive forensic audit of the entire repository reveals that the current update mechanism suffers from a **dual-implementation architecture discrepancy**, severe file-locking vulnerabilities, lack of cryptographic verification, hardcoded fallback versions, and an incomplete in-Revit download flow that downloads files to `%TEMP%` and abandons them without installation.

This document details the forensic audit findings across all updater components, root causes of updater instability, and specifies the required production-grade architecture.

---

## 2. AUDIT INVENTORY: CURRENT ARTIFACTS & IMPLEMENTATIONS

| Component | Location | Role / Status |
|---|---|---|
| **In-Revit Command** | `KhimTools/Tools/KhimGen/Updater/Commands/CmdCheckUpdate.cs` | Triggered from Ribbon / Workspace to check updates |
| **In-Revit Service** | `KhimTools/Tools/KhimGen/Updater/Services/UpdateService.cs` | Fetches `update_info.json`, downloads to `%TEMP%` |
| **In-Revit Model** | `KhimTools/Tools/KhimGen/Updater/Models/UpdateInfo.cs` | Data contract with hardcoded fallback versions |
| **In-Revit View** | `KhimTools/Tools/KhimGen/Updater/Views/UpdaterWindow.xaml/.cs` | WPF dialog showing download progress; no install action |
| **Standalone Updater App** | `KhimTools/App/KhiemToolsApp.csproj` | Produces `KhimTools_Installer.exe` (WinExe, dual net48/net8.0) |
| **Standalone Updater Window** | `KhimTools/App/AppUpdaterWindow.xaml/.cs` | Standalone UI, kills Revit, downloads & extracts directly over bundle |
| **Release Manifest** | `update_info.json` (root) | Outdated manifest (`v2.7.0`), missing hashes and build metadata |
| **Autodesk Bundle Manifest** | `KhimTools/Deploy/PackageContents.xml` | Package manifest defining components for Revit 2020-2028 |
| **Addin Manifests** | `KhimTools/Deploy/Modern/KhimTools.addin`, `Deploy/Legacy/KhimTools.addin` | Manifests referencing `KhimTools.dll` |
| **Build/Obfuscation Script** | `KhimTools/Deploy/Obfuscate.ps1` | Obfuscation and bundle assembly script |
| **Startup Diagnostics** | `KhimTools/Core/RegistrationDiagnostics.cs` & `App.cs` | Records loaded assembly location and registration health |

---

## 3. CURRENT ARCHITECTURE & FLOW ANALYSIS

### 3.1 Flow A: In-Revit Updater Flow (`UpdateService.cs` + `UpdaterWindow.xaml.cs`)
```
Revit Ribbon / Workspace ("Check Update")
              ↓
CmdCheckUpdate.Execute()
              ↓
UpdateService.CheckForUpdatesAsync()
  - WebClient GET raw.githubusercontent.com/.../update_info.json
  - CATCH block fallback: Hardcoded "v2.7.0", latest release URL
              ↓
UpdaterWindow opens
  - Displays CurrentVersion and LatestVersion
  - User clicks "Cập nhật" (BtnAction)
              ↓
UpdateService.DownloadAndStageUpdateAsync()
  - Downloads to Path.Combine(Path.GetTempPath(), "KhimTools_Update.zip")
              ↓
Download Completes
  - Sets BtnAction.Content = "Đóng"
  - PanelStatus.Visibility = Visible
              ↓
User clicks "Đóng" -> Window closes
  - [CRITICAL DEFECT]: ZIP remains in %TEMP% and is NEVER extracted or installed!
```

### 3.2 Flow B: Standalone Installer Flow (`AppUpdaterWindow.xaml.cs`)
```
User launches KhimTools_Installer.exe
              ↓
CheckCurrentLocalVersion()
  - Heuristics: checks update_info.json in bundle -> installed_version.txt
    -> regex on PackageContents.xml -> FileVersionInfo / AssemblyVersion on 4 DLL paths
              ↓
User clicks "Kiểm tra cập nhật" (BtnCheckUpdate)
  - Layer 1: GET raw.githubusercontent.com/.../update_info.json?t={ticks}
  - Layer 2 (fallback): GET api.github.com/repos/.../releases/latest
  - Layer 3 (fallback): Hardcodes latestTag = "v2.7.0"
              ↓
User prompts "Cập nhật ngay" -> Yes
              ↓
EnsureRevitClosed()
  - Looks for process "Revit". If found, prompts user.
  - If user selects Yes: calls p.CloseMainWindow(), waits 3000ms, then p.Kill()
  - Thread.Sleep(1000) for OS lock release
              ↓
PerformInstallOrUpdateAsync()
  - Deletes and creates %TEMP%\KhimTools_Installer\
  - Downloads bundle.zip (via directZipUrl or GitHub releases/download/{tag})
  - Calls DeployZipToTargets(bundleZipPath, tag)
              ↓
DeployZipToTargets()
  - CleanLegacyAddinFiles(): Deletes %AppData% bundle & .addin files in Revit 2020-2028
  - Copies %ProgramData% KhimTools.bundle to KhimTools.bundle_backup
  - Calls ExtractZipSafely(zipFilePath, targetBundle) directly over production bundle!
  - Checks if Legacy/KhimTools.dll or Modern/KhimTools.dll exists
    - If missing: restores from backup, throws exception
    - If present: writes installed_version.txt, deletes _backup directory
```

---

## 4. CURRENT DEFECTS & CRITICAL RISKS

### 4.1 In-Process Update Dead-End
- In Revit, `CmdCheckUpdate` invokes `UpdateService.DownloadAndStageUpdateAsync()`, which downloads the ZIP to `%TEMP%\KhimTools_Update.zip`.
- **Once downloaded, it does absolutely nothing.** It displays "Đóng" (Close). The user is under the impression that an update was performed, but Revit continues running the old DLL, and the downloaded file is abandoned in `%TEMP%`.

### 4.2 Destructive Direct In-Place Extraction (`KhimTools_Installer.exe`)
- `ExtractZipSafely` writes directly into `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle` while iterating entries.
- If a file is locked by a background Revit worker, Revit accelerator, or security scanner:
  - An `IOException` occurs halfway through extraction.
  - The bundle is left in a **half-updated, corrupted state** (mix of new and old DLLs, or missing DLLs).
  - The user opens Revit, and K-TOOLS fails to load or crashes on startup.

### 4.3 Hardcoded Versions and "False Latest" Fallbacks
- `UpdateInfo.cs` line 10: `LatestVersion { get; set; } = "v2.7.0";`
- `UpdateService.cs` line 49: `LatestVersion = "v2.7.0"` in the catch block!
- `AppUpdaterWindow.xaml.cs` line 285: `if (string.IsNullOrEmpty(latestTag)) latestTag = "v2.7.0";`
- **Violation:** If the user has no internet or GitHub API is unreachable, the system falsely reports that `v2.7.0` is the latest version. If the server is unreachable, the status MUST be `UNKNOWN`, never a hardcoded historical release.

### 4.4 Absence of SHA256 & Cryptographic Verification
- Neither `UpdateService` nor `AppUpdaterWindow` computes or checks SHA256 hashes.
- A corrupted download, incomplete packet stream, proxy cache corruption, or compromised mirror could lead to arbitrary binary execution or corrupt Revit startup.

### 4.5 Disconnect Between Source Code, Release Tag, and Manifest
- As discovered in our forensic audit:
  - Repository source code is actively developed with fixes (e.g. Ribbon failure isolation, Rebar containment validation).
  - But `update_info.json` points to an old release (`v2.7.0`, August 2026).
  - There is no Build ID or Git Commit tracking in the update metadata.
  - Two builds with the same version number cannot be distinguished.

### 4.6 Workspace Command Invocation Namespace Mismatch
- In `KhimWorkspaceViewModel.cs` line 168:
  ```csharp
  RunCommandByName(app, "KhimTools.Tools.Updater.Commands.CmdCheckUpdate");
  ```
- But in `CmdCheckUpdate.cs` line 8:
  ```csharp
  namespace KhimTools.Updater.Commands
  ```
- Result: Clicking "Check Update" from the Khim Workspace Pane fails silently because reflection cannot find the class `KhimTools.Tools.Updater.Commands.CmdCheckUpdate`.

### 4.7 Duplicate Bundles & Stale DLL Collisions
- Autodesk Revit searches for Application Plugins in:
  1. `%ProgramData%\Autodesk\ApplicationPlugins\`
  2. `%AppData%\Autodesk\ApplicationPlugins\`
  3. `%AppData%\Autodesk\Revit\Addins\{year}\`
  4. `%ProgramData%\Autodesk\Revit\Addins\{year}\`
- If an old install exists in `%AppData%` while a new install is in `%ProgramData%`, Revit will either:
  - Load the old DLL first (ignoring the update).
  - Throw duplicate `AddInId` GUID collision warnings on Revit startup (`4F1B2C3D-5E6F-4A7B-8C9D-0E1F2A3B4C5D`).
  - Result in "stale DLL" behavior where updates appear not to take effect.

### 4.8 Brittle Revit Process Termination
- `EnsureRevitClosed()` attempts `p.CloseMainWindow()`, waits 3 seconds, then calls `p.Kill()`.
- Killing Revit forcefully can cause data loss for end users who had unsaved models.
- If Revit is running multiple background worker threads or child processes (`RevitWorker.exe`), file locks on `KhimTools.dll` may persist beyond the 1-second sleep.

---

## 5. ROOT CAUSES

1. **Lack of Staging Architecture:** No isolated staging folder (`%LocalAppData%\KTools\Updates\{version}\`). Downloads were directed to general `%TEMP%` or directly extracted into the live bundle.
2. **Missing Out-of-Process Update Worker:** The in-Revit code cannot update itself because Windows NT file locking prevents overwriting loaded DLLs.
3. **No Transactional Atomic Swap:** Updates were performed via direct file copies rather than directory swap/rename operations.
4. **No Cryptographic Manifest:** Absence of SHA256, Git commit, Build timestamp, and Revit version matrix in `update_info.json`.
5. **No Automated Rollback Guarantee:** Existing backup code simply copied files to `_backup` and deleted them immediately upon partial ZIP extraction without verifying actual assembly loadability.

---

## 6. RECOMMENDED PRODUCTION-GRADE ARCHITECTURE

```
+-------------------------------------------------------------------------+
|                        IN-REVIT CLIENT (KhimTools.dll)                  |
+-------------------------------------------------------------------------+
|  1. Periodic / Manual Check -> Fetch Update Manifest (HTTPS)            |
|  2. If Update Available -> Background Download to Isolated Staging:     |
|     %LocalAppData%\KTools\Updates\{version}\package.zip                 |
|  3. Validate Staging Package:                                           |
|     - Check File Exists & Size                                          |
|     - Compute & Compare SHA256 Hash against Manifest                    |
|     - Test-extract to Staging Folder & verify:                          |
|         * PackageContents.xml exists and version matches                |
|         * Contents/Legacy/KhimTools.dll & Contents/Modern/KhimTools.dll |
|         * Assembly / FileVersion matches manifest version               |
|         * Current Revit version (e.g. 2025/2026) is in supportedRevit   |
|  4. Status -> "UPDATE READY"                                            |
|  5. User Dialog: [ Restart & Update ]  [ Later ]                        |
|     - If "Later": Abort without touching active installation            |
|     - If "Restart & Update": Launch KToolsUpdater.exe with arguments,   |
|       pass Revit PID, then exit Revit gracefully.                       |
+-------------------------------------------------------------------------+
                                    │
                                    │ Launches out-of-process
                                    ▼
+-------------------------------------------------------------------------+
|                   EXTERNAL UPDATER (KToolsUpdater.exe)                  |
+-------------------------------------------------------------------------+
|  1. Wait for Revit Process (PID) to exit completely + verify file locks |
|  2. Pre-installation Scan:                                              |
|     - Detect & log duplicate bundles in %AppData% and %ProgramData%     |
|     - Clean legacy .addin conflicts in %AppData%/Revit/Addins           |
|  3. Create Backup Snapshot of Current Bundle:                           |
|     %LocalAppData%\KTools\Backups\{current_version}_{timestamp}\        |
|  4. Atomic Installation:                                                |
|     - Move/Copy verified staged package to destination bundle           |
|     - Write metadata (installed_manifest.json)                          |
|  5. Post-Installation Verification:                                     |
|     - Verify target DLLs exist, readable, correct SHA256 / FileVersion  |
|     - Verify PackageContents.xml syntax & version                       |
|  6. Rollback Mechanism (Triggered on ANY verification error):           |
|     - Restore previous bundle from Backup Snapshot                      |
|     - Verify restored version                                           |
|     - Log error in %LocalAppData%\KTools\Logs\update_{timestamp}.log    |
|     - Alert user of rollback with exact error                           |
|  7. Launch Revit (if requested by user)                                 |
+-------------------------------------------------------------------------+
```

---

## 7. AUDIT CONCLUSION & NEXT ACTIONS

The current update logic cannot be patched with quick fixes; it requires the structural refactoring specified above to meet commercial software reliability standards.

The refactoring will be executed according to the strict implementation plan:
1. Manifest & Version Model definition (`KToolsUpdateManifest.cs`).
2. Verification Engine (`PackageVerificationService.cs` with SHA256, ZIP structure, and DLL inspection).
3. Staging Manager (`StagingManager.cs` managing `%LocalAppData%\KTools\Updates\`).
4. Standalone Updater Worker (`KToolsUpdater.exe` with backup, atomic swap, and rollback).
5. In-Revit UX update (`UpdateService.cs` + `UpdaterWindow.xaml` with "Restart & Update" flow).
6. Startup Diagnostics Enhancement (verifying loaded DLL, commit, build ID, and duplicate bundle warnings).
