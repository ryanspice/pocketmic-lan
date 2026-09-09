<#
    Builds the Windows release that is present in this checkout: the shared engine and the
    self-contained WinForms receiver. The older WinUI and launcher projects are not part of the
    v0.1.4 source tree, so the release package intentionally contains the receiver executable.
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

# A stray quote character in PATH breaks path parsing inside native tools. Strip it for this
# process only; the user's persistent environment is not changed.
$env:Path = ($env:Path -replace '"', '')

$Root = Split-Path -Parent $PSScriptRoot
$ReceiverProject = Join-Path $Root 'windows-receiver\PocketMicReceiver.csproj'
$Publish = Join-Path $Root 'release\PocketMicReceiver-win-x64'
$Archive = Join-Path $Root 'release\PocketMicReceiver-win-x64.zip'
$Executable = Join-Path $Publish 'PocketMicReceiver.exe'

function Test-DotnetHasSdk {
    param([string] $Path)
    if (-not $Path -or -not (Test-Path $Path)) { return $false }
    $sdks = & $Path --list-sdks 2>$null
    return $LASTEXITCODE -eq 0 -and $sdks
}

$candidates = @(
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source,
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source,
    (Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe')
) | Where-Object { $_ }

$dotnetPath = $candidates | Where-Object { Test-DotnetHasSdk $_ } | Select-Object -First 1
if (-not $dotnetPath) {
    throw '.NET 8 SDK not found. A dotnet runtime without an SDK does not count. Install the SDK, then rerun this script.'
}
$dotnet = [pscustomobject]@{ Source = $dotnetPath }
Write-Host "dotnet: $dotnetPath"

$env:DOTNET_ROOT = Split-Path -Parent $dotnetPath
$env:Path = "$env:DOTNET_ROOT;$env:Path"

if (-not (Test-Path $ReceiverProject)) {
    throw "Windows receiver project not found at $ReceiverProject"
}

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $Archive -Force -ErrorAction SilentlyContinue

& $dotnet.Source publish $ReceiverProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --nologo `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $Publish

if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited with code $LASTEXITCODE." }
if (-not (Test-Path $Executable)) {
    throw "Publish completed but PocketMicReceiver.exe was not found at $Executable"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Publish,
    $Archive,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

$files = @(Get-ChildItem $Publish -Recurse -File)
$payloadMb = [math]::Round(($files | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$archiveMb = [math]::Round((Get-Item $Archive).Length / 1MB, 1)

Write-Host ''
Write-Host "Built: $Archive"
Write-Host ("  front end : WinForms")
Write-Host ("  payload   : {0} files, {1} MB" -f $files.Count, $payloadMb)
Write-Host ("  archive   : {0} MB" -f $archiveMb)
