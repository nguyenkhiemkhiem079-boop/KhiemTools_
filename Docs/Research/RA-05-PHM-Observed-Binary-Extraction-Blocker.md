# RA-05 — PHM observed binary extraction status

**Status:** BLOCKED — three technical Inno Setup 6.7.0 parser candidates have been identified, but none is accepted for local extraction. The PHM installer has not been extracted, launched, or installed. This is not a DLL/command inventory.

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

## Alternate parser review — 2026-10-23

A second candidate was reviewed from its primary GitHub source: [`Peiratooo/innoextract-go`](https://github.com/Peiratooo/innoextract-go), main commit `a9620314070af84f730cc988903c8d7a707ed70b` (2026-08-28; GitHub reports the commit as unverified). Its README states that the metadata parser accepts Inno Setup data versions 6.0–6.7 inclusive and includes a 6.6.1 sample integration test; it does not provide a 6.7.0/PHM fixture. The README also documents a 64 MiB header limit, a default 1 GiB extraction/decompression memory limit, and an in-memory API that does not write output files. Encrypted 6.5+ data is explicitly unsupported; the encryption state of the observed PHM binary has not been established by this review.

Static inspection of `internal/pathutil/path.go` found normalization of Windows separators, root/drive prefixes, traversal components, and invalid path characters. The library returns file data and paths to its caller, so an eventual extraction wrapper would still need to enforce output-root containment, Windows device-name/collision rules, and total resource limits before any disk write. No code was built or run, and no installer was supplied to this candidate.

This source is young (created 2026-08-20; 2 stars and 1 fork when checked), has no GitHub release or Actions workflow, and its latest commit is unsigned/unverified. Its Go module declares Go 1.26.5 and external `xz` and `x/crypto` modules. These facts, the lack of a 6.7.0 fixture, and the absence of an independent build/review are insufficient for acceptance. The candidate is recorded for comparison only and was not used to inspect or extract either PHM artifact.

The first two candidates provide different partial evidence: `InnoUnpack.NET` has the upstream 6.7.3 installer fixture exercising setup-data 6.7.0, while `innoextract-go` documents a memory-only API and a 6.0–6.7 parser boundary but tests only a 6.6.1 sample. Neither has accepted provenance or independent PHM compatibility evidence. RA-05 remains `BLOCKED`; no extraction or execution occurred.

## Release candidate check — 2026-10-23

A third technical candidate was checked against the exact GitHub release tag [`ui_2_2_13`](https://github.com/jrathlev/InnoUnpacker-Windows-GUI/releases/tag/ui_2_2_13), which resolves to commit `b94b34935ff4b1019359462c154509de9fd951b9`. The release notes identify console `innounp` 2.71.2 and claim support through Inno Setup 7.1.0. At that pinned commit, `innounp-2/sources/Struct6700u.pas` has blob `648b865cc93a1db4082aa2e908364e1400bdff81` and explicitly defines setup-data version `6.7.0`. This is source-level format evidence for the observed marker, not a PHM parse result.

GitHub release metadata lists `innounp-2.zip` at 618,221 bytes with digest `sha256:10fce27b6344b7674d5625cc34afca1c88c12183d957c6d93e401097fa2556cd`. The archive was not downloaded or run. The pinned commit is unsigned (`verification.verified=false`, reason `unsigned`); the asset digest is an integrity value reported by GitHub, not a publisher signature or independent provenance proof. The repository root has an MIT license, while the pinned tree also contains `innounp-2/sources/licenses/gpl.txt`; licensing of the console source and bundled components requires review before any redistribution or adoption.

This candidate improves the format-compatibility evidence and provides a pinned source and release asset identity. It still has no independent build/reproduction, no approved isolated extraction environment, and no PHM-specific result. It is not accepted for use. RA-05 and BL-059 remain `BLOCKED`; no installer or extractor binary was downloaded, extracted, installed, or executed.

## Next evidence needed

1. Pin and independently verify the extractor source/package identity and provenance; review or replace it with an organization-approved parser.
2. Establish a disposable, isolated analysis environment with a new empty output directory and no execution of extracted content.
3. Recompute and compare the PHM input hash immediately before extraction; record the tool commit/hash, environment, command, and output manifest.
4. Inventory only the `87DF…` binary under its own evidence ID. Do not equate it with the unavailable `571A…` artifact or transfer conclusions between them.
