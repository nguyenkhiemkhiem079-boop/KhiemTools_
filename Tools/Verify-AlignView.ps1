$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
foreach($name in @('RevitAPI.dll','RevitAPIUI.dll')){[Reflection.Assembly]::LoadFrom(('C:\Program Files\Autodesk\Revit 2024\'+$name)) | Out-Null}
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path 'KhimTools\bin\Release\net48\KhimTools.dll'))
$type=$assembly.GetType('KhimTools.ViewportAlign.Forms.AlignViewportForm')
$form=[Activator]::CreateInstance($type,[object[]]@($null,$null))
try{
$null=$form.Handle;$form.PerformLayout();$checks=0
foreach($name in @('_rdViewsAndTitles','_rdViewsOnly','_rdTitlesOnly','_chkOnlyNotes','_chkOnlyKeyplan','_chkOnlyView','_chkOnlySchedule')){
$control=$type.GetField($name,[Reflection.BindingFlags]'NonPublic,Instance').GetValue($form)
if(!$control.Parent.ClientRectangle.Contains($control.Bounds)){throw "$name clipped"}
$checks++
}
Write-Output "PASS: $checks Align View label bounds checks. No model operation executed."
}finally{$form.Dispose()}
