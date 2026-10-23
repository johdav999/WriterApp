# Chrome storage recovery and Brainstorm recheck

Session: 2026-10-22, localhost app 5399 / collaboration worker 1234.
Evidence: `artifacts/chrome-storage-5399/`. Real accounts use Chrome (Editor)
and the in-app browser (Owner), manuscript `2921410a` / section `acebf857`.
Starting manuscript: the existing 171-word story, plus the previously retained
empty chapter. The baseline HTML is preserved in this session's `baseline.json`.

## Root cause and repair

The previous generic open-error handler did not retain Chrome's original
DOMException category. Its original OS/browser trigger cannot be reconstructed
from that message. The origin's LevelDB LOG/LOG.old showed ordinary manifest/log
recovery, not a reported corruption. No browser data was cleared or modified to
work around the failure.

The application had two concrete connection-lifetime defects:

1. `IndexedDbJournal` created one readonly database-opening Promise in its
   constructor. A failed open permanently rejected that Promise. Every subsequent
   operation on that journal reused the failure; a closed connection likewise
   could not reopen. This explains why the editor stayed unusable after storage
   reads elsewhere recovered, until the page was reopened.
2. `brainstorming-store.js` opened and closed a database on every operation,
   including repeated account-identity checks, reads and conversation checkpoints.
   It had no recovery for a transient pre-transaction open failure.

Both now use `indexeddb-connection.js`. It reuses a healthy connection, releases
it on version change, detects browser closure, drops failed opening promises, and
reopens before a transaction starts. UnknownError/AbortError opening failures have
two bounded retries. A synchronous closed-connection exception before transaction
creation can reconnect once. Started transactions, quota/security/version errors,
and blocked upgrades are never automatically replayed. Closing a journal cancels
future opens; late successful blocked opens are closed.

## Issue ledger

| ID | Severity | Finding | Acceptance / regression | State |
|---|---|---|---|---|
| STORAGE-CONNECTION | P1 | Failed/closed journal connection is permanently cached; Brainstorm repeatedly opens/closes connections. | Transient open/forced close recover before exactly one write; data and schema survive; started write abort is never replayed. | Verified in deterministic Node and isolated real browser checks |
| REPLAY-READY | P2 | Rapid Undo after Keep can run before the replacement shared editor reconnects, producing “Wait for shared writing to save.” | Keep/replay remains busy until the current mounted writer is ready; retired callbacks cannot satisfy readiness. | Verified: three rapid Undo/Redo cycles and full browser flow passed |
| UAT-TIMEOUT | P2 | An earlier repeated planning run timed out at CharacterNote. The hidden default Blazor error UI in its HTML was insufficient evidence of a renderer exception. | Capture WASM console exceptions in addition to pageerror; complete all planning/replay checks. | Diagnostic full flow passed with no captured exceptions; original timeout not reproduced |
| UAT-AUTH | prerequisite | Both real account sessions needed normal authentication refresh during verification. | Refresh normal authentication, preserving account separation and manuscript identity. | User refreshed both; Chrome confirmed as Editor and in-app browser as Owner on the original manuscript |

## Completed checks

- TypeScript collaboration check and web/device editor bundle builds passed.
- **17 Node tests passed**: connection reuse, bounded open recovery, failed-promise
  invalidation, forced close, close-before-transaction race, quota/security/version
  rejection, no write replay, version-change/close handling and error classification.
- The isolated real-browser storage check passed **six assertions**. Two injected
  UnknownError open failures produced exactly one durable write. A closed database
  reopened without reloading. Two hundred concurrent transactions reused the same
  connection. An explicitly aborted started write was not replayed. Original data
  survived closing/reopening with its existing schema.
- **22 compiled Brainstorm host/preparation tests passed** (`host-tests.log`).
- The full isolated WASM browser flow passed (`isolated-diagnostic-flow/result.json`):
  both directions of live draft publication, private composer retention, Keep,
  three rapid Undo/Redo cycles, manuscript and conversation reload, all four
  planning destinations, retained receipts, inspection and guarded Undo. The
  fixture uses real isolated Kestrel/SQLite/Node services and synthetic accounts
  with deterministic AI. No browser or Blazor renderer exceptions were captured.
- Actual provider requests completed for an amber-window paragraph (Editor),
  a blue-boat paragraph (Owner), and the owner's bronze-lantern revision.
  Publication reached the already-open recipient in both directions, preserving
  the other account's private draft/chat and unsent composer. The final owner
  revision was verified live in Chrome after both sessions were refreshed.

Isolated synthetic accounts and deterministic provider tests are kept separate
from the real signed-in/provider acceptance.

## Completed real-account acceptance

`real-account-result.json` records the completed flow on port **5399**. Chrome
remained the Editor account and the in-app browser the Owner account on the same
original manuscript.

- Editor's amber-window draft was reviewed and kept. Owner's already-open normal
  manuscript received it without reload (`owner-live-keep.png`). Guarded Undo and
  Redo both updated the owner live. Chrome reload retained the kept prose and
  reopening the exact private conversation retained the draft and composer.
- StoryNote, SynopsisField (Logline), and CharacterNote (Emma) were each reviewed,
  approved, inspected, and confirmed in the owner's actual Synopsis fields.
  StoryNote's committed receipt survived Chrome reload and was inspected again.
- SceneIdea was reviewed and saved under the **pre-existing** New Chapter.
  Owner's outline displayed `Storage verification scene` without creating prose
  (`owner-scene-idea.png`). Inspection succeeded, then guarded Undo removed it.
- All four planning operations finished **Undone**. Owner's reloaded Notes and
  Logline were empty again. The final Keep Undo restored the original story.
- Both final editor HTML values exactly matched their baseline after excluding
  remote-caret decorations (`final-content-checks.json`). Both were editable,
  showed **Live / 2 people**, and reported **Saved to shared manuscript** with
  **171 words**. Reloading both browsers preserved that exact original content.
- No captured Chrome or in-app-browser console errors remained. Some UI checks
  exceeded the automation API's short wait, especially the scene commit; its
  retained receipt subsequently confirmed Committed. No uncertain operation was
  submitted again and no browser storage was cleared.

Both manuscript tabs remain open. The pre-existing empty New Chapter and private
test conversations/proposals remain. Final screenshots are
`final-chrome-manuscript.png` and `final-owner-manuscript.png`. This is local web
acceptance with real accounts and live provider generation, not hosted production
or native-device acceptance. The original OS/browser trigger behind the old
generic IndexedDB error is unknown; the reproducible application connection
lifetime defects are repaired and covered by recovery tests.
