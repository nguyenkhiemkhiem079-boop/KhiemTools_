$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'Tests/KhimTools.MEP.Domain.Tests/KhimTools.MEP.Domain.Tests.csproj'
& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw "K-MEP domain QA failed ($LASTEXITCODE)." }
