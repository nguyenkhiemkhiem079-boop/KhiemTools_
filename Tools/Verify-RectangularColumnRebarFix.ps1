param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")),
    [string]$AssemblyPath = ""
)

$ErrorActionPreference = "Stop"
$checks = 0

function Assert-Fix([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}

function Read-Source([string]$relativePath) {
    $path = Join-Path $RepositoryRoot $relativePath
    Assert-Fix (Test-Path -LiteralPath $path) "Missing source file: $relativePath"
    return Get-Content -LiteralPath $path -Raw
}

$generator = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Core/RectangularColumnRebarGenerator.cs"
$creation = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Core/RebarShapeCreationHelper.cs"
$stirrup = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Core/RectangularStirrupHelper.cs"
$settings = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Core/RebarSettingsModels.cs"
$form = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Forms/RectangularColumnReinforcementForm.cs"
$fixture = Read-Source "KhimTools/Tools/KhimStructural/RebarTool/Commands/CmdRebarFixtureQa.cs"

Assert-Fix ($settings -match "MultiCellClosed") "The normal rectangular-column tie layout is missing."
Assert-Fix ($settings -match "TieLayout\s*\{[^}]*\}\s*=\s*ColumnTieLayoutType\.MultiCellClosed") "MultiCellClosed is not the settings default."
Assert-Fix ($settings -match "HasInnerDiamondStirrup\s*\{[^}]*\}\s*=\s*false") "Legacy diamond ties are enabled by default."
Assert-Fix ($settings -match "HasCrossLinks\s*\{[^}]*\}\s*=\s*false") "Cross links are enabled by default."
Assert-Fix ($generator -match "CreateSingleTieStation") "The one-station QA entry point is missing."
Assert-Fix ($generator -match "SetLayoutAs(NumberWithSpacing|Single)") "Shape-driven tie layout is missing."
Assert-Fix ($creation -match "CreateFromRebarShape") "Rectangular ties do not use Rebar.CreateFromRebarShape."
Assert-Fix ($creation -match "TryValidateCreatedRebar") "Created rebar is not regenerated and validated."
Assert-Fix ($creation -match "HookAngleMatchesRebarShapeDefinition") "Tie hook angles are not checked against the shape definition."
Assert-Fix ($stirrup -notmatch "CurveLoop|Line.CreateBound\(.*halfB") "Rectangular ties still contain the old ad-hoc closed-loop factory."
Assert-Fix ($form -match "RebarGenerationFailurePreprocessor") "The rectangular-column form is not using the rollback-only failure preprocessor."
Assert-Fix ($form -match "Column transaction rolled back") "Column rollback diagnostics are missing."
Assert-Fix ($fixture -match "COLUMN_FIXTURE_TIE_COUNT") "The rectangular-column station fixture is missing."
Assert-Fix ($fixture -match "base \+ 500") "The fixture does not test the required base + 500 mm station."

if (-not [string]::IsNullOrWhiteSpace($AssemblyPath) -and (Test-Path -LiteralPath $AssemblyPath)) {
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))
    $layoutType = $assembly.GetType("KhimTools.RebarTool.Core.ColumnTieLayoutType", $false)
    Assert-Fix ($layoutType -ne $null) "Compiled assembly has no ColumnTieLayoutType."
    Assert-Fix ($layoutType.GetField("MultiCellClosed") -ne $null) "Compiled assembly has no MultiCellClosed enum value."
    $generatorType = $assembly.GetType("KhimTools.RebarTool.Core.RectangularColumnRebarGenerator", $false)
    Assert-Fix ($generatorType.GetMethod("CreateSingleTieStation") -ne $null) "Compiled assembly has no CreateSingleTieStation method."
}

Write-Host "PASS: $checks rectangular-column rebar safety checks."
