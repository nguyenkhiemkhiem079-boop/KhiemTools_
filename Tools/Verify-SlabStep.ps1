param([string]$AssemblyPath=(Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'),
    [string]$RevitApiDirectory='C:/Program Files/Autodesk/Revit 2023')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
foreach($name in @('RevitAPI.dll','RevitAPIUI.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null}
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$audit=$assembly.GetType('KhimTools.SlabStep.Services.SlabStepAudit')
$method=$audit.GetMethod('HeightMatches')
$checks=0
foreach($case in @(@(50.0,50.0,$true),@(51.0,50.0,$true),@(51.1,50.0,$false),@(-50.0,50.0,$false),@(0.0,0.0,$false),@([double]::NaN,50.0,$false))){
    if($method.Invoke($null,@($case[0],$case[1])) -ne $case[2]){throw 'Step height tolerance regression'}
    $checks++
}
$type=$assembly.GetType('KhimTools.SlabStep.Forms.SlabStepForm')
$form=$type.GetMethod('CreateLayoutPreview',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@())
$root=$null
try{
    $null=$form.Handle
    $root=$form.Controls[0]; $form.Controls.Remove($root); $root.Size=$form.ClientSize; $root.Visible=$true
    $root.CreateControl(); $root.PerformLayout()
    $generate=$type.GetField('_btnGenerate',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($form)
    if($generate.Enabled){throw 'Preview allows generation without document'}
    if(!$root.AutoScroll){throw 'Step inputs cannot scroll at small window size'}
    $output=[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/slab-step')).FullName
    $bitmap=[Drawing.Bitmap]::new($root.Width,$root.Height)
    try{$root.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$root.Width,$root.Height));$bitmap.Save((Join-Path $output 'SlabStep.png'))}finally{$bitmap.Dispose()}
    $checks+=2
}finally{if($root){$root.Dispose()};$form.Dispose()}
Write-Output "PASS: $checks Slab Step numeric/UI checks. No live Revit geometry test executed."
