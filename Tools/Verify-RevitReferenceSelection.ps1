$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'KhimTools\KhimTools.csproj'
$checks = 0

function Assert-Default([string]$framework, [string]$expected) {
    $output = & dotnet msbuild $script:project "-getProperty:RevitVersionForReference" "-p:TargetFramework=$framework"
    if ($LASTEXITCODE -ne 0 -or $output.Trim() -ne $expected) { throw "Reference default mismatch for ${framework}: expected $expected, got '$output'." }
    $script:checks++
}

function Assert-Target([string]$framework, [string]$version) {
    & dotnet msbuild $script:project -target:ValidateRevitApiReference "-p:TargetFramework=$framework" "-p:RevitRequestedVersion=$version" "-p:RevitVersionForReference=$version" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Requested=$version Resolved=$version should be accepted for $framework." }
    $script:checks++
}

Assert-Default 'net48' '2024'
Assert-Default 'net8.0-windows' '2025'
Assert-Target 'net48' '2024'
Assert-Target 'net8.0-windows' '2025'

$mismatch = & dotnet msbuild $project -target:ValidateRevitApiReference '-p:TargetFramework=net8.0-windows' '-p:RevitRequestedVersion=2025' '-p:RevitVersionForReference=2027' 2>&1
$mismatchExit = $LASTEXITCODE
if ($mismatchExit -eq 0 -or ($mismatch -join "`n") -notmatch 'Revit API reference mismatch: requested 2025, resolved 2027') {
    throw 'Requested=2025 Resolved=2027 must fail with an explicit reference-mismatch diagnostic.'
}
$checks++

Write-Output "PASS: $checks deterministic Revit API reference-selection checks."
