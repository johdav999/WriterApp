$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../scripts/local-collaboration-ports.ps1')
function Assert-Port($condition,$message){if(!$condition){throw $message}}

$occupied=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$occupied.Server.ExclusiveAddressUse=$true
try {
    $occupied.Start()
    $port=$occupied.LocalEndpoint.Port
    Assert-Port (!(Test-LocalCollaborationPort $port)) 'An occupied IPv4 port must be unavailable.'
    $available=Get-AvailableLocalCollaborationPort -PreferredPort $port
    Assert-Port ($available -ne $port -and (Test-LocalCollaborationPort $available)) 'An occupied preferred port must select a free fallback.'
    Assert-Port ((Get-AvailableLocalCollaborationPort -PreferredPort $available) -eq $available) 'A free preferred port must be retained.'
    $worker=Get-AvailableLocalCollaborationPort -PreferredPort $available -Exclude @($available)
    Assert-Port ($worker -ne $available -and (Test-LocalCollaborationPort $worker)) 'App and worker ports must be distinct even when preferences match.'
    Write-Output 'PASS: IPv4 occupancy, fallback, free preference and distinct worker'
}finally{$occupied.Stop()}
if([Net.Sockets.Socket]::OSSupportsIPv6){
    $occupiedV6=[Net.Sockets.TcpListener]::new([Net.IPAddress]::IPv6Loopback,0)
    $occupiedV6.Server.DualMode=$false
    try {
        $occupiedV6.Start()
        Assert-Port (!(Test-LocalCollaborationPort $occupiedV6.LocalEndpoint.Port)) 'An IPv6-only listener must also block a localhost port.'
        Write-Output 'PASS: IPv6-only occupancy'
    }finally{$occupiedV6.Stop()}
}
