param([Parameter(Mandatory)][string]$SeedDatabase, [int]$Port=5399,
 [string]$EvidenceDirectory='artifacts/brainstorming-p12')
$ErrorActionPreference='Stop'
$taskRepo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskEvidence=(Resolve-Path -LiteralPath $EvidenceDirectory).Path
if(-not $taskEvidence.StartsWith((Join-Path $taskRepo 'artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a repository artifact evidence directory.'}
$taskDatabase=(Resolve-Path -LiteralPath $SeedDatabase).Path
if(-not $taskDatabase.StartsWith($taskEvidence+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a disposable database inside the artifact evidence directory.'}
$taskBinary=Join-Path $taskEvidence 'release-build/Release/net10.0/BlazorApp.dll'
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Acceptance port already occupied.'}
$taskSettings=@{
 ASPNETCORE_ENVIRONMENT='Development';DatabaseProvider='Sqlite';ConnectionStrings__DefaultConnection="Data Source=$taskDatabase";
 WriterApp__Database__AutoMigrateOnStartup='false';WriterApp__WasmClient__Enabled='true';WriterApp__AI__Enabled='true';WriterApp__AI__Streaming__Enabled='true';WriterApp__AI__Speech__Enabled='false';
 WriterApp__AI__Providers__DefaultTextProviderId='openai';WriterApp__AI__Providers__AllowProviderFallback='false';WriterApp__AI__Providers__OpenAI__TextModel='gpt-4.1-mini';
 Stripe__Enabled='false';Mailgun__Enabled='false';Mailgun__ApiKey=''
}
$taskOld=@{};foreach($taskName in $taskSettings.Keys){$taskOld[$taskName]=[Environment]::GetEnvironmentVariable($taskName);[Environment]::SetEnvironmentVariable($taskName,$taskSettings[$taskName])}
try{
 $taskHost=Start-Process (Get-Command dotnet).Source -ArgumentList @($taskBinary,'--urls',"http://127.0.0.1:$Port") -WorkingDirectory $taskRepo -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskEvidence 'web-host.log') -RedirectStandardError (Join-Path $taskEvidence 'web-host-error.log') -PassThru
 $taskHost.Id | Set-Content (Join-Path $taskEvidence 'web-host.pid')
 @{pid=$taskHost.Id;startUtc=$taskHost.StartTime.ToUniversalTime();auth='Built-in Development LocalDev dev-oid';database=$taskDatabase;binary=$taskBinary;sha256=(Get-FileHash $taskBinary).Hash;speechEnabled=$false;model='gpt-4.1-mini';keyPresent=[bool]$env:OPENAI_API_KEY;url="http://127.0.0.1:$Port"} | ConvertTo-Json | Set-Content (Join-Path $taskEvidence 'web-host-config.json')
 Write-Output "Owned acceptance server PID $($taskHost.Id). Explicit live AI flag is required by the browser runner. Speech is disabled."
}finally{foreach($taskName in $taskOld.Keys){[Environment]::SetEnvironmentVariable($taskName,$taskOld[$taskName])}}
