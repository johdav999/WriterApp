# Prosa Customer Auth With App Service EasyAuth And Entra External ID

## Purpose
- This runbook documents the current and target authentication setup for the active Prosa app in this repo.
- It is implementation-specific to this codebase.
- It does not describe app-managed OpenID Connect or JWT bearer middleware, because this app does not use that model.

## Current State
- Interactive sign-in is handled by Azure App Service Authentication / EasyAuth.
- The server trusts the `X-MS-CLIENT-PRINCIPAL` header and converts it into ASP.NET authentication via `Application/Security/EasyAuthAuthenticationHandler.cs`.
- The first app-specific auth boundary is `GET /api/auth/me` in `Program.cs`.
- `/api/auth/me` resolves the external identity, blocks deleted users, provisions `UserProfiles` and `UserEntitlements`, and returns app auth state.
- The client login flow is now provider-configurable through `WriterApp:Auth:*` settings and `WriterApp.Client/Utilities/EasyAuthUrlBuilder.cs`.
- The underlying auth/session/cookie transaction is still owned by App Service, not by this app.

## Target State
- Keep EasyAuth as the interactive auth front door.
- Use a Microsoft Entra External ID customer tenant as the customer identity system.
- Configure App Service Authentication with a provider name that maps to the External ID customer flow.
- Keep the current app model where a stable external identity maps into `UserProfiles.UserId`.
- Optionally run dual-provider mode during migration:
  - customer provider for external users
  - internal/admin provider for workforce users

## Code Paths This Runbook Applies To
- Server auth bridge: `Application/Security/EasyAuthAuthenticationHandler.cs`
- Canonical identity resolution: `Application/Security/ExternalIdentityClaims.cs`
- User id resolution: `Application/Security/UserIdResolver.cs`
- First-login provisioning: `Program.cs` and `Application/Security/AuthMeProvisioningService.cs`
- Client auth route generation: `WriterApp.Client/Utilities/EasyAuthUrlBuilder.cs`
- Client entry pages: `WriterApp.Client/Pages/Login.razor`, `WriterApp.Client/Pages/Register.razor`, `WriterApp.Client/Pages/Start.razor`, `WriterApp.Client/Pages/Logout.razor`
- Admin bootstrap fallback: `Application/Security/AdminAccessResolver.cs`

## Azure Setup

### 1. Create the customer identity app in Entra External ID
- Create or use a Microsoft Entra External ID customer tenant.
- Register the app that represents the Prosa App Service.
- Configure the sign-in flow required for customer identities.
- Allow the external account types you intend to support.
- If personal email sign-up is required, configure the External ID user flow or custom policy accordingly.

Use placeholders only:
- Tenant: `<external-id-tenant>`
- App registration client id: `<external-id-client-id>`
- App registration secret: configure in Azure only, do not store in repo docs

### 2. Configure App Service Authentication
- Keep App Service Authentication enabled.
- Keep unauthenticated handling aligned with the app host design you want for that environment.
- Add or update the customer auth provider in App Service Authentication.
- Prefer a named provider route that is not hardcoded to `aad` unless you intentionally want the legacy Microsoft provider path.

Recommended provider naming:
- Customer provider: `<customer-provider-name>`
- Internal provider: `<internal-provider-name>`

The client now generates routes in this shape:
- `/.auth/login/<provider-name>?post_login_redirect_uri=<absolute-url>`
- `/.auth/logout?post_logout_redirect_uri=<absolute-url>`

### 3. Redirect URIs and callback handling
- App Service Authentication owns the callback processing.
- Use the callback/redirect URIs required by the App Service auth provider configuration you choose.
- The exact callback path is determined by App Service Authentication and provider type. Do not invent a custom callback path in app code.
- The post-login redirect URI sent by the client should be an app URL such as:
  - `https://<app-host>/app/start?plan=free&returnUrl=%2Fprojects`
  - `https://<app-host>/documents`

### 4. Provider name alignment
- The provider name configured in App Service must match the provider name used by this app.
- Example only:
  - App Service provider name: `customer-entra`
  - Repo config: `WriterApp:Auth:CustomerLoginProvider=customer-entra`

If the names do not match, the app will generate `/.auth/login/<wrong-name>` links and sign-in will fail before the request reaches app code.

## Repo Configuration

### Required auth settings
These are read from `WriterApp:Auth` and bound into `WriterAuthOptions`.

- `WriterApp:Auth:LoginProvider`
  - Default EasyAuth provider when dual-provider mode is off.
  - Safe default is `externalid` for customer-facing auth.
- `WriterApp:Auth:CustomerLoginProvider`
  - Customer provider route segment used in dual-provider mode.
- `WriterApp:Auth:InternalLoginProvider`
  - Internal/admin provider route segment used in dual-provider mode.
- `WriterApp:Auth:UseDualProviderMode`
  - `false`: single provider mode
  - `true`: separate customer and internal login choices

Related informational settings still present in startup:
- `WriterApp:Auth:UseExternalIdAuth`
- `WriterApp:Auth:ExternalIdTenantId`
- `WriterApp:Auth:ExternalIdClientId`

These are used by startup validation/diagnostics in `Program.cs`. They are not an app-managed OIDC implementation.

### Bootstrap admin settings
- Primary bootstrap config: `BOOTSTRAP_ADMIN_USER_ID`
- Transitional fallback only: `BOOTSTRAP_ADMIN_OID`

Long-term preferred admin model:
- Persist `AdminRoleAssignment.UserId` rows
- Keep bootstrap only as a narrow emergency/admin-setup path

### Example appsettings shape
```json
{
  "WriterApp": {
    "Auth": {
      "LoginProvider": "<customer-provider-name>",
      "CustomerLoginProvider": "<customer-provider-name>",
      "InternalLoginProvider": "<internal-provider-name>",
      "UseDualProviderMode": true
    }
  }
}
```

### Example Azure App Settings
```text
WriterApp__Auth__LoginProvider=<customer-provider-name>
WriterApp__Auth__CustomerLoginProvider=<customer-provider-name>
WriterApp__Auth__InternalLoginProvider=<internal-provider-name>
WriterApp__Auth__UseDualProviderMode=true
BOOTSTRAP_ADMIN_USER_ID=<canonical-admin-user-id>
```

## Canonical Identity Mapping Rules

### Legacy workforce users
- If the EasyAuth claims contain `oid` or Microsoft objectidentifier, the app keeps using that value as `UserId`.
- This preserves existing `UserProfiles`, entitlements, admin assignments, and deleted-user tombstones keyed by legacy OID-based ids.

### External ID customer users
- If no `oid` is present, but both `iss` and `sub` are present, the app builds:
- `extid:{escaped-normalized-issuer}:{escaped-subject}`

The implementation lives in `Application/Security/ExternalIdentityClaims.cs`.

### Fallback
- If neither of the above exists but `sid` exists, the app falls back to `sid`.
- The app does not use email as an identity key.

## Provisioning Flow
1. User clicks `Start free` or `Sign in`.
2. Client builds `/.auth/login/<provider-name>` through `EasyAuthUrlBuilder`.
3. App Service Authentication completes the interactive auth flow.
4. EasyAuth injects `X-MS-CLIENT-PRINCIPAL`.
5. `EasyAuthAuthenticationHandler` maps claims into the app principal.
6. Client or protected route calls `GET /api/auth/me`.
7. `/api/auth/me` resolves canonical `UserId`, blocks tombstoned users, provisions profile and entitlements, and returns app auth state.
8. `WriterApp.Client/Pages/Start.razor` routes:
   - new free user with no projects and incomplete onboarding -> `/app/onboarding`
   - returning user -> requested return URL
   - paid plan upgrade path -> checkout
   - deleted user -> deleted-account UX

## Migration And Coexistence Options

### Option A: Dual-provider coexistence
- Keep current workforce provider for internal users.
- Add External ID customer provider for new users.
- Enable `UseDualProviderMode=true`.
- Point customer CTAs to `CustomerLoginProvider`.
- Point internal/admin CTAs to `InternalLoginProvider`.

Use this when:
- existing internal users must keep their current data and admin assignments without relinking
- customer auth is being introduced without forcing existing identity migration

### Option B: Single customer provider cutover
- Set `UseDualProviderMode=false`.
- Point `LoginProvider` at the customer provider.

Use this only when:
- you accept that identities may change across providers/tenants, or
- you have a separate explicit user relink/data migration plan

## Identity Continuity Risk
- Identity continuity across tenants/providers is the main migration risk in this app.
- Existing workforce users are currently keyed by legacy OID-based `UserId`.
- New customer identities are keyed by issuer plus subject.
- The app does not automatically merge old and new identities for the same human.

If a workforce user later signs in through a different External ID identity:
- they will resolve to a different `UserId`
- existing data will not follow automatically
- admin assignments and deleted-user tombstones will also remain tied to the old `UserId`

## Deleted Users After Migration
- Deleted identities remain keyed by canonical `UserId`.
- `DeletedUserIdentityService` still blocks access before onboarding or normal app use.
- Deleted users should see the deleted-account UX, not onboarding.
- This behavior applies equally to:
  - legacy OID-based users
  - new External ID issuer+subject users

## Admin Behavior After Migration
- Persisted `AdminRoleAssignment.UserId` remains the preferred admin mechanism.
- The resolver still supports the legacy external `Admin` claim compatibility path already present in code.
- Bootstrap fallback is now generic:
  1. persisted `AdminRoleAssignment.UserId`
  2. legacy external `Admin` claim compatibility
  3. `BOOTSTRAP_ADMIN_USER_ID`
  4. transitional `BOOTSTRAP_ADMIN_OID`

## Troubleshooting

### Sign-in link goes to the wrong provider
- Check `WriterApp:Auth:LoginProvider`
- Check `WriterApp:Auth:CustomerLoginProvider`
- Check `WriterApp:Auth:InternalLoginProvider`
- Check `WriterApp:Auth:UseDualProviderMode`
- Verify the App Service provider name matches the route segment exactly

### EasyAuth login succeeds but the app still treats the user as anonymous
- Verify App Service is injecting `X-MS-CLIENT-PRINCIPAL`
- Check `EasyAuthAuthenticationHandler`
- Check startup auth configuration in `Program.cs`
- Inspect `/api/auth/me` response

### New customer signs in but gets a different account than expected
- Check the claims in the EasyAuth principal
- Confirm whether the identity resolved via legacy `oid` or `extid:{issuer}:{sub}`
- This is usually an identity continuity issue, not a provisioning bug

### Deleted user reaches onboarding or normal app routes
- Check `/api/auth/me` response for `403` and `code=account_deleted`
- Check `/api/onboarding/state`
- Check client deleted-account state handling in:
  - `WriterApp.Client/State/AuthMeStateService.cs`
  - `WriterApp.Client/Services/OnboardingService.cs`
  - `WriterApp.Client/Pages/Start.razor`

### Admin bootstrap stopped working
- Prefer `BOOTSTRAP_ADMIN_USER_ID`
- Verify the configured value matches the actual canonical `UserId`
- If still using `BOOTSTRAP_ADMIN_OID`, confirm the session is resolving as a legacy OID-based identity

### Customer sign-in loops or callback fails
- Verify App Service Authentication provider setup
- Verify the app registration redirect URIs configured in Azure
- Verify the generated login route uses the expected provider name
- Verify `post_login_redirect_uri` points back to the correct app host

## Rollout
1. Configure the new customer provider in a staging App Service.
2. Set staging auth config to the new provider names.
3. Verify:
   - customer login route
   - `/api/auth/me` provisioning
   - onboarding first-run path
   - deleted-user behavior
   - admin behavior
4. If dual-provider mode is needed, enable it before production rollout.
5. Switch public `Start free` / `Sign in` entry to the customer provider only after staging verification.

## Rollback
- Rollback is primarily configuration-based.
- Restore prior App Service provider configuration.
- Restore prior auth route config:
  - `WriterApp:Auth:LoginProvider=aad`
  - `WriterApp:Auth:CustomerLoginProvider=aad`
  - `WriterApp:Auth:UseDualProviderMode=false`
- If dual-provider mode caused issues, disable it and return to the previous single-provider route.
- No schema rollback is required for the runtime identity-resolution changes already in this repo.
- Note that user rows created under the new canonical `extid:{issuer}:{sub}` format will remain in the database unless explicitly cleaned up later.
