# Bind rather than connect: this also detects reserved ports and IPv6 listeners.
function Test-LocalCollaborationPort {
    param([ValidateRange(1,65535)][int]$Port)
    $listeners=[Collections.Generic.List[Net.Sockets.TcpListener]]::new()
    try {
        $addresses=@([Net.IPAddress]::Loopback)
        if([Net.Sockets.Socket]::OSSupportsIPv6){$addresses+=,[Net.IPAddress]::IPv6Loopback}
        foreach($address in $addresses){
            $listener=[Net.Sockets.TcpListener]::new($address,$Port)
            $listeners.Add($listener)
            $listener.Server.ExclusiveAddressUse=$true
            if($address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetworkV6){$listener.Server.DualMode=$false}
            $listener.Start()
        }
        return $true
    } catch [Net.Sockets.SocketException] {
        if($_.Exception.SocketErrorCode -in @([Net.Sockets.SocketError]::AddressAlreadyInUse,[Net.Sockets.SocketError]::AccessDenied)){return $false}
        throw
    } finally {foreach($listener in $listeners){$listener.Stop()}}
}

function Get-AvailableLocalCollaborationPort {
    param([ValidateRange(1024,65535)][int]$PreferredPort,[int[]]$Exclude=@())
    for($offset=0;$offset -lt (65536-1024);$offset++){
        $candidate=1024+(($PreferredPort-1024+$offset) % (65536-1024))
        if($candidate -notin $Exclude -and (Test-LocalCollaborationPort $candidate)){return $candidate}
    }
    throw 'No available local collaboration port was found between 1024 and 65535.'
}
