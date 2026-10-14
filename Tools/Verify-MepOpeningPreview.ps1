param([string]$AssemblyPath=(Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$planType=$assembly.GetType('KhimTools.MEP.Penetrations.MepOpeningCutPlanV1',$true)
$resolve=$planType.GetMethods()|Where-Object {$_.Name -eq 'Resolve' -and $_.GetParameters().Length -eq 7}
$plan=$resolve.Invoke($null,[object[]]@([double]500,[double]900,[double]800,[double]1200,[double]2000,[double]1800,[double]25))
$point=$assembly.GetType('KhimTools.MEP.Penetrations.MepPreviewPointV1',$true)
$start=[Activator]::CreateInstance($point,[object[]]@([double]700,[double]1000,[double]-200))
$finish=[Activator]::CreateInstance($point,[object[]]@([double]700,[double]1000,[double]400))
$geometryType=$assembly.GetType('KhimTools.MEP.Penetrations.MepOpeningPreviewGeometryV1',$true)
$geometry=[Activator]::CreateInstance($geometryType,[object[]]@([double]2000,[double]1800,[double]200,$plan,$start,$finish))
$formType=$assembly.GetType('KhimTools.MEP.Penetrations.MepOpeningPreviewFormV1',$true)
$ctor=$formType.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')|Select-Object -First 1
$output=[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/mep-openings')).FullName
$checks=0;$renders=0
function Assert([bool]$ok,[string]$label){if(!$ok){throw "MEP preview QA failed: $label"};$script:checks++}
function InBounds($parent){foreach($c in $parent.Controls){if($c.Left -lt 0 -or $c.Top -lt 0 -or $c.Right -gt $parent.ClientSize.Width+1 -or $c.Bottom -gt $parent.ClientSize.Height+1){return $false};if(!(InBounds $c)){return $false}};return $true}
foreach($scale in @(1,1.25,1.5)){foreach($profile in @('minimum','1366x768','1920x1080')){
    $form=$ctor.Invoke([object[]]@($geometry,'SYNTHETIC OFFLINE QA — not native Revit geometry. Explicit identities/dimensions/offsets.'))
    try {
        $form.Scale([Drawing.SizeF]::new($scale,$scale))
        $requested=switch($profile){'minimum'{$form.MinimumSize};'1366x768'{[Drawing.Size]::new(1366,768)};'1920x1080'{[Drawing.Size]::new(1920,1080)}}
        $form.Size=$requested;$form.CreateControl();$form.PerformLayout()
        Assert ($form.Size.Width -le $requested.Width -and $form.Size.Height -le $requested.Height) "window not silently enlarged $profile/$scale"
        Assert (InBounds $form) "actual controls inside parents $profile/$scale"
        $canvas=$form.Controls|Where-Object {$_.GetType().Name -eq 'PreviewCanvas'}
        foreach($iso in @($false,$true)){
            $canvas.GetType().GetField('Isometric',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($canvas,$iso)
            $bitmap=[Drawing.Bitmap]::new($canvas.Width,$canvas.Height)
            try {
                $canvas.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height))
                $orange=0;$blue=0
                for($x=0;$x -lt $bitmap.Width;$x+=2){for($y=30;$y -lt $bitmap.Height;$y+=2){$color=$bitmap.GetPixel($x,$y);if($color.R -gt 200 -and $color.G -gt 70 -and $color.G -lt 200 -and $color.B -lt 60){$orange++};if($color.B-$color.R -gt 100 -and $color.B-$color.G -gt 100){$blue++}}}
                Assert ($orange -gt 10) "actual cut rendered $profile/$scale/$iso"
                Assert ($blue -gt 2) "centerline or symbolic point rendered $profile/$scale/$iso"
                $bitmap.Save((Join-Path $output "$profile-scale$scale-iso$iso.png"),[Drawing.Imaging.ImageFormat]::Png);$renders++
            }finally{$bitmap.Dispose()}
        }
    }finally{$form.Dispose()}
}}
Write-Host "MEP_OPENING_PREVIEW=PASS ($checks assertions; $renders synthetic renders). LIVE_REVIT=NOT_EXECUTED; sizes/scaling simulated."
