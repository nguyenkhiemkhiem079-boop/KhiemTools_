$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0

function Assert([bool]$condition, [string]$label) {
    if (-not $condition) { throw "FAIL: $label" }
    $script:checks++
}

$handlerPath = Join-Path $root 'KhimTools\Core\ActionEventHandler.cs'
$appPath = Join-Path $root 'KhimTools\Core\App.cs'
$handler = Get-Content -LiteralPath $handlerPath -Raw
$app = Get-Content -LiteralPath $appPath -Raw
$productionCs = @(Get-ChildItem -LiteralPath (Join-Path $root 'KhimTools') -Filter '*.cs' -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$productionText = ($productionCs | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"

Assert ($handler -match 'IExternalEventHandler\s*,\s*IDisposable') 'ExternalEvent handler has an explicit disposal lifecycle'
Assert ($handler -match '_externalEvent\.Dispose\(\)') 'ExternalEvent native resources are released'
Assert ($handler -match 'lock\s*\(_queueLock\)' -and $handler -match '_pendingActions\.Clear\(\)') 'Raise/execute/shutdown queue lifetime is synchronized and cleared'
Assert ($handler -match 'ObjectDisposedException' -and $handler -match 'ExternalEventRequest\.Denied' -and $handler -match 'ExternalEventRequest\.TimedOut') 'Disposed and rejected ExternalEvent requests cannot leave runnable stale work'
Assert ($handler -match 'catch\s*\(Exception ex\)' -and $handler -match 'ExternalEvent action failed') 'Callback exceptions are logged and isolated per queued action'
Assert ($app -match 'EventHandler\s*=\s*null' -and $app -match 'eventHandler\.Dispose\(\)') 'Application startup-failure and shutdown paths release and clear the shared handler'
Assert ($productionText -notmatch '\bTask\.Run\s*\(|\bnew\s+Thread\s*\(|\bParallel\.(For|ForEach|Invoke)\s*\(') 'Production code contains no background worker/parallel Revit API execution pattern'
$asyncFiles = @($productionCs | Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match '\basync\s+(?:void|Task)\b' })
$asyncRevitFiles = @($asyncFiles | Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match 'Autodesk\.Revit\.(DB|UI)' })
Assert ($asyncRevitFiles.Count -eq 0) 'Async production files do not reference Revit DB/UI APIs'
Assert ($productionText -notmatch 'static\s+(?:readonly\s+)?(?:Document|UIDocument|Element)\s+\w+\s*(?:[;=])') 'No static live Revit document/element fields are present'
Assert ($productionText -notmatch '(?i)\bPHM(?:Tools)?\b') 'No PHM source/runtime identifier occurs in production C#'
$trackedTextFiles = @(& git -C $root ls-files -- '*.cs' '*.csproj' '*.props' '*.targets' '*.json' '*.xml' '*.ps1' '*.config' '*.yml' '*.yaml' '*.toml')
$secretPattern = '(?i)(AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|client_secret\s*=\s*[^\s;]{8,})'
$secretFiles = @()
foreach ($relativePath in $trackedTextFiles) {
    $fullPath = Join-Path $root $relativePath
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        if ((Get-Content -LiteralPath $fullPath -Raw) -match $secretPattern) { $secretFiles += $relativePath }
    }
}
Assert ($secretFiles.Count -eq 0) 'Tracked source/config files contain no recognized private-key or credential signatures'
$envFiles = @(Get-ChildItem -LiteralPath $root -Force -File | Where-Object { $_.Name -like '.env*' })
Assert ($envFiles.Count -eq 0) 'Repository root contains no .env secret/config files'
$todoFiles = @($productionCs | Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match '(?i)\b(TODO|FIXME|HACK)\b' })
Assert ($todoFiles.Count -eq 0) 'Production C# has no TODO/FIXME/HACK markers'

Write-Host "PASS: Phase 16 reliability/security static audit ($checks checks; $($productionCs.Count) production C# files; no host execution claimed)" -ForegroundColor Green
exit 0
