param(
    # One below the ceiling: the control channel occupies $Port + 1 and must also be a valid port.
    [ValidateRange(1, 65534)]
    [int] $Port = 49500
)

# Run in an elevated PowerShell window only if Windows Firewall did not prompt automatically.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Rule = 'PocketMic UDP Receiver'

# Two ports, one rule. The audio stream lands on $Port; the control channel — discovery,
# receiver statistics, and DSP config push — always sits one port above it. Opening only the
# audio port leaves the phone unable to find the PC at all, which reads as "nothing works".
$ControlPort = $Port + 1
$wanted = @($Port, $ControlPort) | ForEach-Object { $_.ToString() }
$wantedSet = ($wanted | Sort-Object) -join ','

$existing = Get-NetFirewallRule -DisplayName $Rule -ErrorAction SilentlyContinue
if ($existing) {
    $currentPorts = @($existing | Get-NetFirewallPortFilter | Select-Object -ExpandProperty LocalPort)
    $currentSet = ($currentPorts | Sort-Object) -join ','
    if ($currentSet -eq $wantedSet) {
        Write-Host "Firewall rule already allows UDP $Port and $ControlPort on private networks."
        exit 0
    }

    # An older rule opened the audio port alone. Replace it rather than adding a second rule,
    # so repeated runs converge on exactly one rule with exactly the ports this version needs.
    $existing | Remove-NetFirewallRule
}

New-NetFirewallRule `
    -DisplayName $Rule `
    -Direction Inbound `
    -Action Allow `
    -Protocol UDP `
    -LocalPort $wanted `
    -Profile Private | Out-Null

Write-Host "Allowed inbound UDP $Port (audio) and $ControlPort (control) on private networks."
