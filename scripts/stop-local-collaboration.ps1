param([ValidateRange(1024,65535)][int]$Port)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$instance=Join-Path $repo 'artifacts/local-collaboration/instance.json'
if($Port){
    $selected=Join-Path $repo "artifacts/local-collaboration/instances/$Port/instance.json"
    # Older launchers wrote only the latest-instance record.
    if(Test-Path -LiteralPath $selected){$instance=$selected}
    elseif(!(Test-Path -LiteralPath $instance) -or ([Uri](Get-Content -LiteralPath $instance -Raw|ConvertFrom-Json).url).Port -ne $Port){Write-Output "No owned local test instance recorded on port $Port.";return}
}
if(!(Test-Path -LiteralPath $instance)){Write-Output 'No owned local test instance recorded.';exit}
$record=Get-Content -LiteralPath $instance -Raw|ConvertFrom-Json
$origin=[Uri]$record.url
if(!$origin.IsAbsoluteUri -or !$origin.IsLoopback -or $origin.Host -ne 'localhost' -or $origin.Scheme -ne 'http' -or $origin.Port -lt 1024 -or $origin.UserInfo -ne '' -or ($Port -and $origin.Port -ne $Port)){throw 'Invalid local test origin in the process record; refusing to stop it.'}
$ownedId=[int]$record.pid
$process=Get-Process -Id $ownedId -ErrorAction SilentlyContinue
if(!$process){Write-Output 'The recorded local test instance is already stopped.';exit}
$details=Get-CimInstance Win32_Process -Filter "ProcessId=$ownedId"
$started=[DateTimeOffset]$record.startedAtUtc
$urlPattern='(?:^|\s)"?'+[Regex]::Escape($origin.GetLeftPart([UriPartial]::Authority))+'"?(?=\s|$)'
if($details.Name -ne 'dotnet.exe' -or $details.CommandLine -notmatch 'BlazorApp.csproj' -or $details.CommandLine -notmatch $urlPattern -or [Math]::Abs(($process.StartTime.ToUniversalTime()-$started.UtcDateTime).TotalSeconds) -gt 5){throw 'The recorded PID no longer matches the owned test launcher; refusing to stop it.'}
[Diagnostics.Process]::GetProcessById($ownedId).Kill($true)
Write-Output 'Stopped the owned local Prosa test process and its collaboration service. The test database is retained.'
