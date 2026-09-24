$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$service = Get-Content -Raw (Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\QuickStructure\Services\QuickStructureService.cs')
$window = Get-Content -Raw (Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\QuickStructure\Forms\QuickStructureWindow.xaml.cs')
$request = Get-Content -Raw (Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\QuickStructure\Models\QuickStructureGenerationRequest.cs')
$settings = Get-Content -Raw (Join-Path $repoRoot 'KhimTools\Tools\KhimStructural\QuickStructure\Models\QuickStructureSettings.cs')
$checks = 0
$failures = 0

function Check([string]$name, [bool]$passed) {
    $script:checks++
    if ($passed) { Write-Host "PASS quick-structure:$name" }
    else { Write-Host "FAIL quick-structure:$name"; $script:failures++ }
}

Check 'UI-routes-through-one-production-generation-request' ($window -match 'QuickStructureService\.Generate\(_doc,\s*request\)')
Check 'batch-service-owns-rollback-capable-group' ($service -match 'QuickStructureGenerationResult\s+Generate[\s\S]*?TransactionBoundary\.ExecuteGroup')
Check 'transaction-boundary-owns-instance-create-and-symbol-activation' ($service -match 'ExecuteCreation[\s\S]*?TransactionBoundary\.Execute\(doc,[\s\S]*?symbol\.Activate\(\)')
Check 'columns-have-exact-count-and-postcondition' ($service -match 'CreateColumnsCore[\s\S]*?points\.Count[\s\S]*?created\.Add\(column\)' -and $service -match 'VerifyCreatedAfterCommit\(doc, columns, "columns"\)')
Check 'beams-require-real-eligible-span-and-postcondition' ($service -match 'CreateBeamsCore[\s\S]*?eligible beam spans' -and $service -match 'VerifyCreatedAfterCommit\(doc, beams, "beams"\)')
Check 'footings-require-point-located-columns-and-postcondition' ($service -match 'CreateFootingsCore[\s\S]*?LocationPoint' -and $service -match 'VerifyCreatedAfterCommit\(doc, footings, "footings"\)')
Check 'required-column-parameters-fail-loudly' ($service -match 'SetRequiredParameter\(column, BuiltInParameter\.FAMILY_TOP_LEVEL_PARAM' -and $service -match 'parameter == null \|\| parameter\.IsReadOnly \|\| !parameter\.Set\(value\)')
Check 'family-symbol-and-level-must-belong-to-active-document' ($service -match 'symbol\.Document\.Equals\(doc\)' -and $service -match 'level\.Document\.Equals\(doc\)')
Check 'footing-without-columns-is-rejected-before-mutation' ($service -match 'request\.CreateFootings[\s\S]*?!request\.CreateColumns')
Check 'all-selected-inputs-validated-before-generation' ($window -match 'doCols && \(column == null \|\| columnBase == null\)' -and $window -match 'doBeams && \(beam == null \|\| beamLevel == null\)' -and $window -match 'doFootings && \(footing == null \|\| footingLevel == null\)')
Check 'invalid-numeric-input-is-not-coerced-to-zero' ($window -match 'throw new FormatException\("Invalid numeric input: " \+ input\)')
Check 'success-message-only-follows-generation-result' ($window -match 'QuickStructureGenerationResult result = QuickStructureService\.Generate' -and $window -match 'Tạo thành công:' -and $window.IndexOf('QuickStructureService.Generate') -lt $window.IndexOf('Tạo thành công:'))
Check 'form-has-no-transaction-lifecycle-ownership' ($window -notmatch 'new Transaction\(|\.Start\(\)|\.Commit\(\)|\.RollBack\(\)')
Check 'atomic-failure-is-surfaced-to-user' ($window -match 'catch \(Exception ex\)' -and $window -match 'Không thể tạo cấu kiện')
Check 'request-carries-explicit-operation-flags-and-domain-inputs' ($request -match 'CreateColumns' -and $request -match 'CreateBeams' -and $request -match 'CreateFootings' -and $request -match 'ColumnSymbol' -and $request -match 'BeamSymbol' -and $request -match 'FootingSymbol')
Check 'unsupported-curved-grid-scope-is-visible-to-the-user' ($window -match 'curved grids excluded' -and $service -match 'if \(!\(c1 is Line\)\) continue')
Check 'column-top-level-and-offsets-are-validated' ($service -match 'ColumnTopLevel\.IsValidObject' -and $service -match 'double\.IsNaN\(offset\).*double\.IsInfinity\(offset\)')
Check 'legacy-floor-option-is-explicitly-not-implemented' ($settings -match 'if \(CreateFloor\)' -and $settings -match 'floor generation is not implemented')
Check 'footing-dependency-is-enforced-in-settings-contract' ($settings -match 'CreateFootings && !CreateColumns')

if ($checks -ne 19) { throw "Quick Structure verifier wiring error: expected 19 checks, ran $checks." }
Write-Host "QUICK_STRUCTURE_ACCEPTANCE_CHECKS=$checks"
if ($failures -gt 0) { Write-Host "QUICK_STRUCTURE_ACCEPTANCE=FAIL"; exit 1 }
Write-Host "QUICK_STRUCTURE_ACCEPTANCE=PASS"
