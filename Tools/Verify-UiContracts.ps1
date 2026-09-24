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

$dimensionForm = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/DimensionTools/Forms/DimensionToolsForm.cs"))
if ($dimensionForm -notmatch "PreviewLifecycleSession<DimensionPlan>" -or $dimensionForm -notmatch "ValueChanged \+= InputChanged" -or $dimensionForm -notmatch "TryGetValid\(Plan.Fingerprint") {
    throw "Dimension preview must invalidate on request-field changes and gate Apply through the canonical lifecycle."
}
$contractChecks++
$modifyForm = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/ModifyObjects/Forms/ModifyObjectsForm.cs"))
if ($modifyForm -notmatch "PreviewLifecycleSession<ModifyObjectPlan>" -or $modifyForm -notmatch "TextChanged \+= InputChanged" -or $modifyForm -notmatch "Plan.IsStale\(_doc\)" -or $modifyForm -notmatch "TryGetValid\(Plan.Fingerprint") {
    throw "Modify Objects preview must invalidate on request-field changes and reject stale model plans before Apply."
}
$contractChecks++
$parameterForm = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/ParameterManager/Forms/ParameterManagerForm.cs"))
if ($parameterForm -notmatch "PreviewLifecycleSession<ParameterManagerPlan>" -or $parameterForm -notmatch "_scope\.SelectedIndexChanged.*InputChanged" -or $parameterForm -notmatch "_parameterScope\.SelectedIndexChanged.*InputChanged" -or $parameterForm -notmatch "_rule\.SelectedIndexChanged \+= InputChanged" -or $parameterForm -notmatch "_value\.TextChanged \+= InputChanged" -or $parameterForm -notmatch "_onlyMissing\.CheckedChanged \+= InputChanged" -or $parameterForm -notmatch "_strict\.CheckedChanged \+= InputChanged" -or $parameterForm -notmatch "TryGetValid\(Plan\.Fingerprint") {
    throw "Parameter Manager request changes must invalidate its accepted preview, and Apply must require the current plan."
}
$contractChecks++
$modifyPlanBuilder = [IO.File]::ReadAllText((Join-Path $root "KhimTools/Tools/KhimGen/ModifyObjects/Core/ModifyObjectPlanBuilder.cs"))
foreach ($requestField in @("context.Operation", "context.MoveVector", "context.ArrayCount", "context.ArrayVector", "context.TargetBaseOffset", "context.ConflictPolicy")) {
    if ($modifyPlanBuilder -notmatch [regex]::Escape($requestField)) { throw "Modify Object plan fingerprint omits request field: $requestField" }
}
$contractChecks++
if ($modifyForm -notmatch 'UnitUtils\.ConvertToInternalUnits\(value, UnitTypeId\.Millimeters\)' -or $modifyForm -notmatch 'finite number in millimetres' -or $modifyForm -notmatch 'Preview failed: ') {
    throw "Modify Objects must validate its millimetre distance input, convert it to Revit internal units, and report preview validation errors."
}
$contractChecks++
$rebarForms = @("BeamReinforcementForm.cs", "SlabReinforcementForm.cs", "RectangularColumnReinforcementForm.cs") | ForEach-Object { [IO.File]::ReadAllText((Join-Path $root ("KhimTools/Tools/KhimStructural/RebarTool/Forms/" + $_))) }
if (@($rebarForms | Where-Object { $_ -notmatch "PreviewLifecycleSession<RebarPreviewSnapshot>" -or $_ -notmatch "TryGetValid\(" }).Count -gt 0) {
    throw "Column, beam and slab must preserve Rebar parity while using the canonical stale-preview lifecycle."
}
$contractChecks++
$rectangularColumnForm = $rebarForms[2]
if ($rectangularColumnForm -notmatch 'AccessibleName\s*=\s*"Rectangular column preview state"' -or
    $rectangularColumnForm -notmatch 'MarkPreviewStale\(\)' -or
    $rectangularColumnForm -notmatch 'RebarLayout\.Footer\(_cmbLanguage,\s*_btnPreview3D,\s*_btnCreateRebar' -or
    $rectangularColumnForm -notmatch 'PreviewLifecycleState\.Valid\s*&&\s*_lastPreview\s*!=\s*null' -or
    $rectangularColumnForm -notmatch '_formGuard\?\.ValidateNow\(\)') {
    throw "Rectangular-column solver preview must be discoverable, visibly stateful, and gate Create until the current preview is accepted."
}
$contractChecks++
$slabForm = $rebarForms[1]
if ($slabForm -notmatch 'Appearance\s*=\s*TabAppearance\.FlatButtons' -or
    $slabForm -notmatch 'ItemSize\s*=\s*new Size\(0,\s*1\)' -or
    $slabForm -notmatch 'AddRoleSelector\(roles,' -or
    $slabForm -notmatch '_workflowTabs\.SelectedIndex\s*=\s*index') {
    throw "Slab daily settings must be navigated through the role-oriented selector without duplicative primary tab headers."
}
$contractChecks++
$iconNames = @([regex]::Matches($ribbon, '"([^"\r\n]+\.png)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
foreach ($iconName in $iconNames) {
    if (!(Test-Path -LiteralPath (Join-Path $root ("KhimTools/Resources/" + $iconName)))) { throw "Ribbon icon resource is missing: $iconName" }
}
if ($ribbon -match '[A-Z]:\\|\\Users\\') { throw "Ribbon resource configuration contains a machine-specific path." }
$contractChecks++
$inventoryPath = Join-Path $root "Tools/UiPreviewProductionInventory.md"
$inventory = [IO.File]::ReadAllText($inventoryPath)
foreach ($inventoryField in @("MODULE | TOOL | PREVIEW EXISTS?", "CAN MUTATE MODEL?", "STALE-STATE PROTECTION", "CANCEL CLEANUP", "EXECUTION PARITY", "VIEWMODEL", "BUTTONS / ACTIONS", "VALIDATION", "UNITS", "RESULT SUMMARY", "HELP", "STATUS", "K-Rebar", "QuickArchi", "Openings", "Quantity Takeoff", "Modify Objects", "Dimension Tools", "Parameter Manager", "Slab Step")) {
    if ($inventory.IndexOf($inventoryField, [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw "Phase 9 UI/preview inventory omits required audit field or surface: $inventoryField" }
}
$contractChecks++
$matrixPath = Join-Path $root "Tools/CrossModuleProductionMatrix.md"
$matrix = [IO.File]::ReadAllText($matrixPath)
$missingMatrixCommands = @($commands | Where-Object {
    $shortName = $_.Substring($_.LastIndexOf('.') + 1)
    $matrix -notmatch ("\b" + [regex]::Escape($shortName) + "\b")
})
if ($missingMatrixCommands.Count -gt 0) {
    throw "Cross-module matrix omits active command types: $($missingMatrixCommands -join ', ')"
}
$contractChecks += $commands.Count
Write-Host "PASS: $($commands.Count) Revit command metadata checks; $($bindings.Count) workspace bindings; 4 ribbon ownership boundaries; $contractChecks contract assertions."
Write-Host "Cross-module matrix inventory: $($commands.Count)/$($commands.Count) command types; status remains per matrix row."
Write-Host "Metadata only: command Execute methods were NOT run."
