param(
    [string]$OutputDirectory = 'artifacts/windows-msix',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$signing = Join-Path $output 'signing'
$password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
& (Join-Path $PSScriptRoot 'New-ProsaTestCertificate.ps1') -OutputDirectory $signing -Password $password
$pfx = Join-Path $signing 'Prosa.TestSigning.pfx'
$project = Join-Path $repoRoot 'WriterApp.Desktop\WriterApp.Desktop.csproj'
$arguments = @(
    'publish', $project, '--configuration', 'Release', '--framework', 'net10.0-windows10.0.19041.0',
    '-p:RuntimeIdentifierOverride=win-x64',
    '-p:WindowsPackageType=Package',
    '-p:AppxPackageSigningEnabled=true',
    "-p:PackageCertificateKeyFile=$pfx",
    "-p:PackageCertificatePassword=$password",
    "-p:AppxPackageDir=$output\"
)
if ($NoRestore) { $arguments += '--no-restore' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "MSIX publish failed with exit code $LASTEXITCODE." }
if (-not (Get-ChildItem -LiteralPath $output -Recurse -Filter '*.msix' -File)) {
    throw 'MSIX publish did not produce a package.'
}
Write-Output "Test-signed MSIX is in $output. Its public certificate is in $signing."
Write-Output 'This generated certificate is for review only; keep one stable signing identity for upgradeable beta releases.'
