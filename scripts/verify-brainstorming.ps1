param(
    [ValidateSet('p02', 'p03', 'p04', 'p05', 'p06', 'p07', 'p08', 'p09', 'p10', 'p11','p12')][string]$Phase = 'p12',
    [ValidatePattern('^[a-z0-9-]+$')][string]$EvidenceSuffix,
    [string]$PlaywrightModule = $env:PLAYWRIGHT_MODULE,
    [switch]$Browser,
    [switch]$NativeStartup
)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskRelative = "artifacts/brainstorming-$Phase"
if($EvidenceSuffix){$taskRelative += "-$EvidenceSuffix"}
$taskEvidence = Join-Path $taskRepo $taskRelative
$taskNativeData = Join-Path $taskEvidence 'native-validation-data'
New-Item -ItemType Directory -Force $taskEvidence | Out-Null
$taskPreviousFixture = $env:WRITERAPP_BRAINSTORMING_FIXTURE
$taskPreviousPlaywright = $env:PLAYWRIGHT_MODULE
$taskPreviousContextHtml = $env:WRITERAPP_BRAINSTORMING_CONTEXT_HTML
$taskPreviousBrowserEvidence = $env:BRAINSTORMING_EVIDENCE
Push-Location $taskRepo
try {
    $env:WRITERAPP_BRAINSTORMING_FIXTURE = Join-Path $taskEvidence 'browser-fixture.json'
    $env:WRITERAPP_BRAINSTORMING_CONTEXT_HTML = Join-Path $taskEvidence 'context-review.html'
    $env:BRAINSTORMING_EVIDENCE = Join-Path $taskEvidence 'browser'
    $taskFilter = 'FullyQualifiedName~Brainstorming|FullyQualifiedName~WritingOutline|FullyQualifiedName~LocalDocumentStructure|FullyQualifiedName~OutlineStructureCommand|FullyQualifiedName~FileLocalDocumentStore|FullyQualifiedName~LocalWriting|FullyQualifiedName~WebAiHistoryBoundary|FullyQualifiedName~WebAiHistoryOutbox|FullyQualifiedName~LocalAiHistoryActions|FullyQualifiedName~DeviceSyncEngine|FullyQualifiedName~MultiDocumentSync|FullyQualifiedName~ClientLogout'
    if ($Phase -ne 'p02') { $taskFilter += '|FullyQualifiedName~DeviceBible|FullyQualifiedName~WebCheckedAi|FullyQualifiedName~AuthState|FullyQualifiedName~EditorContentCompatibility' }
    if ($Phase -in @('p04','p05','p06','p07','p08','p09','p10','p11','p12')) { $taskFilter += '|FullyQualifiedName~AiOrchestrator|FullyQualifiedName~AiUsagePolicy|FullyQualifiedName~AiQuota|FullyQualifiedName~UsageMeter|FullyQualifiedName~OpenAiProvider' }
    if ($Phase -in @('p07','p08','p09','p10','p11','p12')) { $taskFilter += '|FullyQualifiedName~NormalWebPageSaveReopensAfterHostRestart|FullyQualifiedName~SqliteHistoryPrunesByUtcTime' }
    if ($Phase -in @('p08','p09','p10','p11','p12')) { $taskFilter += '|FullyQualifiedName~LocalPlanning|FullyQualifiedName~LocalStoryboard|FullyQualifiedName~LocalSynopsisPanel|FullyQualifiedName~LocalScenePanel|FullyQualifiedName~WebPlanningRecovery|FullyQualifiedName~PlanningSync|FullyQualifiedName~SceneApproval' }
    if($Phase -in @('p09','p10','p11','p12')){$taskFilter+='|FullyQualifiedName~DeviceSyncApi'}
    if($Phase -in @('p10','p11','p12')){$taskFilter+='|FullyQualifiedName~AiSelectionAndRouting'}
    & dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj "-p:BaseOutputPath=$taskRelative/build/" --filter $taskFilter --logger 'trx;LogFileName=combined-regressions.trx' --results-directory $taskEvidence --nologo -v:minimal *> (Join-Path $taskEvidence 'combined-regressions.log')
    if ($LASTEXITCODE -ne 0) { throw 'Brainstorming or existing writing regressions failed. See combined-regressions.log.' }
    # These builds deliberately run sequentially: isolated outputs still share obj intermediates.
    & dotnet build BlazorApp.csproj -c Release "-p:BaseOutputPath=$taskRelative/release-build/" --nologo -v:minimal *> (Join-Path $taskEvidence 'server-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Server/client build failed. See server-build.log.' }
    & dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug "-p:BaseOutputPath=$taskRelative/native-build/" "-p:ProsaValidationDataDirectory=$taskNativeData" --nologo -v:minimal *> (Join-Path $taskEvidence 'desktop-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed. See desktop-build.log.' }
    if ($Browser) {
        if ([string]::IsNullOrWhiteSpace($PlaywrightModule)) { throw 'Pass the installed Playwright module path for browser verification.' }
        $env:PLAYWRIGHT_MODULE = $PlaywrightModule
        & node Tests/brainstorming-store.mjs *> (Join-Path $taskEvidence 'browser.log')
        if ($LASTEXITCODE -ne 0) { throw 'Production IndexedDB browser checks failed. See browser.log.' }
        if ($Phase -ne 'p02') {
            & node Tests/brainstorming-context.mjs *> (Join-Path $taskEvidence 'context-browser.log')
            if ($LASTEXITCODE -ne 0) { throw 'Actual editor source/context render checks failed. See context-browser.log.' }
        }
        if ($Phase -in @('p04','p05','p06','p07','p08','p09','p10','p11','p12')) {
            & node Tests/brainstorming-conversation.mjs *> (Join-Path $taskEvidence 'conversation-browser.log')
            if ($LASTEXITCODE -ne 0) { throw 'Conversation/editor projection browser checks failed. See conversation-browser.log.' }
        }
        if ($Phase -in @('p05','p06','p07','p08','p09','p10','p11','p12')) {
            & node Tests/brainstorming-workspace.mjs *> (Join-Path $taskEvidence 'workspace-browser.log')
            if ($LASTEXITCODE -ne 0) { throw 'Brainstorm workspace browser checks failed. See workspace-browser.log.' }
        }
    }
    if ($Browser -and $Phase -in @('p06','p07','p08','p09','p10','p11','p12')) {
        & node Tests/brainstorming-drafts.mjs *> (Join-Path $taskEvidence 'drafts-browser.log')
        if ($LASTEXITCODE -ne 0) { throw 'Actual proposal editor/browser checks failed. See drafts-browser.log.' }
    }
    if ($Browser -and $Phase -in @('p07','p08','p09','p10','p11','p12')) {
        & node Tests/brainstorming-keep.mjs *> (Join-Path $taskEvidence 'keep-browser.log')
        if ($LASTEXITCODE -ne 0) { throw 'Actual Keep editor mapping/review checks failed. See keep-browser.log.' }
    }
    if ($Browser -and $Phase -in @('p08','p09','p10','p11','p12')) {
        & node Tests/brainstorming-planning.mjs *> (Join-Path $taskEvidence 'planning-browser.log')
        if ($LASTEXITCODE -ne 0) { throw 'Planning review and durable journal browser checks failed. See planning-browser.log.' }
    }
    if($Browser -and $Phase -in @('p09','p10','p11','p12')) {
        & node Tests/brainstorming-recovery.mjs *> (Join-Path $taskEvidence 'recovery-browser.log')
        if($LASTEXITCODE -ne 0){throw 'Recovery history/IndexedDB browser checks failed. See recovery-browser.log.'}
    }
    if($Browser -and $Phase -in @('p10','p11','p12')) {
        & node Tests/brainstorming-dictation.mjs *> (Join-Path $taskEvidence 'dictation-browser.log')
        if($LASTEXITCODE -ne 0){throw 'Production microphone module/browser checks failed. See dictation-browser.log.'}
    }
    if($Browser -and $Phase -in @('p11','p12')) {
        & node Tests/brainstorming-voice.mjs *> (Join-Path $taskEvidence 'voice-browser.log')
        if($LASTEXITCODE -ne 0){throw 'Production voice/browser checks failed. See voice-browser.log.'}
    }
    if ($NativeStartup) {
        $taskExe = Join-Path $taskRepo "WriterApp.Desktop/$taskRelative/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe"
        $taskPriorPids = @(Get-Process -Name WriterApp.Desktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
        $taskOwnedProcess = Start-Process -FilePath $taskExe -WorkingDirectory (Split-Path $taskExe) -WindowStyle Hidden -PassThru
        try {
            Start-Sleep -Milliseconds 7000
            $taskOwnedProcess.Refresh()
            $taskReport = [ordered]@{ scope='Rebuilt isolated Windows executable startup only; no brainstorming UI/provider/audio acceptance'; executable=$taskExe; pid=$taskOwnedProcess.Id; alive=(!$taskOwnedProcess.HasExited); dataDirectory=$taskNativeData; isolatedDocumentDirectoryExists=(Test-Path (Join-Path $taskNativeData 'documents')); priorUserPids=$taskPriorPids; timestampUtc=[DateTimeOffset]::UtcNow.ToString('O') }
            $taskReport.sha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash
            if (!$taskOwnedProcess.HasExited) { $taskReport.windowTitle = $taskOwnedProcess.MainWindowTitle }
            $taskReport | ConvertTo-Json | Set-Content (Join-Path $taskEvidence 'native-startup.json')
            if ($taskOwnedProcess.HasExited -or !$taskReport.isolatedDocumentDirectoryExists) { throw 'Native startup failed or did not initialize the isolated document store. See native-startup.json.' }
        } finally {
            # Only this explicitly launched PID is owned; preserve every pre-existing editor.
            if (!$taskOwnedProcess.HasExited) { $null=$taskOwnedProcess.CloseMainWindow(); if (!$taskOwnedProcess.WaitForExit(3000)) { $taskOwnedProcess.Kill(); $taskOwnedProcess.WaitForExit() } }
            $taskOwnedProcess.Dispose()
        }
    }
    & git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace verification failed.' }
    Write-Output "Brainstorming verification passed. Evidence: $taskEvidence"
} finally {
    $env:WRITERAPP_BRAINSTORMING_FIXTURE = $taskPreviousFixture
    $env:PLAYWRIGHT_MODULE = $taskPreviousPlaywright
    $env:WRITERAPP_BRAINSTORMING_CONTEXT_HTML = $taskPreviousContextHtml
    $env:BRAINSTORMING_EVIDENCE = $taskPreviousBrowserEvidence
    Pop-Location
}

