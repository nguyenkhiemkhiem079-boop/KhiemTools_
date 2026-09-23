param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = (Join-Path ${env:ProgramFiles} "Autodesk/Revit 2023")
)
$ErrorActionPreference = "Stop"
# This is a metadata-only standalone reflection check; newer RevitAPIUI builds require a live
# Revit UI process, so the supported net48 reflection harness uses the installed Revit 2023 API.
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
foreach ($name in @("RevitAPI.dll", "RevitAPIUI.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$ribbon = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Core/RibbonBuilder.cs"))
$workspace = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/Workspace/ViewModels/KhimWorkspaceViewModel.cs"))
$commands = @([regex]::Matches("$ribbon $workspace", '"(KhimTools\.[\w.]+\.Cmd\w+)"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($commands.Count -lt 60) { throw "Command discovery unexpectedly returned only $($commands.Count) commands." }
foreach ($name in $commands) {
    $type = $assembly.GetType($name, $true)
    if (!$type.IsPublic -or $type.IsAbstract -or ![Autodesk.Revit.UI.IExternalCommand].IsAssignableFrom($type) -or
        $null -eq $type.GetConstructor([Type[]]@())) {
        throw "Invalid Revit command entry point: $name"
    }
}
$viewModel = $assembly.GetType("KhimTools.Tools.Workspace.ViewModels.KhimWorkspaceViewModel", $true)
$pane = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/Workspace/Views/KhimWorkspacePane.xaml"))
$bindings = @([regex]::Matches($pane, 'Command="\{Binding (\w+)\}"') | ForEach-Object {$_.Groups[1].Value})
foreach ($name in $bindings) {
    if ($null -eq $viewModel.GetProperty($name)) { throw "Broken workspace command binding: $name" }
}

# Check ownership boundaries, not only whether a command class exists.
function Method-Body([string]$name) {
    $match = [regex]::Match($ribbon, "private static void $name\([^\r\n]*\)\s*\{")
    if (!$match.Success) { throw "Missing ribbon builder: $name" }
    $next = $ribbon.IndexOf("private static ", $match.Index + $match.Length)
    if ($next -lt 0) { $next = $ribbon.Length }
    $ribbon.Substring($match.Index, $next - $match.Index)
}
$layout = Method-Body "BuildLayoutPanel"
if ($layout -match "CmdFamilyManager|CmdSlabStep|CmdSheetExport") { throw "Model/library/publish command misplaced in Layout." }
$gen = Method-Body "BuildGenPanel"
if ($gen -notmatch "CmdFamilyManager" -or $gen -notmatch "CmdSlabStep") { throw "K-GEN is missing model/library commands." }
$workspaceBody = Method-Body "BuildWorkspacePanel"
if ([regex]::Matches($workspaceBody, "new PushButtonData").Count -ne 1 -or $workspaceBody -notmatch "CmdToggleWorkspace") {
    throw "Workspace must only toggle the dockable pane."
}
if ((Method-Body "BuildPublishPanel") -notmatch "CmdSheetExport") { throw "Publish is missing Print/Export." }
Write-Host "PASS: $($commands.Count) compiled Revit command entry points; $($bindings.Count) workspace bindings; ribbon ownership boundaries."
Write-Host "Metadata only: command Execute methods were NOT run."
