# RB-01 compile compatibility matrix

**Recorded:** 2026-10-23  
**Branch:** `codex/rea-core00-20261023`  
**Source revision:** `3e188918a4e6a01ae3d071eca947e223dd80ac0d`  
**Scope:** compile-only verification of the K-TOOLS add-in against each locally installed Revit API reference. No Revit process, model, test suite, installer, or host command was run.

## Results

| Revit API | Target framework | Build result | Warnings | Errors |
|---|---|---:|---:|---:|
| 2022 | `net48` | PASS | 2 | 0 |
| 2023 | `net48` | PASS | 2 | 0 |
| 2024 | `net48` | PASS | 12 | 0 |
| 2025 | `net8.0-windows` | PASS | 10 | 0 |
| 2026 | `net8.0-windows` | PASS | 154 | 0 |

No compiler warnings were reported in the BL-012 changed C# files (`RebarShapeAssetIdentity.cs`, `RebarShapeLibrary.cs`, and `RebarShapeLoaderWindow.xaml.cs`). Warnings were reported in other project files; the 2026 target also reports deprecated Rebar hook and curve intersection API usage in existing generator files.

The versioned Rebar Shape manifest copied into both build output folders has SHA-256 `E67A9BDBC0E978CFABF1DA8EB3966C8364929A41736E75A0BDF92772F7665D19`, matching the source manifest.

## Commands

Each command was run from the repository root with `--configuration Release` and `--verbosity quiet`:

```powershell
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net48 -p:RevitVersionForReference=2022 -p:RevitRequestedVersion=2022 -p:DeployKhimToolsBundle=false --verbosity quiet
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net48 -p:RevitVersionForReference=2023 -p:RevitRequestedVersion=2023 -p:DeployKhimToolsBundle=false --verbosity quiet
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net48 -p:RevitVersionForReference=2024 -p:RevitRequestedVersion=2024 -p:DeployKhimToolsBundle=false --verbosity quiet
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --verbosity quiet
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2026 -p:RevitRequestedVersion=2026 -p:DeployKhimToolsBundle=false --verbosity quiet
```

## Remaining acceptance gates

Compile success does not prove Revit runtime behavior. RB-01 still requires an approved detached disposable model for RFA loading, identity persistence across save/reopen, shape and hook compatibility, actual creation, preview/create parity, rollback, and Undo. Revit-host and installer acceptance remain open; Wave 3 stays gated on RB-01 exit evidence.
