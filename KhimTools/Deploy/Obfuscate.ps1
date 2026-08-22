param(
    [switch]$SkipBuild,
    [string]$TargetFramework = "all"   # "net48" | "net8.0-windows" | "all"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Set local dotnet home to avoid sandbox permission issues
$env:DOTNET_CLI_HOME = Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) ".tools\dotnet_home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

# ── Paths ───────────────────────────────────────────────────────────────────
$ScriptDir      = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot    = Resolve-Path "$ScriptDir\.."        # KhimTools/
$RepoRoot       = Resolve-Path "$ProjectRoot\.."       # repo root
$CrProjTemplate = Join-Path $ProjectRoot "KhimTools.crproj"

$ToolsDir       = Join-Path $RepoRoot ".tools"
$ConfuserDir    = Join-Path $ToolsDir "ConfuserEx"
$ConfuserExe    = Join-Path $ConfuserDir "Confuser.CLI.exe"

$ReleaseDir     = Join-Path $ScriptDir "Release"      # Deploy/Release/
$BundleDir      = Join-Path $ReleaseDir "KhimTools.bundle"

# ConfuserEx download URL (mkaring fork - maintained)
$ConfuserUrl    = "https://github.com/mkaring/ConfuserEx/releases/download/v1.6.0/ConfuserEx-CLI.zip"

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step { param($msg) Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Fail { param($msg) Write-Host "  [FAIL] $msg" -ForegroundColor Red; exit 1 }

# ── Step 1: Check / Download ConfuserEx ──────────────────────────────────────
Write-Step "Checking ConfuserEx CLI..."

if (-not (Test-Path $ConfuserExe)) {
    Write-Warn "Confuser.CLI.exe not found. Downloading from GitHub..."
    New-Item -ItemType Directory -Force -Path $ConfuserDir | Out-Null
    $ZipPath = Join-Path $ToolsDir "ConfuserEx-CLI.zip"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $ConfuserUrl -OutFile $ZipPath -UseBasicParsing
        Expand-Archive -Path $ZipPath -DestinationPath $ConfuserDir -Force
        Remove-Item $ZipPath -Force
        Write-Ok "ConfuserEx downloaded to: $ConfuserDir"
    } catch {
        Write-Warn "Could not download ConfuserEx automatically ($($_.Exception.Message)). Will proceed without obfuscation if needed."
    }
} else {
    Write-Ok "ConfuserEx found at: $ConfuserExe"
}

# ── Step 2: Build Release ────────────────────────────────────────────────────
$frameworks = if ($TargetFramework -eq "all") { @("net48", "net8.0-windows") } else { @($TargetFramework) }

if (-not $SkipBuild) {
    foreach ($tfm in $frameworks) {
        Write-Step "Building Release for $tfm..."
        $csproj = Join-Path $ProjectRoot "KhimTools.csproj"
        dotnet build $csproj -c Release -f $tfm --no-restore --nologo
        if ($LASTEXITCODE -ne 0) { 
            # Try with restore if no-restore fails
            dotnet build $csproj -c Release -f $tfm --nologo
            if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed for $tfm!" }
        }
        Write-Ok "Build succeeded: $tfm"
    }
} else {
    Write-Warn "Skipping build step (-SkipBuild)."
}

# ── Step 3: Obfuscate & Prepare Bundles ───────────────────────────────────────
if (Test-Path $ReleaseDir) {
    Remove-Item -Path $ReleaseDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
New-Item -ItemType Directory -Force -Path $BundleDir | Out-Null

# Copy PackageContents.xml
Copy-Item (Join-Path $ScriptDir "PackageContents.xml") (Join-Path $BundleDir "PackageContents.xml") -Force

$ContentsDir = Join-Path $BundleDir "Contents"
New-Item -ItemType Directory -Force -Path $ContentsDir | Out-Null

foreach ($tfm in $frameworks) {
    Write-Step "Processing $tfm output..."

    $isLegacy  = ($tfm -eq "net48")
    $subFolder = if ($isLegacy) { "Legacy" } else { "Modern" }
    $targetDir = Join-Path $ContentsDir $subFolder
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    # Copy .addin manifest
    $addinSource = Join-Path $ScriptDir "$subFolder\KhimTools.addin"
    if (Test-Path $addinSource) {
        Copy-Item $addinSource (Join-Path $targetDir "KhimTools.addin") -Force
    }

    # Copy binaries
    $binDir = Join-Path $ProjectRoot "bin\Release\$tfm"
    Get-ChildItem -Path $binDir -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $targetDir $_.Name) -Force
    }

    # Copy RebarShapes subfolder
    $shapesDir = Join-Path $binDir "RebarShapes"
    if (Test-Path $shapesDir) {
        Copy-Item $shapesDir (Join-Path $targetDir "RebarShapes") -Recurse -Force
    }

    # Obfuscation (if ConfuserEx exists)
    if (Test-Path $ConfuserExe) {
        Write-Step "Obfuscating $tfm with ConfuserEx..."
        $obfOutput = Join-Path $ReleaseDir "$tfm\Obfuscated"
        New-Item -ItemType Directory -Force -Path $obfOutput | Out-Null

        # Build probe paths for ConfuserEx
        $probeDirs = @()
        if ($isLegacy) {
            $probeDirs += "C:\Program Files\Autodesk\Revit 2024"
            $probeDirs += "C:\Program Files\Autodesk\Revit 2023"
            $probeDirs += "C:\Program Files\Autodesk\Revit 2022"
        } else {
            $probeDirs += "C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.30"
            $probeDirs += "C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.23"
            $probeDirs += "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.30"
            $probeDirs += "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.23"
            $nugetRef = Join-Path $env:USERPROFILE ".nuget\packages\nice3point.revit.api.revitapi\2025.0.2\ref\net8.0"
            if (Test-Path $nugetRef) { $probeDirs += $nugetRef }
            $nugetRefUI = Join-Path $env:USERPROFILE ".nuget\packages\nice3point.revit.api.revitapiui\2025.0.2\ref\net8.0"
            if (Test-Path $nugetRefUI) { $probeDirs += $nugetRefUI }
            $probeDirs += "C:\Program Files\Autodesk\Revit 2026"
            $probeDirs += "C:\Program Files\Autodesk\Revit 2025"
        }
        $probeXml = ($probeDirs | Where-Object { Test-Path $_ } | ForEach-Object { "<probePath>$($_.Replace('\', '/'))</probePath>" }) -join "`n  "

        $crprojContent = Get-Content $CrProjTemplate -Raw
        $crprojContent = $crprojContent `
            -replace '\{OUTPUT_DIR\}', $obfOutput.Replace('\', '/') `
            -replace '\{INPUT_DIR\}',  $binDir.Replace('\', '/') `
            -replace '\{PROBE_PATHS\}', $probeXml

        $TempCrproj = Join-Path $ToolsDir "KhimTools_obf_$tfm.crproj"
        Set-Content -Path $TempCrproj -Value $crprojContent -Encoding UTF8

        & $ConfuserExe -n "$TempCrproj"
        if ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $obfOutput "KhimTools.dll"))) {
            # Replace target DLL with obfuscated DLL
            Copy-Item (Join-Path $obfOutput "KhimTools.dll") (Join-Path $targetDir "KhimTools.dll") -Force
            Write-Ok "Obfuscation succeeded and applied for $tfm."
        } else {
            Write-Warn "ConfuserEx finished with non-zero code for $tfm; using release DLL."
        }

        if (Test-Path $TempCrproj) {
            Remove-Item -Path $TempCrproj -Force -ErrorAction SilentlyContinue
        }
    }
}

# ── Step 4: Create Distribution ZIP Bundles ──────────────────────────────────
Write-Step "Creating ZIP distribution bundles..."

$ZipBundle1 = Join-Path $ReleaseDir "KhimTools_Bundle.zip"
$ZipBundle2 = Join-Path $ReleaseDir "K-TOOLS_Bundle.zip"

if (Test-Path $ZipBundle1) { Remove-Item $ZipBundle1 -Force }
if (Test-Path $ZipBundle2) { Remove-Item $ZipBundle2 -Force }

# Compress the KhimTools.bundle directory
Compress-Archive -Path "$BundleDir\*" -DestinationPath $ZipBundle1 -Force
Copy-Item $ZipBundle1 $ZipBundle2 -Force

Write-Ok "Zip 1: $ZipBundle1"
Write-Ok "Zip 2: $ZipBundle2"

# ── Step 5: Build Standalone Installer App ───────────────────────────────────
Write-Step "Building Standalone Installer EXE..."
$appCsproj = Join-Path $ProjectRoot "App\KhiemToolsApp.csproj"
if (Test-Path $appCsproj) {
    dotnet build $appCsproj -c Release -f net48 --nologo
    $installerExe = Join-Path $ProjectRoot "App\bin\Release\net48\KhimTools_Installer.exe"
    if (Test-Path $installerExe) {
        Copy-Item $installerExe (Join-Path $ReleaseDir "KhimTools_Installer.exe") -Force
        Copy-Item $installerExe (Join-Path $ReleaseDir "K-TOOLS_Installer.exe") -Force
        Write-Ok "Installer EXE copied to: $(Join-Path $ReleaseDir "K-TOOLS_Installer.exe")"
    }
}

# ── Step 6: Summary ──────────────────────────────────────────────────────────
Write-Step "BUILD & RELEASE SUMMARY"
Write-Host "  Bundle directory: $BundleDir" -ForegroundColor White
Write-Host "  Package zip 1:    $ZipBundle1" -ForegroundColor Green
Write-Host "  Package zip 2:    $ZipBundle2" -ForegroundColor Green
Write-Host "  Installer EXE:    $(Join-Path $ReleaseDir "K-TOOLS_Installer.exe")" -ForegroundColor Green
Write-Host "`nRelease ready for deployment and GitHub Release upload!" -ForegroundColor Cyan
