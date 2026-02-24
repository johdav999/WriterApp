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
  - production defaults to Azure host URL
- Restart `npm run dev` in `Prosa.Landing` after env var changes.
