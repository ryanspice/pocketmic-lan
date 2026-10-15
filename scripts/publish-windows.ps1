param(
    [string] $OutputDirectory,
    [string] $ArchivePath,
    [string] $OpusDllPath,
    [switch] $ClassicOnly
)

<#
    Builds the self-contained modern and compatibility receiver ZIP. Set -OpusDllPath to include the pinned
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
$ModernProject = Join-Path $Root 'windows-receiver-winui\PocketMicReceiver.WinUI.csproj'
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

# Publishing clears generated output. Restrict cleanup to known build roots, never the
# repository root, an installed app, or an arbitrary caller-supplied directory.
$allowedRoots = @((Join-Path $Root 'release'), (Join-Path $Root 'windows-receiver\bin'))
foreach ($target in @($Publish, $Archive)) {
    if (-not ($allowedRoots | Where-Object {
        $target.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
    })) { throw "Publish target is outside the generated build directories: $target" }
    $ancestor = $target
    while ($ancestor -and $ancestor.Length -gt $Root.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Publish target traverses a link: $ancestor"
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}

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

if (Test-Path -LiteralPath $Publish) { Remove-Item -LiteralPath $Publish -Recurse -Force }
if (Test-Path -LiteralPath $Archive) { Remove-Item -LiteralPath $Archive -Force }
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

if (-not $ClassicOnly) {
    & $dotnet.Source publish $ModernProject -c Release -r win-x64 --self-contained true --nologo `
        -p:Platform=x64 -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $Publish 'modern')
    if ($LASTEXITCODE -ne 0) { throw "Modern receiver publish exited with code $LASTEXITCODE." }
    foreach ($modernFile in @('PocketMicReceiver.WinUI.exe', 'PocketMicReceiver.WinUI.pri', 'App.xbf', 'WINDOWS-APP-SDK-LICENSE.txt', 'WINDOWS-APP-SDK-NOTICE.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Publish "modern\$modernFile") -PathType Leaf)) {
            throw "The modern receiver payload is missing $modernFile."
        }
    }
    # Preserve the notices from the exact self-contained runtime packs selected by restore.
    $assets = Get-Content -LiteralPath (Join-Path $Root 'windows-receiver-winui\obj\project.assets.json') -Raw | ConvertFrom-Json
    $runtime = Get-Content -LiteralPath (Join-Path $Publish 'modern\PocketMicReceiver.WinUI.runtimeconfig.json') -Raw | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $packName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
        $pack = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object {
            Join-Path $_ "$packName\$($framework.version)"
        } | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        if (-not $pack) { throw "Runtime license source is missing: $packName $($framework.version)" }
        $notices = @(Get-ChildItem -LiteralPath $pack -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if (-not $notices.Count) { throw "Runtime license is missing: $packName" }
        foreach ($notice in $notices) {
            Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $Publish "modern\$($framework.name)-$($notice.Name)")
        }
    }
}
Copy-Item -LiteralPath (Join-Path $Root 'windows-receiver\Assets\PocketMic.ico') -Destination $Publish
'SHOW and graceful EXIT over the current-user activation pipe.' | Set-Content -LiteralPath (Join-Path $Publish 'ACTIVATION-V1.txt') -Encoding ascii

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
    if (-not $ClassicOnly) {
        Copy-Item -LiteralPath $OpusDllPath -Destination (Join-Path $Publish 'modern\opus.dll') -Force
    }
    Copy-Item -LiteralPath $OpusLicense -Destination (Join-Path $Publish 'OPUS-COPYING.txt') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-PocketMic.ps1') -Destination $Publish
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-PocketMic.cmd') -Destination $Publish
}

$checksums = Get-ChildItem -LiteralPath $Publish -Recurse -File | Sort-Object FullName | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetRelativePath($Publish, $_.FullName)
}
$checksums | Set-Content -LiteralPath (Join-Path $Publish 'SHA256SUMS.txt') -Encoding ascii

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
    if (-not $ClassicOnly) {
        foreach ($modernFile in @('PocketMicReceiver.WinUI.exe', 'PocketMicReceiver.WinUI.pri', 'App.xbf', 'WINDOWS-APP-SDK-LICENSE.txt', 'WINDOWS-APP-SDK-NOTICE.txt')) {
            if (-not $zip.GetEntry("modern/$modernFile")) { throw "Windows package is missing modern/$modernFile." }
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
Write-Host ("  front end : " + $(if ($ClassicOnly) { 'WinForms compatibility' } else { 'WinUI 3 Mica + WinForms compatibility' }))
Write-Host ("  payload   : {0} files, {1} MB" -f $files.Count, $payloadMb)
Write-Host ("  archive   : {0} MB" -f $archiveMb)
Write-Host ("  SHA-256   : {0}" -f (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant())
