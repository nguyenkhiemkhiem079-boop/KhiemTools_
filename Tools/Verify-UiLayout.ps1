param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "../KhimTools/bin/Release/net48/KhimTools.dll"),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "../artifacts/ui-qa")
)
$ErrorActionPreference = "Stop"
if ([Threading.Thread]::CurrentThread.ApartmentState -ne "STA") {
    throw "Run with Windows PowerShell -STA."
}
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
Add-Type @'
public class UiQaFamily {
    public string Name { get; set; }
    public string Category { get; set; }
    public bool IsLoadedInDocument { get; set; }
    public int SymbolCount { get; set; }
    public string FormattedSize { get; set; }
    public string FullPath { get; set; }
}
'@
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$theme = $assembly.GetType("KhimTools.Core.UI.KhimWpfTheme", $true)
$iconType = $assembly.GetType("KhimTools.Core.UI.UiIconExtension", $true)
$output = [IO.Directory]::CreateDirectory($OutputDirectory).FullName
$results = [Collections.Generic.List[object]]::new()
function Assert($condition, [string]$message) {
    if (!$condition) { throw $message }
}
function Walk([Windows.DependencyObject]$visual) {
    $visual
    for ($i = 0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($visual); $i++) {
        Walk ([Windows.Media.VisualTreeHelper]::GetChild($visual, $i))
    }
}
function Render-Surface($element, [double]$width, [double]$height, [double]$scale, [string]$name) {
    $element.ApplyTemplate() | Out-Null
    $element.InvalidateMeasure()
    [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ContextIdle)
    $element.Measure([Windows.Size]::new($width, $height))
    if ([double]::IsInfinity($height)) { $height = $element.DesiredSize.Height }
    $element.Arrange([Windows.Rect]::new(0, 0, $width, $height))
    $element.UpdateLayout()
    $theme.GetMethod("Apply").Invoke($null, @($element)) | Out-Null
    $element.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.FrameworkElement]::LoadedEvent))
    $element.Measure([Windows.Size]::new($width, $height))
    $element.Arrange([Windows.Rect]::new(0, 0, $width, $height))
    $element.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(
        [int][Math]::Ceiling($width*$scale), [int][Math]::Ceiling($height*$scale),
        96*$scale, 96*$scale, [Windows.Media.PixelFormats]::Pbgra32)
    $backdrop = [Windows.Media.DrawingVisual]::new()
    $drawing = $backdrop.RenderOpen()
    $drawing.DrawRectangle([Windows.Media.BrushConverter]::new().ConvertFromString("#F5F7FA"), $null, [Windows.Rect]::new(0,0,$width,$height))
    $drawing.Close()
    $bitmap.Render($backdrop)
    $bitmap.Render($element)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = Join-Path $output "$name.png"
    $stream = [IO.File]::Create($file)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    $results.Add([pscustomobject]@{Surface=$name; Width=$width; Height=$height; Dpi=96*$scale; File=$file})
}

# Source-only layout harness: removes event handlers; never executes Revit commands.
function Load-Surface([string]$file) {
    [xml]$xml = [IO.File]::ReadAllText($file)
    $x = "http://schemas.microsoft.com/winfx/2006/xaml"
    $xml.DocumentElement.RemoveAttribute("Class", $x)
    foreach ($node in $xml.SelectNodes("//*")) {
        foreach ($attr in @($node.Attributes)) {
            if ($attr.Prefix -in @("mc","d") -or $attr.LocalName -in @("Click","TextChanged","SelectionChanged","MouseDoubleClick","Loaded","Closing","KeyDown","Checked","Unchecked","ValueChanged")) {
                $node.RemoveAttributeNode($attr) | Out-Null
            }
        }
        if ($node.HasAttribute("Source") -and $node.GetAttribute("Source") -match '^\{ui:UiIcon ([-\w]+\.png)\}$') {
            $node.SetAttribute("Source", (Join-Path $root "KhimTools/Resources/$($Matches[1])"))
        }
    }
    $reader = [Xml.XmlNodeReader]::new($xml)
    try { return [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Close() }
}

# Validate every embedded icon through the same loader used by WPF.
$icons = @($assembly.GetManifestResourceNames() | Where-Object { $_ -match '\.Resources\..+_(16|32)\.png$' })
Assert ($icons.Count -ge 90) "The generated icon catalog is incomplete."
foreach ($resource in $icons) {
    $name = $resource.Substring($resource.IndexOf(".Resources.") + 11)
    $bitmap = $iconType.GetMethod("Load").Invoke($null, @($name))
    $size = if ($name -like "*_16.png") {16} else {32}
    Assert ($bitmap.PixelWidth -eq $size -and $bitmap.PixelHeight -eq $size) "Wrong icon size: $name"
    $pixels = [byte[]]::new($size*$size*4)
    $converted = [Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap,[Windows.Media.PixelFormats]::Bgra32,$null,0)
    $converted.CopyPixels($pixels, $size*4, 0)
    $opaque = 0
    for ($i=3; $i -lt $pixels.Length; $i+=4) { if ($pixels[$i] -gt 0) { $opaque++ } }
    Assert ($opaque -gt 5 -and $opaque -lt $size*$size) "Blank or opaque icon: $name"
}

$files = Get-ChildItem (Join-Path $root "KhimTools/Tools") -Recurse -Filter *.xaml
foreach ($file in $files) {
    Write-Host "Rendering $($file.BaseName)"
    $surface = Load-Surface $file.FullName
    $element = $surface.Content
    # Render content without a native top-level window; preserve inherited resources.
    $element.Resources.MergedDictionaries.Add($surface.Resources)
    [Windows.Documents.TextElement]::SetFontFamily($element, $surface.FontFamily)
    [Windows.Documents.TextElement]::SetFontSize($element, $surface.FontSize)
    $surface.Content = $null
    $width = if ($surface -is [Windows.Window]) { $surface.Width } else {300}
    $height = if ($surface -is [Windows.Window] -and ![double]::IsNaN($surface.Height)) { $surface.Height } else {640}
    if ($file.BaseName -eq "FamilyManagerWindow") {
        $rows = [Collections.Generic.List[UiQaFamily]]::new()
        foreach ($name in @("JP_T00", "JP_T51", "Concrete_Rectangular_Column_400x600")) {
            $row = [UiQaFamily]::new()
            $row.Name = $name
            $row.Category = if ($name.StartsWith("JP_")) {"Rebar Shape"} else {"Columns"}
            $row.IsLoadedInDocument = $name -eq "JP_T00"
            $row.SymbolCount = 1
            $row.FormattedSize = "128 KB"
            $row.FullPath = "C:/QA-Fixtures/Library/$name.rfa"
            $rows.Add($row)
        }
        $surface.FindName("GridFamilies").ItemsSource = $rows
        $surface.FindName("TxtTotalFamilies").Text = "3 families"
    }
    if ($file.BaseName -eq "GraphicOverdriveWindow") {
        $presetType = $assembly.GetType("KhimTools.OverrideTool.Services.OverrideColorSettings", $true)
        $presets = $presetType.GetMethod("DefaultPresets").Invoke($null, @())
        $surface.FindName("ColorPalette").ItemsSource = $presets
        $height = [double]::PositiveInfinity
    }
    $sizes = if ($file.BaseName -eq "KhimWorkspacePane") { @(240,300,420) }
             elseif ($file.BaseName -in @("FamilyManagerWindow","RebarShapeLoaderWindow")) { @(720,$width) }
             else { @($width) }
    foreach ($w in $sizes) {
        foreach ($scale in @(1.0,1.5,2.0)) {
            Render-Surface $element $w $height $scale "$($file.BaseName)-$w-$scale"
        }
    }
    if ($file.BaseName -eq "GraphicOverdriveWindow") {
        $chips = @(Walk $element | Where-Object { $_ -is [Windows.Controls.Button] -and $_.DataContext -is $presets[0].GetType() })
        Assert ($chips.Count -eq 16) "Palette must have exactly sixteen color buttons."
        for ($i=0; $i -lt 16; $i++) {
            Assert ($chips[$i].Background.Color.ToString() -eq "#FF$($presets[$i].HexColor.Substring(1))") "Swatch color mismatch at $i"
            Assert ($chips[$i].ActualWidth -eq 34 -and $chips[$i].ActualHeight -eq 34) "Swatch dimensions changed."
        }
        $expander = Walk $element | Where-Object { $_ -is [Windows.Controls.Expander] } | Select-Object -First 1
        $expander.IsExpanded = $true
        Render-Surface $element $width ([double]::PositiveInfinity) 1.5 "GraphicOverdrive-expanded"
    }
    if ($surface -is [Windows.Window]) { $surface.Close() }
}
$results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output "layout-results.json") -Encoding UTF8
Write-Host "PASS: $($icons.Count) embedded icons, $($files.Count) XAML surfaces, $($results.Count) renders; sixteen exact-color 34px swatches."
Write-Host "Source-only layout QA. Revit command execution and printer output are NOT covered."
