# Prompt 8 planning decision acceptance packet

Date: 2026-10-07. Product: Prosa web/server and Windows desktop Brainstorm workspace.
Role: author. Revision: accumulated uncommitted prompts 1–8 in the original checkout,
with unrelated work preserved. Synthetic owned standalone/project manuscripts,
chapters with existing sections/pages, authored synopsis, notes and scene metadata.
No real manuscript or provider credential used.
Environment: migrated SQLite, authenticated in-process TestServer, atomic device
document files, actual shared Razor and production host callbacks, production
IndexedDB in headless Edge 154.0.4258.62 and isolated rebuilt Windows startup.
Handler/transport fixtures are distinct from signed-in interactive acceptance.

```powershell
./scripts/verify-brainstorming.ps1 -Phase p08 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence: ignored `artifacts/brainstorming-p08`, including combined TRX/logs,
production-host `planning-review-{web,device}.html`, validated exact cache/Unknown
JSON, browser results/screenshots and `native-startup.json`. No new editor source
or generated editor assets were changed by P8. Existing actual-editor context,
draft and Keep browser suites exercise the currently shipped bundles.

## Flow packets

| Flow | Preconditions and actions | Expected and observed | Evidence/scope |
|---|---|---|---|
| P8-01 Select discussion or type a decision | Complete retained turn/context; choose any of the four contextual actions or start a new typed decision. | Owned current manuscript/chapter source captured after pending saves; candidate stays separate; typed decision becomes a retained complete user turn. Extracted canon is untouched. Both reachable production hosts passed. | `BrainstormingPlanningHostTests`; synthetic authentication/interop with actual shared Review/Approve handlers. |
| P8-02 Review selected fields and save | Edit candidate; inspect before/after; approve Logline while excluding Theme; separately approve story note, named character note and scene title/summary/notes. | Only selected fields change. Notes use existing authored synopsis Notes, with an explicit character label. Scene is a marked unlinked planning node/card/notes; no section, page or prose created. Original links/tags/status/fields and old manifests retained. | Four destinations in both hosts and server/local persistence tests; SQLite/device files. |
| P8-03 Reopen, duplicate and uncertain response | Reopen services/session; repeat same operation; lose an HTTP/local commit response; reopen durable private journal. | Exact immutable review/receipt survives. Duplicate identity returns original outcome; changed approval refuses. Unknown must inspect before any explicit retry. Foreign account partition is empty; no automatic application or false success. | Authenticated HTTP lost-response test, local atomic marker inspection, shared no-dispatch assertions, actual IndexedDB reload in Edge. |
| P8-04 Stale/deleted/private/failure refusal | Change writing/planning/order, delete chapter/document, change account/backend during capture, send malformed scope, or fail SQLite synopsis update with a trigger. | No fields or receipt falsely committed. Atomic rollback retains original manuscript/planning; remove fixture fault then explicitly retry same approval. Private late capture cannot populate new UI. | Source/CAS/ownership tests, real migrated SQLite failure trigger and production handler delayed-capture test. |
| P8-05 Undo, recovery and project sync | Undo confirmed approved fields; modify another field; then modify affected field or scene tags/order/links. Sync a marked unlinked idea through ordinary project sync. | Safe undo retains unrelated edits. Divergent targets refuse restore and expose exact original fields for copying. Project idea/card/notes round-trip with ID mapping and unchanged sections/pages; ordinary unmarked unlinked scenes remain invalid. | Local/server guarded undo, actual project sync upload/download test and production UI Undo handlers. |

Scoped verification: **694 passed, zero failed/skipped** in the combined suite.
The recorded initial focused planning run passed 28 cases; the final combined run
also includes the additional ownership, scene-target and standalone context cases.
Server/web Release and Windows Debug builds passed with zero warnings/errors.
Test-only renderer BL0006 warnings and existing LocalStoryboard xUnit2031 warning
remain; these are not production build warnings. Six planning browser groups passed,
alongside existing storage, common editor, context/conversation/workspace, draft and
Keep suites. Wide web and narrow device screenshots were visually inspected:
before/after, partial selection, explicit approval and journal remain accessible;
provider-shaped HTML appears as literal text. Browser checks assert focus and no
horizontal clipping at 390px.

## Issue ledger

| ID | Severity | Flow | Finding and regression | Status |
|---|---|---|---|---|
| P8-I01 | P1 | P8-02/03 | Planning had no reviewed mutation/recovery path. Durable bounded selected-field reviews, transactional server ledger, atomic local operation markers and inspect-before-retry now exist. | Fixed; both production hosts and persistence verified |
| P8-I02 | P1 | P8-02/05 | Existing scene creation requires manuscript links; ordinary sync mirrored every scene into prose. Explicit marked unlinked ideas now validate and skip prose mirroring. Unmarked unlinked scenes still refuse. | Fixed; real project-sync regression |
| P8-I03 | P1 | P8-02/03 | Synopsis save sets container PlanningSyncEnabled, which incorrectly changed a standalone session into a project scope. Classify using actual project/saved outline state instead. | Fixed; migrated standalone save/reopen/undo |
| P8-I04 | P1 | P8-04 | An identity change during a delayed snapshot could retain private review text. Clear private state and reject the late capture by identity epoch. | Fixed; actual component delayed-capture regression |
| P8-I05 | P1 | P8-04/05 | Standalone local synopsis was absent from the wire fingerprint/context. Preserve it in the atomic document envelope, fingerprint real edits, include local context and block stale server substitution. | Fixed locally; cloud transfer is P8-G03 |
| P8-G01 | P2 | All | Fixture callbacks plus browser render/storage checks are not a complete signed-in browser/native journey. | Open interactive acceptance gate |
| P8-G02 | P2 | P8-04/05 | Live SQL Server transactions, provider quality and packaged distribution have no current evidence. | Open acceptance gates |
| P8-G03 | P2 | P8-05 | Standalone authored synopsis/notes have no existing sync wire field. Device values remain local-only and server context preparation refuses when these values would be omitted. Full planning receipt/history transfer also remains P9. | Explicit production limitation; prompt 9 dependency |
| P8-D01 | P2 | Separate quality workflow | P7's unrelated targeted-quality `valid` and `429-between` diagnostic remains recorded without changing those flows. Existing list-ending normalization acceptance belongs to that separate quality work. | Preserved; separate reproduction |

## Persistence and supported boundaries

Use matching rebuilt shared/web/server/device code and the SQLite/SQL Server
`BrainstormingPlanningSaves` migrations. They add only the planning operation table,
owned-session FK and unique candidate index; no manuscript conversion. SQLite is
actually migrated in tests; SQL Server model/script checks do not prove a live run.
Version-1 private caches gain optional planning journals; retained earlier caches
remain readable. Reviews retain exact operation/candidate/turn/context/draft identity,
source revisions, selected before/after and review hash. Old manifests are unchanged.

Server save/Undo and journal/session outcome share a serializable transaction and
the existing document sync clock. Device save/Undo and the scoped hashed operation
marker share an atomic document CAS. Private session journals retain recovery content;
Unknown cannot dispatch. Confirmed saves reload planning and invalidate next-context
readiness; the author must explicitly prepare fresh context before generation.
Keep approval and planning approval remain separate.

Project synopsis/scenes use ordinary project sync and existing identity mapping.
Standalone device synopsis uses `ExtensionData.authoredSynopsis`, visible in the
ordinary Synopsis panel and included in local context. It is preserved across
downloads but intentionally not claimed as uploaded. Resolve P8-G03 in prompt 9
through a supported owned wire contract and conflict handling. Local planning
markers/private receipts are not fabricated server confirmations or conversation
CRUD application claims. Extracted canon remains behind its existing approval path.

## Reproduction for remaining gates

For P8-G01, use isolated two-chapter data in a matching authenticated Development
web host and the exact rebuilt binary/output/data paths in the status file. Open
Brainstorm, prepare context, select a completed discussion message, review each
destination, edit and partially approve, cancel another review, reopen the ordinary
Synopsis/Storyboard and session, inspect and undo. Repeat with unsaved editor text,
account/backend changes during a delayed save, offline local approval and a later
affected-field edit. Preserve all user-owned editors/documents. Native startup is
only startup evidence.

For P8-G02, apply both applicable migration chains to an authorized isolated SQL
Server test database. Repeat duplicate concurrent approval, transaction failure,
lost response, inspection and guarded undo with owned data. Separately run the
configured live provider's exploration/draft/planning suggestion journey and verify
usage. Planning save itself makes no provider request. Verify installed packaging
separately; microphone/playback remain prompts 10–11.

For P8-G03, create a standalone device manuscript, save a story/character note and
Logline, reopen offline and confirm normal Synopsis and local context contain them.
After mapping/synchronizing, server context must explain that authored local notes
cannot yet transfer rather than send an older cloud copy. Prompt 9 must add explicit
owned standalone planning transfer, retained conflicts and cross-device verification,
then prove current server context includes the approved values. Also audit receipt,
history and pending-journal synchronization without manufacturing commit evidence.
