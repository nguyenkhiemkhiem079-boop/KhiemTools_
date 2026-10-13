# Test results — 2026-10-13

Latest Wave2 Wall (2026-10-14), exact final uncommitted source atop f6a588ddc429c676f72f9d943a298b40d79ccec6: Test-All PASS50/50 actual suites, 3064 configured weight (NOT assertion count), 244.89s. K-Rebar170/170; Golden177 (20 new pure Wall assertions); UI contracts220/93 commands; catalog252/33 fixtures; actual source API builds2022–2026 zero errors; MSI build and complete payload134 PASS. Full offline Rebar18151 controls/372 renders; focused final Wall170/9 at minimum/1366x768/1920x1080 ×100/125/150% simulated scaling. Evidence: artifacts/full-stack-20261013/wall-test-all.log, wall-installer.log, wall-layout-final.log and canonical latest.json. WALL-MANUAL-OWNERSHIP-ROLLBACK registered/compiled, NOT_EXECUTED; no confirmed disposable model, no live mutation. Final audit fixes (strict unknown JSON, exact-line neighbor gate, collision diagnostic bound, status wrapping) preceded this run; historical pre-fix run is not final evidence.

Latest Wave1 support: canonical Test-All PASS50/50 executed suites, 3030 configured weight, 260.82s. K-Rebar163/163, Golden157 (17 new input/store support assertions), batch11, catalog247/32 fixtures; API2022–2026 actual source builds zero errors. Full offline Rebar18038 checks/366 renders; focused Manual+support-binding812/27 at minimum/1366x768/1920x1080 and100/125/150% simulation. Current MSI build/payload134 PASS. Evidence `artifacts/full-stack-20261013/support-test-all.log`, `support-installer.log`, `support-layout.log`, `support-krebar.log`. Tested exact uncommitted source atopcc31ab52 before committing it unchanged. Support CSG/Undo host fixture REGISTERED/COMPILED, NOT_EXECUTED; no model confirmation or mutation. Input-policy/store assertions do NOT establish live support geometry acceptance.

Latest continuation batch: Test-All PASS50/50 executable suites, 3005 configured weight, 243.56s. K-Rebar160/160, Golden140, batch orchestration11 (production code linked to synthetic API doubles), installer safety3 (two rejected launches plus gate ordering). All Revit2022–2026 source builds zero errors; payload134 and existing UI/config/module gates pass. New actual scripts: `Tools/Verify-BeamBatchContract.ps1` and `Tools/Verify-InstallerSafety.ps1`, both exit0. Evidence `artifacts/full-stack-20261013/batch2-test-all.log`, `batch2-installer.log` and canonical latest.json. Current source was tested uncommitted atop7366e216 then committed unchanged. Signing readiness returns BLOCKED/exit2, both candidates NotSigned, lifecycle/live Revit NOT_EXECUTED. A Windows PowerShell5.1 expected-stderr handling defect in the new safety probe was fixed and the native5.1 probe/canonical gate passed; no mutation occurred.

Continuation checkpoint (independent multi-Beam Manual assignment): Test-All PASS48/48 suites, 2991 configured weight, 234.62s; K-Rebar160, Golden140; source builds2022–2026 zero errors. Installer rebuilt; complete payload gate PASS. Existing layout/UI/configuration gates pass. Log `artifacts/full-stack-20261013/batch-test-all.log`, build `batch-installer.log`. No host execution; identity goldens are not transaction or geometry acceptance. Prior source report below is preserved.

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
