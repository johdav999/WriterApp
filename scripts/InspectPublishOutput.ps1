param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$resolved = Resolve-Path -Path $PublishDir -ErrorAction Stop
$root = $resolved.Path

Write-Host "Inspecting publish output: $root"

$matches = Get-ChildItem -Path $root -Recurse -File |
    Where-Object { $_.Name -match 'e_sqlite3|libe_sqlite3' }

Write-Host ""
Write-Host "Native sqlite binaries:"
foreach ($file in $matches) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\')
    "{0,-80} {1,12} bytes" -f $relative, $file.Length
}

Write-Host ""
Write-Host "Native runtime folders (win*/native, linux*/native):"
$nativeFiles = Get-ChildItem -Path $root -Recurse -File |
    Where-Object {
        $_.FullName -match [regex]::Escape("\runtimes\win") -or
        $_.FullName -match [regex]::Escape("\runtimes\linux")
    } |
    Where-Object { $_.FullName -match [regex]::Escape("\native\") }

foreach ($file in $nativeFiles) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\')
    "{0,-80} {1,12} bytes" -f $relative, $file.Length
}

Write-Host ""
Write-Host "Done."
