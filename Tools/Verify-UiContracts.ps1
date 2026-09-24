param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = (Join-Path ${env:ProgramFiles} "Autodesk/Revit 2024")
)
$ErrorActionPreference = "Stop"
# ReflectionOnlyLoad is available in Windows PowerShell/.NET Framework, not PowerShell Core.
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this metadata verifier with Windows PowerShell 5.1 (powershell.exe); Revit API metadata is .NET Framework-only.'
}
# This check reads metadata only. Reflection-only loading avoids initializing Revit UI/native
# components and binds the verifier to the same net48/Revit 2024 API identity as the assembly.
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$api = $null
$apiUi = $null
$assembly = $null
$resolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    try {
        return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
    } catch {
        $leaf = ([Reflection.AssemblyName]$eventArgs.Name).Name + ".dll"
        foreach ($directory in @($RevitApiDirectory, (Split-Path -Parent (Resolve-Path $AssemblyPath)))) {
            $candidate = Join-Path $directory $leaf
            if (Test-Path -LiteralPath $candidate) {
                try { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate) } catch { }
            }
        }
        return $null
    }
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolveHandler)
foreach ($name in @("RevitAPI.dll", "RevitAPIUI.dll")) {
    $loaded = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $RevitApiDirectory $name))
    if ($name -eq "RevitAPI.dll") { $api = $loaded } else { $apiUi = $loaded }
}
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path $AssemblyPath))
$externalCommandType = $apiUi.GetType("Autodesk.Revit.UI.IExternalCommand", $true)
$ribbon = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Core/RibbonBuilder.cs"))
$workspace = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/Workspace/ViewModels/KhimWorkspaceViewModel.cs"))
$commands = @([regex]::Matches("$ribbon $workspace", '"(KhimTools\.[\w.]+\.Cmd\w+)"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($commands.Count -lt 60) { throw "Command discovery unexpectedly returned only $($commands.Count) commands." }
$contractChecks = 1
foreach ($name in $commands) {
    $type = $assembly.GetType($name, $true)
    if (!$type.IsPublic -or $type.IsAbstract -or !$externalCommandType.IsAssignableFrom($type) -or
        $null -eq $type.GetConstructor([Type[]]@())) {
        throw "Invalid Revit command entry point: $name"
    }
    $contractChecks++
}
$viewModel = $assembly.GetType("KhimTools.Tools.Workspace.ViewModels.KhimWorkspaceViewModel", $true)
$contractChecks++
$pane = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/Workspace/Views/KhimWorkspacePane.xaml"))
$bindings = @([regex]::Matches($pane, 'Command="\{Binding (\w+)\}"') | ForEach-Object {$_.Groups[1].Value})
foreach ($name in $bindings) {
    if ($null -eq $viewModel.GetProperty($name)) { throw "Broken workspace command binding: $name" }
    $contractChecks++
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
$contractChecks++
$gen = Method-Body "BuildGenPanel"
if ($gen -notmatch "CmdFamilyManager" -or $gen -notmatch "CmdSlabStep") { throw "K-GEN is missing model/library commands." }
$contractChecks++
$workspaceBody = Method-Body "BuildWorkspacePanel"
if ([regex]::Matches($workspaceBody, "new PushButtonData").Count -ne 1 -or $workspaceBody -notmatch "CmdToggleWorkspace") {
    throw "Workspace must only toggle the dockable pane."
}
$contractChecks++
if ((Method-Body "BuildPublishPanel") -notmatch "CmdSheetExport") { throw "Publish is missing Print/Export." }
 $contractChecks++
Write-Host "PASS: $($commands.Count) Revit command metadata checks; $($bindings.Count) workspace bindings; 4 ribbon ownership boundaries; $contractChecks contract assertions."
Write-Host "Metadata only: command Execute methods were NOT run."
