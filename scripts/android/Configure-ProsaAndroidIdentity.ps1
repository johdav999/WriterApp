# Uses the existing native public client; adds only the exact signed Android callback.
# Public local build settings contain no credentials and are ignored by Git.
[CmdletBinding()]
param(
    [string]$ApkPath = 'WriterApp.Android/bin/Debug/net10.0-android/android-x64/com.prosa.writer.android-Signed.apk',
    [string]$AndroidSdkDirectory = 'C:\Program Files (x86)\Android\android-sdk',
    [switch]$RegisterCallback
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$config = Get-Content (Join-Path $repo 'WriterApp.Desktop/native-auth.json') -Raw | ConvertFrom-Json
if (!$config.ClientId -or !$config.TenantId) { throw 'Configure the existing Prosa native public client first.' }
$apk = if ([IO.Path]::IsPathRooted($ApkPath)) { $ApkPath } else { Join-Path $repo $ApkPath }
$signer = Get-ChildItem (Join-Path $AndroidSdkDirectory 'build-tools') -Directory |
    Sort-Object { [version]($_.Name -replace '-.*$', '') } -Descending |
    ForEach-Object { Join-Path $_.FullName 'apksigner.bat' } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$signer) { throw 'Android SDK apksigner is required.' }
$certificate = & $signer verify --print-certs $apk
if ($LASTEXITCODE -ne 0) { throw 'The Android APK signature could not be verified.' }
$matches = [regex]::Matches(($certificate -join "`n"), 'certificate SHA-1 digest: ([0-9a-fA-F]{40})')
if ($matches.Count -ne 1) { throw 'The APK must have exactly one signing certificate.' }
$signature = [Convert]::ToBase64String([Convert]::FromHexString($matches[0].Groups[1].Value))
$callback = 'msauth://com.prosa.writer.android/' + [Uri]::EscapeDataString($signature)
$credential = az account get-access-token --tenant $config.TenantId --resource-type ms-graph -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$credential.accessToken) { throw 'Sign in to the Prosa customer tenant administrator session first.' }
$headers = @{ Authorization = 'Bearer ' + $credential.accessToken }
function Graph([string]$method, [string]$path, $body) {
    $parameters = @{ Method = $method; Uri = 'https://graph.microsoft.com/v1.0/' + $path; Headers = $headers }
    if ($null -ne $body) { $parameters.ContentType = 'application/json'; $parameters.Body = $body | ConvertTo-Json -Depth 12 }
    Invoke-RestMethod @parameters
}
$application = Graph GET "applications(appId='$($config.ClientId)')?`$select=id,appId,displayName,signInAudience,publicClient,requiredResourceAccess" $null
if ($application.signInAudience -ne 'AzureADMyOrg') { throw 'Existing native client must use the Prosa customer tenant audience.' }
$scopeUri = $config.Scopes[0]
$scopeMatch = [regex]::Match($scopeUri, '^api://([0-9a-fA-F-]{36})/access_as_user$')
if ($config.Scopes.Count -ne 1 -or !$scopeMatch.Success) { throw 'Expected the existing delegated Prosa API scope.' }
$apiId = $scopeMatch.Groups[1].Value
$api = Graph GET "applications(appId='$apiId')?`$select=api" $null
$scope = @($api.api.oauth2PermissionScopes | Where-Object { $_.value -eq 'access_as_user' -and $_.isEnabled })
if ($scope.Count -ne 1) { throw 'The Prosa API scope is not enabled and unambiguous.' }
$permission = @($application.requiredResourceAccess | Where-Object resourceAppId -EQ $apiId)
$preauthorized = @($api.api.preAuthorizedApplications | Where-Object appId -EQ $application.appId)
if (@($permission.resourceAccess | Where-Object { $_.id -eq $scope[0].id -and $_.type -eq 'Scope' }).Count -ne 1 -or
    @($preauthorized.delegatedPermissionIds) -notcontains $scope[0].id) { throw 'Existing native client must already be approved for the Prosa delegated API scope.' }
$linked = Graph GET 'identity/authenticationEventsFlows/a5e832fa-5107-42f3-b08a-dc65b344b2a5/conditions/applications/includeApplications' $null
if (@($linked.value.appId) -notcontains $application.appId) { throw 'The native client is not associated with the existing Prosa login flow.' }
$redirects = @($application.publicClient.redirectUris)
if ($RegisterCallback -and $redirects -notcontains $callback) {
    $redirects += $callback
    Graph PATCH "applications/$($application.id)" @{ publicClient = @{ redirectUris = $redirects } } | Out-Null
}
$verified = Graph GET "applications/$($application.id)?`$select=publicClient,requiredResourceAccess,signInAudience" $null
if (($RegisterCallback -and @($verified.publicClient.redirectUris) -notcontains $callback) -or $verified.signInAudience -ne $application.signInAudience -or
    (ConvertTo-Json -Depth 12 -Compress -InputObject $verified.requiredResourceAccess) -ne
    (ConvertTo-Json -Depth 12 -Compress -InputObject $application.requiredResourceAccess)) { throw 'Registration read-back did not preserve the expected permissions and callback.' }
$config.RedirectUri = $callback
$config | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $repo 'WriterApp.Android/native-auth.local.json') -Encoding utf8
$project = [xml]'<Project><PropertyGroup /></Project>'
foreach ($property in @{
    ProsaAndroidAuthSettingsFile = '$(MSBuildThisFileDirectory)native-auth.local.json'
    ProsaAndroidSignatureHash = $signature
}.GetEnumerator()) {
    $node = $project.CreateElement($property.Key)
    $node.SetAttribute('Condition', "'`$($($property.Key))' == ''")
    $node.InnerText = $property.Value
    [void]$project.SelectSingleNode('/Project/PropertyGroup').AppendChild($node)
}
$project.Save((Join-Path $repo 'WriterApp.Android/native-auth.local.props'))
@{ ClientId = $config.ClientId; Package = 'com.prosa.writer.android'; Callback = $callback;
    CallbackRegistered = @($verified.publicClient.redirectUris) -contains $callback;
    LocalConfigurationWritten = $true; ExistingPermissionsPreserved = $true } | ConvertTo-Json
