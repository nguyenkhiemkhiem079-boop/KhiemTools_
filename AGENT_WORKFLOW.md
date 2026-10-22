# Điều phối nhiều agent cho K-TOOLS

## Mục đích

Codex làm đầu mối nâng cấp K-TOOLS/KhimTools: đọc yêu cầu và repo, chia task, theo dõi phụ thuộc, giữ trạng thái chung, rà soát diff và tích hợp. Agent bên ngoài đóng góp theo task được giao; repo và tiêu chí nghiệm thu là nguồn chuẩn cuối cùng.

## Vai trò mặc định

Đây là cách phân công khởi đầu theo loại việc, có thể đổi theo quyền truy cập và năng lực agent trong phiên làm việc:

| Agent | Việc ưu tiên | Đầu ra cần trả |
|---|---|---|
| Codex — Lead/Integrator | Phân tích repo; lập kế hoạch; xác định file sở hữu; chia và theo dõi task; kiểm tra diff; giải quyết phụ thuộc; tích hợp và tổng kết. | Task ledger, quyết định kỹ thuật, diff tích hợp và trạng thái kiểm chứng. |
| Code Buddy — Implementer | Task code đã có phạm vi và tiêu chí rõ; sửa một module hoặc nhóm file riêng. | Diff/commit, file đổi, lệnh đã chạy và câu hỏi còn mở. |
| Antigravity — UI reviewer | Rà soát UI/UX, luồng hiển thị, bố cục và nghiệm thu trực quan khi có môi trường chạy phù hợp. | Nhận xét gắn với màn hình/luồng và bước tái hiện. |
| Grok — Adversarial reviewer | Rà soát độc lập: lỗi logic, trường hợp biên, bảo mật, hồi quy và giả định yếu. | Findings theo mức độ, vị trí, tác động, cách tái hiện; không sửa code khi chưa được giao. |
| Gemini — Spec/research reviewer | Đối chiếu yêu cầu, docs, kiến trúc và đề xuất phương án hoặc review thứ hai. | Nguồn/đường dẫn nội bộ đã đọc, phương án, trade-off và điểm chưa chắc chắn. |

Tên trên là roster điều phối thủ công. Codex hiện không có kết nối phiên này để tự gửi lệnh hoặc theo dõi trạng thái trong Code Buddy, Antigravity, Grok hay Gemini. Nếu công cụ agent không đọc được file repo tự động, dán phần “Gói chỉ dẫn chung” ở cuối tài liệu vào đầu task.

## Cách chia task

1. Ghi mục tiêu, tiêu chí hoàn thành, phạm vi, giới hạn và trạng thái nhánh trước khi giao việc.
2. Tách task theo subsystem và quyền sở hữu file. Các vùng chính hiện tại: add-in Revit (`KhimTools/`), domain (`src/KhimTools.Domain/`), app (`KhimTools/App/`), installer (`Installer/`), UI/UX và đặc tả (`Docs/`), công cụ kiểm tra (`Tools/`, `KhimTools/Tests/`).
3. Chỉ chạy song song khi đầu vào đã ổn định và các task không ghi vào cùng file, cùng project contract hoặc cùng đường dẫn deployment. Nếu có phụ thuộc, biểu diễn rõ task nào phải xong trước.
4. Nếu không có branch/worktree riêng cho mỗi agent, giao các agent review/đề xuất ở chế độ chỉ đọc; chỉ một agent được sửa repo tại một thời điểm.
5. Codex xác minh diff với yêu cầu và trạng thái hiện tại của repo trước khi tích hợp. Một lời khẳng định trong phần bàn giao không thay cho diff hoặc kết quả lệnh.

## Giao việc và theo dõi

Mỗi task có một chủ sở hữu chính. Ghi trạng thái ở bảng bên dưới hoặc trong issue/ghi chú cùng đợt nâng cấp. Dùng `ready_for_review` khi agent đã xong việc và chưa được Codex tích hợp.

| ID | Task | Chủ sở hữu | Phạm vi file | Phụ thuộc | Trạng thái | Bàn giao/ghi chú |
|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — |

Trạng thái chuẩn: `todo`, `in_progress`, `blocked`, `ready_for_review`, `integrated`.

## Mẫu prompt giao task

```text
K-TOOLS / <TASK-ID> — <tiêu đề>
Vai trò của bạn: <implementer | UI reviewer | adversarial reviewer | spec reviewer>

Mục tiêu:
<kết quả mong đợi>

Nguồn cần đọc:
- AGENTS.md
- AGENT_WORKFLOW.md
- <spec/guide/file liên quan>

Phạm vi được giao:
- <file, thư mục hoặc subsystem được sở hữu>
Không được sửa:
- <vùng khác hoặc giao diện công khai ngoài phạm vi>

Tiêu chí hoàn thành:
- <điều kiện kiểm tra được>
Phụ thuộc/giả định:
- <đầu vào đã chốt hoặc điều kiện cần>

Kiểm chứng phù hợp:
- <lệnh kiểm tra hẹp nhất; nếu không thể chạy thì ghi rõ>

Bàn giao theo mẫu K-TOOLS trong AGENT_WORKFLOW.md. Không tích hợp hoặc push thay đổi.
```

## Mẫu bàn giao

```text
TASK: <ID>
STATUS: ready_for_review | blocked
Tóm tắt: <đã làm hoặc phát hiện gì>
Files/diff/commit: <đường dẫn hoặc mã tham chiếu>
Kiểm chứng: <lệnh và kết quả; hoặc chưa chạy cùng lý do>
Giả định/rủi ro: <mô tả ngắn>
Cần Codex/người dùng quyết định: <câu hỏi hoặc “không có”>
```

## Quy tắc tích hợp

- Codex kiểm tra phạm vi diff, tính nhất quán với spec và tài liệu kỹ thuật tại subsystem.
- Khi nhiều task cùng chạm một hợp đồng công khai, định nghĩa hợp đồng trước rồi giao các phần triển khai theo thứ tự.
- Với thay đổi Revit API, rà soát thread/context API, transaction và cả `net48` + `net8.0-windows`.
- Với thay đổi triển khai/cài đặt, rà soát project, bundle/manifests, WiX và scripts kiểm chứng cùng nhau.
- Chỉ chạy kiểm tra phù hợp với thay đổi và môi trường. Các script MSI có thể cài/gỡ phần mềm; chỉ chạy trong máy/VM kiểm thử được chỉ định. Không tuyên bố xác nhận Revit thực tế khi chỉ chạy kiểm tra tĩnh.
- Ghi rõ việc chưa kiểm chứng, xung đột hoặc quyết định còn mở trong trạng thái cuối.

## Gói chỉ dẫn chung

Dán phần này vào đầu task khi giao cho agent ngoài Codex:

> Làm theo `AGENTS.md` và `AGENT_WORKFLOW.md` trong repo K-TOOLS. Chỉ làm phạm vi file được giao; báo trước khi cần mở rộng. Không cùng ghi vào working tree/nhánh với agent khác. Trước khi bàn giao, nêu diff hoặc commit, file đã sửa, kiểm chứng đã chạy và kết quả, giả định/rủi ro, điểm còn mở. Không push, merge, phát hành hoặc deploy nếu task không yêu cầu rõ.
