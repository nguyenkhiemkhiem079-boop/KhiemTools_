param([string]$AssemblyPath = (Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
foreach ($name in @('RevitAPI.dll','RevitAPIUI.dll')) {
    [Reflection.Assembly]::LoadFrom("C:/Program Files/Autodesk/Revit 2023/$name") | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$type = $assembly.GetType('KhimTools.ElementTags.Forms.ElementTagsForm')
$flags = [Reflection.BindingFlags]'NonPublic,Instance,Static'
$checks = 0
function Layout-Tree($control) {
    $control.CreateControl()
    $control.PerformLayout()
    foreach ($child in $control.Controls) { Layout-Tree $child }
}
foreach ($size in @(@(1100,720),@(1360,900))) {
    $form = $type.GetMethod('CreateLayoutPreview',$flags).Invoke($null,@())
    try {
        $form.Size = [Drawing.Size]::new($size[0],$size[1])
        $handle = $form.Handle
        Layout-Tree $form
        foreach ($name in @('_grid','_btnTagAll','_btnCheckHost','_btnClashTag','_btnCheck3d','_btnReset','_tabResult','_btnZoomTo','_btnPass','_btnClose','_btnHighlightRed','_btnResetColor')) {
            $control = $type.GetField($name,$flags).GetValue($form)
            if (!$control.Parent.ClientRectangle.Contains($control.Bounds)) { throw "$name clipped: $($control.Bounds)" }
            foreach ($other in $control.Parent.Controls) {
                if ($other -ne $control -and $other.Visible -and $control.Bounds.IntersectsWith($other.Bounds)) { throw "$name overlaps $($other.GetType().Name)" }
            }
            $checks++
        }
    } finally { $form.Dispose() }
}
Write-Host "PASS: $checks Elements Tag layout checks. Offline fixture; no Revit tagging executed."
