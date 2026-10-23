param([switch]$SkipBuild,[ValidateRange(1024,65535)][int]$Port=5398,[ValidateRange(1024,65535)][int]$CollaborationPort=1234)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'local-collaboration-ports.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$folder=Join-Path $repo 'artifacts/local-collaboration'
$appPort=Get-AvailableLocalCollaborationPort -PreferredPort $Port
$workerPort=Get-AvailableLocalCollaborationPort -PreferredPort $CollaborationPort -Exclude @($appPort)
if($appPort -ne $Port){Write-Output "App port $Port is busy; using $appPort."}
if($workerPort -ne $CollaborationPort){Write-Output "Collaboration port $CollaborationPort is busy; using $workerPort."}
# Restore assets contain absolute package-cache paths. Keep this launcher away
# from another Windows account's obj files and from the ordinary developer host.
$profilePath=[Environment]::GetFolderPath('UserProfile')
$buildHasher=[Security.Cryptography.SHA256]::Create()
try{$buildKey=([BitConverter]::ToString($buildHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($profilePath)))).Replace('-','').Substring(0,12).ToLowerInvariant()}
finally{$buildHasher.Dispose()}
# Separate outputs also let another instance build while the previous host holds its DLLs.
$buildRoot="artifacts/local-collaboration-build/$buildKey/$appPort"
$buildArguments=@("-p:BaseOutputPath=$buildRoot/bin/","-p:BaseIntermediateOutputPath=$buildRoot/obj/")
[IO.Directory]::CreateDirectory($folder)|Out-Null
Push-Location $repo
try {
    if(!$SkipBuild){
        # Force reevaluation for the active SDK/cache, including the client's
        # browser-wasm runtime pack. --no-restore is safe only after this succeeds.
        & dotnet restore BlazorApp.csproj --force @buildArguments
        if($LASTEXITCODE-ne 0){throw 'Application restore failed. Check NuGet connectivity/package availability, then rerun this script without -SkipBuild.'}
        & npm run check:collaboration --prefix WriterApp.Client
        if($LASTEXITCODE-ne 0){throw 'TypeScript check failed'}
        & npm run build --prefix WriterApp.Client
        if($LASTEXITCODE-ne 0){throw 'Editor build failed'}
        & npm run build:schema --prefix WriterApp.Collaboration
        if($LASTEXITCODE-ne 0){throw 'Schema build failed'}
        & dotnet build BlazorApp.csproj --no-restore @buildArguments
        if($LASTEXITCODE-ne 0){throw 'Application build failed'}
    }
    # A sandbox build can record a NuGet cache belonging to its build user. Resolve
    # those package roots for the account running the local host, without changing build output.
    $manifestPath=Join-Path $repo "$buildRoot/bin/Debug/net10.0/BlazorApp.staticwebassets.runtime.json"
    if(!(Test-Path -LiteralPath $manifestPath)){throw "No local collaboration build exists for this Windows account on port $appPort. Run this script without -SkipBuild first."}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    for($i=0;$i-lt $manifest.ContentRoots.Count;$i++){
        $assetRoot=$manifest.ContentRoots[$i]
        if(!(Test-Path -LiteralPath $assetRoot -ErrorAction SilentlyContinue)-and $assetRoot-match '\\.nuget\\packages\\(.+)$'){
            $packageRoot=Join-Path ([Environment]::GetFolderPath('UserProfile')) ('.nuget/packages/'+$Matches[1])
            if(Test-Path -LiteralPath $packageRoot){$manifest.ContentRoots[$i]=$packageRoot}
        }
    }
    # A build can take time; check again in case another application claimed a port.
    $appPort=Get-AvailableLocalCollaborationPort -PreferredPort $appPort
    $workerPort=Get-AvailableLocalCollaborationPort -PreferredPort $workerPort -Exclude @($appPort)
    $origin="http://localhost:$appPort"
    $instanceFolder=Join-Path $folder "instances/$appPort"
    [IO.Directory]::CreateDirectory($instanceFolder)|Out-Null
    $localManifest=Join-Path $instanceFolder 'staticwebassets.runtime.json'
    $manifest|ConvertTo-Json -Depth 100 -Compress|Set-Content -LiteralPath $localManifest -Encoding utf8
    # Preserve the older launcher's record before updating the latest-instance shortcut.
    $latest=Join-Path $folder 'instance.json'
    if(Test-Path -LiteralPath $latest){
        $previous=Get-Content -LiteralPath $latest -Raw|ConvertFrom-Json
        $previousUri=[Uri]$previous.url
        if($previousUri.IsLoopback -and $previousUri.Host -eq 'localhost' -and $previousUri.Scheme -eq 'http'){
            $previousFolder=Join-Path $folder "instances/$($previousUri.Port)"
            [IO.Directory]::CreateDirectory($previousFolder)|Out-Null
            Copy-Item -LiteralPath $latest -Destination (Join-Path $previousFolder 'instance.json')
        }
    }
    $saved=@{}
    foreach($name in @('ASPNETCORE_ENVIRONMENT','ASPNETCORE_STATICWEBASSETS','Collaboration__LocalTest__Enabled','Collaboration__LocalTest__Port','Collaboration__LocalTest__CollaborationPort')){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
    try{
        $env:ASPNETCORE_ENVIRONMENT='Staging';$env:Collaboration__LocalTest__Enabled='true'
        $env:ASPNETCORE_STATICWEBASSETS=$localManifest
        $env:Collaboration__LocalTest__Port=[string]$appPort
        $env:Collaboration__LocalTest__CollaborationPort=[string]$workerPort
        $launchArguments=@('run','--project','BlazorApp.csproj','--no-build','--no-launch-profile')+$buildArguments+@('--urls',$origin)
        $process=Start-Process dotnet -ArgumentList $launchArguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $instanceFolder 'server.log') -RedirectStandardError (Join-Path $instanceFolder 'server-error.log')
        $record=[ordered]@{pid=$process.Id;url="$origin/collaboration/test";collaborationPort=$workerPort;startedAtUtc=[DateTime]::UtcNow.ToString('o');database=(Join-Path $folder 'writerapp.db')}
        $record|ConvertTo-Json|Set-Content (Join-Path $instanceFolder 'instance.json') -Encoding utf8
        $record|ConvertTo-Json|Set-Content $latest -Encoding utf8
    }finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name])}}
    Write-Output "Local Prosa: $origin/app/collaboration (diagnostics: /collaboration/test)"
    Write-Output "Collaboration worker: http://127.0.0.1:$workerPort. Both browsers must use $origin."
    Write-Output "Owned launcher PID: $($process.Id). Logs: $instanceFolder. Retained database: $folder/writerapp.db"
    Write-Output "Stop this instance: ./scripts/stop-local-collaboration.ps1 -Port $appPort"
}finally{Pop-Location}
