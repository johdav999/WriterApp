# Auth And Public Link Flows

## Public routes and SPA redirects
- Public top-level links are handled by server redirects:
  - `/login` -> `/app/login` (302, query preserved)
  - `/start` -> `/app/start` (302, query preserved)
  - `/billing/checkout` -> `/app/billing/checkout` (302, query preserved)
  - `/logout` -> `/app/logout` (302, query preserved)

## Safe `returnUrl` rules
- All client flows that consume `returnUrl` validate it as a safe relative path.
- Allowed: starts with `/`.
- Rejected:
  - starts with `//`
  - contains `://`
  - contains `\`
- Fallback:
  - login/start/billing/redirect-to-login -> `/projects`
  - logout -> `/`

## Login behavior
- Local development (FakeAuth):
  - `/app/login` does not call Easy Auth.
  - It immediately navigates to validated `returnUrl`.
- Azure / non-development (Easy Auth):
  - App Service Authentication should own interactive redirects for protected routes.
  - The WASM client must not auto-navigate to `/.auth/login/*`.
  - `/app/login` can render a manual sign-in link:
    - `/.auth/login/aad?post_login_redirect_uri=<absolute-url>`
  - Absolute URL is built from current origin + validated `returnUrl`.

## Logout behavior
- Local development:
  - `/app/logout` immediately navigates to validated `returnUrl` (fallback `/`).
- Azure / non-development:
  - `/app/logout` calls:
    - `/.auth/logout?post_logout_redirect_uri=<absolute-url>`
  - Absolute URL is built from current origin + validated `returnUrl`.

## Azure App Service auth settings for `/app/*`
- Authentication: **On**
- Identity provider: Microsoft (AAD / External ID, matching your tenant setup)
- Unauthenticated requests: **HTTP 302 redirect to identity provider**
- Session/token store: default App Service settings are fine
- Auth probe contract:
  - `GET /api/auth/me` must return `200` for both authenticated and anonymous callers.
  - Anonymous response must be `{ isAuthenticated: false }` shape (no `401` for probe).
  - Client probe endpoints must stay passive: no automatic navigation/reload on probe failures.
  - Platform-led auth only: app code must not automatically navigate to `/.auth/login/*`.

### Practical scoping notes
- App Service EasyAuth is primarily app-level, not a full path-by-path policy engine in all portal flows.
- Recommended for this app:
  - protect the Writer app host globally (Require authentication),
  - serve truly public marketing pages from a separate host/static site, or
  - keep public endpoints outside the protected app surface and avoid mixing public + private UX under one EasyAuth-protected app unless you accept global auth behavior.

## Auth redirect regression guardrail
- PowerShell: `./scripts/check-no-forced-auth-nav.ps1`
- Bash: `./scripts/check-no-forced-auth-nav.sh`
- CI recommendation: run one of these scripts on every PR/build and fail on non-zero exit.

## Landing page CTA links (`Prosa.Landing`)
- `NEXT_PUBLIC_APP_URL` controls where marketing CTA links point (`/login`, `/start?...`).
- Local default should include the WriterApp port, for example:
  - `NEXT_PUBLIC_APP_URL=http://localhost:5387`
- If not set:
  - development defaults to local host URL with port
  - production defaults to https://app.prosa-app.com
- Restart `npm run dev` in `Prosa.Landing` after env var changes.

## Stripe configuration contract
- The server reads Stripe config from one logical options source: `StripeOptions`.
- Stable webhook endpoint path: `POST /api/stripe/webhook`.
- Preferred Azure App Settings names (recommended):
  - `Stripe__Mode` (`test` or `live`)
  - `Stripe__SecretKey`
  - `Stripe__WebhookSecret`
  - `Stripe__PriceStandard`
  - `Stripe__PricePro`
  - `Stripe__SuccessUrl` (optional, default `/app/account?billing=success`)
  - `Stripe__CancelUrl` (optional, default `/app/account?billing=cancel`)
  - `Stripe__BillingPortalReturnUrl`
- Also supported (fallback) for compatibility:
  - `WriterApp__Stripe__*`
  - flat env vars like `STRIPE_MODE`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STANDARD`, `STRIPE_PRICE_PRO`, `STRIPE_SUCCESS_URL`, `STRIPE_CANCEL_URL`, `STRIPE_BILLING_PORTAL_RETURN_URL`

### Startup validation behavior
- Development:
  - if `Stripe__SecretKey` is missing, Stripe is disabled and startup continues.
- Non-development:
  - missing required Stripe values causes startup failure with clear error messages.
- Safety rails:
  - `Stripe__Mode=live` rejects test secret keys (`sk_test_...` / `rk_test_...`).
  - `Stripe__Mode=test` rejects live secret keys (`sk_live_...` / `rk_live_...`).
- When Stripe is enabled, required values are:
  - `Mode` (`test|live`)
  - `SecretKey`
  - `WebhookSecret`
  - `PriceStandard`
  - `PricePro`
  - `BillingPortalReturnUrl`

### Setup steps (test mode)
1. In Azure App Service (or local environment), set:
   - `Stripe__Mode=test`
   - `Stripe__SecretKey=<your test secret key>`
   - `Stripe__WebhookSecret=<your test webhook signing secret>`
   - `Stripe__PriceStandard=<test price id>`
   - `Stripe__PricePro=<test price id>`
   - `Stripe__BillingPortalReturnUrl=/app/account`
2. Optionally set:
   - `Stripe__SuccessUrl=/app/account?billing=success`
   - `Stripe__CancelUrl=/app/account?billing=cancel`
3. Restart the app and verify startup logs show `Enabled=true` and `Mode=test`.

### Setup steps (live mode)
1. Replace test values with live Stripe values:
   - `Stripe__Mode=live`
   - live secret key, live webhook secret, live price IDs
2. Confirm `Stripe__BillingPortalReturnUrl` points to your production return path.
3. Restart and verify startup logs show `Enabled=true` and `Mode=live`.

Security note:
- Do not commit Stripe keys or webhook secrets to source control.

### Billing status endpoint
- `GET /api/billing/status` returns Stripe configuration status (boolean-only signal for keys/prices):
  - `mode`
  - `enabled`
  - `keysPresent`
  - `standardPriceConfigured`
  - `proPriceConfigured`

## Stripe CLI local webhook workflow
Use this to test billing webhook handling locally without deploying.

### Prerequisites
1. Install Stripe CLI and sign in to your Stripe account.
2. Run WriterApp locally (`http` or `https` launch profile).
3. Set local Stripe env vars (`Stripe__Mode=test`, test secret key, test price ids).

### Commands
1. Log in:
   - `stripe login`
2. Start webhook forwarding (example uses local HTTP profile to avoid TLS cert issues):
   - `stripe listen --forward-to http://localhost:5387/api/stripe/webhook`
   - if you run HTTPS locally instead: `stripe listen --forward-to https://localhost:7384/api/stripe/webhook`
3. Copy the signing secret printed by `stripe listen` (`whsec_...`) and set it as:
   - `Stripe__WebhookSecret`
4. Trigger a test event:
   - `stripe trigger checkout.session.completed`

### Local user flow checklist
1. Start WriterApp in `Development` and confirm Stripe is enabled in logs.
2. Open the app, sign in with local dev auth, and go to billing page.
3. Click `Upgrade to Standard` or `Upgrade to Pro` and verify redirect to Stripe Checkout.
4. Complete checkout with Stripe test card.
5. Confirm webhook request arrives in the Stripe CLI terminal and returns `200`.
6. Verify entitlement changes in app (`/api/auth/me` should reflect new plan/status).
7. Optionally trigger more events for regression checks:
   - `stripe trigger customer.subscription.updated`
   - `stripe trigger invoice.paid`
   - `stripe trigger invoice.payment_failed`

## Admin Audit Events migration
- Runtime writes to `AdminAuditEvents` for admin actions (plan override, user ops, token ops, sync actions).
- If you see `SQLite Error 1: 'no such table: AdminAuditEvents'`, apply pending EF migrations.

### Local
1. Run: `dotnet ef database update --project BlazorApp.csproj`
2. Confirm startup logs show migration state when `Admin:EnableAdminApi=true`:
   - `Admin API migration check. CurrentMigration=..., PendingCount=...`

### Azure App Service
1. Preferred: call the guarded migrate endpoint (staging/admin only):
   - `POST /api/admin/db/migrate`
   - include `X-DB-MIGRATE-KEY` if `DB_MIGRATE_KEY` is configured.
2. Database path is typically `/home/site/data/writerapp.db`; the migration must be applied to that deployed file.
3. After migration, verify logs show:
   - `Admin API migration check... PendingCount=0`

