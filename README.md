# Khim Tools v2 — Kiến trúc theo OS Tool DEV_GUIDE.md

## Đổi gì so với bản trước (KhimTools v1 — .csproj cũ, copy tay vào Addins)

| | v1 (cũ) | v2 (bản này) |
|---|---|---|
| Loại `.csproj` | Old-style, phải khai từng `<Compile Include>` | **SDK-style**, tự gom hết `.cs` — không cần khai tay |
| Multi-target | 4 Configuration riêng (Debug2023/24/25/26) | **2 TargetFramework**: `net48` (Revit 2022-2024) + `net8.0-windows` (Revit 2025-2026), build 1 lần ra cả 2 |
| Cài đặt | Copy tay `.dll`+`.addin` vào `%AppData%\...\Addins\<năm>\` | **Tự động** copy thành `.bundle` vào `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle\` ngay sau khi build — mở Revit là có, không copy tay |
| Gọi Revit API từ thread khác | Chưa cần (WinForms modal) | Có sẵn `Core/ActionEventHandler.cs`, dùng khi sau này bạn build tool WPF non-modal |

**Code nghiệp vụ (SlabJoin/, RebarTool/) giữ nguyên 100% — không sửa gì bên trong**, vì WinForms hiện tại gọi `ShowDialog()` ngay trong `IExternalCommand.Execute()`, tức đã chạy đúng trên Revit main thread, không bắt buộc phải bọc qua `ActionEventHandler`. `ActionEventHandler` chỉ cần khi bạn build tool mới dùng WPF `Show()` (non-modal) hoặc code chạy async.

## Việc bạn PHẢI làm trước khi build

1. **Sửa đường dẫn Revit trong `KhimTools.csproj`** nếu máy bạn không cài đúng năm mặc định:
   ```xml
   <RevitVersionForReference>2024</RevitVersionForReference>   <!-- dùng cho net48 -->
   <RevitVersionForReference>2026</RevitVersionForReference>   <!-- dùng cho net8.0-windows -->
   ```
   Đây chỉ là **năm dùng để lấy RevitAPI.dll làm reference lúc build** (API tương đối ổn định trong nhóm 2022-2024 và nhóm 2025-2026), không giới hạn việc bạn chạy add-in ở năm nào trong nhóm đó lúc runtime.

2. **Cài .NET 8 SDK** nếu máy chưa có (build net8.0-windows cần), tải tại dotnet.microsoft.com — Visual Studio 2022 bản mới thường đã kèm sẵn.

## CẦN VERIFY khi build (chưa test được vì môi trường này không có Visual Studio/Revit)

1. **`PackageContents.xml`** (`Deploy/PackageContents.xml`) — mình viết theo đúng cấu trúc bundle phổ biến của Autodesk (`ApplicationPackage` > `Components` > `RuntimeRequirements` + `ComponentEntry`), nhưng **chưa test thật**. Nếu Revit không nhận bundle (không thấy tab Khim Tools dù build xong), khả năng cao lỗi ở file này — so sánh lại với 1 bundle mẫu thật (Autodesk App Store có nhiều add-in mã nguồn mở dùng format này để đối chiếu).
2. **Post-build target `DeployKhimToolsBundle`** trong `.csproj` — copy đúng file build ra vào đúng `Contents/Legacy/` hoặc `Contents/Modern/`, dựa theo `$(TargetFramework)`. Nếu build lỗi ngay bước này (không phải lỗi code), có thể do quyền ghi vào `%ProgramData%` — thử chạy Visual Studio **as Administrator**.
3. Build 2 TargetFramework cùng lúc (`dotnet build` hoặc Ctrl+Shift+B trong VS) sẽ chạy `DeployKhimToolsBundle` **2 lần** (1 lần mỗi TFM) — bình thường, đúng như thiết kế (mỗi lần đổ vào đúng subfolder Legacy/Modern riêng).

## Cách build

Mở `KhimTools.sln` bằng Visual Studio 2022, chọn Configuration `Debug` hoặc `Release` (không còn theo năm nữa), Platform `x64`, bấm Build. VS sẽ tự build cả 2 TargetFramework nếu configured đúng multi-target, sau đó bundle tự có mặt ở `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle\`. Mở Revit (bất kỳ năm nào 2022-2026) là load được ngay, **không cần copy tay nữa**.

## Nếu bundle không lên sau khi build

Kiểm tra thủ công `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle\` có đúng cấu trúc:
```
KhimTools.bundle\
├── PackageContents.xml
└── Contents\
    ├── Legacy\   (net48 — Revit 2022-2024)
    │   ├── KhimTools.dll
    │   ├── KhimTools.addin
    │   └── RebarShapes\*.rfa
    └── Modern\   (net8.0-windows — Revit 2025-2026)
        ├── KhimTools.dll
        ├── KhimTools.addin
        └── RebarShapes\*.rfa
```
Nếu thiếu file nào, báo mình biết đang thiếu gì để sửa target `DeployKhimToolsBundle`.
