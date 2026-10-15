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
Get-ChildItem -LiteralPath $source -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $_.Name
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
    Write-Host 'PASS: an inaccessible unrelated process is ignored; failed activation restores the old install and removes staging.'

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
} finally {
    $env:LOCALAPPDATA = $originalLocalData
    Remove-Item -LiteralPath Function:\Move-Item -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath Function:\Get-Process -ErrorAction SilentlyContinue
}
