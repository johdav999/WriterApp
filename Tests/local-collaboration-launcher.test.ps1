$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -LiteralPath (Join-Path $repo 'scripts/start-local-collaboration.ps1') -Raw
# These fixtures test orchestration, not real listeners or child processes.
# The actual launch/occupied-port checks are exercised separately on localhost.
$portHelper=". (Join-Path `$PSScriptRoot 'local-collaboration-ports.ps1')"
if(!$source.Contains($portHelper)){throw 'Update the fixture port-helper replacement to match the launcher.'}
$source=$source.Replace($portHelper,@'
function Get-AvailableLocalCollaborationPort {
    param($PreferredPort,$Exclude)
    if($PreferredPort -eq 5398){return 5399}
    if($PreferredPort -eq 1234){return 1235}
    return $PreferredPort
}
'@)
$fixtureRoot=Join-Path $repo ('artifacts/launcher-tests/'+[Guid]::NewGuid().ToString('N'))
$global:LocalCollaborationLauncherFixture=[pscustomobject]@{Commands=[Collections.Generic.List[object]]::new();Launch=$null;Scenario="";CaseRoot=""}

function Assert-Launcher($condition,$message){if(!$condition){throw $message}}
function dotnet {
    $values=@($args)
    $global:LocalCollaborationLauncherFixture.commands.Add([pscustomobject]@{Tool='dotnet';Arguments=$values})
    $global:LASTEXITCODE=0
    if($global:LocalCollaborationLauncherFixture.scenario -eq 'restore-fails'){$global:LASTEXITCODE=17;return}
    if($values[0] -eq 'build'){
        $output=($values|Where-Object {$_ -like '-p:BaseOutputPath=*'}).Substring('-p:BaseOutputPath='.Length)
        $manifest=Join-Path $global:LocalCollaborationLauncherFixture.caseRoot ($output+'Debug/net10.0/BlazorApp.staticwebassets.runtime.json')
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($manifest))|Out-Null
        [IO.File]::WriteAllText($manifest,'{"ContentRoots":[]}')
    }
}
function npm {
    $global:LocalCollaborationLauncherFixture.commands.Add([pscustomobject]@{Tool='npm';Arguments=@($args)})
    $global:LASTEXITCODE=0
}
function Start-Process {
    param($FilePath,$ArgumentList,$WorkingDirectory,$WindowStyle,[switch]$PassThru,$RedirectStandardOutput,$RedirectStandardError)
    $global:LocalCollaborationLauncherFixture.launch=[pscustomobject]@{Arguments=$ArgumentList;WorkingDirectory=$WorkingDirectory;WindowStyle=$WindowStyle;Environment=$env:ASPNETCORE_ENVIRONMENT;Port=$env:Collaboration__LocalTest__Port;WorkerPort=$env:Collaboration__LocalTest__CollaborationPort;Log=$RedirectStandardOutput}
    [pscustomobject]@{Id=12345}
}
foreach($scenario in @('restore-fails','missing-skip-build','success')){
    $global:LocalCollaborationLauncherFixture.Scenario=$scenario
    $global:LocalCollaborationLauncherFixture.commands.Clear();$global:LocalCollaborationLauncherFixture.launch=$null
    $global:LocalCollaborationLauncherFixture.caseRoot=Join-Path $fixtureRoot $global:LocalCollaborationLauncherFixture.scenario
    $scripts=Join-Path $global:LocalCollaborationLauncherFixture.caseRoot 'scripts'
    [IO.Directory]::CreateDirectory($scripts)|Out-Null
    $launcher=Join-Path $scripts 'start-local-collaboration.ps1'
    [IO.File]::WriteAllText($launcher,$source)
    $beforeEnvironment=$env:ASPNETCORE_ENVIRONMENT
    $beforePort=$env:Collaboration__LocalTest__Port
    $beforeWorkerPort=$env:Collaboration__LocalTest__CollaborationPort
    if($scenario -eq 'success'){
        $previousFolder=Join-Path $global:LocalCollaborationLauncherFixture.caseRoot 'artifacts/local-collaboration'
        [IO.Directory]::CreateDirectory($previousFolder)|Out-Null
        [IO.File]::WriteAllText((Join-Path $previousFolder 'instance.json'),'{"pid":6789,"url":"http://localhost:5398/collaboration/test"}')
    }
    $failure=$null
    try {if($global:LocalCollaborationLauncherFixture.scenario -eq 'missing-skip-build'){& $launcher -SkipBuild|Out-Null}else{& $launcher|Out-Null}}
    catch{$failure=$_.Exception.Message}
    if($global:LocalCollaborationLauncherFixture.scenario -eq 'restore-fails'){
        Assert-Launcher ($failure -like 'Application restore failed.*') "Restore failure must give a recovery message. Actual: $failure"
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.commands.Count -eq 1 -and $global:LocalCollaborationLauncherFixture.commands[0].Arguments[0] -eq 'restore') 'A failed restore must stop before editor builds and launch.'
        Assert-Launcher ($null -eq $global:LocalCollaborationLauncherFixture.launch) 'A failed restore must not launch a host.'
    }elseif($global:LocalCollaborationLauncherFixture.scenario -eq 'missing-skip-build'){
        Assert-Launcher ($failure -like 'No local collaboration build exists*') 'SkipBuild must reject a missing account-scoped build.'
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.commands.Count -eq 0 -and $null -eq $global:LocalCollaborationLauncherFixture.launch) 'Missing SkipBuild must not restore, build or launch.'
    }else{
        Assert-Launcher ($null -eq $failure) "Successful launcher failed: $failure"
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.commands[0].Arguments[0] -eq 'restore' -and $global:LocalCollaborationLauncherFixture.commands[0].Arguments -contains '--force') 'Restore must run first and reevaluate cache paths.'
        $build=$global:LocalCollaborationLauncherFixture.commands|Where-Object {$_.Tool -eq 'dotnet' -and $_.Arguments[0] -eq 'build'}
        foreach($property in ($build.Arguments|Where-Object {$_ -like '-p:Base*Path=*'})){
            Assert-Launcher ($global:LocalCollaborationLauncherFixture.commands[0].Arguments -contains $property -and $global:LocalCollaborationLauncherFixture.launch.Arguments -contains $property) 'Restore, build and run must use the same output/restore directories.'
        }
        Assert-Launcher (($build.Arguments|Where-Object {$_ -like '-p:BaseIntermediateOutputPath=artifacts/local-collaboration-build/*/obj/'}).Count -eq 1) 'Intermediate output must be isolated from shared obj files.'
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.launch.WindowStyle -eq 'Hidden' -and $global:LocalCollaborationLauncherFixture.launch.Environment -eq 'Staging') 'Launch must remain hidden and use local Staging.'
        Assert-Launcher ([string]$env:ASPNETCORE_ENVIRONMENT -eq [string]$beforeEnvironment) 'Launcher must restore the parent environment.'
        Assert-Launcher ([string]$env:Collaboration__LocalTest__Port -eq [string]$beforePort -and [string]$env:Collaboration__LocalTest__CollaborationPort -eq [string]$beforeWorkerPort) 'Launcher must restore the parent port configuration.'
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.launch.Arguments -contains 'http://localhost:5399' -and $global:LocalCollaborationLauncherFixture.launch.Port -eq '5399' -and $global:LocalCollaborationLauncherFixture.launch.WorkerPort -eq '1235') 'Selected ports must reach Kestrel and local profile configuration.'
        Assert-Launcher ($global:LocalCollaborationLauncherFixture.launch.Log -like '*instances*5399*server.log') 'Separate instances must have separate logs.'
        $state=Join-Path $global:LocalCollaborationLauncherFixture.caseRoot 'artifacts/local-collaboration'
        $previous=Get-Content (Join-Path $state 'instances/5398/instance.json') -Raw|ConvertFrom-Json
        $latest=Get-Content (Join-Path $state 'instance.json') -Raw|ConvertFrom-Json
        Assert-Launcher ($previous.pid -eq 6789 -and $latest.pid -eq 12345 -and $latest.collaborationPort -eq 1235 -and $latest.url -eq 'http://localhost:5399/collaboration/test') 'Launching a second instance must retain the first process record and record selected ports.'
    }
    Write-Output ('PASS: '+$scenario)
}
