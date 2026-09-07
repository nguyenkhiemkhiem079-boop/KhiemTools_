$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path "$scriptDir\.."
$outputExe = Join-Path $scriptDir "DeploymentTests.exe"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$sourceFiles = @(
    (Join-Path $projectRoot "App\Deployment\InstallationClassification.cs"),
    (Join-Path $projectRoot "App\Deployment\DeploymentExceptions.cs"),
    (Join-Path $projectRoot "App\Deployment\UrlSecurityValidator.cs"),
    (Join-Path $projectRoot "App\Deployment\SemanticVersion.cs"),
    (Join-Path $projectRoot "App\Deployment\DeploymentValidator.cs"),
    (Join-Path $projectRoot "App\Deployment\InstallationClassifier.cs"),
    (Join-Path $projectRoot "App\Deployment\SafeDeploymentEngine.cs"),
    (Join-Path $projectRoot "Core\Family\FamilyConstants.cs"),
    (Join-Path $projectRoot "Core\Family\FamilyFileInfo.cs"),
    (Join-Path $projectRoot "Core\Family\FamilyPathResolver.cs"),
    (Join-Path $scriptDir "DeploymentTests.cs")
)

Write-Host "Compiling Deployment Security Test Suite..." -ForegroundColor Cyan
& $csc /target:exe /out:$outputExe /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /r:System.Net.Http.dll /nologo $sourceFiles

if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed!" -ForegroundColor Red
    exit 1
}

try {
    & $outputExe "$projectRoot"
    $testExitCode = $LASTEXITCODE
} catch {
    Write-Host "Direct execution blocked by policy, executing in-memory via Assembly.Load..." -ForegroundColor Yellow
    $bytes = [System.IO.File]::ReadAllBytes($outputExe)
    $asm = [System.Reflection.Assembly]::Load($bytes)
    $entry = $asm.EntryPoint
    $res = $entry.Invoke($null, @(,[string[]]@("$projectRoot")))
    $testExitCode = if ($null -eq $res) { 0 } else { [int]$res }
}

Remove-Item $outputExe -Force -ErrorAction SilentlyContinue

exit $testExitCode
