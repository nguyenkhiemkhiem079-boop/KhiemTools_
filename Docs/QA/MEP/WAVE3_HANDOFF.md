# Wave3 K-MEP opening workflow — final handoff (2026-10-14)

## Checkpoint and acceptance

Repository nguyenkhiemkhiem079-boop/KhiemTools_; branch fix/preview-hardening; Draft PR #3. Start218f5882e033e168b2ee5246f4176cbf46d4f46a, source checkpoints6fd7be5b317320320783c2bb5a23ab4bdf9a7613 and018b054d8739dd4ad37d8f51e2ea6c806cc76de5, both ordinary-pushed/fetched with exact local/remote equality and clean worktrees. Documentation-only final delivery follows the latter source; use final report/fetched PR head, not a self-referential document SHA. Master601d1feb8731356f3ac2f509aa5aabeecbd8fd3a untouched. No reset, force push, merge, native model test, installation or publication.

CODE_COMPLETE=YES for the explicitly bounded native workflow below. FULL_PRODUCT_FEATURE_COMPLETE=NO. HOST_ACCEPTANCE=NOT_EXECUTED. PHASE_ACCEPTANCE_COMPLETE=NO. PRODUCTION_READY=NO. Actual geometry/rollback/Undo and release gates remain open; offline tests never close them.

## Audit-to-execution trace

- Reused MepOpeningAnalysisService, exact native MepOpeningCreationServiceV1, production frame/geometry and checked TransactionBoundary helpers; no Beam/Slab/Wall generator rewrite.
- Actual native solid clashes for eligible straight duct (round/rectangular), pipe and tray. Coordinates use transformed actual bounds/verified host frames. Foreign document identities reject before native ElementId grouping. Links are distinguished and excluded explicitly; linked transforms/cuts are unsupported, not guessed.
- Five initially empty required inputs: finite non-negative clearance/minimum width/minimum height and signed X/Y offsets in mm. Explicit zero is meaningful, not inferred. Cut contains the entire clash plus clearance and stays strictly interior. Offset clipping, overflow and boundary contact reject. No engineering/fire/insulation/code constraints invented.
- Required immutable detached 2D host-plane and 3D isometric wireframe review shows measured host, canonical full-depth cut, and actual MEP centerline (2D symbolic point, not outer profile/diameter). Identities, sizes/ranges/minima/offsets/clearance visible. Paint invokes no Revit API. Approval precedes a separate native confirmation defaulting to Cancel.
- Create refreshes every approved input and physical frame/endpoints/volume fingerprint within one caller-owned transaction/group. Native ownership and actual removed-solid/full-clash postconditions precede commit. Uncertain CSG, joins, reinforcing interference and repeat/non-prismatic cuts reject; no bars/linked elements deleted/rehosted.
- Existing runtime fixture adds read-only state, controlled stale penetrant movement with rollback, repeat-cut rejection, test-only failure and full observable identity/state rollback. Native references are reacquired after rollback. UI Cancel and successful Undo are separate operator scenarios.

## Supported boundary — not engineering approval

One native penetrant/host pair per command. Structural straight vertical unjoined uncut rectangular Basic Wall, or structural horizontal global-axis-aligned unjoined uncut rectangular Floor. One actual host/MEP solid; straight perpendicular penetration traversing both faces. Rectangle is conservative, not a circular/minimum outer profile. System/fabric reinforcement blocks globally; nearby Rebar envelopes block conservatively. No persistent preview elements or engineering defaults.

UNSUPPORTED: linked penetrants/hosts/cuts, Beam/Column/family opening hosts, oblique/curved/fittings/accessories/conduit, sloped/rotated/nonrectangular Floors, slanted/curved/nonprismatic Walls, pre-cut hosts/multiple openings on one host, joined geometry, automatic reinforcing redesign/fire-rating/insulation compliance and automatic batch selection. Read-only framing clashes do not imply framing creation support. Keep these fail-closed boundaries until independently approved/validated expansion.

## Final exact-source verification

Clean source018b054d8739dd4ad37d8f51e2ea6c806cc76de5, before documentation-only edits:

| Gate | Result / evidence type |
| --- | --- |
| Test-All | PASS55/55 actual suites;3274 configured weight, NOT assertions;283.23s |
| Golden | PASS238 pure production-linked/domain assertions;15 new cut/offset/projection cases |
| MEP acceptance/domain/creation | PASS34 source /12 arithmetic /20 source-integration checks |
| MEP graphical preview | PASS54 bounds/pixel assertions /18 synthetic renders; minimum/1366×768/1920×1080 ×100/125/150% simulation;2D and3D |
| Preserved Rebar | PASS190 static /37 persistence; full offline layout18250 controls/378 renders |
| API compatibility | Actual Release source compilation2022/2023/2024/2025/2026,0 errors;20 synthetic adapter assertions separately |
| Runtime harness/catalog | PASS50 static /267 catalog checks,36 fixtures; native fixture REGISTERED_COMPILED_NOT_EXECUTED |
| MSI build/payload | Rebuilt candidate/134 extracted-byte/source checks PASS; no installation |
| Signing | BLOCKED exit2, both NotSigned; no trusted timestamp/signing/lifecycle acceptance |
| git diff --check | PASS; generated binaries/artifacts excluded from commits |

Evidence: artifacts/mep-wave3-test-all.log, artifacts/full-stack-qa/latest.json (SourceCommit018b054, SourceHasUncommittedChanges=false), mep-wave3-final-compat.log, mep-wave3-package.log, mep-wave3-signing.log, artifacts/ui-qa/mep-openings/*.png. Reproduce with Windows PowerShell5.1: Verify-KMepAcceptance, Verify-KMepDomain, Verify-MepOpeningCreation, Verify-Phase17GoldenPerformance and STA Verify-MepOpeningPreview. Rebuild Installer/Build-Installer.ps1 before Test-All after production changes; payload source fingerprint must match. Verify-RevitCompatibility requires SDKs2022–2026; unavailable resources cannot certify a build.

## Consolidated P0/P1/P2

P0 none confirmed in bounded source slice, not native safety certification. P1 fixed missing minima/offsets/graphics, native/foreign identity ambiguity/link fallback, round-duct omission, VersionGuid-only in-transaction staleness and invisible perpendicular2D projection. P1 acceptance open: native cut/clash/clearance/parity/stale/duplicate/cancel/rollback/unrelated state/Undo and per-year loading. P2 intentional limits: conservative rectangles/Rebar envelopes/system blockers, single-pair selection and centerline-only wireframe/advanced hosts. No unsupported design calculation invented.

## Exact next-agent instructions

1. Fetch origin; verify fix/preview-hardening, final reported SHA equals remote, clean tree. Preserve divergence without reset. Read this and CONFIRMED_OPENING_WAVE.md; no recovery restart or reimplementation.
2. Obtain operator-confirmed disposable model path/title/year. Use writable nonworkshared QA copy, never named production/project models. Running Revit alone does not establish safety/permission. Confirm Runtime QA's disposable-copy dialog.
3. Execute MEP-NATIVE-OPENING-ROLLBACK on separate Wall AND Floor resources, each with duct/pipe/tray. Finder uses one pair: record actual host/MEP identities/category; do not call a Wall run Floor acceptance. Missing resources BLOCKED. Export genuine reports and identity/state snapshots.
4. Operator matrix: positive/explicit-zero/boundary-invalid clearance; nonzero valid/invalid minima/offsets; flipped/rotated supported Wall/reversed penetrant; missing fields; link/foreign/oblique/endpoint/pre-cut/join/system-Rebar rejection; stale host/MEP; neighboring/different-host Rebar; UI Cancel with no additions; repeat cut; native Create then Undo/unrelated-state restoration. Verify actual geometry/host/full clash, not counts. Complete loading/host acceptance per required year; builds are not loading.
5. Fix genuine P0/P1, focused/full QA/builds, normal commit/push/fetch. Keep Draft PR #3/PRODUCTION_READY=NO until host and authorized signing/disposable-VM lifecycle/required PR gates close. No merge/release.
