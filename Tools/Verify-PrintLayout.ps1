param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = "C:/Program Files/Autodesk/Revit 2023"
)
$ErrorActionPreference = "Stop"
if ([Threading.Thread]::CurrentThread.ApartmentState -ne "STA") { throw "Run with Windows PowerShell -STA." }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
foreach ($name in @("RevitAPI.dll", "RevitAPIUI.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$type = $assembly.GetType("KhimTools.SheetExport.Forms.SheetExportForm", $true)
$flags = [Reflection.BindingFlags]"NonPublic,Instance"
$factory = $type.GetMethod("CreateLayoutPreview", [Reflection.BindingFlags]"NonPublic,Static")
$style = $assembly.GetType("KhimTools.Core.KhimUiStyle").GetMethod("StyleControlTree")
$output = [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot "../artifacts/ui-qa")).FullName
function Prepare-Controls($control) {
    $control.CreateControl()
    $null = $control.Handle
    foreach ($child in $control.Controls) { Prepare-Controls $child }
    $control.PerformLayout()
}
$checks = 0
foreach ($size in @(@(1120,700), @(1360,820), @(1600,900))) {
    $form = $factory.Invoke($null, @())
    $content = $null
    try {
        $form.Size = [Drawing.Size]::new($size[0],$size[1])
        $handle = $form.Handle
        $style.Invoke($null, @($form)) | Out-Null
        $content = $form.Controls[0]
        $form.Controls.Remove($content)
        $content.Size = $form.ClientSize
        $content.Visible = $true
        Prepare-Controls $content
        $itemsField = $type.GetField('_allSheetItems', $flags)
        $items = [Activator]::CreateInstance($itemsField.FieldType)
        $itemType = $assembly.GetType('KhimTools.SheetExport.Models.SheetExportItem')
        foreach ($sample in @(@('S100','GENERAL ARRANGEMENT',$true), @('S101','FLOOR PLAN',$false), @('A200','ELEVATIONS',$true))) {
            $item = [Activator]::CreateInstance($itemType)
            $item.SheetNumber = $sample[0]; $item.SheetName = $sample[1]; $item.IsSelected = $sample[2]
            $items.Add($item)
        }
        $itemsField.SetValue($form, $items)
        $type.GetMethod('ApplySearchAndFilter', $flags).Invoke($form, @()) | Out-Null
        $tree = $type.GetField('_sheetTree', $flags).GetValue($form)
        $grid = $type.GetField('_gridSheets', $flags).GetValue($form)
        if ($grid.Rows.Count -ne 2 -or $tree.Nodes.Count -lt 2) { throw 'Sheet tree / export queue mismatch.' }
        $tree.Nodes[0].Checked = $false
        if ($items[0].IsSelected -or $grid.Rows.Count -ne 1) { throw 'Group deselection does not update export queue.' }
        $tree.Nodes[0].Checked = $true
        if (!$items[1].IsSelected -or $grid.Rows.Count -ne 3) { throw 'Group selection does not update export queue.' }
        foreach ($node in $tree.Nodes) { $node.Expand() }
        $checks += 3
        foreach ($name in @("_txtSearchSheet","_cmbDisciplineFilter","_cmbPaperFilter","_cmbIssueStatusFilter",
            "_chkFilterModifiedOnly","_btnSelectAll","_btnClearAll","_btnInvert","_btnRefreshList",
            "_rbFormatPdf","_rbFormatDwg","_rbFormatBoth","_btnSaveSelection","_btnLoadSelection","_btnPrint")) {
            $control = $type.GetField($name, $flags).GetValue($form)
            if (!$control.Parent.ClientRectangle.Contains($control.Bounds)) {
                throw "$name is clipped at $($size[0])x$($size[1]): $($control.Bounds), parent $($control.Parent.ClientRectangle)"
            }
            $checks++
        }
        $outputField = $type.GetField("_txtOutputDirectory", $flags).GetValue($form)
        $setup = $outputField.Parent.Parent
        foreach ($name in @("_txtOutputDirectory","_txtIssueSetName","_txtNamingPattern","_txtFileCombineName")) {
            $control = $type.GetField($name, $flags).GetValue($form)
            if ($control.Right + $control.Parent.Left -gt $setup.ClientSize.Width - $setup.Padding.Right) {
                throw "$name overflows the setup viewport at $($size[0])x$($size[1])."
            }
            $checks++
        }
        $bitmap = [Drawing.Bitmap]::new($content.Width,$content.Height)
        try {
            $content.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0,0,$content.Width,$content.Height))
            $colors = [Collections.Generic.HashSet[int]]::new()
            for ($x=0; $x -lt $bitmap.Width; $x+=17) {
                for ($y=0; $y -lt $bitmap.Height; $y+=17) { $colors.Add($bitmap.GetPixel($x,$y).ToArgb()) | Out-Null }
            }
            if ($colors.Count -lt 10) { throw "Blank Print/Export render: $($colors.Count) sampled colors." }
            $bitmap.Save((Join-Path $output "PrintExport-$($size[0])x$($size[1]).png"), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    } finally {
        if ($null -ne $content) { $content.Dispose() }
        $form.Dispose()
    }
}
Write-Host "PASS: $checks control-bound checks; Print/Export rendered at three window sizes."
Write-Host "Layout fixture only: no model opened, print/export disabled, no PDF/DWG files produced."
