param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$Password
)

$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$rsa = [Security.Cryptography.RSA]::Create(3072)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=Prosa Development', $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add(
        [Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
    $request.CertificateExtensions.Add(
        [Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
    $purpose = [Security.Cryptography.OidCollection]::new()
    $purpose.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3')) | Out-Null
    $request.CertificateExtensions.Add(
        [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($purpose, $true))
    $certificate = $request.CreateSelfSigned(
        [DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddMonths(6))
    try {
        [IO.File]::WriteAllBytes((Join-Path $destination 'Prosa.TestSigning.pfx'),
            $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))
        [IO.File]::WriteAllBytes((Join-Path $destination 'Prosa.TestSigning.cer'),
            $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    }
    finally { $certificate.Dispose() }
}
finally { $rsa.Dispose() }

Write-Output 'Created temporary Prosa MSIX test-signing certificate files. Keep the PFX and password private.'
