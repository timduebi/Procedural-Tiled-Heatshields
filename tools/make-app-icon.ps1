# Writes GameData/AblatorDecals/Icons/AppIcon.png: the 38x38 editor toolbar icon (white hex tiles with dark joints on
# transparent, like the stock app icons). Not in Textures/: everything there is offered as a tile texture.
param([string]$Out = "$PSScriptRoot\..\GameData\AblatorDecals\Icons\AppIcon.png")

Add-Type -AssemblyName System.Drawing
$size = 38
$bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# 7 flat-topped hexes (centre + ring), circumradius r, small joint between them
$r = 6.6
$joint = 1.4
$cx = $size / 2; $cy = $size / 2
$dx = 1.5 * $r; $dy = [Math]::Sqrt(3) * $r
$h = $dy / 2
$centres = @(@(0, 0), @(0, (-$dy)), @(0, $dy), @($dx, (-$h)), @($dx, $h), @((-$dx), (-$h)), @((-$dx), $h))
$fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 235, 235))
$hot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 170, 60))
$i = 0
foreach ($c in $centres) {
    $pts = for ($k = 0; $k -lt 6; $k++) {
        $a = [Math]::PI / 3 * $k
        New-Object System.Drawing.PointF ([float]($cx + $c[0] + ($r - $joint / 2) * [Math]::Cos($a))), ([float]($cy + $c[1] + ($r - $joint / 2) * [Math]::Sin($a)))
    }
    $g.FillPolygon($(if ($i -eq 0) { $hot } else { $fill }), [System.Drawing.PointF[]]$pts) # centre tile glows
    $i++
}
$g.Dispose()
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "wrote $Out"
