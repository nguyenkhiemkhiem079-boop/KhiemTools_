Add-Type -AssemblyName System.Drawing

$resourcesPath = "Resources"
if (-not (Test-Path $resourcesPath)) {
    New-Item -ItemType Directory -Path $resourcesPath | Out-Null
}

$baseInk = [System.Drawing.Color]::FromArgb(255, 30, 41, 59)    # Slate Navy #1E293B
$accent = [System.Drawing.Color]::FromArgb(255, 2, 132, 199)    # Sky Blue #0284C7
$accentLight = [System.Drawing.Color]::FromArgb(255, 224, 242, 254) # Soft Sky Blue
$white = [System.Drawing.Color]::White

function Create-Icon {
    param (
        [string]$name,
        [int]$size,
        [scriptblock]$drawAction
    )
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    
    $penBase = New-Object System.Drawing.Pen $baseInk, ($size / 16)
    $penAccent = New-Object System.Drawing.Pen $accent, ($size / 16)
    $brushBase = New-Object System.Drawing.SolidBrush $baseInk
    $brushAccent = New-Object System.Drawing.SolidBrush $accent
    $brushAccentLight = New-Object System.Drawing.SolidBrush $accentLight
    
    # Run drawing action
    & $drawAction $g $size $penBase $penAccent $brushBase $brushAccent $brushAccentLight
    
    $outputPath = Join-Path $resourcesPath "$name.png"
    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    
    $penBase.Dispose()
    $penAccent.Dispose()
    $brushBase.Dispose()
    $brushAccent.Dispose()
    $brushAccentLight.Dispose()
    $g.Dispose()
    $bmp.Dispose()
}

# 1. icon_workspace: A window with sidebar (BaseInk) and main area with content lines (Accent)
$drawWorkspace = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $pad = $size * 0.1
    $w = $size - (2 * $pad)
    # Window Frame
    $g.DrawRectangle($penBase, $pad, $pad, $w, $w)
    # Sidebar
    $sbW = $w * 0.3
    $g.FillRectangle($brushBase, $pad, $pad, $sbW, $w)
    # Content lines (Accent)
    $g.DrawLine($penAccent, $pad + $sbW + ($w * 0.1), $pad + ($w * 0.3), $pad + $w - ($w * 0.1), $pad + ($w * 0.3))
    $g.DrawLine($penAccent, $pad + $sbW + ($w * 0.1), $pad + ($w * 0.6), $pad + $w - ($w * 0.3), $pad + ($w * 0.6))
}

# 2. icon_copylink: Overlapping rectangles (BaseInk & Accent) with a line link
$drawCopyLink = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $w = $size * 0.5
    # Bottom-left block (Base)
    $g.DrawRectangle($penBase, $size * 0.1, $size * 0.4, $w, $w)
    # Top-right block (Accent)
    $g.FillRectangle($brushAccentLight, $size * 0.4, $size * 0.1, $w, $w)
    $g.DrawRectangle($penAccent, $size * 0.4, $size * 0.1, $w, $w)
    # Center link line
    $g.DrawLine($penAccent, $size * 0.3, $size * 0.7, $size * 0.7, $size * 0.3)
}

# 3. icon_join: Overlapping beams forming an intersection (Accent)
$drawJoin = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $pad = $size * 0.15
    $thick = $size * 0.25
    # Horizontal Beam (Base)
    $g.DrawRectangle($penBase, $pad, ($size - $thick)/2, $size - (2*$pad), $thick)
    # Vertical Column (Base)
    $g.DrawRectangle($penBase, ($size - $thick)/2, $pad, $thick, $size - (2*$pad))
    # Overlap region (Accent)
    $g.FillRectangle($brushAccent, ($size - $thick)/2, ($size - $thick)/2, $thick, $thick)
}

# 4. icon_grid_plan: Grid lines with a level floor outline (Accent)
$drawGridPlan = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Draw Grid Lines
    $g.DrawLine($penBase, $size*0.25, $size*0.1, $size*0.25, $size*0.9)
    $g.DrawLine($penBase, $size*0.75, $size*0.1, $size*0.75, $size*0.9)
    $g.DrawLine($penBase, $size*0.1, $size*0.35, $size*0.9, $size*0.35)
    $g.DrawLine($penBase, $size*0.1, $size*0.65, $size*0.9, $size*0.65)
    # Draw Floor Plan Box (Accent)
    $g.FillRectangle($brushAccentLight, $size*0.35, $size*0.2, $size*0.5, $size*0.5)
    $g.DrawRectangle($penAccent, $size*0.35, $size*0.2, $size*0.5, $size*0.5)
}

# 5. icon_detail: Eye or list details
$drawDetail = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Eye-like outline or modern list
    # Let's draw list details (Bullet points + Lines)
    for ($i = 0; $i -lt 3; $i++) {
        $y = $size * (0.2 + 0.25 * $i)
        $g.FillEllipse($brushAccent, $size*0.15, $y, $size*0.15, $size*0.15)
        $g.DrawLine($penBase, $size*0.4, $y + $size*0.07, $size*0.85, $y + $size*0.07)
    }
}

# 6. icon_align: Dash axis (Accent) with elements aligning to it
$drawAlign = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Aligning Axis (Accent)
    $penDash = New-Object System.Drawing.Pen $accent, ($size / 16)
    $penDash.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawLine($penDash, $size*0.5, $size*0.1, $size*0.5, $size*0.9)
    $penDash.Dispose()
    
    # Left & Right aligned elements
    $g.FillRectangle($brushBase, $size*0.15, $size*0.2, $size*0.35, $size*0.15)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.2, $size*0.35, $size*0.15)
    
    $g.FillRectangle($brushBase, $size*0.5, $size*0.45, $size*0.35, $size*0.15)
    $g.DrawRectangle($penBase, $size*0.5, $size*0.45, $size*0.35, $size*0.15)
    
    $g.FillRectangle($brushBase, $size*0.2, $size*0.7, $size*0.3, $size*0.15)
    $g.DrawRectangle($penBase, $size*0.2, $size*0.7, $size*0.3, $size*0.15)
}

# 7. export_sheet: Document outline with a bold right arrow
$drawExportSheet = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Document Page Outline (Base)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.5, $size*0.7)
    # Folding Corner
    $g.DrawLine($penBase, $size*0.5, $size*0.15, $size*0.65, $size*0.3)
    # Export Arrow (Accent)
    # Arrow tail: line
    $g.DrawLine($penAccent, $size*0.4, $size*0.5, $size*0.8, $size*0.5)
    # Arrow head: polygon
    $pts = @(
        (New-Object System.Drawing.PointF ($size*0.7), ($size*0.35)),
        (New-Object System.Drawing.PointF ($size*0.7), ($size*0.65)),
        (New-Object System.Drawing.PointF ($size*0.9), ($size*0.5))
    )
    $g.FillPolygon($brushAccent, $pts)
}

# 8. icon_mep_tags: Label tag with circular anchor
$drawMepTags = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Tag Frame
    $g.FillRectangle($brushAccentLight, $size*0.3, $size*0.2, $size*0.55, $size*0.4)
    $g.DrawRectangle($penBase, $size*0.3, $size*0.2, $size*0.55, $size*0.4)
    # Anchor Leader Line
    $g.DrawLine($penAccent, $size*0.3, $size*0.4, $size*0.15, $size*0.7)
    $g.FillEllipse($brushAccent, $size*0.1, $size*0.65, $size*0.1, $size*0.1)
    # Text placeholder in Tag
    $g.DrawLine($penBase, $size*0.4, $size*0.33, $size*0.75, $size*0.33)
    $g.DrawLine($penBase, $size*0.4, $size*0.47, $size*0.65, $size*0.47)
}

# 9. icon_update: Circular refresh arrows
$drawUpdate = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Loop arrows
    $g.DrawArc($penBase, $size*0.2, $size*0.2, $size*0.6, $size*0.6, 45, 230)
    $g.DrawArc($penAccent, $size*0.2, $size*0.2, $size*0.6, $size*0.6, 225, 110)
    # Accent arrow head
    $pts1 = @(
        (New-Object System.Drawing.PointF ($size*0.8), ($size*0.45)),
        (New-Object System.Drawing.PointF ($size*0.95), ($size*0.35)),
        (New-Object System.Drawing.PointF ($size*0.8), ($size*0.25))
    )
    $g.FillPolygon($brushAccent, $pts1)
}

# 10. rebar_col: Square column with stirrups and internal rebar dots
$drawRebarCol = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    # Stirrup
    $g.DrawRectangle($penAccent, $size*0.25, $size*0.25, $size*0.5, $size*0.5)
    # Reinforcement Dots (Accent)
    $dotSize = $size * 0.12
    $g.FillEllipse($brushAccent, $size*0.27 - $dotSize/2, $size*0.27 - $dotSize/2, $dotSize, $dotSize)
    $g.FillEllipse($brushAccent, $size*0.73 - $dotSize/2, $size*0.27 - $dotSize/2, $dotSize, $dotSize)
    $g.FillEllipse($brushAccent, $size*0.27 - $dotSize/2, $size*0.73 - $dotSize/2, $dotSize, $dotSize)
    $g.FillEllipse($brushAccent, $size*0.73 - $dotSize/2, $size*0.73 - $dotSize/2, $dotSize, $dotSize)
}

# 11. rebar_beam: Horizontal structure with cage reinforcement
$drawRebarBeam = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Horizontal Concrete Beam Outline
    $g.DrawRectangle($penBase, $size*0.1, $size*0.3, $size*0.8, $size*0.4)
    # Rebar lines
    $g.DrawLine($penAccent, $size*0.15, $size*0.38, $size*0.85, $size*0.38)
    $g.DrawLine($penAccent, $size*0.15, $size*0.62, $size*0.85, $size*0.62)
    # Stirrups
    for ($x = 0.25; $x -lt 0.8; $x += 0.15) {
        $g.DrawLine($penAccent, $size*$x, $size*0.38, $size*$x, $size*0.62)
    }
}

# 12. rebar_slab: Concrete slab slice with dot wire mesh
$drawRebarSlab = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawRectangle($penBase, $size*0.1, $size*0.25, $size*0.8, $size*0.5)
    # Rebar grid
    $g.DrawLine($penAccent, $size*0.15, $size*0.4, $size*0.85, $size*0.4)
    $g.DrawLine($penAccent, $size*0.15, $size*0.6, $size*0.85, $size*0.6)
    # Mesh dots
    $d = $size * 0.08
    for ($x = 0.2; $x -lt 0.9; $x += 0.25) {
        $g.FillEllipse($brushAccent, $size*$x - $d/2, $size*0.4 - $d/2, $d, $d)
        $g.FillEllipse($brushAccent, $size*($x + 0.1) - $d/2, $size*0.6 - $d/2, $d, $d)
    }
}

# 13. rebar_fdn: Trapezoid shape (Base) with rebar mesh at bottom (Accent)
$drawRebarFdn = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Draw Trapezoid
    $pts = @(
        (New-Object System.Drawing.PointF ($size*0.35), ($size*0.2)),
        (New-Object System.Drawing.PointF ($size*0.65), ($size*0.2)),
        (New-Object System.Drawing.PointF ($size*0.85), ($size*0.75)),
        (New-Object System.Drawing.PointF ($size*0.15), ($size*0.75))
    )
    $g.DrawPolygon($penBase, $pts)
    # Rebar mesh bottom
    $g.DrawLine($penAccent, $size*0.2, $size*0.68, $size*0.8, $size*0.68)
    for ($x = 0.3; $x -le 0.75; $x += 0.15) {
        $g.DrawLine($penAccent, $size*$x, $size*0.64, $size*$x, $size*0.72)
    }
}

# 14. icon_section_cut: Building slab sliced by dynamic blue plane
$drawSectionCut = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Isometric box (Base)
    $g.DrawRectangle($penBase, $size*0.2, $size*0.3, $size*0.5, $size*0.5)
    # Slice line (Accent)
    $g.DrawLine($penAccent, $size*0.1, $size*0.55, $size*0.8, $size*0.55)
    # Arrow heads
    $g.FillPolygon($brushAccent, @(
        (New-Object System.Drawing.PointF ($size*0.75), ($size*0.48)),
        (New-Object System.Drawing.PointF ($size*0.75), ($size*0.62)),
        (New-Object System.Drawing.PointF ($size*0.87), ($size*0.55))
    ))
}

# 15. icon_cover_setup: Frame with a dashed internal offset zone
$drawCoverSetup = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    # Dashed inner line representing concrete cover zone
    $penDash = New-Object System.Drawing.Pen $accent, ($size / 16)
    $penDash.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawRectangle($penDash, $size*0.28, $size*0.28, $size*0.44, $size*0.44)
    $penDash.Dispose()
}

# 16. icon_room3d: Cube in isometric with one side highlighted
$drawRoom3d = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Clean isometric box outline
    $g.DrawRectangle($penBase, $size*0.15, $size*0.2, $size*0.7, $size*0.6)
    # Blue inner room bounding box
    $g.FillRectangle($brushAccentLight, $size*0.25, $size*0.3, $size*0.5, $size*0.4)
    $g.DrawRectangle($penAccent, $size*0.25, $size*0.3, $size*0.5, $size*0.4)
}

# 17. icon_finishes: Double walls showing boundary finish
$drawFinishes = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    # Double line offsetting inside representing wall/floor finishes
    $g.DrawRectangle($penAccent, $size*0.22, $size*0.22, $size*0.56, $size*0.56)
    $g.DrawRectangle($penAccent, $size*0.28, $size*0.28, $size*0.44, $size*0.44)
}

# 18. icon_mep_openings: MEP pipe clash wall with opening frame
$drawMepOpenings = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Wall background
    $g.DrawRectangle($penBase, $size*0.15, $size*0.3, $size*0.7, $size*0.4)
    # Pipe passing (Base)
    $g.FillRectangle($brushBase, $size*0.3, $size*0.2, $size*0.4, $size*0.6)
    # Opening box cutout (Accent)
    $g.DrawRectangle($penAccent, $size*0.25, $size*0.25, $size*0.5, $size*0.5)
}

# 19. icon_sectionbox: 3D bounding box / Section Box (Accent) over a wireframe model cube (Base)
$drawSectionBox = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Wireframe isometric cube (Base)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    # Section Box Crop boundary (Accent)
    $g.FillRectangle($brushAccentLight, $size*0.25, $size*0.25, $size*0.5, $size*0.5)
    $g.DrawRectangle($penAccent, $size*0.25, $size*0.25, $size*0.5, $size*0.5)
    # Corner crop lines
    $g.DrawLine($penAccent, $size*0.2, $size*0.2, $size*0.3, $size*0.2)
    $g.DrawLine($penAccent, $size*0.2, $size*0.2, $size*0.2, $size*0.3)
    $g.DrawLine($penAccent, $size*0.8, $size*0.8, $size*0.7, $size*0.8)
    $g.DrawLine($penAccent, $size*0.8, $size*0.8, $size*0.8, $size*0.7)
}

# 20. icon_callout_pro: Dashed callout region with pointer tail and detail bubble (Accent)
$drawCalloutPro = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Standard architectural sheet/view boundaries (Base)
    $g.DrawRectangle($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    # Dashed callout rectangle (Accent)
    $penDash = New-Object System.Drawing.Pen $accent, ($size / 16)
    $penDash.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawRectangle($penDash, $size*0.28, $size*0.28, $size*0.36, $size*0.36)
    $penDash.Dispose()
    # Pointer tail to bubble
    $g.DrawLine($penAccent, $size*0.64, $size*0.46, $size*0.75, $size*0.35)
    # Detail bubble (Accent)
    $g.FillEllipse($brushAccent, $size*0.72, $size*0.2, $size*0.2, $size*0.2)
}

# 21. icon_view_callout: Callout symbol spawning multiple view plans/elevations
$drawViewCallout = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    # Main callout circle/bubble (Base)
    $g.DrawEllipse($penBase, $size*0.15, $size*0.15, $size*0.35, $size*0.35)
    $g.DrawLine($penBase, $size*0.32, $size*0.5, $size*0.32, $size*0.8)
    # Multiple views spawning out (Accent)
    $g.FillRectangle($brushAccentLight, $size*0.55, $size*0.15, $size*0.3, $size*0.2)
    $g.DrawRectangle($penAccent, $size*0.55, $size*0.15, $size*0.3, $size*0.2)
    $g.FillRectangle($brushAccentLight, $size*0.55, $size*0.42, $size*0.3, $size*0.2)
    $g.DrawRectangle($penAccent, $size*0.55, $size*0.42, $size*0.3, $size*0.2)
    $g.FillRectangle($brushAccentLight, $size*0.55, $size*0.7, $size*0.3, $size*0.2)
    $g.DrawRectangle($penAccent, $size*0.55, $size*0.7, $size*0.3, $size*0.2)
}

# --- Register standard icons at 32x32 and 16x16 ---
$iconMap = @{
    "icon_workspace"    = $drawWorkspace
    "icon_copylink"     = $drawCopyLink
    "icon_join"         = $drawJoin
    "icon_grid_plan"    = $drawGridPlan
    "icon_detail"       = $drawDetail
    "icon_sectionbox"   = $drawSectionBox
    "icon_callout_pro"  = $drawCalloutPro
    "icon_view_callout" = $drawViewCallout
    "icon_align"        = $drawAlign
    "export_sheet"      = $drawExportSheet
    "icon_mep_tags"     = $drawMepTags
    "icon_update"       = $drawUpdate
    "rebar_col"         = $drawRebarCol
    "rebar_beam"        = $drawRebarBeam
    "rebar_slab"        = $drawRebarSlab
    "rebar_fdn"         = $drawRebarFdn
    "icon_section_cut"  = $drawSectionCut
    "icon_cover_setup"  = $drawCoverSetup
    "icon_room3d"       = $drawRoom3d
    "icon_finishes"     = $drawFinishes
    "icon_mep_openings" = $drawMepOpenings
}

$iconMap.Keys | ForEach-Object {
    Create-Icon "$_" 32 $iconMap[$_]
    Create-Icon "$_`_32" 32 $iconMap[$_]
    Create-Icon "$_`_16" 16 $iconMap[$_]
}

# Additional specific icons mapping to existing resources
Create-Icon "rebar_col_rect_16" 16 $drawRebarCol
Create-Icon "rebar_col_circ_16" 16 $drawRebarCol
Create-Icon "rebar_col_circ_32" 32 $drawRebarCol
Create-Icon "rebar_draw_16" 16 $drawDetail
Create-Icon "rebar_cover_16" 16 $drawCoverSetup

# 19. Swatches & Override specific icons
$colors = @{
    "red"     = [System.Drawing.Color]::FromArgb(255, 239, 68, 68)
    "orange"  = [System.Drawing.Color]::FromArgb(255, 249, 115, 22)
    "yellow"  = [System.Drawing.Color]::FromArgb(255, 234, 179, 8)
    "green"   = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)
    "cyan"    = [System.Drawing.Color]::FromArgb(255, 6, 182, 212)
    "blue"    = [System.Drawing.Color]::FromArgb(255, 59, 130, 246)
    "magenta" = [System.Drawing.Color]::FromArgb(255, 217, 70, 239)
    "gray"    = [System.Drawing.Color]::FromArgb(255, 100, 116, 139)
}

$colors.Keys | ForEach-Object {
    $c = $colors[$_]
    $name = $_
    Create-Icon "override_$name`_16" 16 {
        param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
        $brush = New-Object System.Drawing.SolidBrush $c
        $g.FillRectangle($brush, 2, 2, $size - 4, $size - 4)
        $brush.Dispose()
    }
}

# Custom Gradient swatch
Create-Icon "override_custom_16" 16 {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $cRed = [System.Drawing.Color]::Red
    $cBlue = [System.Drawing.Color]::Blue
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $cRed, $cBlue, 45.0
    $g.FillRectangle($brush, 2, 2, $size - 4, $size - 4)
    $brush.Dispose()
}

# Halftone Option Swatch
$drawHalftone = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawEllipse($penBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7)
    $g.FillPie($brushBase, $size*0.15, $size*0.15, $size*0.7, $size*0.7, 90, 180)
}
Create-Icon "override_halftone_16" 16 $drawHalftone
Create-Icon "override_halftone_32" 32 $drawHalftone

# Reset Swatch
$drawReset = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawArc($penBase, $size*0.2, $size*0.2, $size*0.6, $size*0.6, 90, 270)
    $g.DrawLine($penAccent, $size*0.15, $size*0.15, $size*0.85, $size*0.85)
}
Create-Icon "override_reset_16" 16 $drawReset
Create-Icon "override_reset_32" 32 $drawReset

# Setting Swatch
$drawSetting = {
    param($g, $size, $penBase, $penAccent, $brushBase, $brushAccent, $brushAccentLight)
    $g.DrawEllipse($penBase, $size*0.25, $size*0.25, $size*0.5, $size*0.5)
    $g.DrawLine($penAccent, $size*0.1, $size*0.5, $size*0.9, $size*0.5)
    $g.DrawLine($penAccent, $size*0.5, $size*0.1, $size*0.5, $size*0.9)
}
Create-Icon "override_setting_16" 16 $drawSetting
Create-Icon "override_setting_32" 32 $drawSetting

Write-Output "Programmatic two-tone icon set generated successfully in Resources/ directory."
