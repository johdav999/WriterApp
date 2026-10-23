# Brainstorm acceptance with two real accounts

Local acceptance on 2026-10-22, app `http://localhost:5399`, collaboration worker
`127.0.0.1:1234`. Chrome is the shared manuscript's Editor; Codex's in-app browser
is its Owner. Both signed in through normal authentication. Manuscript
`2921410a-4c86-4272-9188-e66b7b60e46b`, section
`acebf857-6552-47f5-b5fa-0ce4c47d8b12`. The baseline was the existing 171-word story.
Evidence is in `artifacts/brainstorm-real-accounts-5399/`.

## Verified flows

- Two actual provider requests completed, one per account: a lantern paragraph
  from the Editor and a silver-bell paragraph from the Owner. Each appeared
  automatically in the other account's already-open Brainstorm draft workspace.
  Private chats, drafts and unsent composers remained separate. See
  `owner-live-draft.png` and `editor-live-draft.png`.
- Editor reviewed the lantern's placement and selected Add here. Owner's normal
  manuscript updated without reload, stayed editable and reported saved. Both
  browsers retained the paragraph after reload. Keep Undo after reopening the
  original conversation restored the original text live; Redo restored the
  lantern live; final Undo restored the 171-word story. See `owner-live-keep.png`.
- Editor saved StoryNote, SynopsisField (Logline), and CharacterNote (Emma) through
  selected-field review. Owner loaded each actual authored synopsis destination
  and saw the saved values. Guarded Undo restored the original empty fields;
  Owner reload confirmed empty Notes and Logline. See `owner-story-note.png`.
- A fresh Editor conversation saved a SceneIdea with reviewed Title, Summary and
  Notes under the checked new chapter. Owner reload showed the scene in its
  outline (`owner-scene-idea.png`). Editor reload reopened that exact conversation,
  retained its Committed receipt, inspected it, and ultimately completed guarded
  Undo. Owner reload confirmed that the scene was removed without creating prose.
- After the first chapter was added, the older Editor conversation reopened with
  its original generated draft, private chat and unsent verification composer.
  It was no longer treated as access-revoked.

## Issue ledger and repairs

| ID | Finding and root cause | Repair and regression |
|---|---|---|
| BS-REAL-RECOVERY | The normal recovery panel labelled HTTP 402 as a backend mismatch. Standard lacks that history feature; Brainstorm saves were unaffected. | Separate plan, sign-in, access, missing-contract and retry messages. Compiled recovery UI tests exercise each status. Real owner and editor now see the accurate plan message. Plan enforcement remains. |
| BS-REAL-CHAPTER | Add chapter used the legacy project-node save endpoint and hit the shared-manuscript write fence. This prevented creating a destination for scene ideas in an empty outline. | Checked `create-chapter` collaboration command appends a manuscript-scoped chapter under the document lock, with source/access checks, idempotent operation identity and transaction-scoped permits. Tests cover retries, stale sources, viewers, invalid parents, preserved prose and closed permits. Legacy outline writes remain fenced. |
| BS-REAL-SCOPE | Before the first chapter, the shared story source exposed a null project. Adding a chapter changed that identity and older sessions were misclassified as access-revoked, clearing the mounted private review. | Shared sources always expose their stable project identity. Existing manuscript-only shared sessions retain authorized history access. A fresh context in an older scope explains that a new conversation is required for the current outline. Regression retains the older session across a chapter append. |
| BS-REAL-MAPPING | A first planning capture maps/synchronizes a fresh local conversation, but the panel validated the returned server snapshot against its older unmapped component cache. | Planning review rereads the durable cache and validates against its current mapping, with account/scope/epoch checks. Compiled panel regression covers first capture and existing account-change tests remain passing. |
| BS-REAL-STORAGE | Chrome briefly failed to open IndexedDB during scene Undo. The previous generic handler discarded the underlying error category, so the original browser-level cause could not be established. The shared-editor flush then labelled its local storage failure as access-changed. | Reads, sync and authoritative inspection succeeded; reopening the saved manuscript established a fresh healthy journal connection, and explicit guarded Undo succeeded. Storage errors now preserve their category and distinguish blocked tabs; a blocked open closes any late connection. Flush reports the actual storage error separately from access revocation. Seven Node regression checks pass; no storage clearing or automatic write replay was introduced. |

Final focused tests: **39 passed, 0 failed, 0 skipped** in `final-tests.log`, plus
**7 passed** in `storage-editor-tests.log`.
The isolated local application build passed in `app-build.log`; no plan or stored
subscription was upgraded. Earlier deterministic browser acceptance is recorded
separately in [the new-port report](brainstorm-new-port-uat.md); it is not evidence
for the two real provider requests described here.

## Final state

`result.json` records the completed real-account checks. Both browsers returned
to the same original manuscript, were editable, showed **Live / 2 people**, and
reported **Saved to shared manuscript**. Their editor HTML exactly matches the
baseline after excluding remote-cursor decorations; formatting and blank
paragraphs are retained. See `final-content-checks.json` and
`final-editor-manuscript.png`. The original story is still **171 words**.

All Keep prose, test synopsis fields, character/story notes and the test scene
were undone through guarded product controls. **One empty `New Chapter` remains**
from destination setup; it was not silently deleted. The test conversations and
validated proposals remain as private recovery evidence. Both browser tabs stay
open. This establishes local real-account/live-provider web acceptance, not
hosted production or native-device acceptance. The transient Chrome IndexedDB
failure recovered, but its original OS/browser-level cause is not established.

Follow-up: [Chrome storage recovery and full Brainstorm recheck](chrome-storage-brainstorm-uat.md)
records the connection-lifetime repair, bounded pre-transaction recovery tests,
shared-editor replay readiness fix, and the completed subsequent real-account
verification with both browsers on port 5399.
