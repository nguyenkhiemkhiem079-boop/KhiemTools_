$token = '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'
$owner = 'nguyenkhiemkhiem079-boop'
$repo  = 'KhiemTools_'
$tag   = 'v2.7.0'

$headers = @{
    'Authorization' = "Bearer $token"
    'Accept'        = 'application/vnd.github.v3+json'
    'User-Agent'    = 'K-TOOLS-ReleaseScript'
}

Write-Host "Creating GitHub Release for $tag..." -ForegroundColor Cyan

$releaseNotes = @"
### K-TOOLS v2.7.0 - Release Notes

- **Kiến trúc 4-Panel Mới**: Phân nhóm toàn bộ công cụ khoa học thành 4 Ribbon Panel: **K-GEN**, **K-STRUCTURAL**, **K-ARCHITECTURAL**, **K-MEP**.
- **Borderless Custom Title Bar (KTBaseForm)**: Giao diện Form hiện đại không viền Windows thô, kéo thả mượt mà, đồng bộ phong cách UI chuyên nghiệp.
- **Sửa Triệt Để Mã Hóa Tiếng Việt (UTF-8)**: Khôi phục chuẩn xác hiển thị tiếng Việt trên toàn bộ hệ thống Form Rebar (Móng, Cột, Dầm, Sàn, Setup Cover).
- **Nâng Cấp DWG/PDF Export & In Ấn**: Bổ sung bộ chọn định dạng rõ ràng (PDF / DWG / Cả hai), thanh tiến trình thời gian thực (ProgressBar) và hộp thoại thống kê báo cáo kết quả chi tiết.
- **Tối Ưu & An Toàn Thép Dầm**: Sửa triệt để lỗi `NullReferenceException` khi thiếu Bar Type và tích hợp `RebarSafetyValidator` kiểm tra thép nằm ngoài hình học Host.
- **Section Cut Đa Hướng**: Bổ sung chế độ lọc cắt mặt cắt theo phương (Cả 2 phương / Chỉ phương X / Chỉ phương Y).
"@

$body = @{
    tag_name         = $tag
    target_commitish = 'master'
    name             = "Release $tag - K-TOOLS"
    body             = $releaseNotes
    draft            = $false
    prerelease       = $false
} | ConvertTo-Json

$release = $null
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases" -Method Post -Headers $headers -Body $body -ContentType "application/json"
    Write-Host "Release created: $($release.html_url)" -ForegroundColor Green
} catch {
    Write-Host "Release might already exist, fetching existing release..." -ForegroundColor Yellow
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$tag" -Headers $headers
    Write-Host "Found release: $($release.html_url)" -ForegroundColor Green
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Resolve-Path "$ScriptDir\.."
$ReleaseDir = Join-Path $RepoRoot "KhimTools\Deploy\Release"
$BundleDir  = Join-Path $ReleaseDir "KhimTools.bundle"

# Tự động nén thư mục KhimTools.bundle thành file ZIP mới nhất trước khi upload
if (Test-Path $BundleDir) {
    Write-Host "Compressing KhimTools.bundle into KhimTools_Bundle.zip..." -ForegroundColor Cyan
    $zip1 = Join-Path $ReleaseDir "KhimTools_Bundle.zip"
    $zip2 = Join-Path $ReleaseDir "K-TOOLS_Bundle.zip"
    if (Test-Path $zip1) { Remove-Item $zip1 -Force }
    if (Test-Path $zip2) { Remove-Item $zip2 -Force }
    Compress-Archive -Path "$BundleDir\*" -DestinationPath $zip1 -Force
    Copy-Item $zip1 $zip2 -Force
    Write-Host "  [OK] Successfully compressed bundle zip files!" -ForegroundColor Green
}

$filesToUpload = @(
    @{ Path = Join-Path $ReleaseDir "KhimTools_Bundle.zip"; Name = "KhimTools_Bundle.zip"; ContentType = "application/zip" },
    @{ Path = Join-Path $ReleaseDir "K-TOOLS_Bundle.zip"; Name = "K-TOOLS_Bundle.zip"; ContentType = "application/zip" },
    @{ Path = Join-Path $ReleaseDir "K-TOOLS_Installer.exe"; Name = "K-TOOLS_Installer.exe"; ContentType = "application/octet-stream" },
    @{ Path = Join-Path $ReleaseDir "KhimTools_Installer.exe"; Name = "KhimTools_Installer.exe"; ContentType = "application/octet-stream" }
)

# Fetch existing assets to avoid duplicate name error
$existingAssets = @()
try {
    $existingAssets = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/$($release.id)/assets" -Headers $headers
} catch { }

foreach ($file in $filesToUpload) {
    if (Test-Path $file.Path) {
        $existing = $existingAssets | Where-Object { $_.name -eq $file.Name }
        if ($existing) {
            Write-Host "Deleting existing asset $($file.Name)..." -ForegroundColor Yellow
            try {
                Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/assets/$($existing.id)" -Method Delete -Headers $headers | Out-Null
            } catch { }
        }

        Write-Host "Uploading $($file.Name)..." -ForegroundColor Cyan
        $uploadUri = "https://uploads.github.com/repos/$owner/$repo/releases/$($release.id)/assets?name=$($file.Name)"
        
        $uploadHeaders = @{
            'Authorization' = "Bearer $token"
            'Content-Type'  = $file.ContentType
            'User-Agent'    = 'K-TOOLS-ReleaseScript'
        }

        try {
            $bytes = [System.IO.File]::ReadAllBytes($file.Path)
            $res = Invoke-RestMethod -Uri $uploadUri -Method Post -Headers $uploadHeaders -Body $bytes
            Write-Host "  [OK] Uploaded $($file.Name) successfully!" -ForegroundColor Green
        } catch {
            Write-Host "  [ERROR] Upload failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`n=== RELEASE $tag PUBLISHED TO GITHUB SUCCESSFULLY ===" -ForegroundColor Green
Write-Host "Release URL: $($release.html_url)" -ForegroundColor Cyan