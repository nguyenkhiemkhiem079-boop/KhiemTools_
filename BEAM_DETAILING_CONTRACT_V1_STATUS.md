# Beam Manual Detailing Contract v1 status — 2026-10-13

Authority: `Docs/Engineering/Beam/MANUAL_DETAILING_CONTRACT_V1.md`; acceptance matrix: `Docs/QA/Beam/PHASE_04C_ACCEPTANCE_MATRIX.md`.

FULL_CONTRACT_IMPLEMENTATION = PARTIAL
SUPPORTED_SLICE_CODE_COMPLETE = YES
HOST_ACCEPTANCE = NOT_EXECUTED
PRODUCTION_READY = NO

Existing supported Manual slice was audited, not reimplemented: one straight constant rectangular Beam, explicit individual coordinates, stations, roles/layers, exact loaded bar/hook/shape identities, explicit returns/stirrups, independently supplied covers, versioned save/load, rollback production preview, stale/complete-duplicate guards, interference checks and parity-protected atomic Create.

Wave 1 corrected curve-plane validation/normal, physical radius at Beam end faces and malformed persisted geometry. Ten additional numerical/validator goldens execute. Wave 3 corrected the 1366x768 minimum-size mismatch and compressed detailing grid columns. No quantities, diameters, anchorage lengths, cutoff stations or covers were invented.

Continuation: independent multi-Beam assignment is now wired through per-host editor/accepted preview and one atomic creation group. Each selected host gets its own saved/explicit plan; no plan is copied, scaled or remapped. All acceptance fingerprints/duplicates are checked before mutation and the next host is rechecked during the group; parity or generation failure rolls back every host. Six pure identity-policy goldens were added (140 total), K-Rebar160. Live batch atomicity/Undo remain unexecuted.

Unavailable: verified support extension, shared-plan copying/mapping, automatic design and structural-code compliance. Role/group intent is captured in plan/preview data; this audit does not certify a durable per-Rebar detailing ledger. Narrow supported code completeness is not full contractual or structural adequacy.

Live acceptance must exercise straight bars plus actual hooks/returns/stirrups, exact type/shape, quantities/paths, stale edits, complete/partial duplicate selectivity, cancel, rollback, existing-bar preservation and Undo on a confirmed disposable model. The registered baseline fixture is straight-bar coverage only; extended scenarios must be executed separately. No such execution occurred here.
