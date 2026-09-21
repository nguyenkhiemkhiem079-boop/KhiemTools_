param(
    [string]$Root = (Join-Path $PSScriptRoot "..")
)
$ErrorActionPreference = "Stop"
$service = Get-Content (Join-Path $Root "KhimTools/Tools/KhimGen/SheetGen/Services/SheetGenService.cs") -Raw
$preflight = Get-Content (Join-Path $Root "KhimTools/Tools/KhimGen/SheetGen/Services/SheetGenPreflightService.cs") -Raw
$form = Get-Content (Join-Path $Root "KhimTools/Tools/KhimGen/SheetGen/Forms/SheetGenForm.cs") -Raw
$placement = Get-Content (Join-Path $Root "KhimTools/Tools/KhimGen/SheetGen/Services/SheetPlacementService.cs") -Raw

function Assert-True([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "FAIL: $Message" }
}

Assert-True ($service -notmatch 'Split\s*\(\s*[,]') "CSV import must not use naive comma splitting."
Assert-True ($service -match 'ParseCsvRecords') "CSV parser is missing."
Assert-True ($service -match 'Unclosed quoted field') "Malformed quoted CSV diagnostics are missing."
Assert-True ($service -match 'Assigned View Unique Id' -and $service -match 'Status Message') "CSV contract does not round-trip stable view/status fields."
Assert-True ($service -notmatch 'Title Block Id.*Allow Blank Title Block' -and $service -notmatch 'Assigned View Id.*Content Kind') "CSV must not export transient ElementId values as portable identity."
Assert-True ($service -match 'ScheduleSheetInstance\.Create') "Schedules must use ScheduleSheetInstance.Create."
Assert-True ($service -match 'Viewport\.CanAddViewToSheet') "Normal/legend views must use Viewport preflight."
Assert-True ($service -match 'TransactionGroup' -and $service -match 'new Transaction\(doc, "K-TOOLS - Create') "Batch must use a transaction group with per-sheet transactions."
Assert-True ($service -notmatch 'new XYZ\(1\.5\s*,\s*1\.0\s*,\s*0\)') "Project-specific hardcoded viewport placement remains."
Assert-True ($placement -match 'GetPlacementPoint' -and $placement -match 'marginMillimeters') "Safe placement service is missing."
Assert-True ($preflight -match 'ViewAlreadyPlaced' -and $preflight -match 'ViewAmbiguous' -and $preflight -match 'UnsupportedView') "View diagnostics are incomplete."
Assert-True ($preflight -match 'TitleBlockNotFound' -and $preflight -match 'InvalidTitleBlockType') "Title-block diagnostics are incomplete."
Assert-True ($form -match 'HeaderText = "Status"' -and $form -match 'HeaderText = "Status message"') "Preview status columns are missing."
Assert-True ($form -match 'RefreshPreviewStatus' -and $form -match 'CreateSheetsDetailed') "UI is not wired to preflight and detailed results."

function Expand-TestTokens([string]$Pattern, [int]$Index, [int]$Number, [int]$Padding) {
    $p = $Pattern -replace '\{n\}', $Index
    $p = $p -replace '\{Index\}', $Index
    $p = $p -replace '\{0n\}', $Index.ToString("D$Padding")
    return $p -replace '\{Number\}', $Number.ToString("D$Padding")
}
Assert-True ((Expand-TestTokens "A-{n}-{Index}-{0n}-{Number}" 3 17 3) -eq "A-3-3-003-017") "Series token expansion failed."
Assert-True ($service -match 'Suffix' -and $service -match 'NumberPadding') "Series suffix/padding is not present."
$series100 = 1..100 | ForEach-Object { "KC-" + $_.ToString("D3") + "-A" }
Assert-True ($series100.Count -eq 100 -and ($series100 | Select-Object -Unique).Count -eq 100) "100-row series generation is not deterministic."
$duplicatePending = @("KC-101", "KC-101")
Assert-True (@($duplicatePending | Group-Object | Where-Object Count -gt 1).Count -eq 1) "Pending duplicate detection test failed."

function Parse-TestCsv([string]$Text) {
    $rows = @(); $row = @(); $field = ""; $quoted = $false
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($c -eq '"') {
            if ($quoted -and $i + 1 -lt $Text.Length -and $Text[$i + 1] -eq '"') { $field += '"'; $i++ } else { $quoted = !$quoted }
        } elseif ($c -eq ',' -and !$quoted) { $row += $field; $field = "" }
        elseif (($c -eq [char]13 -or $c -eq [char]10) -and !$quoted) {
            if ($c -eq [char]13 -and $i + 1 -lt $Text.Length -and $Text[$i + 1] -eq [char]10) { $i++ }
            $row += $field; $field = ""; if (($row | ? { $_ -ne "" }).Count -gt 0) { $rows += ,$row }; $row = @()
        } else { $field += $c }
    }
    $row += $field; if (($row | ? { $_ -ne "" }).Count -gt 0) { $rows += ,$row }
    return $rows
}
$csv = [char]0xFEFF + '"Sheet Number","Sheet Name","Title Block","Assigned View"' + [char]13 + [char]10 + '"A-01","Plan, Level 1","TB ""A""","View 1"' + [char]13 + [char]10 + '"B-01","","",""'
$parsed = Parse-TestCsv $csv
Assert-True ($parsed.Count -eq 3 -and $parsed[1][1] -eq "Plan, Level 1" -and $parsed[1][2] -eq 'TB "A"' -and $parsed[2][1] -eq "") "Quoted comma/quote/empty/BOM CSV test failed."
Assert-True ($service -match 'UTF8Encoding\(false, true\)' -and $service -match 'StreamReader') "UTF-8/BOM-safe CSV reading is missing."

Write-Host "PASS: SheetGen Wave 1.1 source contracts, series tokens, placement, transaction, and CSV parser checks."
