$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$featureRoot = Join-Path $root 'KhimTools\Tools\KhimGen\ScheduleSplit'
$checks = 0
function Read-Feature([string]$relative) { $path = Join-Path $featureRoot $relative; if (-not (Test-Path -LiteralPath $path)) { throw "FAIL: missing ScheduleSplit file $relative" }; return Get-Content -LiteralPath $path -Raw }
function Assert-Text([string]$text, [string]$needle, [string]$label) { if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }; $script:checks++ }
function Assert-NotText([string]$text, [string]$needle, [string]$label) { if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }; $script:checks++ }
$command = Read-Feature 'Commands\CmdSplitSchedule.cs'; $form = Read-Feature 'Forms\ScheduleSplitForm.cs'
$models = (Get-ChildItem (Join-Path $featureRoot 'Models') -Filter '*.cs' -Recurse | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$services = (Get-ChildItem (Join-Path $featureRoot 'Services') -Filter '*.cs' -Recurse | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Assert-Text $command 'class CmdSplitSchedule : IExternalCommand' 'public command'
Assert-Text $form 'Select an explicit ScheduleSheetInstance' 'explicit source selection'
Assert-Text $form 'Refresh Plan' 'preview action'
Assert-Text $form 'Split & Place' 'apply action'
Assert-Text $form 'WORKING_COPY' 'working copy default'
Assert-Text $form 'Cancel' 'cancel action'
Assert-Text $form 'MinimumSize' 'DPI-safe minimum form size'
Assert-Text $form 'TableLayoutPanel' 'responsive form layout'
Assert-Text $models 'ScheduleSplitPlan' 'plan model'
Assert-Text $models 'ScheduleSplitExecutionResult' 'result model'
Assert-Text $models 'ScheduleSplitSourceMode' 'source mode model'
Assert-Text $services 'ScheduleSplitApiAdapter' 'API compatibility adapter'
Assert-Text $services 'ScheduleSheetInstance.Create(doc, sheetId, scheduleId, point, segmentIndex)' 'segment placement API'
Assert-Text $services 'ScheduleSplitPreflightService' 'preflight service'
Assert-Text $services 'ScheduleSplitVerificationService.Verify' 'post execution verification'
Assert-Text $services 'TransactionGroup' 'atomic transaction group'
Assert-Text $services 'SCHEDULE_SHARED_ELSEWHERE' 'shared source guard'
Assert-Text $services 'SourceFingerprint' 'source fingerprint'
Assert-Text $services 'PLACEMENT_COLLISION' 'collision auditing'
Assert-Text $services 'ReLayoutExistingSegments' 'existing split relayout'
Assert-Text $services 'CanViewBeDuplicated' 'working copy duplication capability'
Assert-Text $services 'ViewDuplicateOption.Duplicate' 'working copy duplication'
Assert-Text $services 'GetSegmentCount' 'segment count API'
Assert-Text $services 'SetSegmentHeight' 'height API compatibility surface'
Assert-Text $services 'TargetSheetId' 'target mapping'
Assert-Text $services 'SegmentIndex' 'segment identity'
Assert-NotText $services '.FirstOrDefault()' 'no arbitrary schedule source fallback'
Assert-NotText $services 'catch { }' 'no silent catches'
$fixturePath = Join-Path $root 'KhimTools\Tools\KhimGen\RuntimeQa\Fixtures\ScheduleSplitRuntimeFixture.cs'; if (-not (Test-Path -LiteralPath $fixturePath)) { throw 'FAIL: runtime fixture missing' }; $fixture = Get-Content $fixturePath -Raw
Assert-Text $fixture 'ScheduleSplitRuntimeFixture' 'runtime fixture'
Assert-Text $fixture 'ScheduleSplitPlanner.BuildPlan' 'fixture uses production planner'
Assert-Text $fixture 'ScheduleSplitPreflightService.Validate' 'fixture uses production preflight'
$ribbon = Get-Content (Join-Path $root 'KhimTools\Core\RibbonBuilder.cs') -Raw; Assert-Text $ribbon 'KhimTools.ScheduleSplit.Commands.CmdSplitSchedule' 'ribbon command registration'
foreach ($file in (Get-ChildItem -LiteralPath $featureRoot -Recurse -Filter '*.cs')) { $text = Get-Content -LiteralPath $file.FullName -Raw; if ($text -match '(?ms)catch\s*\{\s*\}') { throw "FAIL: silent catch in $($file.FullName)" }; $checks++ }
Write-Output "PASS: $checks Schedule Split Stage 3.2 static checks. No Revit model operation executed."
