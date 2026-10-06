param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    function Read-PackageEntry([string]$name) {
        $entry = $archive.GetEntry($name)
        if (-not $entry) { throw "Missing package entry: $name" }
        $stream = $entry.Open(); $buffer = [IO.MemoryStream]::new()
        try { $stream.CopyTo($buffer); return ,$buffer.ToArray() }
        finally { $stream.Dispose(); $buffer.Dispose() }
    }
    [xml]$manifest = [Text.Encoding]::UTF8.GetString((Read-PackageEntry 'AppxManifest.xml')).TrimStart([char]0xFEFF)
    [xml]$blockMap = [Text.Encoding]::UTF8.GetString((Read-PackageEntry 'AppxBlockMap.xml')).TrimStart([char]0xFEFF)
    $verifiedFiles = 0
    foreach ($file in $blockMap.BlockMap.File) {
        $bytes = Read-PackageEntry ($file.Name.Replace('\', '/'))
        if ($bytes.Length -ne [long]$file.Size) { throw "Package size mismatch: $($file.Name)" }
        $offset = 0
        foreach ($block in $file.Block) {
            $length = [Math]::Min(65536, $bytes.Length - $offset)
            $actual = [Convert]::ToBase64String([Security.Cryptography.SHA256]::HashData([byte[]]$bytes[$offset..($offset + $length - 1)]))
            if ($actual -ne $block.Hash) { throw "Package block hash mismatch: $($file.Name)" }
            $offset += $length
        }
        if ($offset -ne $bytes.Length) { throw "Unverified package bytes: $($file.Name)" }
        $verifiedFiles++
    }
    $signature = Read-PackageEntry 'AppxSignature.p7x'
    if ([Text.Encoding]::ASCII.GetString($signature, 0, 4) -ne 'PKCX') { throw 'Unsupported MSIX signature header.' }
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new()
    $cms.Decode([byte[]]$signature[4..($signature.Length - 1)])
    # Verify the CMS signature without treating this review certificate as trusted.
    $cms.CheckSignature($true)
    $certificate = $cms.SignerInfos[0].Certificate
    if ($certificate.Subject -ne $manifest.Package.Identity.Publisher) { throw 'Publisher and signing subject differ.' }

    $assemblyBytes = Read-PackageEntry 'WriterApp.Desktop.dll'
    $assemblyStream = [IO.MemoryStream]::new($assemblyBytes, $false)
    $pe = [Reflection.PortableExecutable.PEReader]::new($assemblyStream)
    try {
        $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $metadata = @{}
        foreach ($handle in $reader.GetAssemblyDefinition().GetCustomAttributes()) {
            $attribute = $reader.GetCustomAttribute($handle)
            if ($attribute.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
            $memberHandle = [Reflection.Metadata.Ecma335.MetadataTokens]::MemberReferenceHandle([Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($attribute.Constructor))
            $member = $reader.GetMemberReference($memberHandle)
            if ($member.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
            $typeHandle = [Reflection.Metadata.Ecma335.MetadataTokens]::TypeReferenceHandle([Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($member.Parent))
            $type = $reader.GetTypeReference($typeHandle)
            if ($reader.GetString($type.Namespace) -ne 'System.Reflection' -or $reader.GetString($type.Name) -ne 'AssemblyMetadataAttribute') { continue }
            $blob = $reader.GetBlobReader($attribute.Value)
            if ($blob.ReadUInt16() -ne 1) { throw 'Invalid assembly metadata attribute.' }
            $key = $blob.ReadSerializedString(); $metadata[$key] = $blob.ReadSerializedString()
        }
    }
    finally { $pe.Dispose(); $assemblyStream.Dispose() }
    $result = [ordered]@{
        Package = $package
        Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
        Identity = $manifest.Package.Identity.OuterXml
        Application = $manifest.Package.Applications.Application.OuterXml
        Dependencies = $manifest.Package.Dependencies.OuterXml
        AssemblySha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assemblyBytes))
        AssemblyMetadata = $metadata
        VerifiedBlockMapFiles = $verifiedFiles
        CmsSignatureValid = $true
        CertificateThumbprint = $certificate.Thumbprint
        CertificateSubject = $certificate.Subject
        CertificateExpires = $certificate.NotAfter.ToUniversalTime().ToString('O')
        TrustedInstallOrDistributionVerified = $false
        InstalledOrLaunched = $false
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding utf8
    Write-Output "Verified $verifiedFiles block-map files and CMS signature; report: $ReportPath"
}
finally { $archive.Dispose() }
