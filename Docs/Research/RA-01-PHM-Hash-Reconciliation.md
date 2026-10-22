# RA-01 — PHM artifact hash reconciliation

**Status:** BLOCKED — the two referenced binaries are not the same artifact, and the baseline binary has not been located.

**Review baseline:** branch `codex/rea-core00-20261023`, starting source commit `18c2cc27350808390b0e7e5a383dafb38b945b40`.

## Artifact ledger

| Evidence ID | Artifact | Size | SHA-256 | Finding |
|---|---|---:|---|---|
| RA01-EV-01 | `PHM-RevitTools-Setup.exe` currently found under `C:\Users\khiem.nguyen\Downloads\app` | 185,187,327 bytes | `87DF6C98B42AC4D303791D1C048CE9ACDB5C45052BF5E8F9AE801DBD65BC5DB8` | Matches the binary hash recorded in the 2026-10-22 review evidence. |
| RA01-EV-02 | PHM binary referenced by the master baseline | Not available | Prefix `571A5A14…` only | The exact file and complete hash are unavailable in the reviewed material. It cannot be equated with EV-01. |

## Decision

Keep both references separate. The 87DF… binary is the only executable currently identified in the reviewed Downloads location. Do not transfer command counts, file manifests, version claims, or runtime conclusions from it to the 571A… baseline reference. RA-05 must inventory each supplied binary under its own hash after the exact 571A… file is provided or recovered.

No installer extraction, command execution, or installation was performed for this record. The observed hash is an integrity identifier; it does not prove vendor origin, safety, or behavior.

## Next evidence needed

1. Locate or supply the exact PHM executable referenced by the baseline, preserving its original bytes.
2. Record its full SHA-256 and size before extraction.
3. Extract each binary independently and keep the output tied to its input hash.
4. Compare manifests, DLLs, frameworks, and commands only within each artifact ledger.
