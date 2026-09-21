param([string]$AssemblyPath=(Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'),
    [string]$RevitApiDirectory='C:/Program Files/Autodesk/Revit 2023')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
foreach($name in @('RevitAPI.dll','RevitAPIUI.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null}
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$service=$assembly.GetType('KhimTools.SheetGen.Services.AutoViewSheetService')
$matcher=$service.GetMethod('MatchesNumber',[Reflection.BindingFlags]'NonPublic,Static')
$checks=0
foreach($case in @(@('S100 - LEVEL 01','S100',$true),@('S1000 - LEVEL 01','S100',$false),@('XS100','S100',$false),@('LEVEL 01 - s100 - UNDER','S100',$true),@('LEVEL 01','',$false))){
    if($matcher.Invoke($null,@($case[0],$case[1])) -ne $case[2]){throw 'Sheet number match regression'}
    $checks++
}
$type=$assembly.GetType('KhimTools.SheetGen.Forms.SheetGenForm')
$form=$type.GetMethod('CreateLayoutPreview',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())
$root=$null
function Prepare($control){$control.CreateControl();$null=$control.Handle;foreach($child in $control.Controls){Prepare $child};$control.PerformLayout()}
try{
    $tabs=$type.GetField('_tabControl',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($form)
    if($tabs.TabCount -ne 3){throw 'Missing Auto tab'}
    $tabs.SelectedIndex=2
    $root=$tabs.SelectedTab.Controls[0];$tabs.SelectedTab.Controls.Remove($root);$root.Size=[Drawing.Size]::new(1080,620);$root.Visible=$true
    Prepare $root
    $toolbar=$root.Controls[0]
    $mode=$toolbar.Controls[0]
    if($mode.Items.Count -ne 2){throw 'Missing mode option'}
    $mode.SelectedIndex=1
    if(!$toolbar.Controls[1].Enabled -or !$toolbar.Controls[2].Enabled){throw 'New-view settings remain disabled'}
    $mode.SelectedIndex=0
    if($toolbar.Controls[1].Enabled -or $toolbar.Controls[2].Enabled){throw 'Existing-view mode edits type/template'}
    $output=[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/auto-view-sheet')).FullName
    $bitmap=[Drawing.Bitmap]::new($root.Width,$root.Height)
    try{$root.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$root.Width,$root.Height));$bitmap.Save((Join-Path $output 'AutoViewSheet.png'))}finally{$bitmap.Dispose()}
    $checks+=4
}finally{if($root){$root.Dispose()};$form.Dispose()}
Write-Output "PASS: $checks Auto View/Sheet matching and UI checks. No Revit model modified."
