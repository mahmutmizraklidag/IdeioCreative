$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$logoPath = Join-Path $projectRoot 'IdeioCreative\wwwroot\img\ideio-logo.png'
$destination = Join-Path $projectRoot 'IdeioCreative\wwwroot\assets\img\social\ideio-creative.png'
[void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $destination))

$canvas = New-Object System.Drawing.Bitmap(1200, 630)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$logo = [System.Drawing.Image]::FromFile($logoPath)
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#f47e3e'))
$muted = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#bebebe'))
$line = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#454750'), 1)
$font = New-Object System.Drawing.Font('Segoe UI', 24, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$format = New-Object System.Drawing.StringFormat
try {
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#161616'))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.FillRectangle($accent, 64, 54, 72, 5)
    $logoWidth = 900
    $logoHeight = $logo.Height * $logoWidth / $logo.Width
    $graphics.DrawImage($logo, [System.Drawing.RectangleF]::new(150, 104, $logoWidth, $logoHeight))
    $graphics.DrawLine($line, 64, 524, 1136, 524)
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $graphics.DrawString('ideiocreative.com', $font, $muted, [System.Drawing.RectangleF]::new(64, 548, 1072, 40), $format)
    $canvas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $format.Dispose()
    $font.Dispose()
    $line.Dispose()
    $muted.Dispose()
    $accent.Dispose()
    $logo.Dispose()
    $graphics.Dispose()
    $canvas.Dispose()
}
Write-Output 'Built assets/img/social/ideio-creative.png (1200 x 630)'
