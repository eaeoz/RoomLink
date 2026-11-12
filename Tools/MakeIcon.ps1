# Builds src\RoomLink\Assets\appicon.ico (multi-size) from Tools\IconSrc\headset_mic_512.png.
#
# Icon source: "Ic headset mic 48px.svg" — Material Design Icons by Google,
# CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/), rendered to PNG at
# 512px by Wikimedia Commons and composited on a rounded-square background here.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# C# helper: recolors a glyph to white while keeping its alpha channel.
# (ColorMatrix property assignment on boxed structs is unreliable from PS.)
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class GlyphRecolor
{
    public static Bitmap ToWhite(Bitmap src)
    {
        var rect = new Rectangle(0, 0, src.Width, src.Height);
        var outp = new Bitmap(src.Width, src.Height);
        var sd = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var od = outp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(sd.Stride) * src.Height;
        var buf = new byte[bytes];
        Marshal.Copy(sd.Scan0, buf, 0, bytes);
        for (int i = 0; i < bytes; i += 4)
        {
            byte a = buf[i + 3];
            buf[i]     = 255;  // B
            buf[i + 1] = 255;  // G
            buf[i + 2] = 255;  // R
            buf[i + 3] = a;
        }
        Marshal.Copy(buf, 0, od.Scan0, bytes);
        src.UnlockBits(sd);
        outp.UnlockBits(od);
        return outp;
    }
}
"@ -ReferencedAssemblies System.Drawing

$root   = $PSScriptRoot
$glyph  = Join-Path $root "IconSrc\headset_mic_512.png"
$outDir = Join-Path $root "..\src\RoomLink\Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$masterPath = Join-Path $outDir "appicon-256.png"
$icoPath    = Join-Path $outDir "appicon.ico"

# ------------------------------------------------------- master 256x256 png
$size  = 256
$master = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($master)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Rounded-square background with a vertical blue gradient (Windows-11-ish)
$radius = 56
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$d = $radius * 2
$path.AddArc(0, 0, $d, $d, 180, 90)
$path.AddArc($size - $d, 0, $d, $d, 270, 90)
$path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
$path.AddArc(0, $size - $d, $d, $d, 90, 90)
$path.CloseFigure()
$bgRect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
$gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, `
    ([System.Drawing.Color]::FromArgb(0x2D, 0xA8, 0xE2)), `
    ([System.Drawing.Color]::FromArgb(0x0B, 0x5E, 0xB5)), 90
$g.FillPath($gradient, $path)

# Draw the glyph as white, centered, using the glyph alpha as a mask
$src   = [System.Drawing.Image]::FromFile($glyph)
$white = [GlyphRecolor]::ToWhite((New-Object System.Drawing.Bitmap $src))
$src.Dispose()
$inner = [int]($size * 0.58)          # glyph occupies 58% of the tile
$offset = [int](($size - $inner) / 2)
$g.DrawImage($white, $offset, $offset, $inner, $inner)
$white.Dispose()
$g.Dispose()

$master.Save($masterPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Master written: $masterPath"

# ------------------------------------------------------------ multi-size ico
# Each frame is a 32bpp BGRA DIB: BITMAPINFOHEADER (biHeight = height * 2),
# XOR pixel rows bottom-up, then an all-zero 1bpp AND mask (alpha carries
# opacity, so the mask is empty).
function New-IcoFile([System.Drawing.Bitmap]$master, [string]$icoPath, [int[]]$sizes) {
    $frames = @()
    foreach ($s in $sizes) {
        $scaled = New-Object System.Drawing.Bitmap $s, $s
        $sg = [System.Drawing.Graphics]::FromImage($scaled)
        $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $sg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $sg.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $sg.DrawImage($master, 0, 0, $s, $s)
        $sg.Dispose()

        $frame = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $frame
        $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2))
        $bw.Write([uint16]1);  $bw.Write([uint16]32); $bw.Write([uint32]0)
        $bw.Write([uint32]($s * $s * 4 + $s * $s / 8)); $bw.Write([int32]0)
        $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)

        $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
        $data = $scaled.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $row = New-Object byte[] ($s * 4)
        for ($y = $s - 1; $y -ge 0; $y--) {                     # bottom-up
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $s * 4)
            $bw.Write($row)
        }
        $scaled.UnlockBits($data)

        $zeroMask = New-Object byte[] ([int][Math]::Ceiling($s / 8.0))
        for ($y = 0; $y -lt $s; $y++) { $bw.Write($zeroMask) }
        $bw.Flush()

        $frames += ,@($s, $frame.ToArray())
        $frame.Dispose()
        $scaled.Dispose()
    }

    $fs = [System.IO.File]::Create($icoPath)
    $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {
        $code = if ($f[0] -ge 256) { 0 } else { $f[0] }
        $w.Write([byte]$code); $w.Write([byte]$code); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$f[1].Length); $w.Write([uint32]$offset)
        $offset += $f[1].Length
    }
    foreach ($f in $frames) { $w.Write($f[1]) }
    $w.Dispose(); $fs.Dispose()
    Write-Host "ICO written: $icoPath ($((Get-Item $icoPath).Length) bytes, $($frames.Count) sizes)"
}

New-IcoFile $master $icoPath @(16, 24, 32, 48, 64, 128, 256)
$master.Dispose()
