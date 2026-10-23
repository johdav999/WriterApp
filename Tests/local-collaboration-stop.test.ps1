$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -LiteralPath (Join-Path $repo 'scripts/stop-local-collaboration.ps1') -Raw
$fixtureRoot=Join-Path $repo ('artifacts/launcher-stop-tests/'+[Guid]::NewGuid().ToString('N'))
$global:LocalCollaborationStopFixture=[pscustomobject]@{Command='';Name='dotnet.exe';Started=[DateTime]::UtcNow}
function Get-Process {param($Id,$ErrorAction) [pscustomobject]@{StartTime=$global:LocalCollaborationStopFixture.Started}}
function Get-CimInstance {param($ClassName,$Filter) [pscustomobject]@{Name=$global:LocalCollaborationStopFixture.Name;CommandLine=$global:LocalCollaborationStopFixture.Command}}
foreach($scenario in @('different-port','port-prefix','different-process','different-start-time','foreign-origin')){
    $caseRoot=Join-Path $fixtureRoot $scenario
    $scripts=Join-Path $caseRoot 'scripts';$state=Join-Path $caseRoot 'artifacts/local-collaboration/instances/5399'
    [IO.Directory]::CreateDirectory($scripts)|Out-Null;[IO.Directory]::CreateDirectory($state)|Out-Null
    $stop=Join-Path $scripts 'stop-local-collaboration.ps1';[IO.File]::WriteAllText($stop,$source)
    $global:LocalCollaborationStopFixture.Started=[DateTime]::UtcNow
    $global:LocalCollaborationStopFixture.Name=if($scenario -eq 'different-process'){'other.exe'}else{'dotnet.exe'}
    $commandPort=switch($scenario){'different-port'{5398};'port-prefix'{53990};default{5399}}
    $global:LocalCollaborationStopFixture.Command="dotnet run --project BlazorApp.csproj --urls http://localhost:$commandPort"
    $record=[ordered]@{pid=[int]::MaxValue;url=if($scenario -eq 'foreign-origin'){'http://example.test:5399/collaboration/test'}else{'http://localhost:5399/collaboration/test'};startedAtUtc=$global:LocalCollaborationStopFixture.Started.ToString('o')}
    if($scenario -eq 'different-start-time'){$record.startedAtUtc=$global:LocalCollaborationStopFixture.Started.AddMinutes(-1).ToString('o')}
    $record|ConvertTo-Json|Set-Content (Join-Path $state 'instance.json')
    $failure=$null
    try{& $stop -Port 5399|Out-Null}catch{$failure=$_.Exception.Message}
    if($failure -notlike '*refusing to stop it.'){throw "Stop guard failed for ${scenario}: $failure"}
    Write-Output "PASS: stop refuses $scenario"
}
