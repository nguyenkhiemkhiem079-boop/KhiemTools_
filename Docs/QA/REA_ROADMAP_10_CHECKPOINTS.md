# K-Tool REA roadmap — 10 checkpoint QA

Tài liệu này quy định cách ghi nhận QA cho roadmap REA. Nó không thay thế contract của từng task hoặc bằng chứng runtime theo model.

## Trạng thái chuẩn

Chỉ dùng bốn trạng thái:

- **PASS** — checkpoint đã được chạy trên đúng baseline và toàn bộ tiêu chí có bằng chứng kiểm tra được.
- **FAIL** — checkpoint đã chạy và kết quả không đạt ít nhất một tiêu chí.
- **BLOCKED** — chưa thể chạy hoặc hoàn tất vì đã xác định được điều kiện tiên quyết còn thiếu.
- **NOT TESTED** — chưa có lần chạy/bằng chứng cho checkpoint; chưa xác định blocker cụ thể.

Không dùng **PARTIAL**. Nếu chỉ một phần checkpoint đã chạy, giữ trạng thái tổng là `NOT TESTED` hoặc `BLOCKED` và ghi rõ phần đã chạy trong evidence; không nâng thành `PASS`.

## Checkpoint và bằng chứng cần lưu

| ID | Checkpoint | Bằng chứng tối thiểu để xét PASS |
|---|---|---|
| QA-01 | Requirements & Scope | Phạm vi, task ID, loại trừ và quyết định áp dụng được ghi rõ. PASS ở đây chỉ xác nhận scope của đợt review, không nghiệm thu tính năng. |
| QA-02 | Research Sources & Evidence | Nguồn, phiên bản/hash của binary, evidence ID, giới hạn của từng quan sát và trace từ nguồn đến kết luận. Không gộp hai binary khác hash thành một nguồn. |
| QA-03 | Architecture & Dependencies | Module sở hữu, manifest/package, framework/API dependency, contract và phụ thuộc được đối chiếu với source baseline. |
| QA-04 | Revit API Validity | Revit/API target được ghi rõ; callsite được đối chiếu với API mục tiêu và build log tương ứng. Compile đơn lẻ không chứng minh host runtime. |
| QA-05 | Geometry & Engineering Accuracy | Fixture/model xác định, input và đơn vị, phần tử native thực tế, hình học/quantity đối chiếu với tiêu chí và mẫu độc lập. |
| QA-06 | Functional Contract Compliance | Contract ID/version, input được chấp nhận/từ chối, output thực tế và từng acceptance criterion có kết quả. |
| QA-07 | UI/UX & Workflow | Revit/version, command và các bước thao tác, ảnh màn hình hoặc log, kích thước/DPI/ngôn ngữ liên quan tới tiêu chí. |
| QA-08 | Model Safety & Error Handling | Model fingerprint trước/sau, transaction/rollback, lỗi được kích hoạt, element identity và Undo khi thuộc acceptance. |
| QA-09 | Build, Automated Tests & Regression | SHA source, lệnh build/test chính xác, exit code, số pass/fail và log. Chỉ ghi các suite thực sự chạy; compile-only không hoàn tất checkpoint này. |
| QA-10 | Revit Runtime & Final Acceptance | Phiên bản Revit, model QA disposable được nhận diện, add-in load/command thực thi, kết quả native, log/ảnh và model state sau kiểm tra. |

## Quy tắc bằng chứng và cập nhật

1. Mỗi kết quả phải ghim source SHA, task/contract ID, ngày chạy, môi trường, lệnh hoặc bước tái hiện và vị trí evidence.
2. Giữ kết quả gắn với đúng baseline. Không dùng report lịch sử để tuyên bố PASS cho source SHA mới nếu chưa xác minh source và phạm vi khớp.
3. Fixture được đăng ký hoặc biên dịch nhưng chưa chạy trong Revit host là `NOT TESTED`; nếu host/model disposable cần thiết nhưng chưa có, ghi blocker và dùng `BLOCKED` cho gate đang chờ điều kiện đó.
4. Không chạy model-changing QA trên project người dùng. QA native yêu cầu model disposable được xác định rõ và được ủy quyền cho thao tác tạo, rollback và Undo.
5. Không chạy hoặc kích hoạt bất cứ Revit test nào khi KHIM (JAYK) đang làm việc trong Revit; điều này cũng áp dụng cho smoke command chỉ đọc. Chỉ tiếp tục sau khi người dùng không còn làm việc và model disposable đã được chỉ định.
6. Cập nhật một dòng run log mới cho mỗi lần chạy; giữ lại các snapshot lịch sử và không sửa chúng thành kết quả mới.

### Mẫu run record

```text
Checkpoint: QA-xx
Status: PASS | FAIL | BLOCKED | NOT TESTED
Source SHA:
Task / Contract:
Environment / Revit version:
Fixture or model identity:
Command or reproduction steps:
Evidence paths / IDs:
Observed result:
Blocker / next action:
```

## Snapshot từ đợt review 2026-10-22

Workbook ghi QA-01 `PASS` cho scope của đợt review; QA-02–QA-04 `BLOCKED`; QA-05–QA-10 `NOT TESTED`. Đây là snapshot review chưa tái xác minh, không phải nghiệm thu sản phẩm hoặc trạng thái hiện tại của source. Những lần chạy mới phải được ghi thành run record theo SHA thực tế.

## CORE-00 execution record — 2026-10-23

Baseline được chụp tại branch `fix/preview-hardening`, SHA `18c2cc27350808390b0e7e5a383dafb38b945b40`. Checkout chính có sẵn một file draft chưa commit; file đó được giữ nguyên. Bằng chứng build được tạo trong worktree riêng trên cùng SHA.

| ID | Trạng thái trong lượt này | Evidence / blocker |
|---|---|---|
| QA-01 | PASS | Scope 16 wave/68 task được chốt; đây không phải nghiệm thu feature. |
| QA-02 | BLOCKED | `PHM-RevitTools-Setup.exe` hiện có hash SHA256 `87df6c98…bc5db8`, khớp binary đã quan sát trong review. Hash baseline được ghi là `571a5a14…`; binary baseline đó chưa được tìm thấy để đối chiếu. REA managed Evidence ID chưa có. |
| QA-03 | BLOCKED | Chưa tái trích xuất PHM installer để lập manifest/DLL/framework/command ledger theo đúng hash. |
| QA-04 | BLOCKED | Build Revit 2025 thành công, nhưng build không xác minh từng API callsite trong host/runtime. |
| QA-05 | BLOCKED | Chưa có model Revit disposable được xác nhận cho kiểm chứng geometry. |
| QA-06 | NOT TESTED | Chưa chạy acceptance theo từng contract trong lượt này. |
| QA-07 | NOT TESTED | Chưa chạy UI/workflow acceptance trong Revit. |
| QA-08 | BLOCKED | Chưa có model disposable được xác nhận cho rollback/Undo; không dùng model đang mở của người dùng. |
| QA-09 | NOT TESTED | Chỉ chạy compile-only build; chưa chạy automated test/regression suite trên baseline này. |
| QA-10 | BLOCKED | Có tiến trình Revit 2025, nhưng không có fixture model disposable được xác nhận; chưa load add-in hoặc chạy lệnh trên host. |

Build-only evidence: `dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --verbosity minimal` — exit 0, 0 errors, 10 warnings. Đây không phải kết quả QA-09 hoặc QA-10 PASS.

RA-07 baseline SHA đã được ghi nhận; RA-08 checkpoint này đã được chuẩn hóa. Signing và MSI install/upgrade/repair/uninstall lifecycle chưa chạy; cần VM kiểm thử được chỉ định. `rea-agents` không có trong PATH của phiên này nên RA-02 vẫn BLOCKED.

## CORE-00 implementation update — 2026-10-23

Các thay đổi sau được thực hiện trong working tree cô lập `codex/rea-core00-20261023`, bắt đầu từ baseline `18c2cc27350808390b0e7e5a383dafb38b945b40`. Code được compile trong trạng thái nội dung tương ứng với source SHA `7a5705153c471744704424be36d910755902d04c` (sau đó được commit và push lên nhánh này). Checkout chính, `master`, và draft PR của người dùng không bị sửa.

- `KToolsLogger` hiện ghi file UTF-8 theo ngày trong `%LOCALAPPDATA%\K-TOOLS\Logs`, giới hạn 4 MiB/file và giữ tối đa ba file xoay vòng. Lỗi ghi log rơi về `Debug` và không chặn Revit.
- `App` ghi nhận startup, Revit version, shutdown, lỗi startup và lỗi đăng ký workspace pane.
- Thêm `Diagnostics/KhimTools.RevitSmoke`: lệnh `IExternalCommand` chỉ hiển thị Revit/assembly version, không đọc hoặc sửa model. Project được thêm vào `KhimTools.sln`; build DLL không đồng nghĩa lệnh đã được load trong Revit.
- Thêm `Docs/Research/RA-01-PHM-Hash-Reconciliation.md`, `RA-05-PHM-Observed-Binary-Extraction-Blocker.md`, và `RA-06-EnergyExtractor-Audit.md`. RA-01 vẫn BLOCKED do chưa tìm thấy executable mang hash baseline `571A…`. RA-05 BLOCKED: binary 87DF… ghi version Inno 6.7.0, chưa có extractor tương thích đã xác minh. RA-06 hoàn tất static audit riêng cho ZIP v1.0.0 ngày 19-10 và v1.5.0 ngày 20-10; đối soát model vẫn BLOCKED.

Compile-only evidence trên working tree hiện tại:

| Target | Command | Result | Giới hạn |
|---|---|---|---|
| Smoke DLL / Revit 2025 | `dotnet build Diagnostics/KhimTools.RevitSmoke/KhimTools.RevitSmoke.csproj --configuration Release -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 --no-restore --verbosity minimal -t:Rebuild` | Exit 0; 0 warnings; 0 errors | Chưa load bằng Add-in Manager/Revit. |
| Product DLL / Revit 2025 | `dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --no-restore --verbosity minimal -t:Rebuild` | Exit 0; 10 warnings; 0 errors | Compile-only; 2 CS0108 and 8 CS0618 ElementId constructor warnings. No test suite or Revit command was run. |

QA-04 vẫn BLOCKED vì chưa kiểm tra từng API callsite và runtime. QA-09 vẫn NOT TESTED vì không chạy automated regression suite; compile-only chỉ được ghi nhận như bằng chứng build. QA-10 và QA-08 vẫn BLOCKED vì chưa có model disposable được xác nhận. Installer/signing lifecycle chưa chạy. Không deployment/cài đặt/model mutation nào được thực hiện. Code/docs đã được commit thành checkpoint `7a57051` và push lên nhánh feature riêng; chưa có merge hoặc production release.

Không chạy bất cứ Revit test nào khi KHIM (JAYK) đang làm việc trong Revit, kể cả smoke command chỉ đọc. PHM executable chưa được chạy; signature hiện `NotSigned` và Inno extraction bị hoãn vì chưa xác minh parser hỗ trợ setup format 6.7.0.

## RA-05 follow-up — 2026-10-23

Đã tìm thấy ứng viên parser có upstream CI chạy thành công trên fixture Inno Setup 6.7.3 (setup-data version 6.7.0): `scgm0/InnoUnpack.NET`, commit `b221a08e7b60a6507067e246de1879ef5cf2df4b`, CI run `31750293620` (80 passed, 0 failed, 0 skipped). Đây là bằng chứng tương thích kỹ thuật trên fixture chuẩn, không phải xác nhận PHM 87DF… có thể giải nén hay xác minh độ tin cậy của parser. Static source review có giới hạn; không chạy test/build/parser hay Revit. Ứng viên chưa được chấp nhận do commit unsigned, không có release, và chưa có môi trường phân tích cô lập được thiết lập. Vì vậy RA-05/BL-059 vẫn `BLOCKED`; PHM chưa được extract hoặc execute. Chi tiết và giới hạn được ghi trong `Docs/Research/RA-05-PHM-Observed-Binary-Extraction-Blocker.md`.

## BL-012 compile-only update — 2026-10-23

Source commit `4bfbda0c7671e52836e27dcf722fb659343ee91c` adds a versioned SHA-256 manifest and persistent identity stamp for the 43 packaged Rebar Shape files. The source manifest matches all 43 source `.rfa` files and the manifests copied to both Revit 2024 and Revit 2025 build outputs. The loader and shape lookup reject legacy unstamped or mismatched elements; no automatic re-stamp or overwrite is performed. The hash manifest is not a digital signature and does not prove the engineering geometry is correct.

Compile-only results now pass for Revit 2022–2026; the exact matrix, warning counts, and commands at source revision `3e188918a4e6a01ae3d071eca947e223dd80ac0d` are recorded in `Docs/QA/REA_RB01_COMPILE_MATRIX.md`. No test suite, MSI build/install, Revit process, model, or Revit command was run. Runtime Extensible Storage persistence, hook/bend compatibility, shape creation, parity, rollback/Undo, and legacy-shape migration remain unverified. QA-04, QA-05, QA-08, and QA-10 remain gated; RB-01 has not passed its host-acceptance exit criteria.

## RA-05 alternate parser review — 2026-10-23

Static review of `Peiratooo/innoextract-go` commit `a9620314070af84f730cc988903c8d7a707ed70b` found an in-memory extraction API and a documented metadata-parser range through Inno data version 6.7, but its only sample integration fixture is 6.6.1. The repository has no release or Actions workflow, and the checked commit is unverified. It also documents 6.5+ encrypted data as unsupported. No build, test, PHM parse, extraction, or execution was performed. This does not change RA-05: neither this candidate nor `InnoUnpack.NET` is accepted for the unsigned PHM artifact. Details are recorded in `Docs/Research/RA-05-PHM-Observed-Binary-Extraction-Blocker.md`.

## Installer package build follow-up — 2026-10-23

From source commit `fa13ee570bb4e10e7481167978d57d245dcc51c6`, the canonical installer script built Revit 2024 and Revit 2025 targets, the version `2.7.2` MSI, and its bootstrapper with 0 errors. A read-only MSI database review confirmed the two Rebar Shape manifest entries route to the legacy and modern Rebar folders. The packages are unsigned, were not installed, and were not opened in Revit. This is package/build evidence only; QA-09 automated regression remains NOT TESTED, QA-10 remains BLOCKED on the disposable model/user availability gate, and MSI lifecycle acceptance remains open. Exact artifact hashes and limits are in `Docs/QA/REA_MSI_BUILD_EVIDENCE.md`.

## RA-07 current branch pin — 2026-10-23

Pinned the current feature-branch snapshot at `81738fbe9a3e8a5f87d5136d00c956f40c36da3a`. `git status` was clean and `git ls-remote origin refs/heads/codex/rea-core00-20261023` returned the same SHA. Comparing `fa13ee570bb4e10e7481167978d57d245dcc51c6..81738fbe9a3e8a5f87d5136d00c956f40c36da3a` shows only documentation paths changed; therefore the installer payload's product-source fingerprint and hashes remain tied to its recorded source revision. This branch pin does not approve integration or release.

## RA-07 branch checkpoint after CORE-00 safeguards — 2026-10-23

At this checkpoint, `codex/rea-core00-20261023` was at `ff44066b8a8cf60da8557f256c9286c15b840d11`, the worktree was clean, and `git ls-remote origin refs/heads/codex/rea-core00-20261023` returned the same SHA. The branch now includes later Runtime QA harness and Rebar fixture source changes plus the installer Revit-process guard. The existing MSI/build evidence remains tied to earlier product source SHA `fa13ee570bb4e10e7481167978d57d245dcc51c6`; it is not evidence for a package rebuilt from this branch tip. This checkpoint does not approve integration or release.

## Runtime QA safety-preflight update — 2026-10-23

Source checkpoint `5da3765` requires two explicit operator attestations before any Runtime QA fixture can start: KHIM (JAYK) is not working in Revit for the full QA run, and the active model is a detached disposable QA copy. The dashboard no longer captures a model fingerprint when it opens. A selected run captures a complete baseline only after preflight; failed preflight creates `BLOCKED` fixture rows and no baseline is reported as `NOT RUN`, not as a failed rollback.

Evidence on source checkpoint `5da3765`:

- `Tools/Verify-RuntimeQaHarness.ps1`: exit 0, 60/60 static contract assertions.
- Revit 2025 compile-only product build: exit 0, 0 errors, 10 existing warnings (`ColumnWallRelatedFormV1.Load()` hiding and `ElementId(int)` obsolescence).
- No Revit process, command, model, or fixture was opened or run. QA-08 and QA-10 remain `BLOCKED` until a disposable model and safe user work window are available; no host acceptance is claimed.

## Research-only recheck — 2026-10-23

Status: `BLOCKED` for QA-02. Source SHA: `f0dd21c24818f0d8143128b7f91af46c0e178b74`. Scope: RA-01 PHM hash recovery, RA-02 REA runner/Pilot inventory, and RA-05 Inno Setup 6.7.0 parser candidates. Evidence is recorded in `Docs/Research/RA-01-PHM-Hash-Reconciliation.md`, `Docs/Research/RA-02-REA-Engine-and-Pilot-Inventory.md`, and `Docs/Research/RA-05-PHM-Observed-Binary-Extraction-Blocker.md`.

The exact `571A5A14…` baseline installer remains unavailable; no verified REA worker or Pilot assignment exists; and the newly pinned parser is only a compatibility candidate with unsigned source, unresolved component licensing, and no PHM-specific result. A process ownership snapshot found Claude, CodeBuddy, Gemini, and Codex processes under the current Windows user, but none is linked to a REA task or Pilot assignment; see RA-02. No Revit test, Revit host command, or model operation occurred during this research-only recheck. QA-08 and QA-10 remain `BLOCKED`; these source checks do not change CORE-00 or RB-01 host acceptance.

## Runtime QA bypass closure — 2026-10-23

Source SHA: `8f68f44`. A source review found the ribbon command `CmdRebarFixtureQa` could create temporary model elements without the dashboard safety attestations or a full-model post-rollback comparison. The command now collects both explicit safety confirmations, runs shared document preflight and captures its baseline before the transaction group, tracks the fixture IDs, and reports success only after confirmed group rollback and complete fingerprint restoration. The harness verifier includes this command path.

| Check | Result | Scope / limitation |
|---|---|---|
| `Tools/Verify-RuntimeQaHarness.ps1` | Exit 0; 65/65 static assertions | Source-order/contract audit only; no Revit fixture ran. |
| Revit 2025 product build | Exit 0; 10 warnings; 0 errors | Compile only; existing `ColumnWallRelatedFormV1.Load()` and `ElementId(int)` warnings. |
| Revit host | NOT RUN | No Revit process, command, model, or fixture was opened or run. |

This closes a test-harness bypass in source. It does not pass QA-08/QA-10 or close CORE-00/RB-01; native acceptance still requires the operator safety conditions and a designated detached disposable model.

## Installer lifecycle Revit-process guard — 2026-10-23

Source change: `Installer/Test-RealMsiRuntime.ps1` now checks for all local `Revit.exe` processes before creating the Windows Installer COM object or performing installer/build actions. If any Revit process is present, it stops and reports the process IDs; if process inspection fails, it also stops. This reduces the risk of replacing the K-TOOLS bundle while Revit is using it. It does not establish that the machine is disposable, authorize removal of an existing installation, or provide installer lifecycle acceptance.

No test suite, installer lifecycle, Revit process, command, model, or fixture was run for this change. QA-08/QA-10 and the installer acceptance gate remain open; CORE-00 and RB-01 are not closed.

The `Test-All.ps1` dashboard and script header also now identify `Verify-RevitRuntimeQA.ps1` as a source/packaging static audit with no host execution. Its existing machine-readable result line is unchanged. No audit was run; this is a reporting-label correction only and does not add runtime evidence.
