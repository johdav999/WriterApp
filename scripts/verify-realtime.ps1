param(
    [string]$SqlServerInstance,
    [switch]$IncludeBrowser,
    [switch]$IncludeLoad,
    [switch]$UseExistingBuild,
    [string]$PlaywrightModule
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidenceRoot = Join-Path $repoRoot ('artifacts/realtime-acceptance/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$steps = [Collections.Generic.List[object]]::new()
$savedEnvironment = @{}
foreach ($key in @('WRITERAPP_SYNC_SQLSERVER_TEST_SERVER','WRITERAPP_COLLAB_BROWSER_TESTS','WRITERAPP_COLLAB_LOAD_TESTS','PLAYWRIGHT_MODULE','EDITOR_EVIDENCE_DIR')) { $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key) }
$scopeFilter = 'FullyQualifiedName~Collaboration|FullyQualifiedName~DocumentSync|FullyQualifiedName~DeviceSyncEngine|FullyQualifiedName~MultiDocumentSync|FullyQualifiedName~SectionsImportEndpoint|FullyQualifiedName~BrainstormingKeep|FullyQualifiedName~WebTranslationPersistence|FullyQualifiedName~WebAiHistory|FullyQualifiedName~LocalEditorSession|FullyQualifiedName~ExportEndpoint|FullyQualifiedName~SearchIndex|FullyQualifiedName~SearchEndpoint|FullyQualifiedName~DeviceAuthentication|FullyQualifiedName~DeviceAnnotation'
$failed = $false
function Run-Step([string]$Name, [string]$Executable, [string[]]$StepArguments) {
    $log = Join-Path $evidenceRoot ($Name + '.log')
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & $Executable @StepArguments *> $log
    $stepExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name=$Name; exitCode=$stepExit; elapsedMs=$clock.ElapsedMilliseconds; log=$log })
    if ($stepExit -ne 0) { throw "Acceptance step '$Name' failed; inspect $log" }
    Write-Output "PASS $Name"
}
Push-Location $repoRoot
try {
    $env:WRITERAPP_COLLAB_BROWSER_TESTS = '0'; $env:WRITERAPP_COLLAB_LOAD_TESTS = '0'
    Remove-Item Env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER -ErrorAction SilentlyContinue
    if ($PlaywrightModule) { $env:PLAYWRIGHT_MODULE = $PlaywrightModule }
    if (!$UseExistingBuild) {
        Run-Step 'typescript' 'npm' @('run','check:collaboration','--prefix','WriterApp.Client')
        Run-Step 'editor-builds' 'npm' @('run','build','--prefix','WriterApp.Client')
        Run-Step 'shared-schema' 'npm' @('run','build:schema','--prefix','WriterApp.Collaboration')
    }
    Run-Step 'node' 'npm' @('test','--prefix','WriterApp.Collaboration')
    $testArguments = @('test','Tests/WriterApp.Tests/WriterApp.Tests.csproj','--no-restore','--filter',$scopeFilter,'--logger','console;verbosity=detailed')
    if ($UseExistingBuild) { $testArguments += '--no-build' }
    Run-Step 'sqlite' 'dotnet' $testArguments
    if ($SqlServerInstance) {
        $env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER = $SqlServerInstance
        Run-Step 'sqlserver' 'dotnet' @('test','Tests/WriterApp.Tests/WriterApp.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~Collaboration|FullyQualifiedName~DocumentSync','--logger','console;verbosity=detailed')
    }
    foreach ($provider in @('sqlite','sqlserver')) {
        if ($provider -eq 'sqlserver' -and !$SqlServerInstance) { continue }
        if ($provider -eq 'sqlite') { Remove-Item Env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER -ErrorAction SilentlyContinue } else { $env:WRITERAPP_SYNC_SQLSERVER_TEST_SERVER=$SqlServerInstance }
        if ($IncludeBrowser) {
            $env:WRITERAPP_COLLAB_BROWSER_TESTS='1'
            Run-Step ($provider+'-browser') 'dotnet' @('test','Tests/WriterApp.Tests/WriterApp.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~CollaborationSessionTests.ActualWebAndDevice','--logger','console;verbosity=detailed')
            $env:WRITERAPP_COLLAB_BROWSER_TESTS='0'
        }
        if ($IncludeLoad) {
            $env:WRITERAPP_COLLAB_LOAD_TESTS='1'
            Run-Step ($provider+'-load') 'dotnet' @('test','Tests/WriterApp.Tests/WriterApp.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~MeasuredHundredRooms','--logger','console;verbosity=detailed')
            Run-Step ($provider+'-manuscript-load') 'dotnet' @('test','Tests/WriterApp.Tests/WriterApp.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~MeasuredHundredPage','--logger','console;verbosity=detailed')
            $env:WRITERAPP_COLLAB_LOAD_TESTS='0'
        }
    }
    if ($IncludeBrowser) {
        $env:EDITOR_EVIDENCE_DIR=Join-Path $evidenceRoot 'editor'
        Run-Step 'editor-journeys' 'node' @('WriterApp.Client/tests/verify-collaboration.mjs')
        Run-Step 'editor-measurements' 'node' @('WriterApp.Client/tests/benchmark-collaboration.mjs')
    }
}
catch { $failed=$true; Write-Warning $_.Exception.Message }
finally {
    $hashes = @()
    foreach ($relative in @('WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.js','WriterApp.Collaboration/src/generated/schema.mjs','WriterApp.Client/package-lock.json','WriterApp.Collaboration/package-lock.json','Tools/Collaboration.Operations/bin/Debug/net10.0/Collaboration.Operations.dll')) {
        $path=Join-Path $repoRoot $relative
        if (Test-Path -LiteralPath $path) { $hashes += [pscustomobject]@{ path=$relative; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } }
    }
    $manifest=[ordered]@{ generatedAtUtc=[DateTime]::UtcNow.ToString('o'); automatedChecksPassed=(!$failed); usedExistingBuild=[bool]$UseExistingBuild; includedBrowser=[bool]$IncludeBrowser; includedLoad=[bool]$IncludeLoad; steps=$steps.ToArray(); hashes=$hashes;
        activation='blocked'; pilotReady=$false; outstandingGates=@('Physical Windows/Android/iOS journeys','Hosted identity/TLS/reverse proxy deployment','Reference-machine/mobile performance and capacity budgets','Cross-host backup/asset/key-ring rehearsal and recovery-point agreement');
        note='Automated success does not grant product activation. Load correctness is separate from measured latency budgets; inspect the performance JSON.' }
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'manifest.json') -Encoding utf8
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key]) }
    Pop-Location
    Write-Output "Evidence: $evidenceRoot"
}
if ($failed) { exit 1 }
