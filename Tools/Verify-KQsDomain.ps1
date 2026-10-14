$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../Tests/KhimTools.Qs.Domain.Tests/KhimTools.Qs.Domain.Tests.csproj'
& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw "K-QS domain QA failed ($LASTEXITCODE)." }
