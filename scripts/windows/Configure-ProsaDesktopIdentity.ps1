# Uses the signed-in administrator's customer-tenant session. Never outputs credentials.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$tenant = 'a000a187-b940-41c1-bebe-f30c4a351099'
$apiAppId = 'c0ecc793-e620-416c-a072-4e9eca30f048'
$flowId = 'a5e832fa-5107-42f3-b08a-dc65b344b2a5'
$credential = az account get-access-token --tenant $tenant --resource-type ms-graph -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Sign in to the Prosa customer tenant first.' }
$headers = @{ Authorization = 'Bearer ' + $credential.accessToken }
function Graph([string]$method, [string]$path, $body) {
    $arguments = @{ Method = $method; Uri = "https://graph.microsoft.com/v1.0/$path"; Headers = $headers }
    if ($null -ne $body) { $arguments.ContentType = 'application/json'; $arguments.Body = $body | ConvertTo-Json -Depth 20 }
    Invoke-RestMethod @arguments
}
$apps = Graph GET 'applications?$select=id,appId,displayName,signInAudience,api,identifierUris,publicClient,requiredResourceAccess' $null
$api = $apps.value | Where-Object appId -EQ $apiAppId
if ($null -eq $api) { throw 'The existing Prosa web/API registration was not found.' }
$scope = $api.api.oauth2PermissionScopes | Where-Object value -EQ 'access_as_user'
if ($null -eq $scope) {
    $scope = @{ id = [guid]::NewGuid().ToString(); value = 'access_as_user'; isEnabled = $true; type = 'Admin';
        adminConsentDisplayName = 'Access Prosa as the signed-in user';
        adminConsentDescription = 'Read and update Prosa writing and account features as the signed-in user.' }
    $api.api.oauth2PermissionScopes = @($api.api.oauth2PermissionScopes) + @($scope)
}
if (!$scope.isEnabled) { throw 'The existing access_as_user scope is disabled; review it before continuing.' }
$api.api.requestedAccessTokenVersion = 2
$identifierUris = @($api.identifierUris)
if ($identifierUris -notcontains "api://$apiAppId") { $identifierUris += "api://$apiAppId" }
# Customer apps and their APIs use Prosa's directory as their single tenant.
# Customers can register with any email domain. Provider federation is separate.
Graph PATCH "applications/$($api.id)" @{ signInAudience = 'AzureADMyOrg'; api = $api.api; identifierUris = $identifierUris } | Out-Null
$desktop = @($apps.value | Where-Object displayName -EQ 'Prosa Windows Desktop')
if ($desktop.Count -gt 1) { throw 'Multiple Prosa Windows registrations found. Resolve ambiguity first.' }
if ($desktop.Count -eq 0) {
    $desktop = Graph POST 'applications' @{ displayName = 'Prosa Windows Desktop'; signInAudience = 'AzureADMyOrg';
        publicClient = @{ redirectUris = @('http://localhost') }; requiredResourceAccess = @(@{
            resourceAppId = $apiAppId; resourceAccess = @(@{ id = $scope.id; type = 'Scope' }) }) }
} else {
    $desktop = $desktop[0]
    $redirects = @($desktop.publicClient.redirectUris)
    if ($redirects -notcontains 'http://localhost') { $redirects += 'http://localhost' }
    $permissions = @($desktop.requiredResourceAccess)
    $resource = $permissions | Where-Object resourceAppId -EQ $apiAppId
    if ($null -eq $resource) { $permissions += @{ resourceAppId = $apiAppId; resourceAccess = @(@{ id = $scope.id; type = 'Scope' }) } }
    elseif (@($resource.resourceAccess.id) -notcontains $scope.id) { $resource.resourceAccess = @($resource.resourceAccess) + @(@{ id = $scope.id; type = 'Scope' }) }
    Graph PATCH "applications/$($desktop.id)" @{ signInAudience = 'AzureADMyOrg'; publicClient = @{ redirectUris = $redirects }; requiredResourceAccess = $permissions } | Out-Null
}
$authorized = @($api.api.preAuthorizedApplications)
$entry = $authorized | Where-Object appId -EQ $desktop.appId
if ($null -eq $entry) { $authorized += @{ appId = $desktop.appId; delegatedPermissionIds = @($scope.id) } }
elseif (@($entry.delegatedPermissionIds) -notcontains $scope.id) { $entry.delegatedPermissionIds = @($entry.delegatedPermissionIds) + @($scope.id) }
$api.api.preAuthorizedApplications = $authorized
Graph PATCH "applications/$($api.id)" @{ api = $api.api } | Out-Null
foreach ($applicationId in @($apiAppId, $desktop.appId)) {
    $principals = Graph GET "servicePrincipals?`$filter=appId eq '$applicationId'&`$select=id,appId" $null
    if (@($principals.value).Count -eq 0) { Graph POST 'servicePrincipals' @{ appId = $applicationId } | Out-Null }
}
$linked = Graph GET "identity/authenticationEventsFlows/$flowId/conditions/applications/includeApplications" $null
if (@($linked.value.appId) -notcontains $desktop.appId) {
    Graph POST "identity/authenticationEventsFlows/$flowId/conditions/applications/includeApplications" @{ appId = $desktop.appId } | Out-Null
}
foreach ($registration in @($api, $desktop)) {
    $verified = Graph GET "applications/$($registration.id)?`$select=appId,signInAudience" $null
    if ($verified.appId -ne $registration.appId -or $verified.signInAudience -ne 'AzureADMyOrg') {
        throw 'Customer application audience verification failed. Both web/API and desktop must use AzureADMyOrg.'
    }
}
@{ TenantId = $tenant; ClientId = $desktop.appId; Authority = 'https://prosaapp.ciamlogin.com/';
    RedirectUri = 'http://localhost'; Scopes = @("api://$apiAppId/access_as_user"); ApiAudience = $apiAppId } | ConvertTo-Json
