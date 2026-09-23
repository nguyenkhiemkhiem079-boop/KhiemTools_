# =====================================================================
# K-TOOLS (KhimTools) Master Verification & Quality Assurance Suite
# Runs all static audits across Security, MSI, Engineering & QA
# =====================================================================

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sw = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "           K-TOOLS MASTER QUALITY ASSURANCE DASHBOARD            " -ForegroundColor Cyan
Write-Host "                 Running All Verification Audits                 " -ForegroundColor Cyan
Write-Host "=================================================================`n" -ForegroundColor Cyan

$suites = @(
    @{
        Name = "Deployment Security & Rebar Engineering Suite (Phase 0, 4, 5, 6, 7, 8)"
        Script = Join-Path $scriptDir "KhimTools\Tests\RunTests.ps1"
        ExpectedCount = 44
    },
    @{
        Name = "MSI Implementation Structural Audit (Phase 2)"
        Script = Join-Path $scriptDir "Installer\Verify-MsiImplementation.ps1"
        ExpectedCount = 14
    },
    @{
        Name = "MSI Lifecycle Install/Upgrade/Repair/Uninstall Audit (Phase 3)"
        Script = Join-Path $scriptDir "Installer\Test-MsiLifecycle.ps1"
        ExpectedCount = 12
    },
    @{
        Name = "Revit Runtime QA & Manifest Verification Suite (Phase 9)"
        Script = Join-Path $scriptDir "Installer\Verify-RevitRuntimeQA.ps1"
        ExpectedCount = 9
    },
    @{
        Name = "Stage 2 Runtime QA Harness Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-RuntimeQaHarness.ps1"
        ExpectedCount = 14
    },
    @{
        Name = "Stage 3.1 Sheet Copy Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-SheetCopyStage31.ps1"
        ExpectedCount = 57
    },
    @{
        Name = "Stage 3.2 Split Schedule Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-ScheduleSplitStage32.ps1"
        ExpectedCount = 49
    }
    @{
        Name = "Stage 3.3 Title Block Sync Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-TitleBlockSyncStage33.ps1"
        ExpectedCount = 56
    },
    @{
        Name = "Stage 3.4 Filter Manager Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-FilterManagerStage34.ps1"
        ExpectedCount = 114
    },
    @{
        Name = "Stage 3.5 Parameter Manager Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-ParameterManagerStage35.ps1"
        ExpectedCount = 174
    },
    @{
        Name = "Stage 3.6 Modify Objects Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-ModifyObjectsStage36.ps1"
        ExpectedCount = 146
    },
    @{
        Name = "Stage 3.7 Dimension Tools Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-DimensionToolsStage37.ps1"
        ExpectedCount = 271
    },
    @{
        Name = "Stage 4 Backend Consolidation Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-BackendConsolidationStage4.ps1"
        ExpectedCount = 61
    }
    @{
        Name = "Stage 4.2 Slab Step Current Workflow Acceptance Audit"
        Script = Join-Path $scriptDir "Tools\Verify-SlabStep.ps1"
        ExpectedCount = 40
    }
)

$allPassed = $true
$totalPassed = 0
$totalTests = 0

foreach ($suite in $suites) {
    Write-Host ">> Executing: $($suite.Name)" -ForegroundColor Yellow
    $startTime = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        & powershell -ExecutionPolicy Bypass -File $suite.Script
        $exitCode = $LASTEXITCODE
        $startTime.Stop()
        
        if ($exitCode -eq 0) {
            Write-Host "   [SUCCESS] $($suite.Name) Passed in $($startTime.ElapsedMilliseconds)ms`n" -ForegroundColor Green
            $totalPassed += $suite.ExpectedCount
            $totalTests += $suite.ExpectedCount
        } else {
            Write-Host "   [FAILED] $($suite.Name) failed with exit code $exitCode`n" -ForegroundColor Red
            $allPassed = $false
            $totalTests += $suite.ExpectedCount
        }
    } catch {
        Write-Host "   [ERROR] $($suite.Name) encountered exception: $($_.Exception.Message)`n" -ForegroundColor Red
        $allPassed = $false
        $totalTests += $suite.ExpectedCount
    }
}

$sw.Stop()

Write-Host "=================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host " MASTER QA RESULT: 100% GREEN ($totalPassed / $totalTests AUDITS PASSED)" -ForegroundColor Green
} else {
    Write-Host " MASTER QA RESULT: FAILED ($totalPassed / $totalTests AUDITS PASSED)" -ForegroundColor Red
}
Write-Host " Total Execution Time: $($sw.Elapsed.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
Write-Host "=================================================================`n" -ForegroundColor Cyan

if (-not $allPassed) {
    exit 1
} else {
    exit 0
}
