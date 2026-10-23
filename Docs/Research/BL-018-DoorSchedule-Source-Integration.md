# BL-018 — Door/Window Schedule source integration

## Provenance

- Integrated source: user-provided `K-TOOL-DoorSchedule-Revit2025 (1).zip`.
- ZIP SHA-256: `AEF7D90C9D0DE10F4F0227A59582E63314AE1F83BAEDE446C7B6A169A54969F0`.
- Package-declared version: 1.0.1; target: Revit 2025, Windows x64, .NET 8.
- The extracted `source/src` tree contains 13 files. Each file's SHA-256 matches its corresponding `source/src` member in the ZIP.
- The archive's precompiled DLL SHA-256 is `A7C212F6DEFC5454850173A79ED6AB5161ACE75E600BAA19CB79B735D5D677B2`. That binary was not copied into the repository, loaded, or executed; the repository builds from the matched source.
- The local compile produced a separate module DLL with SHA-256 `EFC4AC3E63C5316E141277D8CFD89B5F729722E3C6F67BCFF7705C9165A148D4`, distinct from the archive's binary hash.
- The package's reference provenance document describes use of Autodesk-signed Revit 2025 API references distributed through the community Nice3point NuGet package when the Revit 2025 installation is unavailable. The repository build on this machine resolved the installed Revit 2025 API references.

## Integration shape

The source is preserved as a separate project at `KhimTools/Tools/KhimArchitectural/DoorWindowScheduleStandalone`. The main K-TOOLS project references it only for `net8.0-windows` builds using Revit 2025 references. It is excluded from the main project's compile glob, so the command remains in its own assembly.

The Architecture ribbon points to `KhimTools.DoorSchedule.Revit2025.dll` only when the build contains the Revit 2025 integration symbol and the running host reports version 2025. The existing Modern bundle copy target will copy the module DLL when bundle deployment runs, and the Modern MSI component declares it as a payload. Neither bundle deployment nor MSI build was run for this update. The standalone command is not exposed on Revit 2022–2024 or 2026.

## Compile record

Command:

```powershell
dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --verbosity minimal -t:Rebuild
```

Result: exit 0; both `KhimTools.dll` and `KhimTools.DoorSchedule.Revit2025.dll` were produced; 0 errors and 12 warnings. Ten warnings are the existing K-TOOLS `ColumnWallRelatedFormV1.Load()` / `ElementId(int)` warnings. Two `MSB3277` warnings in the standalone project describe Microsoft.VisualBasic/System.Drawing metadata conflicts among Revit host references and .NET reference assemblies. These are compile-time warnings; the build does not establish host resolution.

## Acceptance still open

No Revit 2025 session, Add-in Manager load, model, schedule, CSV, phase/filter, sheet placement, worksharing, update, or Undo operation was run for this integration. Door counts, Type Marks, shared parameter mapping, and native schedule membership remain `NOT TESTED`. The source package's tests and build scripts were not executed. This record is source provenance and compile evidence only, not BL-018 acceptance or release authorization.
