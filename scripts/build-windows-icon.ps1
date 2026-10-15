# Reproduce the Windows icon directly from the Android launcher vector, without redrawing it.
# Requires ImageMagick only when regenerating the committed .ico asset.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml] $vector = Get-Content -Raw (Join-Path $root 'android/app/src/main/res/drawable/ic_launcher_foreground.xml')
$android = 'http://schemas.android.com/apk/res/android'
$paths = foreach ($path in $vector.vector.path) {
    '<path fill="{0}" d="{1}"/>' -f $path.GetAttribute('fillColor', $android), $path.GetAttribute('pathData', $android)
}
$assets = Join-Path $root 'windows-receiver/Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$svg = Join-Path $assets 'PocketMic.svg'
[IO.File]::WriteAllText($svg, '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 108 108" width="256" height="256">' + ($paths -join '') + "</svg>`n")
& magick -background none $svg -define icon:auto-resize=256,128,64,48,32,24,16 (Join-Path $assets 'PocketMic.ico')
if ($LASTEXITCODE -ne 0) { throw 'Windows icon conversion failed.' }
