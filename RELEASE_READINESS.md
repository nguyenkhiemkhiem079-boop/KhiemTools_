# Release readiness — 2026-10-13

Wave3 current candidate: MSI93eab6aebf22b66aee9b855aaf23aa76b24127f00a49d46ca0ee95a2fe17122e; bootstrapper1d0b650cd23f3cfba87b70de1eb424a71fe3b402439ee639761bc52719af1d41. Build/payload134 and canonical51/51 suites/3096 configured weight PASS, all five source API builds zero errors. Bounded native MEP Wall/Floor opening code exists, but actual cuts/rollback/Undo/host loading NOT_EXECUTED. Unsigned candidate/lifecycle pending; no release/installation. Older candidates and MEP-unimplemented statements below are historical. PRODUCTION_READY=NO.

Latest Wall candidate (2026-10-14): MSI SHA256 74a1e4c5ecd0d4fc108bac79d25d01cf364d6d188638e265b477e2eef419b65e; bootstrapper013d2f2896430ee240d9db2d8824ab61bc2005a51f3edc2391f48fa32119543f. Current-source build/payload134, canonical50/50 suites/3064 configured weight, all five API builds PASS. Wall straight-prism code slice implemented with Wall ownership; advanced Wall/detail features explicitly unsupported. MEP creation/consolidated preview work remains pending. HOST_ACCEPTANCE=NOT_EXECUTED; PRODUCTION_READY=NO. Package candidate is not a signed or lifecycle-certified release; nothing installed/published. Older candidate hashes/Wall-unimplemented statements below are historical.

Superseding Wave1 candidate: MSI SHA256 `54ac3fdd4d6ebb668ecbe4df849e4b3b25ba54c01d1063c84b8c4a4af94aca4e`; bootstrapper `b76680fbb40d09853aa198e8382abddfa2220c80d2fda5477db9f632ed5750de`. Complete payload134 and canonical50 suites/3030 weight PASS, five API builds zero errors. Bound rectangular support extension implemented, real support CSG/Undo NOT_EXECUTED. Older hashes/unsupported-support notes below are historical. Signing/lifecycle remain pending; no install or release publication.

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
