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

Các thay đổi sau được thực hiện trong working tree cô lập `codex/rea-core00-20261023`; `HEAD` vẫn là baseline `18c2cc27350808390b0e7e5a383dafb38b945b40`, chưa có commit mới. Checkout chính và draft của người dùng không bị sửa.

- `KToolsLogger` hiện ghi file UTF-8 theo ngày trong `%LOCALAPPDATA%\K-TOOLS\Logs`, giới hạn 4 MiB/file và giữ tối đa ba file xoay vòng. Lỗi ghi log rơi về `Debug` và không chặn Revit.
- `App` ghi nhận startup, Revit version, shutdown, lỗi startup và lỗi đăng ký workspace pane.
- Thêm `Diagnostics/KhimTools.RevitSmoke`: lệnh `IExternalCommand` chỉ hiển thị Revit/assembly version, không đọc hoặc sửa model. Project được thêm vào `KhimTools.sln`; build DLL không đồng nghĩa lệnh đã được load trong Revit.
- Thêm `Docs/Research/RA-01-PHM-Hash-Reconciliation.md`, `RA-05-PHM-Observed-Binary-Extraction-Blocker.md`, và `RA-06-EnergyExtractor-Audit.md`. RA-01 vẫn BLOCKED do chưa tìm thấy executable mang hash baseline `571A…`. RA-05 BLOCKED: binary 87DF… ghi version Inno 6.7.0, chưa có extractor tương thích đã xác minh. RA-06 hoàn tất static audit riêng cho ZIP v1.0.0 ngày 19-10 và v1.5.0 ngày 20-10; đối soát model vẫn BLOCKED.

Compile-only evidence trên working tree hiện tại:

| Target | Command | Result | Giới hạn |
|---|---|---|---|
| Smoke DLL / Revit 2025 | `dotnet build Diagnostics/KhimTools.RevitSmoke/KhimTools.RevitSmoke.csproj --configuration Release -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 --no-restore --verbosity minimal -t:Rebuild` | Exit 0; 0 warnings; 0 errors | Chưa load bằng Add-in Manager/Revit. |
| Product DLL / Revit 2025 | `dotnet build KhimTools/KhimTools.csproj --configuration Release --framework net8.0-windows -p:RevitVersionForReference=2025 -p:RevitRequestedVersion=2025 -p:DeployKhimToolsBundle=false --no-restore --verbosity minimal -t:Rebuild` | Exit 0; 10 warnings; 0 errors | Compile-only; 2 CS0108 and 8 CS0618 ElementId constructor warnings. No test suite or Revit command was run. |

QA-04 vẫn BLOCKED vì chưa kiểm tra từng API callsite và runtime. QA-09 vẫn NOT TESTED vì không chạy automated regression suite; compile-only chỉ được ghi nhận như bằng chứng build. QA-10 và QA-08 vẫn BLOCKED vì chưa có model disposable được xác nhận. Installer/signing lifecycle chưa chạy. Không có deployment, cài đặt, commit hoặc push nào được thực hiện tính đến snapshot build này.

Không chạy bất cứ Revit test nào khi KHIM (JAYK) đang làm việc trong Revit, kể cả smoke command chỉ đọc. PHM executable chưa được chạy; signature hiện `NotSigned` và Inno extraction bị hoãn vì chưa xác minh parser hỗ trợ setup format 6.7.0.
