$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot '../Tests/KhimTools.DualWallBatch.Contract/KhimTools.DualWallBatch.Contract.csproj'
$output=& dotnet run --project $project -c Release --no-launch-profile
if($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch 'DUAL_WALL_BATCH_CONTRACT=PASS \(13 assertions\)'){throw "Production-linked A/B batch tests failed: $output"}
$output|Write-Host
