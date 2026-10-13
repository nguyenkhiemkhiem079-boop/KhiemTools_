$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = & dotnet run --project (Join-Path $root 'Tests/KhimTools.BeamBatch.ContractTests/KhimTools.BeamBatch.ContractTests.csproj') -c Release
if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch 'BEAM_BATCH_CONTRACT=PASS \(11 assertions\)') { throw "Beam batch orchestration contract failed: $output" }
Write-Output ($output -join "`n")
