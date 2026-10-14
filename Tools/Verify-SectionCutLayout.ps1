$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
[Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2023/RevitAPI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2023/RevitAPIUI.dll') | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'))
$type = $assembly.GetType('KhimTools.SectionCutTool.Forms.SectionCutForm')
$flags = [Reflection.BindingFlags]'NonPublic,Instance,Static'
$checks = 0
try { $form = $type.GetMethod('CreateLayoutPreview',$flags).Invoke($null,@()) } catch { Write-Host $_.Exception.InnerException.ToString(); throw }
function Layout-Tree($control) {
    $control.CreateControl(); $control.PerformLayout()
    foreach ($child in $control.Controls) { Layout-Tree $child }
}
try {
    $form.Show()
    foreach ($size in @(@(1020,720),@(1280,800))) {
        $form.Size = [Drawing.Size]::new($size[0],$size[1])
        Layout-Tree $form
        [Windows.Forms.Application]::DoEvents()
        foreach ($name in @('_gridElements','_cmbCategoryFilter','_btnPickRevit','_btnSelectAll','_btnDeselectAll','_btnPreview','_btnGenerate','_btnClose')) {
            $control = $type.GetField($name,$flags).GetValue($form)
            if (!$control.Parent.ClientRectangle.Contains($control.Bounds)) { throw "$name clipped" }
            $checks++
            foreach ($other in $control.Parent.Controls) {
                if ($other -ne $control -and $other.Visible -and $control.Bounds.IntersectsWith($other.Bounds)) { throw "$name overlaps $($other.Text)" }
                if ($other -ne $control -and $other.Visible) { $checks++ }
            }
        }
    }
    $tabs = $type.GetField('_tabControl',$flags).GetValue($form)
    if ($tabs.TabPages.Count -ne 4) { throw 'Expected four short tabs' }
    $checks++
    $long = $type.GetField('_chkCreateLongitudinal',$flags).GetValue($form)
    $cross = $type.GetField('_chkCreateCrossSection',$flags).GetValue($form)
    if (!$long.Checked -or $cross.Checked) { throw 'Expected longitudinal default' }
    $checks++
    $presets = @($tabs.TabPages[0].Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })[0]
    $presets.Controls[1].PerformClick()
    if ($long.Checked -or !$cross.Checked) { throw 'Cross preset failed' }
    $checks++
    $presets.Controls[0].PerformClick()
    if (!$long.Checked -or $cross.Checked) { throw 'Long preset failed' }
    $checks++
    $outDir = Join-Path $PSScriptRoot '../artifacts/ui-qa'
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $bitmap = [Drawing.Bitmap]::new($form.Width,$form.Height)
    try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $outDir 'SectionCut-simple.png')) } finally { $bitmap.Dispose() }
    $checks++
    Write-Host "PASS: Section Cut bounds, sibling overlap, four tabs and mode presets ($checks assertions). Offline fixture only."
} finally { $form.Close(); $form.Dispose() }
