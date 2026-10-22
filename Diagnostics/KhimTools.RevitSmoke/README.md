# K-TOOLS Revit 2025 runtime smoke assembly

This is a small, read-only `IExternalCommand` assembly for checking that Revit 2025 can load and invoke a .NET 8 K-TOOLS add-in assembly. It only displays the host and assembly versions. It does not access the active document, create transactions, or alter the model.

## Build

From the repository root:

```powershell
dotnet build Diagnostics/KhimTools.RevitSmoke/KhimTools.RevitSmoke.csproj --configuration Release -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025
```

The assembly is written under `Diagnostics/KhimTools.RevitSmoke/bin/Release/net8.0-windows/`.

## Runtime use

Load `KhimTools.RevitSmoke.dll` with the Revit 2025 Add-in Manager as an external command. A successful dialog proves only assembly load and command invocation. It does not establish product workflow, model geometry, transaction, installer, or production acceptance. Do not use an open production model to run mutation fixtures; this smoke command itself is read-only.
