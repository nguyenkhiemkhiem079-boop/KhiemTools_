param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = (Join-Path ${env:ProgramFiles} "Autodesk/Revit 2024")
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory "RevitAPI.dll")) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$flags = [Reflection.BindingFlags]"NonPublic,Instance"
$checks = 0
function Assert-State($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Field($form, $name) { $form.GetType().GetField($name, $flags).GetValue($form) }
$numericRoot = [Windows.Forms.Panel]::new()
try {
    $positions = [Windows.Forms.TextBox]::new()
    $positions.ReadOnly = $true
    $positions.Text = '0, 3'
    $length = [Windows.Forms.TextBox]::new()
    $length.Text = '100'
    $numericRoot.Controls.AddRange(@($positions, $length))
    $guardType = $assembly.GetType('KhimTools.RebarTool.Forms.RebarFormGuard', $true)
    $rule = $guardType.GetMethod('RequireNumericTextBoxes').Invoke($null, @($numericRoot, 'Invalid length'))
    Assert-State ($rule.IsValid.Invoke()) 'Read-only beam positions block valid numeric inputs.'
    $length.Text = '-1'
    Assert-State (!$rule.IsValid.Invoke()) 'Negative editable beam length is accepted.'
    $length.Text = 'abc'
    Assert-State (!$rule.IsValid.Invoke()) 'Non-numeric editable beam length is accepted.'
} finally { $numericRoot.Dispose() }
foreach ($name in @("RectangularColumn", "CircularColumn", "Foundation", "Slab", "Beam")) {
    $type = $assembly.GetType("KhimTools.RebarTool.Forms.$($name)ReinforcementForm", $true)
    $form = $type.GetMethod("CreateLayoutPreview", [Reflection.BindingFlags]"NonPublic,Static").Invoke($null, @())
    $root = [Windows.Forms.Panel]::new()
    try {
        foreach ($child in @($form.Controls)) { $root.Controls.Add($child) }
        $primary = Field $form $(if ($name -eq "Beam") { "_btnOk" } else { "_btnCreateRebar" })
        Assert-State (!$primary.Enabled) "$name allows creation without a model/host."
        if ($name -in @("RectangularColumn", "CircularColumn")) {
            $custom = Field $form "_chkCustomCover"
            $cover = Field $form "_numCustomCover"
            Assert-State (!$cover.Enabled) "$name custom cover starts editable."
            $custom.Checked = $true
            Assert-State $cover.Enabled "$name custom cover toggle did not enable input."
            $custom.Checked = $false
            Assert-State (!$cover.Enabled) "$name custom cover toggle did not disable input."
            $foundation = Field $form "_rdBaseFoundation"
            $floor = Field $form "_rdBaseStandardLevel"
            $foundation.Checked = $true
            Assert-State (!$floor.Checked) "$name column base modes are not exclusive."
            $floor.Checked = $true
            Assert-State (!$foundation.Checked) "$name column base modes are not reversible."
        }
        if ($name -eq "Beam") {
            $views = @("_pnlViewMainTop", "_pnlViewMainBot", "_pnlViewAddTop", "_pnlViewAddBot", "_pnlViewStirrup", "_pnlViewAntiBulge")
            for ($index=0; $index -lt 6; $index++) {
                $type.GetMethod("SwitchSettingTab", $flags).Invoke($form, @($index)) | Out-Null
                for ($other=0; $other -lt 6; $other++) {
                    Assert-State ((Field $form $views[$other]).Visible -eq ($index -eq $other)) "Beam mode $index has wrong visible editor."
                }
            }
            $uniform = Field $form "_rbStirrupUniform"
            $ends = Field $form "_rbStirrup2Ends"
            $uniform.Checked = $true
            Assert-State (!$ends.Checked) "Beam stirrup distribution modes are not exclusive."
            $ends.Checked = $true
            Assert-State (!$uniform.Checked) "Beam stirrup distribution modes are not reversible."
        }
    } finally { $root.Dispose(); $form.Dispose() }
}
$joinType = $assembly.GetType('KhimTools.SlabJoin.Forms.JoinElementsForm', $true)
$join = [Activator]::CreateInstance($joinType, [object[]]@($null, $null))
try {
    foreach ($size in @([Drawing.Size]::new(800,520), [Drawing.Size]::new(1200,800))) {
        $join.Size = $size
        $join.CreateControl()
        $join.PerformLayout()
        $split = $join.Controls | Where-Object { $_ -is [Windows.Forms.SplitContainer] } | Select-Object -First 1
        Assert-State ($split.SplitterDistance -ge $split.Panel1MinSize) 'Join left panel violates minimum size.'
        Assert-State ($split.SplitterDistance -le ($split.Width - $split.SplitterWidth - $split.Panel2MinSize)) 'Join right panel violates minimum size.'
    }
} finally { $join.Dispose() }
Write-Host "PASS: $checks Rebar and Join input-state checks."
Write-Host "UI state only: no generation, geometry, Revit selection or model transactions executed."
