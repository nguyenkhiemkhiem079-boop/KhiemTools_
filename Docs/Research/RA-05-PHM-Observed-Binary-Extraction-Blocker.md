# RA-05 — PHM observed binary extraction status

**Status:** BLOCKED — a technical Inno Setup 6.7.0 candidate has been identified and statically reviewed, but it has not been accepted for local extraction. The PHM installer has not been extracted, launched, or installed. This is not a DLL/command inventory.

## Observed artifact

- Path: `C:\Users\khiem.nguyen\Downloads\app\PHM-RevitTools-Setup.exe`
- Size: 185,187,327 bytes
- SHA-256: `87DF6C98B42AC4D303791D1C048CE9ACDB5C45052BF5E8F9AE801DBD65BC5DB8`
- PE version resources: Product `PHM-RevitTools`, ProductVersion `1.6.0`
- Static Inno marker: `Inno Setup Setup Data (6.7.0)`; message resources identify `6.5.0`
- Authenticode: `NotSigned`

`NotSigned` records the observed signature state; it does not establish whether the installer is malicious or benign. The executable was not launched, installed, or extracted.

## Technical compatibility candidate

Static review identified [`scgm0/InnoUnpack.NET`](https://github.com/scgm0/InnoUnpack.NET) at commit [`b221a08e7b60a6507067e246de1879ef5cf2df4b`](https://github.com/scgm0/InnoUnpack.NET/commit/b221a08e7b60a6507067e246de1879ef5cf2df4b), dated 2026-08-13. The repository declares MIT and targets .NET 10. Its project file has no package references.

The checked-in fixture list includes the official Inno Setup 6.7.3 installer and expects setup-data version `6.7.0`. `ArchiveSmokeTests.ExtractAllFiles` exercises extraction for that fixture. GitHub Actions run [`31750293620`](https://github.com/scgm0/InnoUnpack.NET/actions/runs/31750293620) completed successfully on the same commit; its logs report 80 passed, 0 failed, and 0 skipped tests, including the 6.7.3 fixture, plus a successful performance gate. This is upstream CI evidence for the standard fixture, not an independent reproduction and not evidence that the distinct PHM binary parses correctly.

## Limited static safety review

At the pinned commit, the core library and CLI were inspected for obvious process-launch, network-client, native-library loading, and dynamic-assembly-loading calls; none were found in the reviewed paths. The library reads the installer as data and writes extracted files. Its extraction path resolver rejects rooted paths and checks the normalized destination remains under the selected output root. The CLI invokes extraction only when explicitly run with the `extract` command.

This was a targeted source review, not a full security audit or sandbox test. In particular, no independent adversarial-archive/path test was run, and this review does not establish protection against resource exhaustion or filesystem reparse points. Any later extraction must target a new empty directory in a disposable, isolated analysis environment; extracted PE/DLL/script files must remain inert and must not be opened or executed as programs.

## Trust and acceptance status

The candidate is not yet accepted for use on the PHM artifact:

- The repository is young (created 2026-08-10), has 1 star and 1 fork, and has no GitHub Release.
- The reviewed commit is unsigned (`verification.verified=false`).
- No local build, package provenance verification, or independent reproduction of its CI was performed.
- Its .NET 10 target and upstream fixture success establish plausible format coverage, not parser trustworthiness or PHM-specific compatibility.
- The PHM artifact is unsigned and has not been tested with this parser.

Therefore RA-05 remains `BLOCKED`. Do not report the PHM manifest, DLL inventory, or command count based on this candidate until the parser is accepted and extraction is performed under the isolation conditions above. Keep the `87DF…` artifact separate from the missing `571A…` baseline artifact in [RA-01](RA-01-PHM-Hash-Reconciliation.md).

## Next evidence needed

1. Pin and independently verify the extractor source/package identity and provenance; review or replace it with an organization-approved parser.
2. Establish a disposable, isolated analysis environment with a new empty output directory and no execution of extracted content.
3. Recompute and compare the PHM input hash immediately before extraction; record the tool commit/hash, environment, command, and output manifest.
4. Inventory only the `87DF…` binary under its own evidence ID. Do not equate it with the unavailable `571A…` artifact or transfer conclusions between them.
