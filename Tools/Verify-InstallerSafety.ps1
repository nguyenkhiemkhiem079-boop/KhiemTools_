$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'Installer/Test-RealMsiRuntime.ps1'
function Test-RejectedLaunch([string]$AdditionalArguments) {
    $process = New-Object Diagnostics.Process
    $process.StartInfo.FileName = (Get-Command powershell).Source
    $process.StartInfo.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $script + '" ' + $AdditionalArguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    try {
        if (!$process.Start()) { throw 'Cannot launch installer safety probe.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return $process.ExitCode -ne 0 -and ($stdout.Result + $stderr.Result) -match 'explicit confirmation of this disposable VM machine name'
    } finally { $process.Dispose() }
}
if (!(Test-RejectedLaunch '')) {
    throw 'Installer lifecycle did not fail closed without disposable-machine confirmation.'
}
$wrongName = 'QA_ONLY_NONEXISTENT_' + [Guid]::NewGuid().ToString('N')
if (!(Test-RejectedLaunch ('-ConfirmedDisposableMachineName ' + $wrongName))) {
    throw 'Installer lifecycle did not fail closed for a different machine confirmation.'
}
$text = Get-Content -LiteralPath $script -Raw
if ($text.IndexOf('Static QA failed; installer mutation is blocked.') -lt 0 -or
    $text.IndexOf('explicit confirmation of this disposable VM machine name') -gt $text.IndexOf('New-Object -ComObject WindowsInstaller.Installer')) {
    throw 'Installer safety gates must precede Windows Installer execution.'
}
Write-Output 'INSTALLER_SAFETY=PASS (3 checks: two actual rejected launches and source ordering). No installation executed.'
exit 0
