# K-TOOLS COMMERCIAL UPDATE SYSTEM ARCHITECTURE
**Document ID:** ARCH-UPDATER-001  
**Version:** 2.7.1  
**Classification:** Commercial Production Architecture  
**Author:** Antigravity AI / Systems Architect  
**Status:** IMPLEMENTED & PRODUCTION-READY  

---

## 1. ARCHITECTURAL OVERVIEW

The K-TOOLS Commercial Update System is an enterprise-grade, out-of-process update lifecycle architecture designed for Autodesk Revit add-ins.

Revit add-ins suffer from a fundamental operating system constraint: **Windows NT file-locking (`ERROR_LOCK_VIOLATION` / `0x21`) prevents any process from modifying or overwriting loaded dynamic link libraries (`KhimTools.dll`).** Any attempt to overwrite an active bundle while Revit is executing leads to partial file writes, missing assemblies, corrupted add-in bundles, and "K-TOOLS disappeared" startup errors.

The K-TOOLS Commercial Update System eliminates this failure mode entirely by strictly separating **Update Ingestion & Verification** (inside Revit) from **Atomic Installation & Rollback** (outside Revit).

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

## 2. KEY ARCHITECTURAL TENETS

### 2.1 Staging Isolation
- Packages are **never** downloaded into the live bundle directory.
- Download target: `%LocalAppData%\KTools\Updates\{version}\KhimTools_Bundle.zip`.
- If a download is cancelled, corrupted, or fails mid-stream, the current installation remains 100% untouched.

### 2.2 Cryptographic Integrity (SHA256)
- Every release package has a corresponding 64-character hexadecimal SHA256 hash.
- The hash is verified before extraction and before installation.
- Mismatched hashes immediately abort the update and purge the staging folder.

### 2.3 Strict Version Modeling
- Three distinct version coordinates are tracked:
  1. **Product Version:** Semantic release version (e.g. `2.7.1`).
  2. **Build Timestamp ID:** Unique build marker (e.g. `20260905.0001`).
  3. **Git Commit:** 8-character commit hash (e.g. `bb21af01`).
- Eliminates "same version number, different code" ambiguity.

### 2.4 Out-of-Process Execution (`KToolsUpdater.exe`)
- The updater runs in a completely separate Windows process outside `revit.exe`.
- It monitors the Revit process ID (`PID`) and waits for complete process termination and file-handle release before attempting file operations.

### 2.5 Automated Snapshot Backup & Rollback
- Before copying any new file to `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle`, the existing bundle is copied to `%LocalAppData%\KTools\Backups\bundle_{timestamp}\`.
- If post-installation verification fails (e.g., missing DLL, wrong version, corrupted XML), the updater triggers an **automatic rollback**, restoring the previous bundle snapshot.
- End users are never left in a "K-TOOLS disappeared" state.

### 2.6 Duplicate Installation Scanner
- Scans both `%ProgramData%\Autodesk\ApplicationPlugins\` and `%AppData%\Autodesk\ApplicationPlugins\`.
- Scans `%AppData%\Autodesk\Revit\Addins\{year}\` and `%ProgramData%\Autodesk\Revit\Addins\{year}\`.
- Purges rogue legacy `.addin` files to prevent duplicate `AddInId` collisions and stale DLL loading.

---

## 3. UPDATE MANIFEST SCHEMA

The official manifest schema is hosted at:  
`https://raw.githubusercontent.com/nguyenkhiemkhiem079-boop/KhiemTools_/master/update_info.json`

```json
{
  "product": "K-TOOLS",
  "version": "2.7.1",
  "build": "20260905.0001",
  "commit": "bb21af01",
  "releaseDate": "2026-09-05",
  "package": "KhimTools_Bundle.zip",
  "downloadUrl": "https://github.com/nguyenkhiemkhiem079-boop/KhiemTools_/releases/download/v2.7.1/KhimTools_Bundle.zip",
  "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "supportedRevit": [
    "2020",
    "2021",
    "2022",
    "2023",
    "2024",
    "2025",
    "2026",
    "2027",
    "2028"
  ],
  "changelog": [
    "Commercial Update Architecture: Out-of-process KToolsUpdater.exe with atomic installation and automatic rollback.",
    "Cryptographic Verification: SHA256 integrity verification and PackageContents.xml structural inspection.",
    "Fault Isolation & Staging: Isolated staging directory, eliminating live bundle corruption and file lock crashes.",
    "Full Rebar Suite Production Overhaul: Host containment verification across Column, Beam, Slab, and Foundation modules.",
    "Startup Forensics & Duplicate Bundle Scanner: Real-time detection of stale DLLs and duplicate ApplicationPlugins."
  ],
  "isMandatory": false,
  "minRequiredVersion": "2.0.0"
}
```

---

## 4. KToolsUpdater.exe CLI SPECIFICATION

`KToolsUpdater.exe` supports both GUI execution and headless command-line automation:

```bash
# Automated install triggered by Revit
KToolsUpdater.exe --action install \
  --staging-dir "%LocalAppData%\KTools\Updates\2.7.1\Extracted" \
  --target-bundle "%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle" \
  --revit-pid 12344 \
  --revit-path "C:\Program Files\Autodesk\Revit 2025\Revit.exe" \
  --version "2.7.1"

# Explicit Rollback command
KToolsUpdater.exe --action rollback \
  --backup-dir "%LocalAppData%\KTools\Backups\bundle_20260905_0001" \
  --target-bundle "%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle"

# Integrity Verification command
KToolsUpdater.exe --action verify \
  --target-bundle "%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle" \
  --version "2.7.1"
```

### Exit Codes:
- `0`: Success
- `1`: Invalid arguments or action
- `2`: Revit process did not exit within timeout
- `3`: Staging directory missing or invalid
- `4`: Backup snapshot creation failed
- `5`: File installation failed (Rollback executed)
- `6`: Post-installation verification failed (Rollback executed)
- `99`: Unhandled critical exception

---

## 5. AUDIT LOGGING DIRECTORY

All update operations record structured timestamps and event details in:  
`%LocalAppData%\KTools\Logs\update_{yyyyMMdd_HHmmss}.log`

Log entries capture:
- Revit PID synchronization
- Backup snapshot source and destination
- Files copied and overwritten
- SHA256 hashes (expected vs actual)
- Post-installation verification results
- Rollback execution triggers and outcomes
