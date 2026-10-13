$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'ProductSourceFingerprint.ps1')
$manifestPath = Join-Path $root 'Installer/Output/PAYLOAD_SHA256.json'
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'BLOCKED: build the current-source installer first; PAYLOAD_SHA256.json is absent.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.ProductSourceFingerprint -ne (Get-KToolProductSourceFingerprint $root)) { throw 'Installer product-source fingerprint is stale; rebuild from the current source.' }
$msi = Join-Path $root 'Installer/Output/K-TOOLS.msi'
if ($manifest.MsiSHA256 -ne (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Installer bytes do not match the captured package manifest.' }
if (@($manifest.Files).Count -ne 132 -or @($manifest.Files | Group-Object Id | Where-Object Count -ne 1).Count -ne 0) { throw 'The complete 132-file MSI input inventory is missing or contains duplicates.' }
$inspection = Join-Path $root ('artifacts/installer-payload/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $inspection -Force | Out-Null
Push-Location $root
try {
    & dotnet tool run wix -- msi decompile $msi -o (Join-Path $inspection 'package.wxs') -x (Join-Path $inspection 'files')
    if ($LASTEXITCODE -ne 0) { throw 'WiX could not inspect the installer payload.' }
} finally { Pop-Location }
[xml]$extractedXml = Get-Content (Join-Path $inspection 'package.wxs') -Raw
$actualFiles = @($extractedXml.SelectNodes('//*[local-name()="File"]'))
if ($actualFiles.Count -ne $manifest.Files.Count) { throw 'MSI payload count differs from the captured complete source-input set.' }
$checks = 2
foreach ($expected in $manifest.Files) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $expected.Source))
    if (!$source.StartsWith($root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        !(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected.SHA256) {
        throw "Installer input has changed since packaging: $($expected.Id)"
    }
    $actual = @($actualFiles | Where-Object Id -eq $expected.Id)
    if ($actual.Count -ne 1) { throw "MSI payload identity is missing or duplicated: $($expected.Id)" }
    $payloadPath = Join-Path $inspection ('files/File/' + $expected.Id)
    if (!(Test-Path -LiteralPath $payloadPath) -or (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected.SHA256) {
        throw "MSI payload byte mismatch: $($expected.Id)"
    }
    $checks++
}
Write-Output "INSTALLER_PAYLOAD=PASS ($checks checks; all 132 extracted files match captured build inputs; product source and MSI hash current). No installation executed."
