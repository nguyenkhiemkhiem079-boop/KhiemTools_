$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot '../Tests/KhimTools.RebarCenterline.Contract/KhimTools.RebarCenterline.Contract.csproj'
$result=& dotnet run --project $project --configuration Release --no-launch-profile
if($LASTEXITCODE -ne 0){throw 'Production-linked final-position adapter regression failed.'}
if(($result -join "`n") -notmatch 'REBAR_WORLD_POSITION_CONTRACT=PASS \(8 assertions\)'){throw 'Missing actual executed adapter assertions.'}
$result|Write-Host
