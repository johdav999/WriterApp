# Brainstorm Send button fix

Date: 2026-10-08. Author flow: open a conversation, type a message, prepare its
current destination/context, explicitly send. Same uncommitted WriterApp checkout;
all P1–12 and unrelated work preserved.

Follow-up: this run missed the real desktop authentication notification boundary.
Ordinary same-account token acquisition could still reset the active workspace.
See [preparation reset root cause](brainstorming-prepare-reset-uat.md) for the
subsequent correction and actual-handler regression. Evidence below is historical.

## Defect and correction

The screenshot shows nonempty text and disabled Send, with the context controls
outside the crop. It alone cannot identify every blocker. Production investigation
confirmed that Windows' primary Prepare action used local review context. That
context intentionally carries a server-authorization limitation, so it cannot
make the shared send-readiness check succeed. The separate server action could
prepare send-ready context, but the composer did not explain that dependency.
Send was also below both speech control groups.

The primary context action now prefers the existing authenticated server preparation
delegate when supplied. Explicit offline local review remains available. It does
not replace newer local writing: save/sync, identity, source equality, reviewed
source choices, budgets and target checks remain required. A failed refresh revokes
previous readiness before reading new context.

Both hosts place Send, Stop and inspection immediately after the composer. When
context is required, **Prepare context for sending** invokes the same destination
and source choices. It saves the composer and prepares context without sending a
message, generating prose, applying writing or requesting microphone access.
Disabled Send now discloses context, capability, pending-operation, dictation or
empty-message blockers beside the action. Preparation failures show their actual
save/sync or review reason. Send and Retry use the same eligibility check.

## Verification

Evidence: `artifacts/brainstorming-p12-send-fix/verification-summary.json`.

- **828 passed, zero failed/skipped**, including three new shared component cases:
  checked-server preference, retained typed text on success/failure, no automatic
  dispatch, readiness revoked after failed refresh, and explicit offline review.
  Existing actual Windows/web host-handler tests exercise deterministic dispatch,
  source mapping and pending-writing refusal.
- Ten Edge 154.0.4258.62 module/render suites pass. Keyboard expectations reflect
  the actions now preceding audio. Synthetic microphone verification waits for
  actual worklet frames rather than assuming audio has started within 400 ms.
- **13 actual server/WASM UI checks pass**, using the existing disposable LocalDev
  identity and SQLite fixture. Typing before review shows the blocker; adjacent
  preparation enables Send and retains multiline Unicode text at wide/390px sizes.
  It produces zero turns and zero conversation-stream requests. Checked Chapter 2
  sources, synthetic proposal discovery/editing, real Keep/Undo/Redo/Notes receipts,
  offline composer, reload/reopen and standalone isolation also pass. Served WASM
  hashes match the rebuilt outputs; no page errors or failed stylesheets.
- Release server/client and isolated Debug Windows builds pass with zero warnings
  or errors. Rebuilt isolated Windows startup passes. The pre-existing user PID
  observed during startup was preserved; only the owned preview was closed.
- Standard Windows Debug output is also rebuilt with normal user-data configuration:
  `WriterApp.Desktop/bin/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
  Its shared UI matches the tested native UI SHA256
  `44426EE87DE51356AEE8274053C3800D21E55282D54016256635354A6973CE9D`.
  No user manuscript was opened or changed. Restart this executable to use the fix.
- Whitespace verification passes. Both owned loopback hosts were stopped after
  matching their recorded PID/start time. Existing user processes were not stopped.

The first broader web attempt reached the successful Send flow and application
checks but switched offline before the planning host finished its document reload
and workspace remount. Its evidence remains in `live-web-attempt-1`. The runner
now waits for the actual reopened, editable composer before offline acceptance;
the final fresh fixture passes the full journey. No production recovery guard was
relaxed to make this check pass.

## Reproduction and handoff

Run `scripts/verify-brainstorming.ps1 -Phase p12 -EvidenceSuffix send-fix` with
`-Browser`, the installed Playwright module path, and `-NativeStartup`. Export a
**fresh** database with `BrainstormingAcceptanceTests` and
`WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB`; use the launch helper with this evidence
directory and `Tests/brainstorming-live-web.mjs` with `BRAINSTORMING_LIVE_AI=0` and
the corresponding rebuilt framework path. Exact final database, host hashes,
receipts, images and browser configuration are retained in the evidence directory.

The fix is implemented and verified locally. Live external provider dispatch and
hands-on Windows interaction were not performed. Previous P12 provider approval,
human microphone/playback, real account/device exchange and SQL mutation acceptance
gates remain open. This follow-up does not make the whole feature release-ready.
No schema, migration, sync protocol, provider configuration or editor bundle change.
Continue from the current implementation-status handoff in the same checkout.
