# RA-05 — PHM observed binary extraction status

**Status:** BLOCKED — installer identity is recorded, but no compatible Inno Setup 6.7.0 extractor is available in this environment. This is not a DLL/command inventory.

## Observed artifact

- Path: `C:\Users\khiem.nguyen\Downloads\app\PHM-RevitTools-Setup.exe`
- Size: 185,187,327 bytes
- SHA-256: `87DF6C98B42AC4D303791D1C048CE9ACDB5C45052BF5E8F9AE801DBD65BC5DB8`
- PE version resources: Product `PHM-RevitTools`, ProductVersion `1.6.0`
- Static Inno marker: `Inno Setup Setup Data (6.7.0)`; message resources identify `6.5.0`
- Authenticode: `NotSigned`

The executable was not launched, installed, or extracted. `NotSigned` is an observed signature state, not proof of malware or benign behavior.

## Extraction-tool check

No `innoextract`, `innounp`, or 7-Zip command was installed in PATH or found in the reviewed Downloads, Codex, and K-TOOL workspace roots. The official [innoextract 1.9 release](https://github.com/dscharrer/innoextract/releases/tag/1.9) describes only preliminary Inno Setup 6.1.0 support. The latest source commit found during this review adds Inno Setup 6.4.0 support ([commit `e58f295`](https://github.com/dscharrer/innoextract/commit/e58f295d80c3bbd18fb01c18983855064ebc361f)); no 6.7.0 support was confirmed. I therefore did not run that older extractor against this installer.

## Next action

Obtain a trusted extractor with confirmed Inno Setup 6.7.0 support, record its source/version/hash, and extract the 87DF… installer only into a new isolated analysis directory. Keep that inventory tied to EV-01. Do not treat it as the missing 571A… baseline binary from [RA-01](RA-01-PHM-Hash-Reconciliation.md); that exact artifact remains unlocated.
