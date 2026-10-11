# Brainstorm preparation resets the conversation — root cause and fix

Date: 2026-10-08. Scope: Windows Author creates/reopens a conversation, types,
prepares checked context, then explicitly sends. Shared Chat/Context/History design,
uncommitted P1–12 and unrelated work preserved in the same checkout.

| Issue | Severity | Expected / observed | Verification |
|---|---|---|---|
| BS-02 | P1 | Preparation should retain the active conversation and message and enable Send when checks pass. Desktop instead returned to Start a conversation. | Reproduced before correction through the real device authentication handler; corrected flow and genuine invalidation cases pass. |

## Root cause

`DeviceAuthenticatedHandler` calls `DeviceAccountService.GetTokenAsync` before each
authenticated request. The account service invokes `Changed` even on successful
silent acquisition for the **same** account/generation. `DeviceBrainstormingSessions`
forwarded every account notification as `IBrainstormingReviewIdentity.Changed`.
The shared workspace, composer and context components interpret this event as loss
of the current private review: they clear active scope and text and return to the
welcome screen. Thus an ordinary context HTTP request invalidated its own workspace.

The earlier Send fix corrected context selection but did not cover this boundary.
Its desktop adapter test used `TestServer.GetTestClient` with a synthetic server
identity, bypassing `DeviceAuthenticatedHandler`. The new regression retains that
real handler and token-acquisition path around actual server controllers and mounted
production components. Token issuance/server identity/provider remain deterministic
test substitutes; this is not live MSAL or external-provider acceptance.

## Correction

Device Brainstorm now emits review-change notifications only when its canonical
identity changes: backend/account, account generation or access-revocation epoch.
Same-account token acquisition and display/status updates retain the workspace.
Subscription lifetime follows the mounted listeners. Actual sign-out, explicit
reauthentication, rejected credentials and revoked source access still invalidate
private review. Access revocation updates the identity before notification, so a
subsequent token acquisition does not repeat the invalidation.

No authentication bypass, automatic send, manuscript replacement, provider setting,
schema/migration, sync protocol or UI redesign was introduced.

## Evidence and verification

- `artifacts/brainstorming-prepare-reset/before-fix.log`: the new mounted Windows
  preparation flow fails because the selected conversation becomes null.
- **832 combined regressions passed**, zero failed/skipped, in
  `artifacts/brainstorming-p12-prepare-reset/combined-regressions.trx`.
  Four new actual-handler cases preserve scope/composer, prepare checked mapped
  context and perform one explicit deterministic generation. They separately verify
  sign-out, reauthentication, rejected credentials and revoked ownership; manuscript
  HTML remains unchanged and preparation makes zero provider calls.
- **14 existing device authentication tests passed** separately.
- Ten browser module/render suites passed. The old static workspace fixture was
  missing the enclosing app height after the sidebar redesign; bounding its test
  container to the viewport restores realistic resizing. No production resize or
  layout code changed.
- Rebuilt running web editor checks pass in Edge 154.0.4258.62: retained Unicode
  draft across Chat/Context/History, preparation enables Send, reopen requires fresh
  review, wide/tablet/phone bounds, audio disclosure, zero generation requests,
  no page errors or failed stylesheets. Uses a fresh disposable SQLite fixture and
  existing LocalDev identity. Evidence: `live-web/result.json` and captures.
- Release server/client and isolated/normal Debug Windows builds have zero
  warnings/errors. Isolated native startup passes; only the owned preview closed.
  Normal desktop data configuration was retained, and no user manuscript was opened.
  Tested and normal `WriterApp.Device.Shared.dll` SHA256 match:
  `4A92836C83BE0A413C33E9DE88951313AEA16DF4EF4E7938B8063A889E32937B`.
- Whitespace and preservation checks pass. The owned web host stopped after matching
  its recorded PID/start time. No external AI request was made.

## Reproduction and handoff

Run `scripts/verify-brainstorming.ps1 -Phase p12 -EvidenceSuffix prepare-reset`
with the installed Playwright path, `-Browser -NativeStartup`. The new regression
is `MountedDesktopPreparationAndSendSurviveRealAuthenticatedHandlerTokenAcquisition`.
Run `DeviceAuthenticationTests` separately. For running web checks, export a fresh
acceptance database, start the existing acceptance helper on loopback 5407, then run
`Tests/brainstorming-redesign.mjs` with `BRAINSTORMING_WEB_SEED` pointing at that
export and the matching `BRAINSTORMING_WEB_URL`. Final evidence directory:
`artifacts/brainstorming-p12-prepare-reset`.

Restart the rebuilt normal desktop executable. If the previous active scope was
already cleared, use History to reopen the saved conversation and retained message,
then prepare context and explicitly send. Original save/sync/source/quota checks
still apply. Hands-on Windows, real account/provider, audio and remaining P12 gates
stay separate; startup and deterministic tests do not close them.
