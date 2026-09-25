param(
    [string] $OutputDirectory,
    [string] $ArchivePath,
    [string] $OpusDllPath
)

<#
    Builds the self-contained WinForms receiver ZIP. Set -OpusDllPath to include the pinned
    native decoder; CI supplies an x64 libopus build so Android Opus v2 streams work out of the box.
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
$PackageReadme = Join-Path $Root 'windows-receiver\PACKAGE-README.md'
$ThirdPartyNotices = Join-Path $Root 'windows-receiver\THIRD-PARTY-NOTICES.md'
$PocketMicLicense = Join-Path $Root 'LICENSE'
$OpusLicense = Join-Path $Root 'windows-receiver\OPUS-COPYING.txt'
$Publish = if ($OutputDirectory) {
    if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $Root $OutputDirectory }
} else {
    Join-Path $Root 'release\PocketMicReceiver-win-x64'
}
$Archive = if ($ArchivePath) {
    if ([System.IO.Path]::IsPathRooted($ArchivePath)) { $ArchivePath } else { Join-Path $Root $ArchivePath }
} else {
    Join-Path $Root 'release\PocketMicReceiver-win-x64.zip'
}
$Publish = [System.IO.Path]::GetFullPath($Publish)
$Archive = [System.IO.Path]::GetFullPath($Archive)
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
New-Item -ItemType Directory -Path (Split-Path -Parent $Archive) -Force | Out-Null

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

foreach ($requiredFile in @($PackageReadme, $ThirdPartyNotices, $PocketMicLicense)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "A required Windows package document is missing: $requiredFile"
    }
}

Copy-Item -LiteralPath $PackageReadme -Destination (Join-Path $Publish 'README-Windows.txt') -Force
Copy-Item -LiteralPath $ThirdPartyNotices -Destination (Join-Path $Publish 'THIRD-PARTY-NOTICES.txt') -Force
Copy-Item -LiteralPath $PocketMicLicense -Destination (Join-Path $Publish 'PocketMic-LICENSE.txt') -Force

if ($OpusDllPath) {
    if (-not (Test-Path -LiteralPath $OpusDllPath -PathType Leaf)) {
        throw "The requested Opus DLL was not found: $OpusDllPath"
    }
    if (-not (Test-Path -LiteralPath $OpusLicense -PathType Leaf)) {
        throw "The requested Opus DLL is missing its accompanying license: $OpusLicense"
    }
    Copy-Item -LiteralPath $OpusDllPath -Destination (Join-Path $Publish 'opus.dll') -Force
    Copy-Item -LiteralPath $OpusLicense -Destination (Join-Path $Publish 'OPUS-COPYING.txt') -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Publish,
    $Archive,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

$zip = [System.IO.Compression.ZipFile]::OpenRead($Archive)
try {
    $executableEntry = $zip.GetEntry('PocketMicReceiver.exe')
    if (-not $executableEntry -or $executableEntry.Length -lt 10MB) {
        throw 'Windows package is missing the self-contained PocketMicReceiver.exe payload.'
    }
    foreach ($requiredEntry in @('README-Windows.txt', 'THIRD-PARTY-NOTICES.txt', 'PocketMic-LICENSE.txt')) {
        if (-not $zip.GetEntry($requiredEntry)) {
            throw "Windows package is missing required documentation: $requiredEntry"
        }
    }
    if ($OpusDllPath) {
        $opusEntry = $zip.GetEntry('opus.dll')
        if (-not $opusEntry -or $opusEntry.Length -lt 100000) {
            throw 'Windows package is missing the expected native Opus decoder payload.'
        }
        if (-not $zip.GetEntry('OPUS-COPYING.txt')) {
            throw 'Windows package is missing the license notice for its native Opus decoder.'
        }
    }
}
finally {
    $zip.Dispose()
}

$files = @(Get-ChildItem $Publish -Recurse -File)
$payloadMb = [math]::Round(($files | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$archiveMb = [math]::Round((Get-Item $Archive).Length / 1MB, 1)

Write-Host ''
Write-Host "Built: $Archive"
Write-Host ("  front end : WinForms")
Write-Host ("  payload   : {0} files, {1} MB" -f $files.Count, $payloadMb)
Write-Host ("  archive   : {0} MB" -f $archiveMb)
Write-Host ("  SHA-256   : {0}" -f (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant())
