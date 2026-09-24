param([string]$Root = (Join-Path $PSScriptRoot ".."))
$ErrorActionPreference = "Stop"
$qaRoot = Join-Path $Root "KhimTools\Tools\KhimGen\RuntimeQa"
$passed = 0
$failed = 0

function Pass([string]$name) { $script:passed++; Write-Host "[PASS] $name" -ForegroundColor Green }
function Fail([string]$name, [string]$detail) { $script:failed++; Write-Host "[FAIL] $name - $detail" -ForegroundColor Red }
function Require-File([string]$relative, [string]$label) {
    $path = Join-Path $Root $relative
    if (Test-Path $path) { Pass $label; return $true }
    Fail $label "Missing $relative"; return $false
}
function Require-Token([string]$file, [string]$token, [string]$label) {
    $path = Join-Path $Root $file
    if ((Get-Content $path -Raw) -match [regex]::Escape($token)) { Pass $label; return $true }
    Fail $label "Token '$token' missing"; return $false
}

Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Commands\CmdRuntimeQa.cs" "CmdRuntimeQa exists" | Out-Null
Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Core\IRuntimeQaFixture.cs" "Fixture interface exists" | Out-Null
Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs" "Registry exists" | Out-Null
Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaReportWriter.cs" "Report writer exists" | Out-Null
Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaSafetyGuard.cs" "Safety guard exists" | Out-Null
Require-File "KhimTools\Tools\KhimStructural\RebarTool\Commands\CmdRebarFixtureQa.cs" "Existing Rebar fixture still exists" | Out-Null
Require-File "KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\SettingsRecoveryRuntimeFixture.cs" "Settings recovery host fixture exists" | Out-Null
Require-Token "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs" "new SettingsRecoveryRuntimeFixture()" "Settings recovery fixture is registered" | Out-Null
Require-Token "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaRegistry.cs" 'Id = "SETTINGS"' "Settings recovery suite is selectable" | Out-Null
Require-Token "KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\SettingsRecoveryRuntimeFixture.cs" "TryReplacePayload" "Settings fixture injects corruption only inside rollback group" | Out-Null
Require-Token "KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\SettingsRecoveryRuntimeFixture.cs" "VerifyAdditionalRollbackState" "Settings fixture verifies exact payload restoration" | Out-Null
Require-Token "KhimTools\Tools\KhimGen\RuntimeQa\Core\RuntimeQaFixtureBase.cs" "groupRollbackSucceeded && RuntimeQaSafetyGuard.VerifyRollback" "Fixture result requires transaction-group rollback" | Out-Null

$status = Get-Content (Join-Path $Root "KhimTools\Tools\KhimGen\RuntimeQa\Models\QaStatus.cs") -Raw
if ($status -match "PASS" -and $status -match "FAIL" -and $status -match "BLOCKED" -and $status -match "SKIPPED" -and $status -match "NOT_RUN") { Pass "Status enum contains PASS/FAIL/BLOCKED/SKIPPED/NOT_RUN" } else { Fail "Status enum" "Required statuses are incomplete" }

$allQa = Get-ChildItem $qaRoot -Recurse -Filter "*.cs" | Get-Content -Raw
if ($allQa -match "TransactionGroup" -and $allQa -match "\.RollBack\(\)") { Pass "TransactionGroup rollback pattern exists" } else { Fail "TransactionGroup rollback" "Fixture rollback boundary missing" }
if ($allQa -match "Path\.GetTempPath\(\)" -and $allQa -match "RuntimeQA") { Pass "Filesystem QA uses temp paths" } else { Fail "Temp filesystem policy" "Runtime QA output is not isolated under temp" }
if ($allQa -notmatch "Task\.Run|Parallel\.ForEach|\.AsParallel\(") { Pass "No background/parallel Revit API work" } else { Fail "Revit API threading" "Unsafe background API pattern found" }
if ($allQa -notmatch "\.Assimilate\(\)") { Pass "QA fixture groups do not assimilate" } else { Fail "QA rollback policy" "A QA fixture calls Assimilate" }

$prodFiles = Get-ChildItem (Join-Path $Root "KhimTools") -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notlike "*\Tools\KhimGen\RuntimeQa\*" -and $_.FullName -notlike "*\Core\RibbonBuilder.cs" }
$prodText = ($prodFiles | Get-Content -Raw) -join "`n"
if ($prodText -notmatch "namespace\s+KhimTools\.RuntimeQa") { Pass "Production code does not depend on QA namespace" } else { Fail "Dependency direction" "A production source file references the QA namespace" }

$cmd = Get-Content (Join-Path $qaRoot "Commands\CmdRuntimeQa.cs") -Raw
if ($cmd -match "ShowDialog" -and $cmd -notmatch "RunAll\(context\)") { Pass "No automatic QA run at startup" } else { Fail "Startup boundary" "CmdRuntimeQa should wait for explicit user action" }
$report = Get-Content (Join-Path $qaRoot "Core\RuntimeQaReportWriter.cs") -Raw
if ($report -match "runtime-qa\.json" -and $report -match "runtime-qa\.txt") { Pass "JSON and human-readable reports" } else { Fail "Report outputs" "Expected report names missing" }

Write-Host "RUNTIME QA HARNESS AUDIT: $passed / $($passed + $failed) PASSED" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
if ($failed -gt 0) { exit 1 } else { exit 0 }
