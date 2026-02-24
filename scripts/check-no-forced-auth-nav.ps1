Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

if (-not (Get-Command rg -ErrorAction SilentlyContinue)) {
    Write-Error "ripgrep (rg) is required for this guardrail."
}

Write-Host "Checking for forced auth navigation regressions..."

$BaseArgs = @(
    "--glob", "!**/bin/**",
    "--glob", "!**/obj/**",
    "--glob", "!**/publish/**",
    "--glob", "!**/.azure-publish/**",
    "--glob", "!**/wwwroot/js/*.bundle.js",
    "--glob", "!**/*.min.js"
)

$BlockedPatterns = @(
    'NavigateTo\([^)]*/\.auth/login',
    'NavigateTo\([^)]*https?://[^)]*/\.auth/login',
    'window\.location(\.href)?\s*=\s*["''].*\/\.auth/login'
)

foreach ($Pattern in $BlockedPatterns) {
    & rg -n -S $Pattern @BaseArgs .
    if ($LASTEXITCODE -eq 0) {
        Write-Error "Found prohibited auth auto-navigation pattern: $Pattern"
    }
}

Write-Host "PASS: no forced auth navigation patterns found."
