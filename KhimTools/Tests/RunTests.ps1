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
    (Join-Path $projectRoot "App\Deployment\DeploymentValidator.cs"),
    (Join-Path $projectRoot "App\Deployment\InstallationClassifier.cs"),
    (Join-Path $projectRoot "App\Deployment\SafeDeploymentEngine.cs"),
    (Join-Path $scriptDir "DeploymentTests.cs")
)

Write-Host "Compiling Deployment Security Test Suite..." -ForegroundColor Cyan
& $csc /target:exe /out:$outputExe /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /r:System.Net.Http.dll /nologo $sourceFiles

if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Running tests..." -ForegroundColor Cyan
& $outputExe
$testExitCode = $LASTEXITCODE

Remove-Item $outputExe -Force -ErrorAction SilentlyContinue

exit $testExitCode
