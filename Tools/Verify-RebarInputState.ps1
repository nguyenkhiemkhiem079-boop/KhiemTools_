param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = "C:/Program Files/Autodesk/Revit 2023"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
foreach ($name in @("RevitAPI.dll", "RevitAPIUI.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$flags = [Reflection.BindingFlags]"NonPublic,Instance"
$checks = 0
function Assert-State($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Field($form, $name) { $form.GetType().GetField($name, $flags).GetValue($form) }
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
Write-Host "PASS: $checks Rebar input-state checks."
Write-Host "UI state only: no generation, geometry, Revit selection or model transactions executed."
