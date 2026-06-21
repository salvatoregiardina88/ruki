#Requires -Version 5
<#
    Genera Logo.ico (multi-risoluzione) dal logo PNG, per l'icona dell'eseguibile e dell'installer.
    Eseguire con Windows PowerShell (powershell.exe), che include System.Drawing.

    Uso:  powershell -ExecutionPolicy Bypass -File build\make_icon.ps1
#>
param(
    [string]$Source = "$PSScriptRoot\..\docs\Logo.png",
    [string]$Output = "$PSScriptRoot\..\src\Ruki.App\Assets\Logo.ico"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$srcBmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source).Path)
$sizes = 256, 64, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($srcBmp, 0, 0, $s, $s)
    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , ($stream.ToArray())
    $bmp.Dispose(); $stream.Dispose()
}
$srcBmp.Dispose()

# Assembla il file ICO (header + voci + dati PNG di ciascuna dimensione).
$count = $sizes.Count
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$count)   # reserved, type=icon, count
$offset = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 = 256
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path (Split-Path $Output) (Split-Path $Output -Leaf)), $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Host "ICO creato: $Output"
