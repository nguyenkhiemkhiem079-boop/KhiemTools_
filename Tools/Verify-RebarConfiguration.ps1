param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'),
    [string]$RevitApiDirectory = (Join-Path ${env:ProgramFiles} 'Autodesk/Revit 2024')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory 'RevitAPI.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$configType = $assembly.GetType('KhimTools.RebarTool.Core.RebarConfiguration')
$storeType = $assembly.GetType('KhimTools.RebarTool.Core.RebarConfigurationStore')
$checks = 0
function Assert($condition,$description) { if (!$condition) { throw $description }; $script:checks++ }
$config = [Activator]::CreateInstance($configType)
Assert ($config.Resolve('Beam','Ld',[decimal]35) -eq 35) 'Fallback'
$config.Project['Ld'] = 40
Assert ($config.Resolve('Beam','Ld',[decimal]35) -eq 40) 'Project inheritance'
$values = [Collections.Generic.Dictionary[string,decimal]]::new()
$values['Ld'] = 45
$config.Members['Beam'] = $values
Assert ($config.Resolve('Beam','Ld',[decimal]35) -eq 45) 'Member override'
Assert ($config.Resolve('Slab','Ld',[decimal]35) -eq 40) 'Member isolation'
$elementValues = [Collections.Generic.Dictionary[string,decimal]]::new()
$elementValues['Ld'] = 50
$config.Elements['id-1'] = $elementValues
Assert ($config.Resolve('Beam','Ld',[decimal]35,'id-1') -eq 50) 'Element override'
$config.Elements.Remove('id-1') | Out-Null
Assert ($config.Resolve('Beam','Ld',[decimal]35,'id-1') -eq 45) 'Restore inheritance'
$directory = [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot "../artifacts/config-qa/$([Guid]::NewGuid().ToString('N'))")).FullName
$store = [Activator]::CreateInstance($storeType,@($directory,'project-1'))
$config.ProjectKey = 'project-1'
$store.Save($config)
Assert ($config.Revision -eq 1) 'Revision increment'
$loaded = $store.Load()
Assert ($loaded.Resolve('Beam','Ld',[decimal]35) -eq 45) 'Round trip'
$stale = $store.Load()
$loaded.Members['Beam']['Ld'] = 55
$store.Save($loaded)
Assert ($loaded.Revision -eq 2) 'Second revision'
Assert ((Get-ChildItem $directory -Filter *.bak).Count -eq 1) 'Previous revision preserved'
$rejected = $false
try { $store.Save($stale) } catch { $rejected = $true }
Assert $rejected 'Reject stale save'
Assert ($store.Load().Members['Beam']['Ld'] -eq 55) 'Stale save did not overwrite'
$recoveryStore = [Activator]::CreateInstance($storeType,@($directory,'project-recovery'))
$recoveryConfig = [Activator]::CreateInstance($configType)
$recoveryConfig.ProjectKey = 'project-recovery'
$recoveryConfig.Project['Ld'] = 41
$recoveryStore.Save($recoveryConfig)
$recoveryConfig.Project['Ld'] = 42
$recoveryStore.Save($recoveryConfig)
$storePath = $storeType.GetField('_path',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($recoveryStore)
[IO.File]::WriteAllText($storePath,'{corrupt')
$restored = $recoveryStore.Load()
Assert ($restored.Project['Ld'] -eq 41) 'Corrupt project settings recover the latest valid revision backup'
$other = [Activator]::CreateInstance($storeType,@($directory,'project-2'))
Assert ($other.Load().Revision -eq 0) 'Project isolation'
foreach ($json in @('null','{"SchemaVersion":99}','{"Project":{"x":-1}}','{"Project":{"x":1000001}}','{"Members":{"Beam":null}}','not json')) {
    $rejected = $false
    try { $storeType.GetMethod('Parse').Invoke($null,@($json)) | Out-Null } catch { $rejected = $true }
    Assert $rejected "Reject invalid JSON: $json"
}
$flags = [Reflection.BindingFlags]'NonPublic,Static,Instance'
$fieldType = $assembly.GetType('KhimTools.RebarTool.Forms.RebarConfigurationField')
$number = [Windows.Forms.NumericUpDown]::new()
$number.Minimum = 10; $number.Maximum = 60; $number.Value = 30
$binding = $fieldType.GetMethod('Number',$flags).Invoke($null,@('Lap','Lap',$number))
$fieldType.GetMethod('Write',$flags).Invoke($binding,@([decimal]40)) | Out-Null
Assert ($number.Value -eq 40) 'Binding applies to generation control'
$rejected = $false
try { $fieldType.GetMethod('Write',$flags).Invoke($binding,@([decimal]70)) } catch { $rejected = $true }
Assert $rejected 'Reject out of range, no clamping'
Assert ($number.Value -eq 40) 'Invalid write leaves control unchanged'
$number.Dispose()
foreach ($name in @('RectangularColumn','CircularColumn','Foundation','Slab','Beam')) {
    $type = $assembly.GetType("KhimTools.RebarTool.Forms.${name}ReinforcementForm")
    $form = $type.GetMethod('CreateLayoutPreview',$flags).Invoke($null,@())
    try {
        function Find-Configuration($control) {
            if ($control -is [Windows.Forms.TabPage] -and $control.Name -eq 'RebarConfigurationPage') { $control }
            foreach ($child in $control.Controls) { Find-Configuration $child }
        }
        $pages = @(Find-Configuration $form)
        Assert ($pages.Count -eq 1) "$name configuration wired"
        function Find-Children($control) {
            $control
            foreach ($child in $control.Controls) { Find-Children $child }
        }
        $controls = @(Find-Children $pages[0])
        $grid = $controls | Where-Object { $_ -is [Windows.Forms.DataGridView] } | Select-Object -First 1
        $scope = $controls | Where-Object { $_ -is [Windows.Forms.ComboBox] } | Select-Object -First 1
        $edited = [decimal]::Parse([string]$grid.Rows[0].Cells[2].Value) + 1
        $grid.Rows[0].Cells[0].Value = $true
        $grid.Rows[0].Cells[2].Value = $edited.ToString()
        $scope.SelectedIndex = 0
        $scope.SelectedIndex = 1
        Assert ([decimal]::Parse([string]$grid.Rows[0].Cells[2].Value) -eq $edited) "$name retains drafts across scope changes"
        if ($name -eq 'Beam') {
            $type.GetField('_configLd',$flags).GetValue($form).Value = 45
            $type.GetField('_configHookTail',$flags).GetValue($form).Value = 16
            $type.GetField('_configSideThreshold',$flags).GetValue($form).Value = 800
            $input = $type.GetMethod('CreateGenerationInput',$flags).Invoke($form,@($null))
            Assert ($input.LdMultiplier -eq 45) 'Beam anchorage reaches generation input'
            Assert ($input.HookTailMultiplier -eq 16) 'Beam hook tail reaches generation input'
            Assert ($input.SideBarThresholdMm -eq 800) 'Beam skin threshold reaches generation input'
            $type.GetField('_rbStirrupUniform',$flags).GetValue($form).Checked = $true
            $type.GetField('_txtStirrupA1Uniform',$flags).GetValue($form).Text = '125'
            $input = $type.GetMethod('CreateGenerationInput',$flags).Invoke($form,@($null))
            Assert ($input.StirrupSpacingA1 -eq $input.StirrupSpacingA2) 'Uniform mode uses equal spacing'
        }
    } finally { $form.Dispose() }
}
Write-Host "PASS: $checks configuration checks. File persistence and UI bindings only; no Revit geometry executed."
