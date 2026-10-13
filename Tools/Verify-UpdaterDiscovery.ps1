$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = & dotnet run --project (Join-Path $root 'Tests/KhimTools.Updater.ContractTests/KhimTools.Updater.ContractTests.csproj') -c Release
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch 'UPDATER_CONTRACT=PASS \(6 checks\)') {
    throw "Updater discovery contract failed: $output"
}
Write-Output ($output -join "`n")
