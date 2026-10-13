[CmdletBinding()]
param([string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$paths = @('Installer/Output/K-TOOLS.msi','Installer/Output/K-TOOLS-Setup.exe')
$artifacts = foreach ($path in $paths) {
    $absolute = Join-Path $root $path
    if (!(Test-Path -LiteralPath $absolute -PathType Leaf)) { throw "BLOCKED: candidate missing: $path" }
    $signature = Get-AuthenticodeSignature -LiteralPath $absolute
    [pscustomobject]@{ Path=$path; SHA256=(Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash; SignatureStatus=[string]$signature.Status; TimestampPresent=($null -ne $signature.TimeStamperCertificate) }
}
$certificateReady = $false
if (![string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Certificate thumbprint must be an exact SHA1 store identity.' }
    $certificate = Get-Item -LiteralPath (Join-Path 'Cert:/CurrentUser/My' $CertificateThumbprint) -ErrorAction SilentlyContinue
    if ($null -eq $certificate) { $certificate = Get-Item -LiteralPath (Join-Path 'Cert:/LocalMachine/My' $CertificateThumbprint) -ErrorAction SilentlyContinue }
    $certificateReady = $null -ne $certificate -and $certificate.HasPrivateKey -and $certificate.NotBefore -le (Get-Date) -and $certificate.NotAfter -gt (Get-Date) -and
        @($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq '1.3.6.1.5.5.7.3.3' }).Count -gt 0
}
$candidateSigned = @($artifacts | Where-Object { $_.SignatureStatus -ne 'Valid' -or !$_.TimestampPresent }).Count -eq 0
$directory = Join-Path $root 'artifacts/signing-readiness'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
[pscustomobject]@{ Status=$(if ($candidateSigned) {'SIGNED_CANDIDATE'} else {'BLOCKED'}); Artifacts=$artifacts; CertificatePrivateKeyAndEkuReady=$certificateReady; SigningToolAvailable=($null -ne (Get-Command signtool -ErrorAction SilentlyContinue)); SigningExecuted=$false; LifecycleExecuted=$false } |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $directory 'latest.json') -Encoding UTF8
$artifacts | Format-Table -AutoSize
if (!$candidateSigned) { Write-Output 'SIGNING_READINESS=BLOCKED: trusted timestamped candidate signatures are absent. No signing or installation executed.'; exit 2 }
Write-Output 'SIGNING_READINESS=SIGNED_CANDIDATE; publisher identity/release approval and disposable lifecycle acceptance still required. No signing executed.'
