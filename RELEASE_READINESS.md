# Release readiness — 2026-10-13

SOURCE_DELIVERY = feature branch checkpoints; reviewed integration PR required
CODE_SIDE_RECOVERY = completed for audited supported scope
FULL_PRODUCT_FEATURE_COMPLETE = NO
HOST_ACCEPTANCE = NOT_EXECUTED
PRODUCTION_READY = NO

The source builds against installed Revit 2022–2026 API assemblies. Current-source MSI/bootstrapper candidates build without installation. Complete MSI extraction verifies 132 payload files against captured actual build inputs and the product-source fingerprint. This is package evidence, not live loading or deployment certification.

Rebuilt batch-integrity continuation QA candidate hashes (supersede previous candidates):

- `Installer/Output/K-TOOLS.msi`: `f7b8d1df495b59bb438e006b7beb07927177572bb3b488df6346b8fae00199b6`
- `Installer/Output/K-TOOLS-Setup.exe`: `ee254afc93c3ea383928468ef15530d305b699718116cc83ef331f8240fc3aac`

Read-only `Tools/Verify-SigningReadiness.ps1` reports BLOCKED/exit2: current MSI/bootstrapper NotSigned, trusted timestamped signatures absent. Real lifecycle harness now requires exact `-ConfirmedDisposableMachineName`, disallows skipped static QA and stops on failed/missing canonical evidence. Safety probes PASS3 without installing. VM and certificate authorization are requested but not confirmed. Full real lifecycle remains NOT_EXECUTED, not certified by static/source checks.

Candidates are unsigned (Authenticode NotSigned), ignored local build outputs, not published releases. No installer, repair, upgrade, uninstall, updater download/install or user addin deployment was executed. Shared legacy/modern bundle bindings need actual year-specific host loading tests. Revit 2022 Schedule Split is explicitly unavailable; legacy int-backed persistence rejects >Int32 identities rather than truncating them.

Release blockers: disposable-host Rebar/Manual geometry and atomicity acceptance; multi-year actual load/command acceptance; signing and disposable installer lifecycle; reviewed integration/required server checks. Wall reinforcement and MEP clash-opening creation remain unsupported/unimplemented. Full Beam contract remains partial (support extension/multi-host Manual unavailable). These are disclosed scope limits, not fabricated implementations.

Next agent: fetch `fix/preview-hardening`, confirm clean tree and local/remote equality using the final delivered SHA. Read this report, `TEST_RESULTS.md`, `REBAR_QA_REPORT.md` and the authoritative Beam contract. Rebuild candidate via `Installer/Build-Installer.ps1 -Configuration Release` before canonical regression if payload artifacts are absent. Run `Test-All.ps1`; review its per-suite JSON and logs. Confirm an explicitly disposable supported Revit model before any mutation fixture. Record model identity/state before/after, exact type/shape/quantity/path parity, duplicate selectivity, stale invalidation, whole rollback and Undo. Keep runtime NOT_EXECUTED until evidence exists. Do not merge master or force-push without approval.
