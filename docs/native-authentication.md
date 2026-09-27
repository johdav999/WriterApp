# Native authentication setup (Release 1, Prompt 6)

The Windows client now uses MSAL.NET 4.90.1 with authorization code + PKCE, the system browser, and a localhost loopback redirect. The backend independently validates access tokens with ASP.NET Core JWT bearer middleware. Native authentication is **disabled by default** until registrations and environment settings are supplied. No Azure settings, deployed app, or registrations were changed by this implementation.

## Existing web flow and compatibility

Web sign-in remains `/.auth/login/<provider>` through Azure App Service Easy Auth. The browser holds its cookie, `/.auth/me` supplies provider state, and `EasyAuthAuthenticationHandler` maps the platform's `X-MS-CLIENT-PRINCIPAL` into an ASP.NET principal. `/api/auth/me` provisions app profiles/entitlements and enforces the existing duplicate/deleted-account checks. The customer provider defaults to `externalid`; optional workforce/admin routing uses `aad`. Development without an Authorization header retains `LocalDevAuthenticationHandler`.

A desktop system-browser login does not copy the Easy Auth cookie into native HTTP. MSAL requests an API access token instead. Any explicit Authorization header selects the native bearer scheme, including malformed/unsupported headers, so invalid native credentials cannot fall back to a web cookie, trusted header, or development admin. Only `/api` accepts native bearer authentication. The explicitly protected `/api/native/session` probe requires the native scheme even when a web session exists. Ordinary protected controllers retain their existing authorization and ownership policies.

Validation requires an RS256 signature from discovered signing keys, exact configured issuer, v2 API audience, expiry (30-second clock tolerance), tenant ID, `oid`, delegated `access_as_user` scope, `ver=2.0`, and an allowlisted `azp` client ID. ID tokens and app-only tokens do not satisfy this boundary. Native role claims do not grant legacy web Admin privileges. Existing persisted admin assignments and bootstrap policy remain authoritative for that authenticated user; web role compatibility is unchanged.

The Windows account menu restores the MSAL session silently, offers explicit browser sign-in, checks `/api/native/session` and then the existing `/api/auth/me`, and signs out locally. MSAL silently renews expired cached access tokens when possible. Interaction-required responses ask the user to sign in; API requests never launch a browser. A 401 marks the session for reauthentication without replaying the request. A 403 leaves the session intact. Network failure does not delete credentials or affect local documents.

MSAL's serialized cache (including refresh/account material) is persisted only through MAUI `SecureStorage`, under a key isolated by authority, client ID, and scope set. No browser storage, plaintext token file, client secret, or provider key is used. Display name/status live separately in memory. Sign-out clears both in-memory MSAL state and the current configuration's secure cache, leaving local writing intact. Browser SSO cookies remain; the next sign-in asks the browser to select an account. If secure deletion fails, the UI says so and blocks silent token use until explicit sign-in or successful sign-out. Changing registration settings creates a separate cache namespace; sign out before switching environments if the old environment's credentials should also be removed.

Authenticated HTTP is restricted to `/api/` on the configured backend origin. HTTPS is required except for HTTP loopback development. Redirect following and cookie storage are disabled, so tokens are not forwarded to another origin. Authentication failures expose generic UI messages rather than raw MSAL/server responses or tokens.

## Azure actions still required

Use a staging slot/registration first and record the following actual values; the repository intentionally contains placeholders only.

1. In the **existing customer External ID tenant**, identify the Prosa web/API registration and tenant GUID. For the existing custom OIDC Easy Auth provider, prefer exposing the API on that same server registration: its application/client GUID is the API audience. Custom OIDC Easy Auth does not have an arbitrary allowed-audience setting. Do not create an unrelated API audience and assume the current edge configuration will accept it. For a built-in Microsoft provider, review its allowed-token-audiences settings instead. Preserve web callbacks, credentials, and any workforce provider.
2. In that API registration, set `api.requestedAccessTokenVersion` to `2` in the manifest. Under **Expose an API**, use Application ID URI `api://<api-application-client-guid>` and add delegated scope `access_as_user`, enabled, with appropriate consent text such as “Access Prosa as the signed-in user.” Do not grant application-only access for the desktop.
3. Create a **separate single-tenant public client registration** for Prosa Windows in the same tenant. Under **Authentication → Add a platform → Mobile and desktop applications**, register exactly `http://localhost`. This is the system-browser loopback callback; MSAL chooses an available port. Do not select SPA/Web, add a client secret, or enable implicit grants. Authorization-code PKCE does not require enabling password/device-code grants under “Allow public client flows.”
4. Under the desktop app's **API permissions**, add the API's delegated `access_as_user` permission and grant required tenant/admin consent. Associate the desktop registration with the existing External ID sign-up/sign-in user flow. Include the profile claims needed by Prosa, particularly `oid`, name and email. Add the desktop client to the API's authorized client applications if your consent policy requires preauthorization.
5. Check identity continuity **before enabling access for existing customers**. This backend uses `oid` when available. Web and native tokens must resolve to the same existing Prosa user ID. Older customer records can instead use `extid:<issuer>:<sub>`; `sub` is application-specific, so these records require a deliberate identity-link/migration exercise preserving ownership, entitlements, and deletion tombstones. This prompt does not auto-link by email. Existing duplicate-account responses remain in force; do not bypass a 409.
6. Get the tenant's v2 OpenID metadata URL from its registration endpoints. Set server Authority to the metadata URL with `/.well-known/openid-configuration` removed and set Issuer to the **exact `issuer` value returned by that document**, including any trailing slash. Do not assume issuer spelling from the sign-in URL. Configure the server settings below as staging slot settings.
7. Keep App Service Authentication **enabled** so untrusted inbound principal headers are stripped. Allow unauthenticated requests through to application authorization where needed for native JWT validation; do not configure API requests to redirect to browser login. Preserve existing web provider cookies and callbacks. Verify that a native Authorization header reaches the app unchanged and that Easy Auth does not reject its audience first. Never bypass all `/api/*` paths blindly: that can break existing web API cookie/header authentication. If the current edge/provider cannot pass the token while preserving the web flow, resolve that routing/provider configuration in staging before production rollout.
8. Deploy the backend changes only when intended, launch Windows with the matching public settings, and complete the live checks below. No deployment was performed as part of implementing this prompt.

Server App Service environment variables (`__` maps to configuration sections):

```text
NativeAuth__Enabled=true
NativeAuth__Authority=<tenant-v2-metadata-base-url>
NativeAuth__Issuer=<exact-issuer-from-metadata>
NativeAuth__TenantId=<customer-tenant-guid>
NativeAuth__Audience=<api-application-client-guid>
NativeAuth__RequiredScope=access_as_user
NativeAuth__AllowedClientIds__0=<desktop-public-client-guid>
```

The audience is the API's GUID for these v2 tokens, **not** the desktop client GUID and **not** the full scope URI. The existing `ExternalIdTenantId` / `ExternalIdClientId` and `WriterApp:Auth:*` settings for web startup/routing remain separate and unchanged. A later iOS client needs its own registration/redirect and an additional allowlist entry.

Desktop environment variables, inherited by the process launching the app:

```powershell
$env:WRITERAPP_AUTH_TENANT_ID = '<customer-tenant-guid>'
$env:WRITERAPP_AUTH_AUTHORITY = 'https://<tenant-subdomain>.ciamlogin.com/'
$env:WRITERAPP_AUTH_CLIENT_ID = '<desktop-public-client-guid>'
$env:WRITERAPP_AUTH_REDIRECT_URI = 'http://localhost'
$env:WRITERAPP_AUTH_SCOPES = 'api://<api-application-client-guid>/access_as_user'
$env:WRITERAPP_API_BASE_URL = 'https://<staging-app-host>/'
dotnet run --project WriterApp.Desktop/WriterApp.Desktop.csproj --framework net10.0-windows10.0.19041.0
```

An editable placeholder script is in `WriterApp.Desktop/native-auth.example.ps1`. Without valid configuration the app still opens, edits, and saves locally. This release's authority validation supports the standard `*.ciamlogin.com` host and tenant-specific `login.microsoftonline.com/<tenant-guid>`; custom branded authority domains need an explicit reviewed extension. iOS continues to build and use local editing; it deliberately registers the unconfigured identity adapter until its native redirect/browser/keychain setup is implemented.

For local API development, use the same real tenant registrations, set the backend's `NativeAuth` settings through environment variables or untracked user settings, and point the desktop at loopback. The bearer scheme still validates real tokens even in Development; it cannot fall back to the local fake admin. Never log, paste into an issue, or commit an access/refresh token to diagnose setup.

## Verification and remaining gate

Automated tests use signed synthetic tokens and fixed in-memory metadata, with no live tenant. They cover successful protected API access and web identity equivalence, wrong audience/issuer/tenant/client/scope, expired/forged/wrong-algorithm tokens, ID/app-only tokens, malformed-header fallback attempts, disabled/non-API boundaries, and preservation of web admin authorization. Device tests cover restoration, sign-out/document preservation, credential-removal failure, origin restrictions, expiry, offline state, 401 without mutation replay, 403, and stale-response rejection.

Live checks still required after the portal work: browser sign-in/cancel; `/api/native/session` then `/api/auth/me` success for an existing customer with the same user ID; restart and silent restoration; expired/revoked consent handling; sign-out and restart; secure cache removal; signed-out offline editing; and existing web/admin sign-in on the deployed staging slot. Native browser/WebView/SecureStorage and Azure edge behavior have not been validated against a real registration here. Prompt 7 can build on the bearer boundary, but paid sync/AI release validation depends on these live checks passing.

Implementation references: [MSAL system browser and loopback callbacks](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers), [MSAL token cache serialization](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization), [ASP.NET Core JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), and [App Service custom OIDC provider restrictions](https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-provider-openid-connect).
