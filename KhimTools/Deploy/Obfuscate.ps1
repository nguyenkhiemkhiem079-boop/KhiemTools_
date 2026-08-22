<#
.SYNOPSIS
    K-TOOLS — Obfuscation Release Script
    Chạy script này khi chuẩn bị build phiên bản release để bảo vệ code.

.DESCRIPTION
    Script tự động:
      1. Kiểm tra / download ConfuserEx CLI (Confuser.CLI.exe)
      2. Build project ở chế độ Release (net48 + net8.0-windows)
      3. Chạy ConfuserEx để obfuscate DLL output
      4. Copy DLL đã obfuscate vào thư mục bundle Deploy/Release/
      5. Báo cáo kết quả

.USAGE
    Mở PowerShell tại thư mục gốc repo, chạy:
        .\KhimTools\Deploy\Obfuscate.ps1

    Hoặc chỉ obfuscate (bỏ qua build):
        .\KhimTools\Deploy\Obfuscate.ps1 -SkipBuild

.NOTES
    Yêu cầu:
      - .NET SDK đã cài (dotnet CLI)
      - ConfuserEx: được download tự động nếu chưa có
        Nguồn: https://github.com/mkaring/ConfuserEx/releases
#>

param(
    [switch]$SkipBuild,
    [string]$TargetFramework = "net48"   # "net48" | "net8.0-windows" | "all"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Đường dẫn ───────────────────────────────────────────────────────────────
$ScriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot  = Resolve-Path "$ScriptDir\.."        # KhimTools/
$RepoRoot     = Resolve-Path "$ProjectRoot\.."       # repo root
$CrProjTemplate = Join-Path $ProjectRoot "KhimTools.crproj"

$ToolsDir     = Join-Path $RepoRoot ".tools"
$ConfuserDir  = Join-Path $ToolsDir "ConfuserEx"
$ConfuserExe  = Join-Path $ConfuserDir "Confuser.CLI.exe"

$ReleaseDir   = Join-Path $ScriptDir "Release"      # Deploy/Release/

# ConfuserEx download URL (mkaring fork — maintained)
$ConfuserUrl  = "https://github.com/mkaring/ConfuserEx/releases/download/v1.6.0/ConfuserEx-CLI.zip"

# ── Helper ───────────────────────────────────────────────────────────────────
function Write-Step { param($msg) Write-Host "`n► $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail { param($msg) Write-Host "  ✗ $msg" -ForegroundColor Red; exit 1 }

# ── Bước 1: Kiểm tra / Download ConfuserEx ──────────────────────────────────
Write-Step "Kiểm tra ConfuserEx CLI..."

if (-not (Test-Path $ConfuserExe)) {
    Write-Warn "Chưa có Confuser.CLI.exe. Đang download từ GitHub..."
    New-Item -ItemType Directory -Force -Path $ConfuserDir | Out-Null
    $ZipPath = Join-Path $ToolsDir "ConfuserEx-CLI.zip"

    try {
        Invoke-WebRequest -Uri $ConfuserUrl -OutFile $ZipPath -UseBasicParsing
        Expand-Archive -Path $ZipPath -DestinationPath $ConfuserDir -Force
        Remove-Item $ZipPath -Force
        Write-Ok "ConfuserEx đã được download vào: $ConfuserDir"
    } catch {
        Write-Fail @"
Không thể download ConfuserEx tự động.
Hãy download thủ công tại: $ConfuserUrl
Giải nén vào: $ConfuserDir
Đảm bảo có file: Confuser.CLI.exe
"@
    }
} else {
    Write-Ok "ConfuserEx tìm thấy: $ConfuserExe"
}

# ── Bước 2: Build Release ────────────────────────────────────────────────────
if (-not $SkipBuild) {
    $frameworks = if ($TargetFramework -eq "all") { @("net48", "net8.0-windows") } else { @($TargetFramework) }

    foreach ($tfm in $frameworks) {
        Write-Step "Build Release — $tfm..."
        $csproj = Join-Path $ProjectRoot "KhimTools.csproj"
        dotnet build $csproj -c Release -f $tfm --nologo
        if ($LASTEXITCODE -ne 0) { Write-Fail "Build thất bại cho $tfm!" }
        Write-Ok "Build thành công: $tfm"
    }
} else {
    Write-Warn "Bỏ qua bước Build (-SkipBuild)."
}

# ── Bước 3: Obfuscate từng framework ─────────────────────────────────────────
$frameworks = if ($TargetFramework -eq "all") { @("net48", "net8.0-windows") } else { @($TargetFramework) }

foreach ($tfm in $frameworks) {
    Write-Step "Obfuscate — $tfm..."

    $InputDir  = Join-Path $ProjectRoot "bin\Release\$tfm"
    $OutputDir = Join-Path $ReleaseDir "$tfm\Obfuscated"

    if (-not (Test-Path (Join-Path $InputDir "KhimTools.dll"))) {
        Write-Fail "Không tìm thấy KhimTools.dll trong $InputDir. Hãy build trước!"
    }

    # Tạo thư mục output
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

    # Tạo file .crproj tạm với đường dẫn thực tế
    $crprojContent = Get-Content $CrProjTemplate -Raw
    $crprojContent = $crprojContent `
        -replace '\{OUTPUT_DIR\}', $OutputDir.Replace('\', '/') `
        -replace '\{INPUT_DIR\}',  $InputDir.Replace('\', '/')

    $TempCrproj = Join-Path $env:TEMP "KhimTools_obfuscate_$tfm.crproj"
    Set-Content -Path $TempCrproj -Value $crprojContent -Encoding UTF8

    Write-Host "  → Input:  $InputDir"
    Write-Host "  → Output: $OutputDir"

    & $ConfuserExe $TempCrproj
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "ConfuserEx thất bại cho $tfm! Kiểm tra log trên."
    }

    Remove-Item $TempCrproj -Force
    Write-Ok "Obfuscation hoàn tất: $OutputDir"
}

# ── Bước 4: Báo cáo kết quả ──────────────────────────────────────────────────
Write-Step "KẾT QUẢ"
Write-Host ""
Write-Host "  DLL đã obfuscate nằm tại:" -ForegroundColor White
foreach ($tfm in $frameworks) {
    $out = Join-Path $ReleaseDir "$tfm\Obfuscated\KhimTools.dll"
    if (Test-Path $out) {
        $size = [math]::Round((Get-Item $out).Length / 1KB, 1)
        Write-Host "    ✓ [$tfm] $out ($size KB)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "  Tiếp theo:" -ForegroundColor White
Write-Host "    1. Mở DLL bằng ILSpy để xác nhận code đã bị obfuscate" -ForegroundColor Gray
Write-Host "    2. Copy DLL vào thư mục bundle Revit để test chức năng" -ForegroundColor Gray
Write-Host "    3. Phân phối thư mục Obfuscated/ cho người dùng" -ForegroundColor Gray
Write-Host ""
Write-Host "  Hoàn tất! 🎉" -ForegroundColor Cyan
