# Test results — 2026-10-13

Final production source checkpoint: `7529d6cce2b359763ecd3c77e43a5213ba781863`.
The final run tested its uncommitted source tree atop `93d86e343763186a1f4026204a95df490e270cd1`; that exact source was then committed unchanged. Subsequent handoff changes are documentation only. Generated evidence stays under ignored local artifacts, not in the product payload.

## Executed gates

| Gate | Result / actual evidence |
|---|---|
| Dependency restore | PASS |
| Test-All | PASS 48/48 executable suites, 241.22 seconds |
| Configured audit weight | 2984/2984; not an actual assertion or live test count |
| K-Rebar | PASS 159/159 source checks |
| Golden regression | PASS 134 numerical/domain assertions |
| Rebar config | PASS 37 assertions |
| Manual offline layout | PASS 734 checks / 18 renders; minimum, 1366x768, 1920x1080 at simulated 100/125/150% |
| Full Rebar offline layout | PASS 17987 control checks / 360 renders |
| WPF offline layout | PASS 100 icons / 10 surfaces / 43 renders |
| UI contracts | PASS 218 assertions; 92 command metadata checks, not command execution |
| Section-cut offline fixture | PASS 105 assertions |
| Runtime harness source audit | PASS 50 checks; registered/compiled, not executed |
| Scenario catalog | PASS 242 checks / 31 fixtures; registration only |
| API adapter contracts | PASS 20 synthetic old/new API assertions |
| Revit source builds | PASS 2022/2023/2024/2025/2026; zero errors; warnings 0/0/10/8/144 |
| Updater discovery | PASS six production-linked fake HTTP checks; no network/install |
| Installer build | PASS MSI and bootstrapper candidates; no installation |
| Extracted installer payload | PASS 134 checks / all 132 files match captured inputs; repeated after isolated API builds and commit |
| Diff whitespace | PASS |
| Live Revit / actual host geometry | BLOCKED / NOT_EXECUTED; no confirmed disposable host available |
| Real Windows DPI/Revit UI | NOT_EXECUTED |
| Signing / installer lifecycle | BLOCKED / NOT_EXECUTED; candidate unsigned |

Some suite names contain "runtime", "production" or "lifecycle"; their source/synthetic mode does NOT establish host or install acceptance. Domain and static gates retain their own boundaries. Architecture 39 source/14 domain, MEP 31/12, QS 49/16 execute; QS configured weight is not its emitted assertion count.

Initial failures were not bypassed: 2022/2023/2026 compile errors were fixed; new noncoplanar/end-radius goldens failed before correction; a canonical Windows PowerShell isolated-path quoting failure was corrected and all 48 suites rerun. An earlier layout PASS did not prove the requested 1366x768 window because of minimum-height clamping; the new assertion and rerun supersede it.

## Actual canonical scripts

Each script below actually returned exit code zero in the final run. These are not inferred from ExpectedCount.

| Script | Result |
|---|---|
| `Tools/Verify-RevitReferenceSelection.ps1` | PASS / exit 0 |
| `KhimTools/Tests/RunTests.ps1` | PASS / exit 0 |
| `Installer/Verify-MsiImplementation.ps1` | PASS / exit 0 |
| `Installer/Test-MsiLifecycle.ps1` | PASS / exit 0 |
| `Tools/Verify-DeploymentProduction.ps1` | PASS / exit 0 |
| `Tools/Verify-InstallerPayload.ps1` | PASS / exit 0 |
| `Installer/Verify-RevitRuntimeQA.ps1` | PASS / exit 0 |
| `Tools/Verify-InternalAutomationApi.ps1` | PASS / exit 0 |
| `Tools/Verify-McpAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-KRebar.ps1` | PASS / exit 0 |
| `Tools/Verify-RebarConfiguration.ps1` | PASS / exit 0 |
| `Tools/Verify-RebarLayout.ps1` | PASS / exit 0 |
| `Tools/Verify-UiLayout.ps1` | PASS / exit 0 |
| `Tools/Verify-SectionCutLayout.ps1` | PASS / exit 0 |
| `Tools/Verify-UiContracts.ps1` | PASS / exit 0 |
| `Tools/Verify-RuntimeQaHarness.ps1` | PASS / exit 0 |
| `Tools/Verify-Phase15ScenarioCatalog.ps1` | PASS / exit 0 |
| `Tools/Verify-Phase16ReliabilitySecurity.ps1` | PASS / exit 0 |
| `Tools/Verify-Phase17GoldenPerformance.ps1` | PASS / exit 0 |
| `Tools/Verify-SheetCopyStage31.ps1` | PASS / exit 0 |
| `Tools/Verify-SheetGenWave11.ps1` | PASS / exit 0 |
| `Tools/Verify-SheetExportWave16.ps1` | PASS / exit 0 |
| `Tools/Verify-DetailNumberWave13.ps1` | PASS / exit 0 |
| `Tools/Verify-ScheduleSplitStage32.ps1` | PASS / exit 0 |
| `Tools/Verify-TitleBlockSyncStage33.ps1` | PASS / exit 0 |
| `Tools/Verify-FilterManagerStage34.ps1` | PASS / exit 0 |
| `Tools/Verify-ParameterManagerStage35.ps1` | PASS / exit 0 |
| `Tools/Verify-ModifyObjectsStage36.ps1` | PASS / exit 0 |
| `Tools/Verify-DimensionToolsStage37.ps1` | PASS / exit 0 |
| `Tools/Verify-BackendConsolidationStage4.ps1` | PASS / exit 0 |
| `Tools/Verify-Stage4DiagnosticAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-Stage4TransactionAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-SlabStep.ps1` | PASS / exit 0 |
| `Tools/Verify-TextAlignWave14.ps1` | PASS / exit 0 |
| `Tools/Verify-ViewportAlignWave12.ps1` | PASS / exit 0 |
| `Tools/Verify-KArchitecturalAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-KArchitecturalDomain.ps1` | PASS / exit 0 |
| `Tools/Verify-KMepAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-KMepDomain.ps1` | PASS / exit 0 |
| `Tools/Verify-KQsAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-KQsDomain.ps1` | PASS / exit 0 |
| `Tools/Verify-QuickStructureAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-GridPlanAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-ElementTagsWave15.ps1` | PASS / exit 0 |
| `Tools/Verify-ElementJoinAcceptance.ps1` | PASS / exit 0 |
| `Tools/Verify-RevitCompatibility.ps1` | PASS / exit 0 |
| `Tools/Verify-UpdaterDiscovery.ps1` | PASS / exit 0 |
| `Tools/Verify-CopyLinkAcceptance.ps1` | PASS / exit 0 |

## Reproduction / evidence

Build current candidates with `Installer/Build-Installer.ps1 -Configuration Release`, then run `Test-All.ps1`. Payload absence/staleness is a fail-closed blocker, not silently skipped. Focused Manual: STA `Tools/Verify-RebarLayout.ps1 -Forms BeamManualDetailing -Scales @(1,1.25,1.5) -WindowProfiles @("minimum","1366x768","1920x1080")`.

Local evidence: `artifacts/full-stack-20261013/test-all.log`, `manual-layout.log`, `installer-build.log`; machine-readable `artifacts/full-stack-qa/latest.json`; isolated build logs `artifacts/revit-compatibility/build-<year>.log`; layout bounds/images `artifacts/ui-qa/rebar`; package hashes and complete manifest `Installer/Output/SHA256SUMS.txt` and `PAYLOAD_SHA256.json`. No live result is manufactured from this evidence.

GitHub checks queried for the code checkpoint: no combined statuses and no PR-triggered workflow runs returned. This means NO REPORTED CHECKS, not CI PASS. Draft PR requires human review and host/release acceptance; no master merge.

