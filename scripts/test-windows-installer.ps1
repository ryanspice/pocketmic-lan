# Exercise failure handling with tiny inert payloads, without changing the real install/shortcuts.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $root ('release\installer-test-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'package'
$testLocalData = Join-Path $testRoot 'local'
$testPrograms = Join-Path $testLocalData 'Programs'
$installed = Join-Path $testPrograms 'PocketMic Receiver'
New-Item -ItemType Directory -Path $source, $installed -Force | Out-Null
Set-Content -LiteralPath (Join-Path $installed 'previous-install.txt') -Value 'preserve me'
foreach ($name in @('PocketMicReceiver.exe', 'opus.dll', 'OPUS-COPYING.txt', 'PocketMic-LICENSE.txt')) {
    Set-Content -LiteralPath (Join-Path $source $name) -Value "inert test payload: $name"
}
New-Item -ItemType Directory -Path (Join-Path $source 'modern\en-US') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $source 'modern\en-US\resources.pri') -Value 'inert nested runtime payload'
Get-ChildItem -LiteralPath $source -File -Recurse | Sort-Object FullName | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(), [IO.Path]::GetRelativePath($source, $_.FullName)
} | Set-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')

$originalLocalData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = $testLocalData
    $unreadableProcess = New-Object psobject
    $unreadableProcess | Add-Member -MemberType ScriptProperty -Name Path -Value { throw 'Injected inaccessible process' }
    $simulatedProcesses = @($unreadableProcess)
    function Get-Process { param($Name, $ErrorAction) $simulatedProcesses }
    function Move-Item {
        param([string] $LiteralPath, [string] $Destination)
        if ([IO.Path]::GetDirectoryName($LiteralPath) -eq $testPrograms -and
            [IO.Path]::GetFileName($LiteralPath).StartsWith('.PocketMic-install-')) {
            if (-not (Test-Path -LiteralPath (Join-Path $LiteralPath 'modern\en-US\resources.pri'))) {
                throw 'Nested runtime was not staged.'
            }
            throw 'Injected activation failure'
        }
        Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
    }
    $failed = $false
    try { & "$PSScriptRoot\Install-PocketMic.ps1" -SourceDirectory $source -NoLaunch }
    catch {
        if ($_.Exception.Message -notmatch 'Injected activation failure') { throw }
        $failed = $true
    }
    if (-not $failed -or -not (Test-Path -LiteralPath (Join-Path $installed 'previous-install.txt'))) {
        throw 'Activation failure did not restore the previous installation.'
    }
    if (@(Get-ChildItem -LiteralPath $testPrograms -Directory -Filter '.PocketMic-install-*').Count) {
        throw 'Activation failure left a staging directory.'
    }
    Write-Host 'PASS: nested runtime is staged; an inaccessible unrelated process is ignored; failed activation restores the old install and removes staging.'

    $stubbornProcess = New-Object psobject -Property @{
        Path = (Join-Path $installed 'PocketMicReceiver.exe')
        Waits = [Collections.Generic.List[int]]::new()
        KillCalled = $false
    }
    $stubbornProcess | Add-Member -MemberType ScriptMethod -Name CloseMainWindow -Value { return $false }
    $stubbornProcess | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
        param([int] $Timeout)
        $this.Waits.Add($Timeout)
        return $false
    }
    $stubbornProcess | Add-Member -MemberType ScriptMethod -Name Kill -Value { $this.KillCalled = $true }
    $simulatedProcesses = @($stubbornProcess)
    $failed = $false
    try { & "$PSScriptRoot\Install-PocketMic.ps1" -SourceDirectory $source -NoLaunch }
    catch {
        if ($_.Exception.Message -notmatch 'could not be stopped') { throw }
        $failed = $true
    }
    if (-not $failed -or -not $stubbornProcess.KillCalled -or
        ($stubbornProcess.Waits -join ',') -ne '3000,5000' -or
        -not (Test-Path -LiteralPath (Join-Path $installed 'previous-install.txt')) -or
        @(Get-ChildItem -LiteralPath $testPrograms -Directory -Filter '.PocketMic-install-*').Count) {
        throw 'Process-stop timeout did not preserve the old install and clean staging.'
    }
    Write-Host 'PASS: a process that cannot terminate is bounded by timeouts before the existing install is moved.'

    Set-Content -LiteralPath (Join-Path $installed 'ACTIVATION-V1.txt') -Value 'test protocol marker'
    Set-Content -LiteralPath (Join-Path $installed 'PocketMicReceiver.exe') -Value 'inert installed executable'
    $gracefulProcess = New-Object psobject -Property @{
        Path = (Join-Path $installed 'modern\PocketMicReceiver.WinUI.exe')
        Waits = [Collections.Generic.List[int]]::new()
        CloseCalled = $false
    }
    $gracefulProcess | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int] $Timeout) $this.Waits.Add($Timeout); return $true }
    $gracefulProcess | Add-Member -MemberType ScriptMethod -Name CloseMainWindow -Value { $this.CloseCalled = $true; return $true }
    $simulatedProcesses = @($gracefulProcess)
    function Start-Process {
        param($FilePath, $ArgumentList, $WindowStyle, [switch] $PassThru)
        if ($ArgumentList -ne '--exit-for-update' -or $WindowStyle -ne 'Hidden') { throw 'Unexpected updater launch.' }
        $helper = New-Object psobject -Property @{ ExitCode = 0 }
        $helper | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($Timeout) return $true }
        $helper | Add-Member -MemberType ScriptMethod -Name Dispose -Value { }
        return $helper
    }
    $failed = $false
    try { & "$PSScriptRoot\Install-PocketMic.ps1" -SourceDirectory $source -NoLaunch }
    catch {
        if ($_.Exception.Message -notmatch 'Injected activation failure') { throw }
        $failed = $true
    }
    if (-not $failed -or $gracefulProcess.CloseCalled -or ($gracefulProcess.Waits -join ',') -ne '6000') {
        throw 'An acknowledged graceful exit was interrupted with a close/kill request.'
    }
    Remove-Item -LiteralPath (Join-Path $installed 'ACTIVATION-V1.txt')
    Remove-Item -LiteralPath Function:\Start-Process
    Write-Host 'PASS: acknowledged update shutdown is allowed to finish before fallback close/kill handling.'

    Add-Content -LiteralPath (Join-Path $source 'PocketMicReceiver.exe') -Value 'tampered'
    $failed = $false
    try { & "$PSScriptRoot\Install-PocketMic.ps1" -SourceDirectory $source -NoLaunch }
    catch {
        if ($_.Exception.Message -notmatch 'Package checksum failed') { throw }
        $failed = $true
    }
    if (-not $failed -or -not (Test-Path -LiteralPath (Join-Path $installed 'previous-install.txt'))) {
        throw 'A corrupt package changed the installation.'
    }
    Write-Host 'PASS: a corrupt package is rejected before changing the installation.'

    Set-Content -LiteralPath (Join-Path $source 'PocketMicReceiver.exe') -Value 'inert test payload: PocketMicReceiver.exe'
    $validManifest = Get-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')
    $hash = (Get-FileHash -LiteralPath (Join-Path $source 'PocketMicReceiver.exe')).Hash.ToLowerInvariant()
    foreach ($invalidName in @('..\escape.exe', 'modern/../escape.exe', 'C:\escape.exe', 'modern\file:stream', 'modern\\file', 'modern\file.', 'POCKETMICRECEIVER.EXE')) {
        @($validManifest) + "$hash  $invalidName" | Set-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')
        $failed = $false
        try { & "$PSScriptRoot\Install-PocketMic.ps1" -SourceDirectory $source -NoLaunch }
        catch {
            if ($_.Exception.Message -notmatch 'Invalid package filename|Duplicate package filename') { throw }
            $failed = $true
        }
        if (-not $failed -or -not (Test-Path -LiteralPath (Join-Path $installed 'previous-install.txt'))) {
            throw "Unsafe manifest path was not rejected before mutation: $invalidName"
        }
    }
    Write-Host 'PASS: traversal, absolute paths, streams, empty segments, trailing-dot aliases and duplicate entries are rejected.'
} finally {
    $env:LOCALAPPDATA = $originalLocalData
    Remove-Item -LiteralPath Function:\Move-Item -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath Function:\Get-Process -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath Function:\Start-Process -ErrorAction SilentlyContinue
}
