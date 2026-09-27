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
$certificate = Import-PfxCertificate -FilePath $pfx -CertStoreLocation 'Cert:\CurrentUser\My' `
    -Password (ConvertTo-SecureString $password -AsPlainText -Force)
try {
    $project = Join-Path $repoRoot 'WriterApp.Desktop\WriterApp.Desktop.csproj'
    $arguments = @(
        'publish', $project, '--configuration', 'Release', '--framework', 'net10.0-windows10.0.19041.0', '-warnaserror',
        '-p:RuntimeIdentifierOverride=win-x64',
        '-p:WindowsPackageType=MSIX',
        '-p:AppxPackageSigningEnabled=true',
        "-p:PackageCertificateThumbprint=$($certificate.Thumbprint)",
        "-p:AppxPackageDir=$output\"
    )
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $symbolTool = & $vswhere -products '*' -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\mspdbcmf.exe' |
            Select-Object -Last 1
        if ($symbolTool) { $arguments += "-p:MsPdbCmfExeFullpath=$symbolTool" }
    }
    if ($NoRestore) { $arguments += '--no-restore' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "MSIX publish failed with exit code $LASTEXITCODE." }
}
finally {
    # Remove only the temporary signing key imported by this invocation, not any trust entries.
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
}
if (-not (Get-ChildItem -LiteralPath $output -Recurse -Filter '*.msix' -File)) {
    throw 'MSIX publish did not produce a package.'
}
Write-Output "Test-signed MSIX is in $output. Its public certificate is in $signing."
Write-Output 'This generated certificate is for review only; keep one stable signing identity for upgradeable beta releases.'
