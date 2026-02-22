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
  - `/app/login` calls:
    - `/.auth/login/aad?post_login_redirect_uri=<absolute-url>`
  - Absolute URL is built from current origin + validated `returnUrl`.

## Logout behavior
- Local development:
  - `/app/logout` immediately navigates to validated `returnUrl` (fallback `/`).
- Azure / non-development:
  - `/app/logout` calls:
    - `/.auth/logout?post_logout_redirect_uri=<absolute-url>`
  - Absolute URL is built from current origin + validated `returnUrl`.

## Landing page CTA links (`Prosa.Landing`)
- `NEXT_PUBLIC_APP_URL` controls where marketing CTA links point (`/login`, `/start?...`).
- Local default should include the WriterApp port, for example:
  - `NEXT_PUBLIC_APP_URL=http://localhost:5387`
- If not set:
  - development defaults to local host URL with port
  - production defaults to Azure host URL
- Restart `npm run dev` in `Prosa.Landing` after env var changes.
