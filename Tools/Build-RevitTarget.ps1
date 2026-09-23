[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('2024', '2025')]
    [string]$RevitVersion,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'KhimTools\KhimTools.csproj'
$targetFramework = if ($RevitVersion -eq '2024') { 'net48' } else { 'net8.0-windows' }

& dotnet msbuild $project -target:ValidateRevitApiReference "-p:TargetFramework=$targetFramework" "-p:RevitRequestedVersion=$RevitVersion" "-p:RevitVersionForReference=$RevitVersion"
if ($LASTEXITCODE -ne 0) { throw "Revit $RevitVersion API reference validation failed." }

& dotnet build $project -c $Configuration -f $targetFramework "-p:RevitRequestedVersion=$RevitVersion" "-p:RevitVersionForReference=$RevitVersion"
if ($LASTEXITCODE -ne 0) { throw "Revit $RevitVersion build failed." }
