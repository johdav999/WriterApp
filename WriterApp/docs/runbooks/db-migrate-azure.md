# Runbook: Run EF Core Migrations on Azure App Service

This runbook explains how to trigger WriterApp database migrations in Azure using the one-off endpoint:

- `POST /api/admin/db/migrate`

Required headers:

- `X-DB-MIGRATE-KEY: <secret>` (if `DB_MIGRATE_KEY` is configured on the app)
- `Authorization: Bearer <AAD access token>`

## Understand the 302

If you get a `302` redirect to `login.microsoftonline.com`, App Service Authentication (EasyAuth) blocked the request before it reached the API code.

Important:

- `X-DB-MIGRATE-KEY` alone is **not enough** when EasyAuth is set to **Require authentication**.
- You must send a bearer token that EasyAuth accepts for this app.

## Prerequisites

- App Service URL, for example: `https://<app-name>.azurewebsites.net`
- Tenant ID: `8acf7923-17e5-492d-a8c6-756ca23599af`
- API audience (App ID URI): `api://ae53fb2e-6d24-4811-9fff-3d35165f46ac`
- A user that can satisfy app-level admin authorization (see troubleshooting section for `401/403`)
- PowerShell + Azure CLI installed

## 1) Sign in and acquire token (PowerShell)

```powershell
az logout
az login --tenant "8acf7923-17e5-492d-a8c6-756ca23599af" --scope "api://ae53fb2e-6d24-4811-9fff-3d35165f46ac/.default"
$token = az account get-access-token --resource "api://ae53fb2e-6d24-4811-9fff-3d35165f46ac" --query accessToken -o tsv
```

Verify the token is present and looks like a JWT:

```powershell
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token acquisition failed (empty token)." }
if ($token -notmatch '^[^.]+\.[^.]+\.[^.]+$') { throw "Token does not look like a JWT." }
```

Decode JWT payload and confirm `aud`:

```powershell
$parts = $token.Split('.')
$payload = $parts[1]
$payload += '=' * ((4 - $payload.Length % 4) % 4)
$payloadJson = [System.Text.Encoding]::UTF8.GetString(
  [Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
)
$claims = $payloadJson | ConvertFrom-Json
$claims.aud

if ($claims.aud -ne "api://ae53fb2e-6d24-4811-9fff-3d35165f46ac") {
  throw "Unexpected token audience: $($claims.aud)"
}
```

## 2) Call migrate endpoint (PowerShell)

```powershell
$appUrl = "https://<app-name>.azurewebsites.net"
$migrateKey = "<your-db-migrate-key>"  # if DB_MIGRATE_KEY is enabled in app settings

$headers = @{
  Authorization    = "Bearer $token"
  "X-DB-MIGRATE-KEY" = $migrateKey
}

$response = Invoke-RestMethod `
  -Method Post `
  -Uri "$appUrl/api/admin/db/migrate" `
  -Headers $headers `
  -ContentType "application/json"

$response | ConvertTo-Json -Depth 10
```

Expected JSON shape:

- `success`
- `pendingBefore`
- `appliedNow`
- `provider`
- `database`
- `timestamp`

## 3) Call migrate and detect redirect vs auth failure

Use `Invoke-WebRequest` with redirects disabled so you can quickly see if EasyAuth intercepted the request:

```powershell
$appUrl = "https://<app-name>.azurewebsites.net"
$migrateKey = "<your-db-migrate-key>"
$headers = @{
  Authorization      = "Bearer $token"
  "X-DB-MIGRATE-KEY" = $migrateKey
}

try {
  Invoke-WebRequest `
    -Method Post `
    -Uri "$appUrl/api/admin/db/migrate" `
    -Headers $headers `
    -MaximumRedirection 0 `
    -ErrorAction Stop
}
catch {
  $status = $_.Exception.Response.StatusCode.value__
  $location = $_.Exception.Response.Headers["Location"]
  "Status: $status"
  if ($location) { "Location: $location" }
}
```

Interpretation:

- `302`: EasyAuth redirect (missing/invalid bearer token for this app)
- `401` or `403`: request reached your app; now failing app-level auth (`AdminOnly`) and/or migrate key check

## Troubleshooting

### 302 redirect to Microsoft login

Cause:

- EasyAuth is redirecting because the request is anonymous or bearer token is missing/invalid for this app.

Actions:

- Ensure `Authorization: Bearer <token>` is present.
- Ensure the token audience is your API: `api://ae53fb2e-6d24-4811-9fff-3d35165f46ac`.
- Re-acquire token with the exact tenant and resource shown above.

### AADSTS65001 consent_required (Microsoft Azure CLI app `04b07795-8ddb-461a-bbee-02f9e1bf7b46`)

What it means:

- `AADSTS65001` means the **Azure CLI public client** has not been granted consent to request tokens for this API in this tenant.

Primary fix paths:

Path A: admin consent via Enterprise Applications

1. `Microsoft Entra ID` -> `Enterprise applications` -> `Microsoft Azure CLI`
2. `Permissions` -> `Grant admin consent` (if available)
3. Retry token acquisition

Path B: verify/grant consent on API side

1. `Microsoft Entra ID` -> `App registrations` -> your API app (`api://ae53fb2e-6d24-4811-9fff-3d35165f46ac`)
2. `Expose an API`:
   - ensure at least one delegated scope exists
   - remember `.default` resolves to whatever delegated scopes/app roles are configured and consented
3. `API permissions`:
   - ensure required permissions are present
   - click `Grant admin consent`
4. Retry token acquisition

Note:

- `Expose an API` -> `Authorized client applications` is optional pre-authorization for known clients. It is not always required, but can be used to pre-authorize Microsoft Azure CLI (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) for delegated scopes.

### 401 or 403 from `/api/admin/db/migrate`

Meaning:

- EasyAuth accepted or partially processed auth, but app-level authorization failed (`AdminOnly`) or migration key check failed.

Checks:

- `401`: token/user not authenticated as expected by app policy.
- `403`: authenticated but not authorized by app policy, or wrong `X-DB-MIGRATE-KEY`.

WriterApp app-level admin policy requires one of:

- Role claim `Admin`, or
- bootstrap admin override configured in app settings:
  - `BOOTSTRAP_ADMIN_ENABLED=true`
  - `BOOTSTRAP_ADMIN_OID=<user object id>`

Also verify:

- `X-DB-MIGRATE-KEY` exactly matches `DB_MIGRATE_KEY` app setting (if configured).

## Post-run checks

- Confirm response has `success: true`.
- Confirm `appliedNow` includes expected migration(s) (or empty if already up to date).
- Re-run the previously failing workflow and confirm schema errors are gone.
