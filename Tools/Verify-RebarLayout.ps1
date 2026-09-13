param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$RevitApiDirectory = "C:/Program Files/Autodesk/Revit 2023",
    [double[]]$Scales = @(1, 1.5, 2),
    [string[]]$Forms = @("RectangularColumn", "CircularColumn", "Foundation", "Slab", "Beam", "ProjectCoverSetup", "SlabEdgePicker")
)
$ErrorActionPreference = "Stop"
if ([Threading.Thread]::CurrentThread.ApartmentState -ne "STA") { throw "Run with Windows PowerShell -STA." }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
foreach ($name in @("RevitAPI.dll", "RevitAPIUI.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory $name)) | Out-Null
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$flags = [Reflection.BindingFlags]"NonPublic,Instance"
$output = [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot "../artifacts/ui-qa/rebar")).FullName
$issues = [Collections.Generic.List[string]]::new()
$checks = 0
$renders = 0
$languageType = $assembly.GetType("KhimTools.Core.LanguageManager")
$languageFlags = [Reflection.BindingFlags]"NonPublic,Static"
# Keep the fixture's language changes in memory; never write the user's config.
$languageType.GetField("_isLoaded", $languageFlags).SetValue($null, $true)
$languageValue = $assembly.GetType("KhimTools.Core.AppLanguage")
function Prepare-Controls($control) {
    $control.CreateControl()
    $null = $control.Handle
    foreach ($child in $control.Controls) { Prepare-Controls $child }
    $control.PerformLayout()
}
function Get-Controls($control) {
    $control
    foreach ($child in $control.Controls) { Get-Controls $child }
}
function Check-Layout($root, $context) {
    @(Get-Controls $root | ForEach-Object {
        [pscustomobject]@{ Type = $_.GetType().Name; Text = $_.Text; Visible = $_.Visible; Bounds = $_.Bounds.ToString(); Parent = if ($_.Parent) { $_.Parent.GetType().Name } else { "" } }
    }) | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $output "$context-controls.json") -Encoding UTF8
    foreach ($control in (Get-Controls $root)) {
        if (!$control.Visible) { continue }
        $leaf = $control -is [Windows.Forms.Label] -or $control -is [Windows.Forms.ButtonBase] -or
            $control -is [Windows.Forms.ComboBox] -or $control -is [Windows.Forms.TextBox] -or
            $control -is [Windows.Forms.NumericUpDown]
        if (!$control.Parent -or $control.Parent -is [Windows.Forms.UpDownBase]) { continue }
        $script:checks++
        $parent = $control.Parent
        $area = $parent.ClientRectangle
        if ($parent -is [Windows.Forms.ScrollableControl] -and $parent.AutoScroll) {
            $area = $parent.DisplayRectangle
            # WinForms excludes trailing padding from the virtual rectangle,
            # although an autosized docked stack includes it in its bounds.
            if ($control -is [Windows.Forms.TableLayoutPanel] -and $control.AutoSize -and $control.Dock -eq "Top") {
                $area.Height += $parent.Padding.Bottom
            }
        }
        if (!$area.Contains($control.Bounds)) {
            $issues.Add("$context | Bounds | $($control.GetType().Name) '$($control.Text)' $($control.Bounds) in $area")
        }
        if (!$leaf) { continue }
        if (($control -is [Windows.Forms.Label] -or $control -is [Windows.Forms.ButtonBase]) -and $control.Text) {
            $needed = $control.GetPreferredSize([Drawing.Size]::new($control.Width,0))
            if ($needed.Height -gt $control.Height + 2) {
                $issues.Add("$context | Text | '$($control.Text)' needs $needed in $($control.ClientSize)")
            }
        }
        foreach ($other in $parent.Controls) {
            if ($other -eq $control -or !$other.Visible -or $other.HasChildren -or $other -is [Windows.Forms.GroupBox]) { continue }
            if ($control.Bounds.IntersectsWith($other.Bounds) -and
                [Drawing.Rectangle]::Intersect($control.Bounds, $other.Bounds).Width -gt 2) {
                $issues.Add("$context | Overlap | '$($control.Text)' with '$($other.Text)'")
            }
        }
    }
}
foreach ($name in $Forms) {
    $className = if ($name -in @("ProjectCoverSetup", "SlabEdgePicker")) { "$($name)Form" } else { "$($name)ReinforcementForm" }
    $type = $assembly.GetType("KhimTools.RebarTool.Forms.$className", $true)
    foreach ($scale in $Scales) {
        foreach ($sizeMode in @("minimum", "wide")) {
            Write-Host "Checking $name $sizeMode scale=$scale"
            $form = $type.GetMethod("CreateLayoutPreview", [Reflection.BindingFlags]"NonPublic,Static").Invoke($null, @())
            $root = $null
            try {
                $form.Size = if ($sizeMode -eq "minimum") { $form.MinimumSize } else { [Drawing.Size]::new(1440,900) }
                $null = $form.Handle
                $assembly.GetType("KhimTools.Core.KhimUiStyle").GetMethod("StyleControlTree").Invoke($null, @($form)) | Out-Null
                [Windows.Forms.Form].GetMethod("OnShown", $flags).Invoke($form, @([EventArgs]::Empty)) | Out-Null
                $root = [Windows.Forms.Panel]::new()
                $root.Size = $form.ClientSize
                $root.Font = $form.Font
                $root.BackColor = $form.BackColor
                foreach ($child in @($form.Controls)) { $root.Controls.Add($child) }
                Prepare-Controls $root
                foreach ($field in $type.GetFields($flags)) {
                    if ($field.Name -notmatch "Dia$") { continue }
                    $combo = $field.GetValue($form)
                    if ($combo -isnot [Windows.Forms.ComboBox]) { continue }
                    $sample = "B500B - D20 - Long reinforcement family name for layout QA"
                    $index = $combo.Items.Add($sample)
                    try {
                        [Windows.Forms.ComboBox].GetMethod("OnDropDown", $flags).Invoke($combo, @([EventArgs]::Empty)) | Out-Null
                        $expected = [Math]::Min([Windows.Forms.TextRenderer]::MeasureText($sample, $combo.Font).Width + 32,
                            [Windows.Forms.Screen]::FromControl($combo).WorkingArea.Width)
                        if ($combo.DropDownWidth -lt $expected) { $issues.Add("$name | Truncated type dropdown: $($field.Name)") }
                        $checks++
                    } finally { $combo.Items.RemoveAt($index) }
                }
                if ($scale -ne 1) {
                    $fonts = @(Get-Controls $root | ForEach-Object { @{ Control = $_; Font = $_.Font } })
                    $root.Scale([Drawing.SizeF]::new($scale,$scale))
                    foreach ($entry in $fonts) {
                        $entry.Control.Font = [Drawing.Font]::new($entry.Font.FontFamily, [single]($entry.Font.Size * $scale), $entry.Font.Style)
                    }
                    Prepare-Controls $root
                }
                $tabs = @(Get-Controls $root | Where-Object { $_ -is [Windows.Forms.TabControl] })
                $states = if ($name -eq "Beam") { 6 } elseif ($name -eq "RectangularColumn") { $tabs[0].TabCount * 2 } elseif ($tabs.Count) { $tabs[0].TabCount } else { 1 }
                for ($state = 0; $state -lt $states; $state++) {
                    if ($name -eq "Beam") {
                        $type.GetMethod("SwitchSettingTab", $flags).Invoke($form, @($state)) | Out-Null
                    } elseif ($tabs.Count) {
                        if ($name -eq "RectangularColumn") {
                            $language = if ($state -lt $tabs[0].TabCount) { "Vietnamese" } else { "English" }
                            $languageType.GetField("_currentLanguage", $languageFlags).SetValue($null, [Enum]::Parse($languageValue,$language))
                            $type.GetMethod("ApplyLanguage", $flags).Invoke($form, @()) | Out-Null
                        }
                        $tabs[0].SelectedIndex = $state % $tabs[0].TabCount
                    }
                    Prepare-Controls $root
                    $context = "$name-$sizeMode-$scale-$state"
                    Check-Layout $root $context
                    $bitmap = [Drawing.Bitmap]::new($root.Width,$root.Height)
                    try {
                        $root.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0,0,$root.Width,$root.Height))
                        $colors = [Collections.Generic.HashSet[int]]::new()
                        for ($x=0; $x -lt $bitmap.Width; $x+=23) {
                            for ($y=0; $y -lt $bitmap.Height; $y+=23) { $colors.Add($bitmap.GetPixel($x,$y).ToArgb()) | Out-Null }
                        }
                        if ($colors.Count -lt 10) { $issues.Add("$context | Blank render") }
                        $bitmap.Save((Join-Path $output "$context.png"), [Drawing.Imaging.ImageFormat]::Png)
                        $renders++
                    } finally { $bitmap.Dispose() }
                    if ($name -in @("RectangularColumn", "CircularColumn") -and $tabs[0].SelectedIndex -eq 0) {
                        $page = $tabs[0].SelectedTab
                        $page.AutoScrollPosition = [Drawing.Point]::new(0, $page.DisplayRectangle.Height)
                        Prepare-Controls $root
                        $bitmap = [Drawing.Bitmap]::new($root.Width,$root.Height)
                        try {
                            $root.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0,0,$root.Width,$root.Height))
                            $bitmap.Save((Join-Path $output "$context-preview.png"), [Drawing.Imaging.ImageFormat]::Png)
                            $renders++
                        } finally { $bitmap.Dispose() }
                        $page.AutoScrollPosition = [Drawing.Point]::Empty
                    }
                }
            } finally {
                if ($root) { $root.Dispose() }
                $form.Dispose()
            }
        }
    }
}
$issues | Sort-Object -Unique | Set-Content (Join-Path $output "issues.txt") -Encoding UTF8
if ($issues.Count) { throw "$($issues.Count) Rebar layout issues. See $output/issues.txt" }
Write-Host "PASS: $checks control checks; $renders Rebar layout renders."
Write-Host "Offline controls only; scaling simulation is not live Revit/Windows DPI QA. No model commands executed."
