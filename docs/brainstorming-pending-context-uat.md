# Brainstorm pending writing blocks Send — root cause and fix

Date: 2026-10-08. Author flow: retained composer → Prepare context → explicit
Save, sync & prepare confirmation → checked context → explicit Send.

## Root cause

The latest screenshot's message comes from `DeviceBrainstormingContext.PrepareServerAsync`:
it rejects `LocalEditorSession.IsDirty` or any sync state other than `Synced`.
The desktop preparation handler flushed editor DOM but never committed pending
writing or synchronized it. Successful background sync could also leave the open
editor's acknowledgment metadata behind the repository. The earlier fixes addressed
the local/server preparation default and same-account token invalidation, but their
desktop success fixture began with already synchronized writing and a version-zero
DOM snapshot. They did not exercise this pending-writing path.

## Correction

Desktop server preparation now requires a destination-specific confirmation inside
the workspace. Clicking Prepare first displays the configured backend and explains
that synchronization covers enabled documents. Only **Save, sync & prepare context**
runs the existing manuscript/planning save, recovery journal and cloud sync flow.
Cancel retains the writing and composer. Preparation never submits an AI turn.
Automatic approval review rejected adding upload to the old unqualified Prepare
action; the explicit confirmation is the accepted implementation.

Preparation refreshes metadata before and after save/sync, adopting it only when
the existing writing fingerprint matches. It rechecks document/editor/page, writing
and account identity, then uses the existing exact server-source checks. It never
substitutes older cloud prose or resolves conflicts automatically. Save failures,
offline state, rejected uploads, concurrent typing, conflicts and remote deletion
retain the local writing and message and disclose the blocker. A local-only document
stays local-only: enable its ordinary cloud sync controls explicitly before retrying.

The Chat/Context/History design, same-account notification correction, source choices,
reviewed destinations, recovery history and unrelated uncommitted work are retained.

## Verification

The mounted production workspace regression now starts with pending rich Unicode
DOM text at an advancing editor version. It invokes the actual desktop preparation,
save and pre-send handlers, real autosave/recovery store, sync journal/engine/API,
`DeviceAuthenticatedHandler`, sync v5 controllers and Brainstorm controllers on a
disposable migrated SQLite TestServer. Token issuance, server identity/entitlement
and AI provider are deterministic substitutes; no external requests or user writing.

It checks no save/provider dispatch before confirmation, cancellation preservation,
confirmed save/upload/acknowledgment/context readiness, retained composer and scope,
then exactly one explicit deterministic Send. Sign-out, reauthentication, rejected
token and revoked-source invalidation still pass. Eight additional sync preparation
cases cover pending and already acknowledged writing plus offline, conflict,
rejection, save failure, in-flight typing and local-only failures.

Evidence: `artifacts/brainstorming-p12-pending-context`, with focused handler evidence
in `artifacts/brainstorming-pending-context-focused.log`. Final verification:

- 840 combined regressions and 14 existing device authentication checks passed.
- Ten production module/Razor browser suites passed in Edge; the rebuilt LocalDev
  web Chat/Context/History journey passed three groups with zero generation requests.
- Server/client Release and isolated/normal desktop Debug builds passed with zero
  warnings/errors. Isolated Windows startup initialized only disposable test data.
- Normal and isolated desktop device/UI assemblies match: device SHA-256
  `AC77659D88C95878FF4B3CBE4B42030D0E60E59779504CB4445E8919EFD55044`, UI SHA-256
  `7516F5C4520FA28820BC333CBF0380FD17CA3330C6C2A37D2423FB5C1E76A1AD`.
- Whitespace checks passed; all 195 initial Git status entries remain present.
  No commit/reset/clean, user manuscript operation, live provider dispatch or user
  process shutdown occurred. Only owned validation processes were closed.

Actual signed-in native/WebView interaction, live MSAL/provider, physical device/web
exchange, SQL mutation and English/Swedish microphone/playback remain separate gates.
An isolated native startup and mounted production handlers do not prove those gates.

## Hands-on continuation

Restart the rebuilt normal Debug Prosa desktop executable, reopen the saved
conversation and keep the typed message. Click Prepare context for sending, inspect
the displayed backend, then click Save, sync & prepare context. Once Context ready
appears, use Send message. If preparation reports offline/conflict/storage/account
trouble or local-only writing, resolve that named condition in the ordinary controls
and retry; do not resend unknown operations or discard the manuscript.
