# Settings persistence inventory (Phase 12)

Inventory is source-based; QA/runtime output is not treated as user settings.

| Owner | Data | Storage | Current safeguards / status |
|---|---|---|---|
| Application preference | UI language | `%APPDATA%/Autodesk/Revit/Addins/KhimTools/language_config.json` | Schema v1; legacy v0 migration; enum validation; atomic replace and `.bak` recovery via `JsonSettingsPersistence`. |
| User preference | Override color palette | `%APPDATA%/KhimTools/override_colors.json` | Schema v1; migrates legacy 9/16-slot palettes, validates RGB values, atomic replace and backup/default recovery. |
| User presets | Join Elements templates | `%APPDATA%/KhimTools/JoinTemplates/*.json` | Schema v1, category/scope validation, atomic save, legacy schema migration, backup/default recovery. |
| User presets | Section Cut templates | `%APPDATA%/KhimTools/SectionTemplates/*.json` | Schema v1 migration; finite/range/enum validation; atomic replace and backup recovery. |
| User presets | Rebar Column/Beam templates | `%APPDATA%/KhimTools/RebarTemplates/*.json` | Schema v1 migration; validates names/enums/numeric bounds; atomic replace and backup recovery. |
| User presets | Rebar Slab/Foundation templates | `%APPDATA%/KhimTools/RebarTemplates/{Slab,Foundation}/*.json` | Schema v1 migration; validates template name, design code and finite/nonnegative numeric fields; sanitizes file stem; atomic replace and backup recovery. |
| Project values | Rebar configuration | Per-project key-hashed files in app data | Schema v1, numeric validation, stale revision protection, atomic replace, revision backups and latest-valid-backup recovery (behavioral verifier now checks recovery). Keys are strings; project element keys must not be reused cross-project. |
| Project values | K-QS project store | `<project>/.kqs/project.kqs.json` | Schema v1; validates identity/count bounds; atomic replacement and `.bak` recovery. |
| Project/user values | QTO rules | `%APPDATA%/KhimTools/QTO/<document-title>/rules.json` | Schema v1 migration, default-rule merge, validation, atomic replace and `.bak` recovery. Folder key uses title, not stable project identity. |
| Project values | QTO snapshots | Same QTO project folder `/Snapshots` | Schema v1/v2 validation; bounded reads; corrupt latest records are skipped in favor of the newest earlier valid immutable snapshot. |
| Project model data | Sheet revision snapshots / naming templates | Revit Extensible Storage | Legacy primary schema IDs/formats retained; separate additive backup schemas preserve the previous valid payload; malformed primary data is not overwritten unless a valid recovery copy exists. Static verifier is integrated; host behavior still requires Revit QA. |
| Project model data | Slab Step audit | Revit Extensible Storage | Document-scoped metadata; not a reusable user profile. |
| User preference | Sheet Export last selection path | `%APPDATA%` JSON in SheetExport form | Schema v1 legacy migration, path-length validation, atomic replace and `.bak` recovery. |
| Developer/QA | Runtime QA reports and fixtures | Temp/report paths | Test artifacts, not product settings. |
| Distribution metadata | Registry / package manifest | Windows registry and bundle XML | Installer detection/configuration, not app settings. Writes are limited to explicit updater behavior. |
| Explicit user import/export | Rebar configuration JSON; sheet selection JSON/CSV | User-selected path | Import/export boundary, not silently loaded startup state. Inputs still require validation. |

`JsonSettingsPersistence` intentionally handles file replacement/recovery only. Each feature retains its own schema, defaults, validation, and ownership; this is not a global settings model.

## Phase 12 remaining work

Remaining verification work: the fixed-AppData preset managers and Sheet Export/Extensible Storage document integration are covered by source/build gates but still need host-side persistence exercises for corruption and recovery. No host result is claimed; runtime remains deferred. The Extensible Storage payload format remains backward-compatible with its existing primary schemas; the additive backup entity behavior needs execution against a disposable Revit document before the Phase 12 recovery gate can be called fully verified.
