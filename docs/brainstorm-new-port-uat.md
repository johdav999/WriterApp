# Brainstorm acceptance with the client served on port 5399

Date: 2026-10-21. Product: Prosa web, full WASM editor. Runtime: the local Staging launcher at `http://localhost:5399`, with its owned Node worker on 1235. Existing instance 5398/1234 and the user's manuscript are preserved.

## Verification boundaries

The browser regression loads the actual client assets from **localhost:5399**, then exercises production Brainstorm/sharing/Keep/planning controllers against an isolated migrated SQLite database, real Kestrel and Node collaboration service. Owner and editor have separate browser contexts and synthetic authentication. The visible application runs on the disposable proxy origin recorded as `manuscriptRoute` in each result packet. Only the external AI provider is deterministic. This does **not** establish real-account/provider acceptance against the Staging database on 5399.

The real-account tab on 5399 opens the normal sign-in screen. A fresh sign-in is required because the alternate port has its own session cookie. The user has been asked to sign in both accounts in separate profiles and open the same manuscript. No credentials, impersonation endpoints or authentication bypasses were added.

## Issue and acceptance ledger

| ID | Flow | Expected / observed | Evidence | Status |
|---|---|---|---|---|
| BS-5399-LIVE | Generate while the other account's Brainstorm is open | Completed validated prose appears automatically; private prompt/chat/composer remain private. | `artifacts/brainstorm-new-port-verified/{collaborator-draft,reverse-collaborator-draft}.png` | Verified in both directions |
| BS-5399-KEEP | Review placement, Keep, undo/redo, reload | Both accounts see generation replacements without reloading the recipient; original prose remains and the sender reload retains the kept prose. | `artifacts/brainstorm-new-port/{kept,owner-manuscript,editor-redone}.png`, `result.json` | Verified in the isolated browser flow |
| BS-5399-PLAN | Review/save/inspect/undo planning | StoryNote, SceneIdea, SynopsisField and CharacterNote all commit and undo; unrelated prose is retained. | `artifacts/brainstorm-new-port-verified/{planning-saved,planning-reloaded}.png`, `result.json`, focused test log | Verified, including receipt reload |
| BS-5399-REPLAY | Undo after reopening a saved conversation | Automatic context preparation left an unsynced local session revision. Planning Undo changed the server session before syncing that context; the following pull correctly retained both branches as a conflict and blocked the next save. The same ordering was present in Keep undo/redo. | Before: `artifacts/brainstorm-new-port-expanded/failure.png`. After: `artifacts/brainstorm-new-port-verified/result.json` | Fixed; full browser recheck passed |
| BS-5399-AUTH | Two real accounts on the running new-port server | Both accounts must sign in and open the same manuscript before real-account actions can be verified. | New Chrome tab at `/app/login?returnUrl=%2Fapp%2Fcollaboration` | Awaiting account sign-in |
| BS-UAT-RUNTIME | Browser test starts | Initial invocation could not resolve `playwright` from the collaboration package. Set `PLAYWRIGHT_MODULE` to the installed bundled runtime; no application/package change required. | First invocation failure, followed by passing full run | Resolved test configuration |
| BS-UAT-SELECTION | Reopen after reload | Selecting the last conversation option could reopen a newly created blank session. Capture the original session ID and select that exact ID. | `artifacts/brainstorm-new-port-fixed/failure.png`, updated browser probe | Resolved test selector |

## Current results

The baseline full browser journey passed in **3m40s**, with client assets from 5399 and no JavaScript page errors. **26** focused HTTP/backend regression cases also passed: publication/privacy/access, Keep placement and replay, all planning kinds, stale/revoked/viewer rejection, rollback, preserving later author changes, and send availability/rejection.

The request log contains deliberate 404 operation inspections before first acceptance and 403 rejections of retired collaboration sessions during generation replacement. The browser subsequently obtains current sessions and sees the replacement prose without reload. These are authorization/idempotence protocol outcomes, not failed planning or Keep writes. The baseline has no server 5xx responses. Do not remove those generation/access fences to suppress network entries.

The expanded probe adds publishing in both directions, preservation of both private composers, and a planning receipt reload before inspection/undo. Its result is retained separately in `artifacts/brainstorm-new-port-expanded/`. The probe now supports `PROBE_EVIDENCE_DIR`, records its asset origin, and writes a failed result packet if the journey fails instead of leaving a misleading `running` marker.

Run with the installed Playwright package path in `PLAYWRIGHT_MODULE`:

```powershell
$env:WRITERAPP_COLLAB_BROWSER_TESTS='1'
$env:PROBE_CLIENT_URL='http://localhost:5399'
$env:PROBE_EVIDENCE_DIR='artifacts/brainstorm-new-port-expanded'
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-build --no-restore `
  -p:BaseOutputPath=artifacts/local-port-tests/bin/ `
  -p:BaseIntermediateOutputPath=artifacts/local-port-tests/obj/ `
  --filter FullyQualifiedName~NormalProsaBrainstormDraftKeepPlanningUndoRedoAndReload
```

## Reload and replay repair

The expanded journey reproduced **BS-5399-REPLAY** after successfully reloading a committed StoryNote receipt. Reopening automatically calls `RememberPreparationAsync`, which appends fresh context and marks the private session `PendingSave`. Planning Undo previously skipped the conversation sync used by Apply. Its server-side `Record` advanced the session revision; the subsequent `PullAsync` saw both the unsent context and the changed server revision and correctly recorded a retained conflict. The next SceneIdea capture was blocked with “Both branches are retained.” No planning fields or manuscript prose were lost.

Planning Undo and Keep undo/redo now checkpoint private proposal edits and synchronize the retained conversation **before** changing the approved server save. The normal conflict and identity checks still apply. Read-only inspection stays available without forcing a new conversation write, preserving recovery of uncertain outcomes. The browser regression now reloads before both planning Undo and Keep undo/redo. The rebuilt focused suite passed **45** cases (`artifacts/brainstorm-new-port-replay-fix-tests.log`), including persistence and explicit competing-branch protection.

The corrected-selector run completed all expanded UI assertions, including the reproduced Undo failure, with **26 successful Keep/planning write requests**, zero failed reviewed writes, zero HTTP 5xx responses, zero Brainstorm session 409 conflicts and zero JavaScript page errors (`artifacts/brainstorm-new-port-final/request-summary.json`). The runner then hit its original five-minute process deadline after printing that its disposable services had stopped. The expanded fixture now owns and destroys its upstream HTTP agent, captures remaining Node resource types at cleanup, and has a bounded six-minute deadline for the longer journey and socket shutdown. No forced successful process exit is used. Final runner completion is tracked in `artifacts/brainstorm-new-port-verified-browser.log`.

## Final verified result

The rebuilt server/client is running on **5399**, with worker **1235**, launcher PID **52688**. The original 5398 instance remains running. The expanded browser test completed successfully with **exit 0**, including natural process cleanup, in less than five minutes including its incremental build. All 14 flow assertions passed, including publication in both directions, private composer isolation, automatic recipient Keep/replay updates, persistence, all four planning destinations, planning Undo after reload and Keep undo/redo after a separate reload. The final packet is `artifacts/brainstorm-new-port-verified/`; `verification-results.json` records the reviewed-write audit and runtime health. The user's database and manuscript were not used for synthetic test writes.

The preceding server/PID and sign-in status describe the 2026-10-21 run. On
2026-10-22 both real accounts signed in on 5399, and acceptance continued with
actual provider generation, live sharing, Keep and planning saves. See
[the real-account report](brainstorm-real-accounts-5399-uat.md) for current runtime
details, evidence and additional repairs; the deterministic result above remains
separate from those live-provider checks.

Related diagnosis and implemented fixes: [shared saves](brainstorm-shared-saves-uat.md), [live draft and generation refresh](brainstorm-live-updates-uat.md), and [port selection and startup](local-collaboration-test.md).
