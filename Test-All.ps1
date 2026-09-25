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
        Name = "Deterministic Revit API Reference Selection"
        Script = Join-Path $scriptDir "Tools\Verify-RevitReferenceSelection.ps1"
        ExpectedCount = 5
    },
    @{
        Name = "Deployment Security & Rebar Engineering Suite (Phase 0, 4, 5, 6, 7, 8)"
        Script = Join-Path $scriptDir "KhimTools\Tests\RunTests.ps1"
        ExpectedCount = 57
    },
    @{
        Name = "MSI Implementation Structural Audit (Phase 2)"
        Script = Join-Path $scriptDir "Installer\Verify-MsiImplementation.ps1"
        ExpectedCount = 14
    },
    @{
        Name = "MSI Lifecycle Install/Upgrade/Repair/Uninstall Audit (Phase 3)"
        Script = Join-Path $scriptDir "Installer\Test-MsiLifecycle.ps1"
        ExpectedCount = 13
    },
    @{
        Name = "Phase 11 Deployment & Reproducible Package Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-DeploymentProduction.ps1"
        ExpectedCount = 14
    },
    @{
        Name = "Revit Runtime QA & Manifest Verification Suite (Phase 9)"
        Script = Join-Path $scriptDir "Installer\Verify-RevitRuntimeQA.ps1"
        ExpectedCount = 9
    },
    @{
        Name = "Phase 13 Internal Automation API Contract"
        Script = Join-Path $scriptDir "Tools\Verify-InternalAutomationApi.ps1"
        ExpectedCount = 23
    },
    @{
        Name = "Phase 14 MCP Protocol & Local Transport Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-McpAcceptance.ps1"
        ExpectedCount = 51
    },
    @{
        Name = "Phase 8 Rebar Preview Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-KRebar.ps1"
        ExpectedCount = 112
    },
    @{
        Name = "Rebar Configuration Persistence & Recovery"
        Script = Join-Path $scriptDir "Tools\Verify-RebarConfiguration.ps1"
        ExpectedCount = 37
    },
    @{
        Name = "Phase 8 Rebar Layout Rendering QA"
        Script = Join-Path $scriptDir "Tools\Verify-RebarLayout.ps1"
        ExpectedCount = 1
        Sta = $true
    },
    @{
        Name = "Phase 8 WPF UI Layout Rendering QA"
        Script = Join-Path $scriptDir "Tools\Verify-UiLayout.ps1"
        ExpectedCount = 1
        Sta = $true
    },
    @{
        Name = "Section Cut Layout Fixture"
        Script = Join-Path $scriptDir "Tools\Verify-SectionCutLayout.ps1"
        ExpectedCount = 105
        Sta = $true
    },
    @{
        Name = "UI Command Contract Metadata Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-UiContracts.ps1"
        ExpectedCount = 215
    },
    @{
        Name = "Stage 2 Runtime QA Harness Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-RuntimeQaHarness.ps1"
        ExpectedCount = 42
    },
    @{
        Name = "Phase 15 Host Scenario Catalog & Registration"
        Script = Join-Path $scriptDir "Tools\Verify-Phase15ScenarioCatalog.ps1"
        ExpectedCount = 242
    },
    @{
        Name = "Phase 16 Reliability & Security Static Audit"
        Script = Join-Path $scriptDir "Tools\Verify-Phase16ReliabilitySecurity.ps1"
        ExpectedCount = 13
    },
    @{
        Name = "Phase 17 Domain Goldens, Edge Cases & Synthetic Stress"
        Script = Join-Path $scriptDir "Tools\Verify-Phase17GoldenPerformance.ps1"
        # 36 existing domain/comparator checks plus 5 production Rebar math goldens and 7 Rebar input-edge checks.
        ExpectedCount = 48
    },
    @{
        Name = "Stage 3.1 Sheet Copy Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-SheetCopyStage31.ps1"
        ExpectedCount = 57
    },
    @{
        Name = "SheetGen Wave 1.1 Production QA"
        Script = Join-Path $scriptDir "Tools\Verify-SheetGenWave11.ps1"
        ExpectedCount = 24
    },
    @{
        Name = "Sheet Export Wave 1.6 Production QA"
        Script = Join-Path $scriptDir "Tools\Verify-SheetExportWave16.ps1"
        ExpectedCount = 138
    },
    @{
        Name = "Detail Number Wave 1.3 Production QA"
        Script = Join-Path $scriptDir "Tools\Verify-DetailNumberWave13.ps1"
        ExpectedCount = 58
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
        ExpectedCount = 168
    },
    @{
        Name = "Stage 3.7 Dimension Tools Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-DimensionToolsStage37.ps1"
        ExpectedCount = 271
    },
    @{
        Name = "Stage 4 Backend Consolidation Structural Audit"
        Script = Join-Path $scriptDir "Tools\Verify-BackendConsolidationStage4.ps1"
        ExpectedCount = 75
    }
    @{
        Name = "Stage 4 Diagnostic Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-Stage4DiagnosticAcceptance.ps1"
        ExpectedCount = 8
    },
    @{
        Name = "Stage 4 Transaction Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-Stage4TransactionAcceptance.ps1"
        ExpectedCount = 19
    },
    @{
        Name = "Stage 4.2 Slab Step Current Workflow Acceptance Audit"
        Script = Join-Path $scriptDir "Tools\Verify-SlabStep.ps1"
        ExpectedCount = 43
    },
    @{
        Name = "Text Align Wave 1.4 Structural Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-TextAlignWave14.ps1"
        ExpectedCount = 64
    },
    @{
        Name = "Viewport Align Wave 1.2 Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-ViewportAlignWave12.ps1"
        ExpectedCount = 67
    },
    @{
        Name = "K-Architectural Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-KArchitecturalAcceptance.ps1"
        ExpectedCount = 39
    },
    @{
        Name = "K-Architectural Domain QA"
        Script = Join-Path $scriptDir "Tools\Verify-KArchitecturalDomain.ps1"
        ExpectedCount = 14
    },
    @{
        Name = "K-MEP Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-KMepAcceptance.ps1"
        ExpectedCount = 31
    },
    @{
        Name = "K-MEP Domain QA"
        Script = Join-Path $scriptDir "Tools\Verify-KMepDomain.ps1"
        ExpectedCount = 12
    },
    @{
        Name = "K-QS Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-KQsAcceptance.ps1"
        ExpectedCount = 49
    },
    @{
        Name = "K-QS Domain QA"
        Script = Join-Path $scriptDir "Tools\Verify-KQsDomain.ps1"
        ExpectedCount = 17
    },
    @{
        Name = "Quick Structure Cross-Module Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-QuickStructureAcceptance.ps1"
        ExpectedCount = 19
    },
    @{
        Name = "Auto Grid & Plan Cross-Module Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-GridPlanAcceptance.ps1"
        ExpectedCount = 17
    },
    @{
        Name = "Element Tags Wave 1.5 Production QA"
        Script = Join-Path $scriptDir "Tools\Verify-ElementTagsWave15.ps1"
        ExpectedCount = 74
    },
    @{
        Name = "Element Join Cross-Module Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-ElementJoinAcceptance.ps1"
        ExpectedCount = 8
    },
    @{
        Name = "CopyLink Cross-Module Production Acceptance"
        Script = Join-Path $scriptDir "Tools\Verify-CopyLinkAcceptance.ps1"
        ExpectedCount = 8
    }
)

$allPassed = $true
$totalPassed = 0
$totalTests = 0

foreach ($suite in $suites) {
    Write-Host ">> Executing: $($suite.Name)" -ForegroundColor Yellow
    $startTime = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        if ($suite.Sta) {
            & powershell -STA -ExecutionPolicy Bypass -File $suite.Script
        } else {
            & powershell -ExecutionPolicy Bypass -File $suite.Script
        }
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
