# Format conversion only: preserve the committed PNG artwork and its alpha channel.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $projectRoot 'src/UniversalControl.App/Assets'
$inputPng = Join-Path $assetDirectory 'app-icon.png'
$outputIco = Join-Path $assetDirectory 'app.ico'
$source = [Drawing.Bitmap]::new($inputPng)
try {
  if ($source.Width -ne $source.Height) { throw 'The source icon must be square.' }
  $frames = foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $stream = [IO.MemoryStream]::new()
    try {
      $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
      $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size))
      $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
      [pscustomobject]@{ Size=$size; Bytes=$stream.ToArray() }
    } finally { $stream.Dispose();$graphics.Dispose();$bitmap.Dispose() }
  }
  $output = [IO.File]::Create($outputIco)
  $writer = [IO.BinaryWriter]::new($output)
  try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
      $dimension = if($frame.Size -eq 256){0}else{$frame.Size}
      $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
      $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset)
      $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
  } finally { $writer.Dispose() }
  $docsDirectory = Join-Path $projectRoot 'docs/images'
  New-Item -ItemType Directory -Path $docsDirectory -Force | Out-Null
  [IO.File]::WriteAllBytes((Join-Path $docsDirectory 'app-icon.png'), ($frames | Where-Object { $_.Size -eq 128 }).Bytes)
  'ICO frames: ' + (($frames | ForEach-Object{$_.Size}) -join ', ')
} finally { $source.Dispose() }
