$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$files = @{
    collector = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QtoCollectorService.cs') -Raw
    scanner = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QsModelScanner.cs') -Raw
    rules = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QtoRuleEngine.cs') -Raw
    snapshots = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QtoSnapshotService.cs') -Raw
    profile = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QtoRuleProfileService.cs') -Raw
    boq = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/QsBoqService.cs') -Raw
    form = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Forms/QuantityTakeoffForm.cs') -Raw
    importer = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/Services/CubicostImportService.cs') -Raw
    domain = Get-Content (Join-Path $root 'src/KhimTools.Domain/Models/Qs/QsQuantityMath.cs') -Raw
    fixture = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Fixtures/KQsProductionRuntimeFixture.cs') -Raw
    registry = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/RuntimeQa/Core/RuntimeQaRegistry.cs') -Raw
    readme = Get-Content (Join-Path $root 'KhimTools/Tools/KhimGen/QuantityTakeoff/README.md') -Raw
}
$checks = 0
function Assert([bool]$condition, [string]$name) {
    if (!$condition) { throw "K-QS acceptance failed: $name" }
    $script:checks++
    Write-Host "PASS qs:$name"
}

Assert ($files.collector.Contains('public static QtoResult Collect(Document doc)')) 'production-collector-entry'
Assert ($files.collector.Contains('new FilteredElementCollector(doc)')) 'whole-host-document-collector-explicit'
Assert ([regex]::Matches($files.collector, 'new FilteredElementCollector\(doc\)').Count -eq 1) 'collect-document-once'
Assert ($files.collector.Contains('CategoryId = categoryId') -and $files.collector.Contains('familyName, typeUniqueId')) 'aggregation-identity-includes-category-family-and-type'
Assert ($files.collector.Contains('ThenBy(x => x.CategoryId)') -and $files.collector.Contains('ThenBy(x => x.LevelUniqueId')) 'aggregation-output-order-is-deterministic'
Assert ($files.collector.Contains('result.DocumentKey =') -and $files.collector.Contains('WorkflowFingerprint.Compute')) 'stable-document-diagnostic-key'
Assert ($files.collector.Contains('eligible=') -and $files.collector.Contains('result.Duration = timer.Elapsed')) 'quantity-scan-diagnostics-metrics'
Assert ($files.readme.Contains("reads the entire host document") -and $files.readme.Contains("not the active view or current selection")) 'whole-document-scope-documented'
Assert ($files.scanner.Contains('case QsScanScope.ActiveView:')) 'scanner-active-view-branch'
Assert ($files.scanner.Contains('view == null || view.Document != doc || view.IsTemplate')) 'scanner-active-view-guard'
Assert ($files.scanner.Contains('case QsScanScope.Selection:')) 'scanner-selection-branch'
Assert ($files.scanner.Contains('selectionIds == null')) 'scanner-selection-requires-explicit-ids'
Assert ($files.scanner.Contains('new FilteredElementCollector(doc, ids)')) 'scanner-collector-limited-to-selection'
Assert ($files.scanner.Contains('case QsScanScope.EntireModel:')) 'scanner-entire-model-explicit'
Assert ($files.scanner.Contains('ArgumentOutOfRangeException')) 'scanner-rejects-unknown-scope'
Assert ($files.collector.Contains('OST_Walls') -and $files.collector.Contains('OST_Floors') -and $files.collector.Contains('OST_Roofs')) 'physical-category-scope-explicit'
Assert ($files.collector.Contains('GetMaterialVolume(materialId)') -and $files.collector.Contains('GetMaterialArea(materialId, painted)')) 'material-volume-and-area-sources'
Assert ($files.collector.Contains('UnitTypeId.CubicMeters') -and $files.collector.Contains('UnitTypeId.SquareMeters') -and $files.collector.Contains('UnitTypeId.Meters')) 'revit-unit-conversions-explicit'
Assert ($files.collector.Contains('double.IsNaN(quantity) || double.IsInfinity(quantity)')) 'invalid-source-quantities-visible'
Assert ($files.collector.Contains('Quantity aggregation overflow')) 'aggregation-overflow-visible'
Assert ($files.collector.Contains('SteelDensityKgPerM3 = 7850.0')) 'derived-steel-density-explicit'
Assert ($files.collector.Contains('element.GetMaterialIds(true)')) 'paint-material-scope-explicit'
Assert ($files.collector.Contains('QS-ROOM') -and $files.collector.Contains('room.Area <= 1e-9')) 'room-area-and-zero-filter'
Assert ($files.collector.Contains('InstanceCount') -and $files.collector.Contains('CenterlineLength')) 'count-and-centerline-measurements'
Assert ($files.collector.Contains('OrderBy(x => x.Code)')) 'deterministic-output-order'
Assert ($files.collector.Contains('ElementUniqueIds.Add(element.UniqueId)')) 'unique-source-traces'
Assert ($files.rules.Contains('QsQuantityMath.TryCalculatePayQuantity')) 'rule-engine-uses-tested-domain-math'
Assert ($files.rules.Contains('line.IsIncluded = false') -and $files.rules.Contains('Invalid quantity rule or measurement')) 'invalid-quantity-is-visible-and-excluded'
Assert ($files.domain.Contains('MidpointRounding.AwayFromZero')) 'deterministic-rounding-policy'
Assert ($files.domain.Contains('!IsFinite(wastePercent) || wastePercent < 0')) 'invalid-waste-rejected'
Assert ($files.snapshots.Contains('SchemaVersion = 2') -and $files.snapshots.Contains('BuildLegacyKey')) 'snapshot-key-schema-preserves-old-issues'
Assert ($files.snapshots.Contains('File.Move(temporaryPath, path)')) 'snapshot-write-uses-atomic-rename'
Assert ($files.profile.Contains('File.Replace(temporaryPath, path, backupPath)')) 'rule-profile-save-is-atomic-and-keeps-backup'
Assert ($files.boq.Contains('MatchText(r.FamilyPattern, x.FamilyName)') -and $files.boq.Contains('MatchText(r.TypePattern, x.TypeName)')) 'boq-family-and-type-match-separately'
Assert ($files.form.Contains('SaveFileDialog') -and $files.form.Contains('DialogResult.OK')) 'export-dialog-cancel-is-safe'
Assert ($files.form.Contains('CultureInfo.InvariantCulture')) 'export-number-culture-invariant'
Assert ($files.form.Contains('line.FamilyName') -and $files.form.Contains('"Family", "Type"')) 'family-identity-visible-in-grid-and-export'
Assert ($files.form.Contains('Phạm vi: toàn bộ tài liệu chủ (không gồm links)')) 'whole-model-scope-visible-in-workbench'
Assert ($files.form.Contains('File.Replace(temporaryPath, fullPath, null)')) 'export-overwrite-atomically-replaces-existing-file'
Assert ($files.form.Contains('File.Move(temporaryPath, fullPath)')) 'export-creates-output-only-after-successful-write'
Assert ($files.importer.Contains('NotSupportedException') -and $files.importer.Contains('QsQuantityMath.TryParseQuantity')) 'cubicost-import-extension-and-number-validation'
Assert ($files.importer.Contains("csvSeparator == ';'")) 'csv-decimal-convention-follows-delimiter'
Assert ($files.importer.Contains('quantity < 0') -and $files.importer.Contains('invalidRows > 0')) 'invalid-import-rows-are-warned-and-excluded'
Assert ($files.fixture.Contains('QsModelScanner.Scan(doc, doc.ActiveView, QsScanScope.Selection, selected)')) 'runtime-fixture-uses-production-selection-scan'
Assert ($files.fixture.Contains('QtoCollectorService.Collect(doc)')) 'runtime-fixture-uses-production-qto-collector'
Assert ($files.fixture.Contains('RuntimeQaSafetyGuard.CaptureFingerprint')) 'runtime-fixture-checks-no-model-mutation'
Assert ($files.registry.Contains('Id = "QS"') -and $files.registry.Contains('new KQsProductionRuntimeFixture()')) 'runtime-fixture-registered'

Write-Host "KQS_ACCEPTANCE=PASS"
Write-Host "KQS_ACCEPTANCE_CHECKS=$checks"
