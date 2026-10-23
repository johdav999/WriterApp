<#
.SYNOPSIS
Starts an installed Android virtual device, preserving its apps and data.
.EXAMPLE
.\android.ps1
.EXAMPLE
.\android.ps1 -Avd pixel_7_-_api_34 -OpenApp
#>
[CmdletBinding()]
param(
    [string]$Avd,
    [string]$SdkPath,
    [string]$AvdDirectory = (Join-Path $env:USERPROFILE '.android\avd'),
    [switch]$OpenApp,
    [ValidateRange(1, 65535)][int]$BackendPort = 5387,
    [switch]$SkipBackendForwarding,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
if (-not $SdkPath) {
    $candidates = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME,
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
        (Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk'))
    $SdkPath = $candidates | Where-Object {
        $_ -and (Test-Path -LiteralPath (Join-Path $_ 'emulator\emulator.exe')) -and
        (Test-Path -LiteralPath (Join-Path $_ 'platform-tools\adb.exe'))
    } | Select-Object -First 1
}
if (-not $SdkPath) { throw 'Android SDK not found. Pass -SdkPath with the SDK directory.' }
$emulatorPath = Join-Path $SdkPath 'emulator\emulator.exe'
$adbPath = Join-Path $SdkPath 'platform-tools\adb.exe'
if (!(Test-Path -LiteralPath $emulatorPath) -or !(Test-Path -LiteralPath $adbPath)) {
    throw 'The SDK must contain emulator\emulator.exe and platform-tools\adb.exe.'
}
$available = @(Get-ChildItem -LiteralPath $AvdDirectory -Filter '*.ini' | Select-Object -ExpandProperty BaseName)
if (-not $Avd) {
    if ($available -contains 'pixel_7_-_api_34') { $Avd = 'pixel_7_-_api_34' }
    elseif ($available.Count -eq 1) { $Avd = $available[0] }
    else { throw "Choose an AVD with -Avd. Available devices: $($available -join ', ')" }
}
if ($Avd -notmatch '^[A-Za-z0-9_.-]+$' -or $available -notcontains $Avd) {
    throw "Unknown AVD '$Avd'. Available devices: $($available -join ', ')"
}
& $adbPath start-server
if ($LASTEXITCODE -ne 0) { throw 'Could not start the Android device bridge.' }
$serial = $null
$devices = @(& $adbPath devices)
foreach ($line in $devices) {
    if ($line -match '^(emulator-\d+)\s+device\b') {
        $candidateSerial = $Matches[1]
        $name = @(& $adbPath -s $candidateSerial emu avd name 2>$null | ForEach-Object { $_.Trim() })
        if ($name -contains $Avd) { $serial = $candidateSerial; break }
    }
}
if ($serial) { Write-Host "Reusing $Avd ($serial)." }
else {
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    $port = 5554
    while (($listeners.Port -contains $port) -or ($listeners.Port -contains ($port + 1)) -or
        ($devices -match "^emulator-$port\s")) {
        $port += 2
        if ($port -gt 5584) { throw 'No free Android emulator port found.' }
    }
    $serial = "emulator-$port"
    $previousAvdHome = $env:ANDROID_AVD_HOME
    try {
        # Native processes may otherwise resolve a different Windows account's home.
        $env:ANDROID_AVD_HOME = $AvdDirectory
        $process = Start-Process -FilePath $emulatorPath -ArgumentList @('-avd', $Avd, '-port', $port) -WindowStyle Normal -PassThru
    }
    finally { $env:ANDROID_AVD_HOME = $previousAvdHome }
    Write-Host "Starting $Avd ($serial), process $($process.Id)..."
}
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $boot = & $adbPath -s $serial shell getprop sys.boot_completed 2>$null
    if ($LASTEXITCODE -eq 0 -and "$boot".Trim() -eq '1') { break }
    if ($process -and $process.HasExited) { throw "Emulator exited with code $($process.ExitCode)." }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)
if ("$boot".Trim() -ne '1') { throw "Android did not finish booting within $TimeoutSeconds seconds. Check the emulator window and rerun this script." }
Write-Host "Android is ready: $Avd ($serial)."
if (-not $SkipBackendForwarding) {
    # Android localhost is the device. Restore this tunnel after every emulator boot.
    & $adbPath -s $serial reverse "tcp:$BackendPort" "tcp:$BackendPort"
    if ($LASTEXITCODE -ne 0) { throw "Could not forward Android localhost:$BackendPort to the PC backend." }
    $forwarding = @(& $adbPath -s $serial reverse --list)
    if ($LASTEXITCODE -ne 0 -or !($forwarding -match "\btcp:$BackendPort\s+tcp:$BackendPort\b")) {
        throw 'Android backend forwarding was not established. Retry the launcher.'
    }
    Write-Host "Android localhost:$BackendPort now connects to PC localhost:$BackendPort."
    try {
        $health = Invoke-WebRequest -Uri "http://localhost:$BackendPort/health" -UseBasicParsing -TimeoutSec 5
        if ($health.StatusCode -eq 200 -and $health.Content.Trim() -eq 'OK') { Write-Host 'Local Prosa backend is responding.' }
        else { Write-Warning "The service on port $BackendPort did not return Prosa's expected health response." }
    }
    catch { Write-Warning "Forwarding is ready, but the local backend is not responding on port $BackendPort. Start the server before syncing." }
}
if ($OpenApp) {
    $installed = & $adbPath -s $serial shell pm path com.prosa.writer.android
    if ($LASTEXITCODE -ne 0 -or !($installed -match '^package:')) {
        throw 'Prosa is not installed on this emulator. Follow docs/android-development.md to build and install it.'
    }
    & $adbPath -s $serial shell monkey -p com.prosa.writer.android -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0) { throw 'Could not open Prosa.' }
}
