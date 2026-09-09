<#
    Builds the Windows release: the shared engine, the WinForms receiver, the WinUI 3 receiver
    when the tooling for it exists, and the launcher that chooses between them.

    The WinForms path has no prerequisites beyond the .NET SDK and must never be allowed to
    break. The WinUI path needs Visual Studio Build Tools, so it is detected and skipped with an
    explanation rather than failing the release of a product that ships two front ends.
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Native tools are checked by exit code below, so they must not also throw from
# $ErrorActionPreference. PowerShell 7.4 turns that on by default.
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

# A stray quote character in the user PATH is inherited by every tool started from here and
# breaks path parsing inside them. Strip it for this process only.
$env:Path = ($env:Path -replace '"', '')

$Root = Split-Path -Parent $PSScriptRoot
$CoreProject = Join-Path $Root 'windows-receiver-core\PocketMicReceiver.Core.csproj'
$ClassicProject = Join-Path $Root 'windows-receiver\PocketMicReceiver.csproj'
$WinUiProject = Join-Path $Root 'windows-receiver-winui\PocketMicReceiver.WinUI.csproj'
$LauncherProject = Join-Path $Root 'windows-launcher\PocketMic.Launcher.csproj'

$Publish = Join-Path $Root 'release\PocketMicReceiver-win-x64'
$WinUiDir = Join-Path $Publish 'winui'
$Archive = Join-Path $Root 'release\PocketMicReceiver-win-x64.zip'
$ClassicExecutable = Join-Path $Publish 'PocketMicReceiver.exe'
$LauncherStaging = Join-Path $Root 'release\.launcher'

# A dotnet.exe on PATH is not necessarily one that has an SDK. The 32-bit install under
# "Program Files (x86)" commonly ships runtime-only and shadows the real 64-bit SDK, so probe
# candidates and keep the first that reports an SDK.
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

# WinUI's targets shell out to the .NET SDK for the 64-bit build, and find it through these.
$env:DOTNET_ROOT = Split-Path -Parent $dotnetPath
$env:Path = "$env:DOTNET_ROOT;$env:Path"

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $Archive -Force -ErrorAction SilentlyContinue
Remove-Item $LauncherStaging -Recurse -Force -ErrorAction SilentlyContinue

# --- 1. Shared engine, then the WinForms receiver -----------------------------------------

& $dotnet.Source build $CoreProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Building the shared engine failed with exit code $LASTEXITCODE." }

& $dotnet.Source publish $ClassicProject `
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
if (-not (Test-Path $ClassicExecutable)) {
    throw "Publish completed but PocketMicReceiver.exe was not found at $ClassicExecutable"
}

# --- 2. The WinUI 3 receiver, if this machine can build one -------------------------------

# WinUI 3 cannot be built by "dotnet build": its targets probe for the PRI packaging task inside
# the running MSBuild's own directory, and the .NET SDK copy of MSBuild never contains it. The
# amd64 MSBuild from a Visual Studio install does. The x86 one cannot locate the 64-bit SDK.
$vsRoots = @('BuildTools', 'Enterprise', 'Professional', 'Community') |
    ForEach-Object { Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\$_" }

$winUiTooling = $null
foreach ($vsRoot in $vsRoots) {
    $priTasks = Join-Path $vsRoot 'MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.Build.Packaging.Pri.Tasks.dll'
    $msbuild = Join-Path $vsRoot 'MSBuild\Current\Bin\amd64\MSBuild.exe'
    if ((Test-Path $priTasks) -and (Test-Path $msbuild)) {
        $winUiTooling = $msbuild
        break
    }
}

$winUiBuilt = $false
if (-not $winUiTooling) {
    Write-Host ''
    Write-Host 'SKIPPING the WinUI 3 receiver: Microsoft.Build.Packaging.Pri.Tasks.dll was not found.'
    Write-Host '  Install Visual Studio 2022 Build Tools with the ".NET desktop build tools" and'
    Write-Host '  "Windows application development" (Windows App SDK C# templates) components.'
    Write-Host '  The release will contain the WinForms receiver only, and PocketMic.exe will run it.'
    Write-Host ''
}
else {
    Write-Host "WinUI MSBuild: $winUiTooling"

    # Built, not published. MSBuild's Publish target omits the compiled XAML (.xbf) and the
    # resource index (.pri) for an unpackaged WinUI app, and the result starts and immediately
    # dies with a stowed exception. The self-contained build output is the deployable app, so
    # that is what gets staged. Wiped first so nothing stale is shipped.
    $winUiBin = Join-Path $Root 'windows-receiver-winui\bin\x64\Release'
    Remove-Item $winUiBin -Recurse -Force -ErrorAction SilentlyContinue

    # Restore and build are separate invocations, not "/t:Restore;Build". The WinUI and XAML
    # compiler targets arrive through NuGet, and MSBuild imports a project's targets when it
    # evaluates it — before a restore in the same invocation has written them. On a machine that
    # already has an obj directory the second target finds them anyway and it appears to work; on
    # a clean clone the XAML never compiles, every x:Name is undefined, and the release quietly
    # falls back to WinForms only.
    & $winUiTooling $WinUiProject `
        /t:Restore `
        /p:Configuration=Release `
        /p:Platform=x64 `
        /v:m `
        /nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Restoring the WinUI 3 receiver failed (exit code $LASTEXITCODE). Continuing with the WinForms receiver only."
    }
    else {
        & $winUiTooling $WinUiProject `
            /t:Build `
            /p:Configuration=Release `
            /p:Platform=x64 `
            /v:m `
            /nologo
    }

    # Located rather than hardcoded so a target-framework bump does not silently ship nothing.
    $winUiOutput = $null
    if ($LASTEXITCODE -eq 0 -and (Test-Path $winUiBin)) {
        $winUiOutput = Get-ChildItem $winUiBin -Directory |
            ForEach-Object { Join-Path $_.FullName 'win-x64' } |
            Where-Object { Test-Path (Join-Path $_ 'PocketMicReceiver.WinUI.exe') } |
            Select-Object -First 1
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The WinUI 3 receiver failed to build (exit code $LASTEXITCODE). Continuing with the WinForms receiver only."
    }
    elseif (-not $winUiOutput) {
        Write-Warning 'The WinUI 3 build reported success but produced no executable. Continuing with the WinForms receiver only.'
    }
    else {
        New-Item -ItemType Directory -Path $WinUiDir -Force | Out-Null
        Copy-Item (Join-Path $winUiOutput '*') $WinUiDir -Recurse -Force
        $winUiBuilt = $true
    }
}

# --- 3. The launcher ----------------------------------------------------------------------

& $dotnet.Source publish $LauncherProject `
    -c Release `
    -r win-x64 `
    --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $LauncherStaging

if ($LASTEXITCODE -ne 0) {
    # Ahead-of-time compilation needs the MSVC linker and the Windows SDK. Where those are
    # missing the launcher is still worth having, just larger.
    Write-Warning 'The ahead-of-time launcher build failed; falling back to a trimmed self-contained launcher.'
    Remove-Item $LauncherStaging -Recurse -Force -ErrorAction SilentlyContinue

    & $dotnet.Source publish $LauncherProject `
        -c Release `
        -r win-x64 `
        --nologo `
        -p:PublishAot=false `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $LauncherStaging

    if ($LASTEXITCODE -ne 0) { throw "Building the launcher failed with exit code $LASTEXITCODE." }
}

Copy-Item (Join-Path $LauncherStaging 'PocketMic.exe') $Publish -Force
Remove-Item $LauncherStaging -Recurse -Force -ErrorAction SilentlyContinue

# --- 4. Archive ---------------------------------------------------------------------------

# ZipFile rather than Compress-Archive: the WinUI payload is several hundred files, and
# Compress-Archive takes minutes over what this does in seconds.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Publish,
    $Archive,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

$files = Get-ChildItem $Publish -Recurse -File
$payloadMb = [math]::Round(($files | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$archiveMb = [math]::Round((Get-Item $Archive).Length / 1MB, 1)

Write-Host ''
Write-Host "Built: $Archive"
Write-Host ("  front ends : {0}" -f $(if ($winUiBuilt) { 'WinForms + WinUI 3' } else { 'WinForms only' }))
Write-Host ("  payload    : {0} files, {1} MB" -f $files.Count, $payloadMb)
Write-Host ("  archive    : {0} MB" -f $archiveMb)
