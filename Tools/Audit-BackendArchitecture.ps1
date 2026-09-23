# K-TOOLS Stage 4 backend architecture inventory (read-only).
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$productionRoot = Join-Path $repoRoot "KhimTools"
$genRoot = Join-Path $productionRoot "Tools\KhimGen"
$domainRoot = Join-Path $repoRoot "src\KhimTools.Domain"

function Get-TextFiles([string]$root) {
    if (-not (Test-Path $root)) { return @() }
    return @(Get-ChildItem -LiteralPath $root -Recurse -File -Include *.cs,*.csproj)
}

$genFiles = Get-TextFiles $genRoot
$allProduction = Get-TextFiles $productionRoot
$entrypoints = @($genFiles | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'IExternalCommand|IExternalApplication' -Quiet })
$transactionFiles = @($genFiles | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'Transaction\s*\(' -Quiet })
$emptyCatchFiles = @($genFiles | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'catch\s*\{\s*\}' -Quiet })
$failureProcessors = @($allProduction | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'IFailuresPreprocessor' -Quiet })
$domainRevit = @((Get-TextFiles $domainRoot) | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'Autodesk\.Revit|RevitAPI' -Quiet })

Write-Host "STAGE4_BACKEND_ARCHITECTURE_AUDIT" -ForegroundColor Cyan
Write-Host "GEN_CS_FILES=$($genFiles.Count)"
Write-Host "GEN_ENTRYPOINT_FILES=$($entrypoints.Count)"
Write-Host "GEN_TRANSACTION_FILES=$($transactionFiles.Count)"
Write-Host "GEN_EMPTY_CATCH_FILES=$($emptyCatchFiles.Count)"
Write-Host "FAILURE_PROCESSOR_FILES=$($failureProcessors.Count)"
Write-Host "DOMAIN_REVIT_REFERENCES=$($domainRevit.Count)"
Write-Host "SHARED_WORKFLOW_FILES=$(@(Get-ChildItem (Join-Path $productionRoot 'Core\Workflow') -File -Filter *.cs -ErrorAction SilentlyContinue).Count)"
Write-Host "SHARED_LOGGING_FILES=$(@(Get-ChildItem (Join-Path $productionRoot 'Core\Logging') -File -Filter *.cs -ErrorAction SilentlyContinue).Count)"
Write-Host "SAFE_FAILURE_POLICY_PRESENT=$([bool](Test-Path (Join-Path $productionRoot 'Core\Revit\Failures\KnownWarningFailurePreprocessor.cs')))"

if ($domainRevit.Count -gt 0) {
    Write-Error "Domain assembly references Autodesk.Revit; boundary is not clean."
}
