$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'Tests/KhimTools.Architectural.Domain.Tests/KhimTools.Architectural.Domain.Tests.csproj'
& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw "K-Architectural domain QA failed ($LASTEXITCODE)." }
