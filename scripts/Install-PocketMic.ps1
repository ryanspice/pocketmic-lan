param(
    [string] $SourceDirectory = $PSScriptRoot,
    [switch] $NoLaunch
)

# Per-user install/update. Keep settings and a timestamped copy of the previous install.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$source = [IO.Path]::GetFullPath($SourceDirectory)
$programs = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$destination = Join-Path $programs 'PocketMic Receiver'
$executable = Join-Path $destination 'PocketMicReceiver.exe'
$manifest = Join-Path $source 'SHA256SUMS.txt'
if ($source.TrimEnd('\') -eq $destination.TrimEnd('\')) {
    throw 'Extract the update into a different folder before installing it.'
}
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Package checksums are missing.' }
$payload = @()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath $manifest) {
    if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Invalid package checksum entry.' }
    $hash = $Matches[1]
    $name = $Matches[2]
    # Nested modern-runtime files are allowed; absolute paths, traversal, streams and aliases are not.
    $segments = $name -split '[/\\]'
    if ($segments.Where({ $_ -in @('', '.', '..') -or $_ -match '[<>:"|?*\x00-\x1F]' -or $_ -match '[. ]$' }).Count) {
        throw 'Invalid package filename.'
    }
    $name = $segments -join [IO.Path]::DirectorySeparatorChar
    if (-not $seen.Add($name)) { throw 'Duplicate package filename.' }
    $path = [IO.Path]::GetFullPath((Join-Path $source $name))
    if (-not $path.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package path.' }
    $ancestor = $path
    while ($ancestor.Length -gt $source.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Package files must not traverse links.'
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) {
        throw "Package checksum failed: $name"
    }
    $payload += $name
}
foreach ($required in @('PocketMicReceiver.exe', 'opus.dll', 'OPUS-COPYING.txt', 'PocketMic-LICENSE.txt')) {
    if ($required -notin $payload) { throw "Package is missing $required" }
}

New-Item -ItemType Directory -Path $programs -Force | Out-Null
if (Test-Path -LiteralPath $destination) {
    if ((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'The installation directory is a link; install into an ordinary folder.'
    }
}
$stage = Join-Path $programs ('.PocketMic-install-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
foreach ($name in $payload) {
    $stagedFile = Join-Path $stage $name
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($stagedFile)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $stagedFile
}
Copy-Item -LiteralPath $manifest -Destination (Join-Path $stage 'SHA256SUMS.txt')

# Only stop the copy being updated. Do not touch a developer's separate receiver process.
$gracefulExitRequested = $false
if ((Test-Path -LiteralPath (Join-Path $destination 'ACTIVATION-V1.txt')) -and
    (Test-Path -LiteralPath $executable)) {
    # New receivers hide on X. The explicit update command lets them stop audio and restore
    # owned recording defaults before exit; legacy builds retain the bounded fallback below.
    $exitRequest = Start-Process -FilePath $executable -ArgumentList '--exit-for-update' -WindowStyle Hidden -PassThru
    try {
        if (-not $exitRequest.WaitForExit(3000)) { $exitRequest.Kill() }
        else { $gracefulExitRequested = $exitRequest.ExitCode -eq 0 }
    } finally { $exitRequest.Dispose() }
}
foreach ($process in @(Get-Process PocketMicReceiver,PocketMicReceiver.WinUI -ErrorAction SilentlyContinue)) {
    # An unrelated same-named process may be owned by another user or exit during enumeration.
    try { $processPath = $process.Path } catch { continue }
    if ($processPath -eq $executable -or $processPath -eq (Join-Path $destination 'modern\PocketMicReceiver.WinUI.exe')) {
        if ($gracefulExitRequested -and $process.WaitForExit(6000)) { continue }
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(3000)) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) {
                throw 'The running PocketMic receiver could not be stopped. The existing installation has not been changed.'
            }
        }
    }
}
$backup = $null
if (Test-Path -LiteralPath $destination) {
    $backup = "$destination.backup-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
    # Both absolute targets are fixed children of the per-user Programs directory.
    if ([IO.Path]::GetDirectoryName($destination) -ne $programs -or
        [IO.Path]::GetDirectoryName($backup) -ne $programs) { throw 'Unsafe install target.' }
    Move-Item -LiteralPath $destination -Destination $backup
}
try {
    if ([IO.Path]::GetDirectoryName($stage) -ne $programs) { throw 'Unsafe staging target.' }
    Move-Item -LiteralPath $stage -Destination $destination
} catch {
    if ($backup -and -not (Test-Path -LiteralPath $destination)) {
        Move-Item -LiteralPath $backup -Destination $destination
    }
    throw
}
} finally {
    # Cleanup is limited to the exact staging directory created by this invocation.
    if ((Test-Path -LiteralPath $stage) -and
        [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($stage)) -eq $programs) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}

try {
$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'PocketMic Receiver.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.Arguments = '--show'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Description = 'PocketMic LAN receiver'
    $shortcut.IconLocation = "$executable,0"
    $shortcut.Save()
}
} catch {
    Write-Warning "PocketMic is installed, but a shortcut could not be created. Open $executable directly. $($_.Exception.Message)"
}
Write-Host "Installed PocketMic $((Get-Item -LiteralPath $executable).VersionInfo.ProductVersion)"
Write-Host "Location: $destination"
if ($backup) { Write-Host "Previous version preserved: $backup" }
Write-Host 'Pairing and audio settings have been preserved.'
if (-not $NoLaunch) { Start-Process -FilePath $executable -ArgumentList '--show' }
