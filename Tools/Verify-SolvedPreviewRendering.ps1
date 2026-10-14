param([string]$AssemblyPath=(Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$api=Join-Path ${env:ProgramFiles} 'Autodesk/Revit 2024/RevitAPI.dll'
[Reflection.Assembly]::LoadFrom($api)|Out-Null
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$renderer=$assembly.GetType('KhimTools.RebarTool.Forms.DetachedSolvedViewRendererV1',$true)
$paint=$renderer.GetMethod('Paint',[Reflection.BindingFlags]'Static,NonPublic')
if(!$paint){throw 'Actual production solved renderer unavailable.'}
$point=$assembly.GetType('KhimTools.RebarTool.Core.SlabSectionPoint',$true)
$path=$assembly.GetType('KhimTools.RebarTool.Core.SolvedLocalPathV1',$true)
$view=$assembly.GetType('KhimTools.RebarTool.Core.DetachedSolvedViewV1',$true)
function Path([string]$role,[double[]]$a,[double[]]$b){
    $points=[Array]::CreateInstance($point,2)
    $points.SetValue([Activator]::CreateInstance($point,[object[]]$a),0)
    $points.SetValue([Activator]::CreateInstance($point,[object[]]$b),1)
    return [Activator]::CreateInstance($path,[object[]]@($role,$points))
}
$paths=[Array]::CreateInstance($path,3)
$paths.SetValue((Path 'VERTICAL' @(-0.2,0,-0.3) @(-0.2,0,0.3)),0)
$paths.SetValue((Path 'HORIZONTAL' @(-0.3,-1,0.2) @(0.3,1,0.2)),1)
$paths.SetValue((Path 'USER_DIRECTED' @(-0.4,0.8,-0.4) @(0.4,0.8,0.4)),2)
$scene=[Activator]::CreateInstance($view,[object[]]@($paths,[double]0,$false))
foreach($pair in @(@('SectionX0',-0.5),@('SectionZ0',-0.5),@('SectionWidth',1),@('SectionHeight',1),@('ElevationX0',-1),@('ElevationY0',-0.5),@('ElevationWidth',2),@('ElevationHeight',1))){$view.GetField($pair[0]).SetValue($scene,[double]$pair[1])}
$view.GetField('HostLabel').SetValue($scene,'SYNTHETIC OFFLINE QA - NOT a Revit result')
$checks=0;$renders=0
$output=[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/solved-views')).FullName
foreach($scale in @(1,1.25,1.5)){
    $bitmap=[Drawing.Bitmap]::new([int](800*$scale),[int](420*$scale));$graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $paint.Invoke($null,[object[]]@($graphics,[Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height),$scene))|Out-Null
        $bitmap.Save((Join-Path $output "synthetic-scale-$scale.png"),[Drawing.Imaging.ImageFormat]::Png)
        $counts=@{green=0;blue=0;purpleLeft=0;purpleRight=0}
        for($x=0;$x -lt $bitmap.Width;$x++){for($y=70;$y -lt $bitmap.Height;$y++){
            $pixel=$bitmap.GetPixel($x,$y)
            # GDI antialiasing blends a 1.5px line with white. Distinct hue tests
            # detect actual palette strokes, not exact unblended RGB. Gray/white
            # pixels cannot pass, and minimum positive-pixel counts are unchanged.
            $green=([Math]::Abs($pixel.R-$pixel.B) -le 2 -and $pixel.G-$pixel.R -gt 30)
            $blue=([Math]::Abs($pixel.R-$pixel.G) -le 2 -and $pixel.B-$pixel.R -gt 30)
            $purple=([Math]::Abs($pixel.R-$pixel.B) -le 2 -and $pixel.R-$pixel.G -gt 30)
            if($x -lt $bitmap.Width/2){if($green){$counts.green++};if($blue){$counts.blue++};if($purple){$counts.purpleLeft++}}
            elseif($purple){$counts.purpleRight++}
        }}
        Write-Host ("scale$scale colors: "+($counts|ConvertTo-Json -Compress))
        if($counts.green -lt 10){throw "Coplanar line not rendered at scale$scale"};$checks++
        if($counts.blue -lt 2){throw "Crossing marker not rendered at scale$scale"};$checks++
        if($counts.purpleLeft -ne 0){throw "Off-plane bar falsely shown in section at scale$scale"};$checks++
        if($counts.purpleRight -lt 10){throw "Actual projection omitted at scale$scale"};$checks++
        $bitmap.Save((Join-Path $output "synthetic-scale-$scale.png"),[Drawing.Imaging.ImageFormat]::Png);$renders++
    }finally{$graphics.Dispose();$bitmap.Dispose()}
}
Write-Host "SOLVED_VIEW_RENDERING=PASS ($checks pixel assertions; $renders synthetic renders). LIVE_REVIT=NOT_EXECUTED. Scale/bitmap simulation only."
