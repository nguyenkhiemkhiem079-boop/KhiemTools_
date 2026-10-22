# REA installer build evidence

**Recorded:** 2026-10-23  
**Source branch:** `codex/rea-core00-20261023`  
**Source commit:** `fa13ee570bb4e10e7481167978d57d245dcc51c6`  
**Product version:** `2.7.2`  
**Build command:** `.\Installer\Build-Installer.ps1`

## Result

The repository's installer build script completed successfully and generated a WiX MSI plus its bootstrapper. The build compiled the Revit 2024 `net48` and Revit 2025 `net8.0-windows` product targets; both completed with 0 errors (12 and 10 warnings respectively). The payload metadata records source commit `fa13ee570bb4e10e7481167978d57d245dcc51c6`, `SourceHasUncommittedChanges=false`, and product source fingerprint `f580154d44b7eda7c495e7671c47d3dc174e7acedb3da29532a55d5ea31ba865`.

| Artifact | SHA-256 | Authenticode |
|---|---|---|
| `Installer/Output/K-TOOLS.msi` | `69c971f4a7e9ba9818de31213e7043bcfd1d90a5891c04f99a1badd69db5987a` | NotSigned |
| `Installer/Output/K-TOOLS-Setup.exe` | `5775b97a899fe40f246495f45f41451640395a2874878ed4eae818a33d799304` | NotSigned |

The MSI database was opened read-only. Its File and Component tables contain both Rebar Shape manifest payload rows:

| File key | Source name | Size | Component | Target directory |
|---|---|---:|---|---|
| `F_Leg_RebarShapeManifest_v1` | `rebar-shape-assets.v1.sha256` | 3,325 bytes | `C_RebarShapeManifestLegacy` | `LEGACYREBARFOLDER` |
| `F_Mod_RebarShapeManifest_v1` | `rebar-shape-assets.v1.sha256` | 3,325 bytes | `C_RebarShapeManifestModern` | `MODERNREBARFOLDER` |

The packaged manifest SHA-256 is `e67a9bdbc0e978cfabf1da8eb3966c8364929a41736e75a0bdf92772f7665d19`, matching the source manifest and the Revit 2024/2025 build outputs recorded in `REA_RB01_COMPILE_MATRIX.md`.

## Scope and limits

- This verifies build completion and the presence/routing of the manifest rows in the MSI database. It does not verify installation, upgrade, repair, rollback, uninstall, elevation, or Revit add-in discovery.
- The MSI and bootstrapper are unsigned. They are local review artifacts, not release-ready packages.
- No installer was launched or installed. No Revit process, model, command, test suite, or model mutation was used for this evidence.
- Keep the artifacts tied to the source commit and hashes above. Rebuild and record new hashes after any source or packaging change.

The build products and WiX intermediates are in Git-ignored `Installer/Output` and `.wix` folders; they are not committed.
