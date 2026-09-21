param([string]$AssemblyPath = (Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'),
    [string]$RevitApiDirectory = 'C:/Program Files/Autodesk/Revit 2023')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
foreach ($name in @('RevitAPI.dll','RevitAPIUI.dll')) { [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null }
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$type = $assembly.GetType('KhimTools.DrawingCheck.DrawingCheckForm', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$constructor = $type.GetConstructors($flags)[0]
$output = [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/drawing-check')).FullName
$checks = 0
$service = $assembly.GetType('KhimTools.DrawingCheck.DrawingCheckService', $true)
$contentCheck = $service.GetMethod('HasMissingTagContent', [Reflection.BindingFlags]'NonPublic,Static')
foreach ($case in @(@('', $true), @('  ? ? ', $true), @('W-01', $false), @('Wall ? review', $false))) {
    if ($contentCheck.Invoke($null, @($case[0])) -ne $case[1]) { throw "Wrong tag content classification: $($case[0])" }
    $checks++
}
foreach ($size in @(@(850,500),@(1100,650))) {
    $form = $constructor.Invoke([object[]]@($null))
    $root = $null
    try {
        $form.Size = [Drawing.Size]::new($size[0],$size[1])
        $null = $form.Handle
        $scope = $type.GetField('_scope',$flags).GetValue($form)
        if ($scope.Items.Count -ne 3 -or $scope.SelectedIndex -ne 0) { throw 'Invalid scope options.' }
        $locate = $type.GetField('_locate',$flags).GetValue($form)
        if ($locate.Enabled) { throw 'Locate enabled without a result.' }
        $grid = $type.GetField('_grid',$flags).GetValue($form)
        if (!$grid.ReadOnly -or $grid.Columns.Count -ne 6) { throw 'Invalid results grid.' }
        $root = $form.Controls[0]
        $form.Controls.Remove($root)
        $root.Size = $form.ClientSize
        $root.Visible = $true
        $root.CreateControl()
        $root.PerformLayout()
        if (!$scope.Parent.ClientRectangle.Contains($scope.Bounds)) { throw 'Scope clipped.' }
        if (!$locate.Parent.ClientRectangle.Contains($locate.Bounds)) { throw 'Locate clipped.' }
        $bitmap = [Drawing.Bitmap]::new($root.Width,$root.Height)
        try { $root.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$root.Width,$root.Height)); $bitmap.Save((Join-Path $output "$($size[0])x$($size[1]).png")) }
        finally { $bitmap.Dispose() }
        $checks += 5
    } finally { if ($root) { $root.Dispose() }; $form.Dispose() }
}
Write-Output "PASS: $checks Drawing Check UI checks. No live model scan executed."
