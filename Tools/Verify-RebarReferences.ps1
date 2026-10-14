param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../KhimTools/bin/Release/net48/KhimTools.dll'),
    [string]$RevitApiDirectory = (Join-Path ${env:ProgramFiles} 'Autodesk/Revit 2024')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
[Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory 'RevitAPI.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$kindType = $assembly.GetType('KhimTools.RebarTool.Forms.RebarReferenceKind')
$factory = $assembly.GetType('KhimTools.RebarTool.Forms.RebarReferenceViews').GetMethod('Create',[Reflection.BindingFlags]'NonPublic,Static')
$output = [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot '../artifacts/ui-qa/rebar-references')).FullName
$renders = 0
foreach ($kind in [Enum]::GetValues($kindType)) {
    foreach ($size in @(@(540,360),@(810,540))) {
        $tabs = $factory.Invoke($null,@($kind))
        try {
            if ($tabs.TabCount -ne 4) { throw "Missing reference views: $kind" }
            for ($i=0;$i -lt 4;$i++) {
                $canvas = $tabs.TabPages[$i].Controls[0].Controls[0]
                $canvas.Parent.Controls.Remove($canvas)
                try {
                    $canvas.Size = [Drawing.Size]::new($size[0],$size[1])
                    $canvas.Visible = $true
                    $handle = $canvas.Handle
                    $bitmap = [Drawing.Bitmap]::new($canvas.Width,$canvas.Height)
                    try {
                        $canvas.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$canvas.Width,$canvas.Height))
                        $blue = 0; $dark = 0
                        for ($x=0;$x -lt $bitmap.Width;$x+=2) {
                            for ($y=0;$y -lt $bitmap.Height;$y+=2) {
                                $p = $bitmap.GetPixel($x,$y)
                                if ($p.B -gt 130 -and $p.R -lt 70) { $blue++ }
                                if ($p.R -lt 180 -and $p.G -lt 180 -and $p.B -lt 180) { $dark++ }
                            }
                        }
                        if ($blue -lt 40 -or $dark -lt 40) { throw "Blank reference render: $kind/$i" }
                        $bitmap.Save((Join-Path $output "$kind-$i-$($size[0]).png"),[Drawing.Imaging.ImageFormat]::Png)
                        $renders++
                    } finally { $bitmap.Dispose() }
                } finally { $canvas.Dispose() }
            }
        } finally { $tabs.Dispose() }
    }
}
Write-Host "PASS: $renders reference renders, five member types and four views at two sizes."
Write-Host 'Schematic drawing QA only; no generated reinforcement or code compliance validated.'
