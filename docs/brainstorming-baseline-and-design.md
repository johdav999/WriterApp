# Brainstorming baseline and contracts

Inspected 2026-10-07 in `C:\Users\Johan\source\repos\WriterApp`, baseline commit
`09352ded5c4f115578329323cf48569f141babd6`. Initial `git status --porcelain=v1`
was empty. No applicable `AGENTS.md` was found in the checkout or its inspected
ancestors. This records prompt 1 evidence, prompt 2 persistence, prompt 3 context
preparation, prompt 4 conversation, prompt 5 workspace, prompt 6 version review and the design for subsequent prompts;
source inspection does not establish provider or interactive acceptance.

## Current integration map

| Concern | Actual paths and behavior | Brainstorming integration |
|---|---|---|
| Web editor | `WriterApp.Client/Pages/DocumentEditor.razor` is the reachable WASM editor for `/documents/{DocumentId}` and `/projects/{ProjectId}/scenes/{SceneNodeId}`. `PageEditor.razor` chooses page or scene-content persistence. `DocumentEditor.CheckedWriting.cs`, `CheckedAi.cs`, `RequestCancellation.cs`, `HistoryDelivery.cs` retain checked source/application, cancellation and durable reporting. Older similarly named server components are not the client editor. | Bind new presentation to these handlers; retain keyed editor, focus and pending writing. Do not create a second manuscript editor or route unreviewed text into Save. |
| Windows editor | `WriterApp.Device.Shared/Pages/DocumentWorkspace.razor`, `Components/DeviceTextEditor.razor`, `Services/LocalEditorSession.cs`. `Snapshot()` overlays pending pages; `SaveAsync()` retains edits made during a save and rebases metadata-only acknowledgements after fingerprint comparison. Workspace flushes planning and editor before checked AI/mutation. `WriterApp.Desktop/MauiProgram.cs` supplies native lifecycle/connectivity/authentication. | Flush through the existing lifecycle; use exact checked editor text/ranges and snapshot. Read-only session review must not block ordinary offline writing. |
| Shared presentation | `WriterApp.UI.Shared/EditorWorkspace.razor` and stylesheet retain the mounted editor, with optional Brainstorm outline/preview/conversation, pane tabs and collapse. Existing `PanelResizeHandle` also resizes the new panes. Navigation/outline is supplied by existing host/project components. | P5's `BrainstormingWorkspace`, `BrainstormingProposalPreview` and conversation/context components use real web/device services and capability adapters. Private document/account changes clear presentation; writing stays mounted through layout changes. No dependency on the client from Shared/UI.Shared/Device.Shared. |
| Current outline | `Data/Documents/ProjectNodeRecord.cs`: ProjectId, nullable DocumentId (legacy ownership migration), ParentId, NodeType, OrderIndex, LinkedSectionId, tombstone. `SectionRecord`: DocumentId and ordered section; `PageRecord`: DocumentId, SectionId, OrderIndex, Content. `DocumentOutlineNodeRecord` is a separate older document-outline representation. | A chapter is a project outline node, not a persisted section/page entity. Use document-scoped active project nodes, not the older outline table or conceptual ordinals. |
| Saved writing outline | `Controllers/AiActionsController.WritingOutline.cs`, `WriterApp.Shared/WritingOutline.cs`, `Services/LocalWritingOutline.cs`: owned document/project, active sync clock, ordered sections, active nodes explicitly scoped to DocumentId, structure fingerprint, bounded graph. Desktop currently requires Synced and mapped nodes/sections. | Reuse its graph validation/order/fingerprint; prompt 3 adds local capture for unsynchronized writing rather than misrepresenting existing cloud-only capture. Do not guess ownership of legacy null-DocumentId nodes. |
| Reading order | `Application/Exporting/OutlineOrderResolver.cs` walks ordered outline children and linked sections; pages have their own ordered identities. `LocalProjectStructure.AttachToProject` creates a Chapter 1 node with scene children linked to the document's sections. | A chapter can span several linked scene/section nodes and many pages; a direct section link also needs traversal. Deduplicate links by section identity, retain saved reading order, disclose orphan/ambiguous links. |
| Linked scene prose | `Controllers/ProjectSceneContentController.cs`: scene ContentJson is actually used as HTML. PUT checks project ownership then updates SceneContents and the linked section: writes all supplied content into the first ordered page (creates one if absent), blanks every later page. Client scene route supplies `UseSceneContentPersistence=true`. | This is a single-scene aggregate replacement, **not** a safe multi-page range insertion/replacement path. Brainstorming targets have no scene-content mutation kind. Navigate to manuscript pages and use checked page/rich mapping; refuse unsupported scene routes with guidance. Do not call this route for Chapter 2 insertion. |
| Structure | `Application/Commands/StructureCommandProcessor.cs`, `Services/LocalDocumentStructure.cs`, `LocalProjectStructure.cs`, `LocalStoryboardData.cs`. Current local Create Scene creates a section as well as a node. | Prompt 7 must prepare section/page/node creation without saving until approval. Prompt 8's planning scene idea needs a path that does not implicitly create empty prose. Preserve all unrelated order, fields and links. |
| Synopsis | `Data/Documents/DocumentSynopsisRecord.cs`, `Controllers/DocumentSynopsisController.cs`, `Application/Synopsis/SynopsisAiContextBuilder.cs`, field catalog/commands. Local `LocalProject.Synopsis` is per manuscript aggregate; `LocalPlanning.Synopsis`/`LocalSynopsisCoaching` require a project. | Saved synopsis is planning intent, not necessarily manuscript fact. Standalone manuscripts can have no local project synopsis; record that omission instead of attaching a project implicitly. Reuse checked field approval, not whole-object overwrite. |
| Notes | Page/Section/Scene note records and their controllers; local `LocalProjectNode.Notes` and scene-card planning fields in `LocalPlanning`. Synopsis Notes is another authored field. | Address each actual field/entity. Story-note destination needs explicit choice; a character-note canon key is a string (e.g. `chr_maya`), not necessarily a Guid. Do not conflate extracted canon with authored notes. |
| Canon | `WriterApp.Shared/Canon/DeviceBibleContracts.cs`, `Controllers/DocumentBiblesController.cs`, checked canon source/services; `LocalBibleStore`, `DeviceBibleService`, `LocalConsistencyRevisions`. Snapshot revisions/freshness and explicit review already exist. | Enable selected sources deliberately; preserve old source revisions in each context. Canon updates require the existing checked review boundary. Never adopt a generated bible as authored character notes. |
| Authenticated AI | `Controllers/AiActionsController.cs`: `[Authorize]`, owned source, feature gates; `AI/Core/AiOrchestrator.cs`: provider routing, usage policy, cancellation. `WebCheckedAi` and `DeviceAuthenticatedHandler` guard account/backend; `DeviceAiService` guards proposal/source/identity. | P4's dedicated `BrainstormingConversationService` uses existing router/policy/quota/meter and checked sources. The generic action route rejects its identifier. No client provider secrets or direct model mutations. |
| Streaming | Legacy `AiStreamingSession` pairs events/completion and generic actions normally await completed JSON. At the P1 baseline there was no production streaming caller outside the orchestrator. P4 adds terminal usage/model to `AiStreamEvent.Completed` and a dedicated configured OpenAI Responses adapter. | Authenticated `BrainstormingConversationController` now streams bounded identified NDJSON consumed by both production host adapters and shared panel. Events have scope/request/operation/turn/sequence; terminal output is separately validated and durable. Fragments never become drafts. |
| History/recovery | `LocalAiStore`, `LocalAiHistoryActions`, `LocalWriting.ApplyAsync`/`ResumeAsync`; web `WebAiHistoryOperations`, `WebAiHistoryOutbox`, scoped recovery services/controllers. Local Applying is durable before document Save; resume compares exact intended before/after; history is Applied only after writing commit. Web operation intents/receipts and report delivery are separate. | Conversation persistence is a new store, separate from AI mutation history. A new brainstorming action must extend history binding in prompt 7; existing history expects a retained action proposal and cannot accept invented action IDs. |
| Local storage/sync | `LocalDocumentCodec` version 4, `FileLocalDocumentStore`, atomic writer/store lease. `DeviceSyncMapping` retains distinct document/section/page/node local/server IDs. Project metadata has its own atomic file; manuscript nodes/synopsis/notes/pages are in the document. Sync v4 retains immutable operation receipts/conflicts. | Brainstorm sessions get additive storage/mapping, not fields inside manuscript content or a guessed local-ID-as-cloud-ID fallback. Recheck mapping and account before every exchange. |
| Collaboration | `realtime.md` and release prompts describe a proposed Yjs/Hocuspocus design. Current production search found no collaborative authority/write path in either editor; writing still uses checked snapshots/version sync. | Do not implement that separate plan. If collaborative authority ships before prompt 7, inspect and use its authoritative write path and block legacy overwrite for migrated documents. |
| Audio | No production microphone capture, transcription, TTS, spoken-dialogue or real-time audio service found in client/device/desktop/AI/controller sources. Windows PDF stream APIs are unrelated. | Capability is unavailable; prompt 10/11 must verify then implement current official supported APIs. No microphone UI, permission request or decorative voice claim in prompt 1. |

## Implemented contract and capability boundary

`WriterApp.Shared/Brainstorming` holds portable version-1 typed contracts, validators,
bounded JSON decoding and read-only capability transport. Both production host
registrations expose `IBrainstormingCapabilityService`; Windows uses
`DeviceBrainstormingCapabilities` through Device.Shared, web uses
`WebBrainstormingCapabilities`. Native HTTP continues through
`DeviceAuthenticatedHandler`, including its expected-account-generation option.
Responses are rejected after a backend/account change; there is no capability cache.

Authenticated `GET api/brainstorming/capabilities` reports the current implementation:
session persistence is Available after prompt 2; conversation generation and streaming
are Available after prompt 4. Manuscript application, planning application, dictation
and spoken dialogue remain NotImplemented. Prompt 1 initially reported all of them
NotImplemented. Implementation availability is separate from current authentication,
configured provider/streaming support, reviewed source readiness and quota admission.
This does not change existing
writing availability, actions, paid features or quota policy. A 404/405/501 gives
UnsupportedServer; 401/403 gives AuthenticationRequired; timeout, malformed/future
JSON, inconsistent capabilities, oversized response or transport failure gives
Unavailable. Host adapters additionally report UnsupportedPlatform for speech,
because no speech adapter exists. Device sign-out/offline skips the request.

| State domain | Version-1 states and meaning |
|---|---|
| Conversation | LocalOnly, PendingSave, Saved, Conflict, Deleted. Saved describes only the conversation record. |
| Generation | Idle, Prepared, Accepted, Streaming, Completed, Cancelled, Failed, Interrupted, Unknown. Unknown reserves the active slot until reconciled; it is not retry authorization. |
| Application | Unreviewed, Approved, Applying, Committed, Cancelled, Failed, Unknown, Undone. Approved pins a candidate; only Committed/Undone can carry durable receipt/history/result source. |
| Turns | Unsent, Accepted, Complete, Incomplete, Cancelled, Failed; ordered sequence, parent, operation, request/context and account/document/session/backend scope. An assistant belongs to one accepted user request; duplicates and incomplete terminal proposals are rejected. |

`BrainstormingScope` specifies local versus server namespace, session, manuscript,
optional project, hashed backend/account keys, or a separate accountless local owner.
Server ownership cannot be anonymous. `BrainstormingMapping` pairs authenticated
scopes without assuming equal local/server IDs. These hashes are isolation keys,
**not** authorization credentials: server services must derive/verify them from the
authenticated owner and validate every referenced document/section/page/node.

Resolved targets distinguish InsertInPage (zero-width checked range), ReplaceRange
(nonempty checked range), and CreateSection (checked parent/before identities;
no existing text range). Each carries content and structure revisions/fingerprint.
Offsets retain the exact UTF-16 text-mapping name; quality/general/translation
mappings are not interchangeable. An optional scene identity provides provenance
and navigation, never authority to flatten the linked section. Standalone scopes
cannot contain invented project chapter/scene/parent nodes.

Contexts retain exact supplied excerpts, source/document identities, title, reason,
full-versus-summary, revisions, freshness, range, inclusion, mandatory status and
summary provenance. Excluded entries contain no text. Other documents require
explicit enabling; server authorization is additionally required. Mandatory
omissions block generation, while the manifest can still be reviewed. No automatic
summary or silent truncation occurs in this contract.

Drafts retain parent/version/revision, request/turn/context/target, instruction,
constraints, introduced facts and prose. Optional manually authored EditorHtml is
inert stored data; generated HTML cannot become an editor snapshot. The actual
editor/content-compatibility preflight and plain-text/rich mapping must independently
validate it before mounting or applying in prompts 6/7. Approval hashes the exact
plain and rich candidate together. Planning proposals identify one actual field,
before/proposed content and native string entity key. Neither contract executes
provider instructions or writes a document.

Limits are explicit: 500 turns and at most 500 entries per aggregate collection;
100 context sources, 100,000 combined context title/reason/text characters;
8,000 characters per user instruction/event fragment; 100,000 prose or manual
snapshot characters; 500,000 source-position bound; 2 MB UTF-8 session envelope;
32 KB capability response and 10-second discovery deadline; JSON depth 32.
Unknown JSON properties/schema, empty/duplicate IDs, undefined enum states, bad
hashes/revisions/timestamps, foreign scope, broken ancestry/order, invalid approval,
partial/cancelled drafts and false commit receipts are rejected. The session limit
requires explicit archival/pagination, never deletion of older turns to fit.

## Behavior matrix and decisions for subsequent prompts

| Case | Existing baseline | Required implementation design |
|---|---|---|
| Duplicate chapter names / displayed Chapter 2 | Titles and hierarchy are authored and can change; section and page entities do not encode a universal chapter ordinal. | Prompt 3 resolves stable node/section/page IDs using saved sibling/traversal order. Contradictory selection, missing chapter, duplicate titles or multiple links produce typed clarification candidates. Confirm the chosen destination before generation; no structure creation during resolution. |
| Existing first section versus new first section | Current scene Create implicitly creates section/page structure. | Capture the existing first linked section or explicit CreateSection position before its stable section/node IDs. At review show insertion versus replacement. Recheck chapter/order stamp and create only in approved application. |
| Unsaved writing | Web has checked save/capture; device has pending editor snapshot and local recovery. Existing local writing outline generation requires Synced. | Flush exact active editor/planning changes, capture all saved+pending pages with the actual bundle mappings, then verify nothing changed during capture. If local writing is ahead of server, implement an authorized bounded local-context request in prompt 3; otherwise report unavailable. Never replace it with an older cloud snapshot. |
| Standalone manuscript | Sections/pages exist without project nodes; local project synopsis can be absent. | Session remains scoped to this document with null project. Resolve direct sections/pages, explain absence of chapter labels/planning destinations; no implicit project attachment. |
| Supporting project documents | Multi-document projects and individual document authorization exist. | Explicit source selection plus per-document authorization/mapping. Do not send siblings automatically. Deleted, revoked or unmapped sources are omissions/errors, not substituted text. |
| Accountless/offline | Ordinary local writing already works accountless/offline. | Add separate local-owner session namespace; cached review is offline. Signing in does not adopt accountless sessions. Explicit transfer is a later reviewed copy with new scoped IDs/mapping; reconnect never sends a billable request or applies a proposal. |
| Cancel / disconnect / late answer | Current checked AI has cancellation/source leases, but no conversation event persistence/transport. | Prompt 4 binds events to captured scope/request/operation/assistant identity and next sequence. Terminal identity refers to a separately validated durable result. Fragments stay incomplete; account/navigation changes discard late events from active presentation. Cancelled/unknown requests cannot become applicable drafts. |
| New draft / edited draft | Current writing tools offer checked one-shot proposals. | Prompt 6 persists each generated/manual version and exact ancestry. Manual HTML remains inert until editor validation. Approval pinning is separate; editing changes revision/hash and invalidates that approval. |
| Timeout during Keep | Local history and web receipts already separate intent, save and report completion. | Prompt 7 uses durable application journal and idempotent operation receipt, then reports history/session separately. Reopen reconciles known receipt/before/after; no double insertion or false success from an outbox. |
| Later edits / undo | Existing scoped history refuses divergence and offers recovery-copy paths. | Record exact touched text/structure/planning snapshots and use guarded existing recovery. Undo must not delete new sections/copies containing later edits. A mismatch preserves current writing and offers review/recovery. |

## Implemented prompt 2 persistence and transport

The production implementation uses one bounded, validated snapshot per session and
separate durable operation receipt rows, replacing the initial design's normalized
turn/proposal tables. This keeps turn order, draft ancestry, source provenance and
operation state atomic within the same revision transaction. `BrainstormingSessions`
stores authenticated owner, backend hash, document/project, concurrency-token revision,
tombstone, update time and snapshot JSON. Its owner/backend/document/ID index supports
bounded listings without selecting private snapshots. `BrainstormingSessionOperations`
has `(SessionId, OperationId)` primary key, exact payload hash and saved receipt; an
identical retry returns that receipt even after later revisions. Both provider
migrations create only these two tables and the scope index; existing writing,
document sync v4 and editor schemas/assets are unchanged. The root has no manuscript
foreign key, so retained conversation recovery records do not block existing document
purges. Access always rechecks the active owned document/project and source documents.

Authenticated routes in `BrainstormingSessionsController`:

- GET `/api/brainstorming/sessions?documentId=...&after=...&limit=...` returns owned
  summaries, default 25/max 50. `after` is a stable session GUID checked against this
  owned document's rows; ordering/comparison are performed by the active provider.
- GET `/api/brainstorming/sessions/{id}?documentId=...` returns one validated snapshot
  bounded to 2 MB and 500 entries per collection.
- GET `/api/brainstorming/sessions/{id}/turns?documentId=...&after=...&limit=...` pages
  ordered turns, default 25/max 50; `after` is the last sequence, and revision is included.
- POST `/api/brainstorming/sessions` creates/updates/deletes through the versioned
  `BrainstormingWrite` envelope. ExpectedRevision + 1 is required. Delete is a permanent
  tombstone with retained content, not a destructive DELETE. Accepted messages,
  immutable context/targets/drafts/planning/approvals and committed receipts cannot be
  rewritten. Forking uses a new session identity and owned parent-session provenance.
- GET `/api/brainstorming/sessions/{id}/operations/{operationId}?documentId=...`
  inspects the exact operation receipt. Missing/unauthorized records return 404,
  malformed input 400, unauthenticated access 401 and conflicts 409 with current data
  when available. These routes never dispatch a provider, bill usage or mutate writing.

The shared boundary is `IBrainstormingCacheStore`, `IBrainstormingRemote` and
`BrainstormingSessionService`. Both hosts register those interfaces and capability
discovery. Presentation will use these services in prompt 5; no storage logic or new
brainstorming UI lives in Razor in prompt 2. `BrainstormingHttpRemote` bounds and
validates responses, pins a host's account/backend lease, checks cancellation before
and after network I/O, and never retries automatically.

`LocalBrainstormingStore` uses the existing atomic writer with a store lock and
separate `documents/brainstorming/<partition>/<document>/<session>.json` files. An
installation-local owner GUID has its own atomic file. Authenticated and accountless
partitions are separate; signing in cannot map/read/adopt the anonymous archive.
Document, section, page and node mappings are explicit existing sync identities,
including retained deleted records. Device mapping never guesses a server ID. A
standalone local document has no authored project, but its server DocumentRecord
always has a backing ProjectId; the mapping permits that distinction. An absent
source mapping or a mapping change during exchange retains the session/outbox and
refuses acknowledgement. Canon source keys (character/place/timeline) remain opaque
native strings, including strings that resemble GUIDs.

`WebBrainstormingStore` uses production module `js/brainstorming-store.js`: IndexedDB
`writerapp.brainstorming.v1`, transactional sessions/rejected/identity stores. Web
manuscript entity IDs are already server IDs but the cache still has a separate Local
namespace and explicit session mapping. Composer text and unsent turns survive reload
and delivery failure. An authenticated tab remembers an account/backend review grant
in sessionStorage plus IndexedDB. If auth bootstrap cannot resolve while truly
offline, that tab may review/edit cached data; the grant never authorizes HTTP. A
fresh tab must authenticate before reviewing. Logout revokes the tab grant before
asynchronous storage cleanup. Cached content does not establish current server
authorization or source freshness; sign-out does not delete archived sessions.

The version-1 cache retains session, composer, mapping, immutable outbox, exact
acknowledgement, deletion acknowledgement, local dedup ledger and remote conflicts in
one envelope. Limits: 8 MB cache; 10 retained remote conflicts; 10 retained local CAS
conflicts; 10,000 local receipts; device listing supports 1,000 sessions per document
and both adapters return at most 50 summaries. Capacity failure retains original
bytes and requires an explicit new branch/archive choice; nothing is pruned silently.
Future/invalid files are preserved and cannot be overwritten by an old reader.

Sync is explicit: map, persist one immutable outbox, inspect its receipt, then write
only if no receipt exists. A lost acknowledgement leaves that same operation pending.
Exact receipt validation precedes a local acknowledgement save. Concurrent local edits
and composer text survive the response; only precisely acknowledged messages become
Accepted. Acknowledged unchanged sessions/tombstones do not post new revisions.
Incompatible server revisions retain both snapshots and stop sync. Local CAS losers
are separately retained and inspectable through `ReadRejectedAsync`. `ForkAsync`
copies the chosen local history into a new unmapped session and preserves the original;
`ReadAsync` exposes both conflict snapshots for deliberate future review. Pulling the
unchanged acknowledged server revision preserves dirty local data without creating
a spurious conflict. No reconnect listener replays anything.

Conversation storage can retain Approved/Applying/Unknown operation references but
cannot create new Committed/Undone claims. Prompt 7 must implement the authoritative
brainstorming mutation-to-session receipt bridge; referring to an unrelated existing
AI history operation is insufficient proof. Local committed references are inert
cached data and must also be reconciled against actual device mutation receipts in
that prompt. Session storage and existing AI application history remain separate.

## Selected application recovery strategy for prompts 7–9

Server: approval stores immutable reviewed candidate, scope, before/after source and
structure snapshots, operation hash and expected revisions **before** mutation.
The checked application service commits page changes, any required section/node
creation, durable operation receipt and authoritative AI history in the same EF
transaction, under document sync-clock concurrency. Session application reporting
can follow independently: if it fails, GET operation status proves the commit and
repairs the session record without rerunning AI/applying text. A timeout is Unknown
until reconciled; a second operation must not apply the same approved candidate.

Device: durable Prepared/Approved journal first; assemble text+section/page/node
changes into one checked LocalDocument and save once through the existing locked
repository. These manuscript-owned fields share the atomic document envelope.
Do not alter unrelated shared project metadata (it has a separate file) in the same
operation. If a future planning destination spans files, implement an explicit
recoverable journal with per-file before/after evidence rather than pretending it is
atomic. Reconcile interrupted Applying using exact before/intended-after signatures,
then record the receipt/history/session with non-cancellable final bookkeeping after
the save. Compare normalized annotation reconciliation too, as existing
`LocalWriting.ResumeAsync` does. Preserve a durable operation marker/receipt with the
saved aggregate so later edits cannot make a committed operation look unexecuted.
If evidence cannot establish before or committed-after, retain Unknown and current
writing for explicit review/recovery. Never automatically save/regenerate on startup.

Generation recovery (implemented in prompt 4): the client durably records the pending
send identity before HTTP; the server atomically records the accepted user/request and
unique operation before provider dispatch. The ledger tracks Accepted/Streaming and
terminal output, inspection text and usage. Disconnect/restart inspects that operation
first. A lost worker is reported Unknown, never restarted. Unknown provider/usage
outcomes retain the active slot for reconciliation; existing quota admission is not
a new transactional token reservation. Settling is saved before the existing
non-idempotent quota charge and usage meter, so interruption cannot trigger automatic
rebilling/replay. Known terminal failure/cancellation or authoritative owned missing
operation permits an explicit new send. Unknown does not; there is no reset/reconcile
UI or automatic paid retry. Old complete drafts and committed status stay inspectable.

See [implementation status](brainstorming-implementation-status.md) for executed
checks, independent acceptance gates and the copyable prompt 5 handoff.

## Implemented prompt 3 context boundary

`BrainstormingStorySnapshot` is an owned, bounded read-only snapshot, not a mutation
command. Both hosts project its pages with the existing editor quality mapper. The
shared resolver traverses saved nodes in reading order and pins section/page or
new-section neighbors. Desktop's persisted chapters contain linked scene nodes;
section/page order remains independently checked. The server excludes unscoped legacy
nodes rather than infer ownership from a project ordinal.

The context manifest retains exact included source text and empty text for exclusions.
Availability, freshness, planning intent, extracted canon and authored prose are
distinct. Large chapters group complete pages with checked part ranges and explicit
identity mapping instead of silently dropping pages to fit the 100-source bound.
Optional inventory omission is disclosed. Explicit author summaries require current
full-source revision/hash attribution; required over-budget sources block dispatch
unless the author deliberately reviews a summary or omission.

`GenerationData` is the exact included-story payload builder used by P4. It is not an
authorization decision: P4's server verifies current ownership/versions, checked
canon entitlements, source mapping and provider token/quota admission before using it.
The context character budget counts included excerpts, titles and reasons; JSON
provenance/transport and provider token limits are separate bounds. Local/pending
writing currently has a visible server-generation limitation. Do not remove it merely
because a client supplies hashes or Ready=true; implement an authorized checked
local-context contract or a fresh synchronized server capture with explicit maps.

The shared review is reachable in both existing editors without replacing the editor
host or adding the later conversation workspace. Production host preparation captures
and rechecks DOM/pending writing, then persists context/targets separately from writing.
Refresh retains old manifests, discussion, composer, versions and approvals; obsolete
draft IDs require review. Account changes clear active review without erasing caches.
No preparation path dispatches a provider, refreshes canon, writes prose or changes
outline/planning/history. Deterministic explicit fact conflicts are surfaced; semantic
natural-language conflict assessment belongs in P4's checked metered conversation.

## Implemented prompt 4 conversation boundary

`BrainstormingConversationAction` supplies bounded accepted dialogue, approval-pinned
decisions, resolved target, optional parent draft, exact `GenerationData` and current
instruction. It routes through the configured server AI router and existing entitlement,
quota and usage policy. Generic action execution/streaming refuses its identifier;
the dedicated authenticated conversation service owns durable acceptance/settlement.
No tool or response can directly change manuscript/planning/history.

`BrainstormingGenerationContext` authorizes and recaptures server sources before
dispatch and before accepting output. Selected canon uses existing entitlement and
freshness checks; support must be owned; summaries retain full-source version/hash;
required omissions and contradiction warnings remain visible. Server text projection
is read-only and compared with the actual common editor quality mapper. Device's new
explicit synchronized server review checks local/server page/section mappings and
content, rechecks local DOM/source/account, then maps the reviewed result back. Web
checks active DOM against the current saved server page before send. Pending writing
is retained and blocked until saved/synced and explicitly reviewed; no silent save or
older-cloud substitution enables it.

`POST api/brainstorming/conversation/stream` emits bounded NDJSON with scope, operation,
request, assistant turn, sequence, answer delta, draft-check status, validated output
and usage. Inspection/stop are owned endpoints; only an authoritative owned missing
operation response clears a pending send. The shared HTTP/workflow uses fresh host
identity leases for every step/event and durable pending identity before HTTP. Reopen,
reconnection and the same operation never initiate another provider request.

OpenAI's dedicated path consumes configured Responses SSE and requires terminal
`response.completed` plus usage; failed/incomplete/tool output and premature EOF are
not successful proposals. The validated response contract distinguishes Explore,
Clarify, Draft, Revise and ProposePlanning without a mode selector. Answer text and
prose are separate. Interrupted raw JSON is inspectable encoded text, never a draft.
Only validated terminal output creates a complete assistant and server-assigned
draft/planning identities, with target/context/parent provenance. Complete response
and contradiction details remain in the durable generation ledger; older proposal
viewers must retrieve that original operation when presenting its warnings.

Version-1 cache gains optional pending/last generation metadata. SQLite and SQL Server
additive generation migrations store unique session/operation, immutable payload hash,
revision and bounded journal/output. Public session writes cannot alter generation
provenance or race an active/unknown operation. Ordinary session save, generation,
proposal persistence and later manuscript/planning commit remain separate states.

Both existing editor reviews now host a shared minimal conversation panel with
send/stop/reopen, encoded discussion, usage, reported contradictions, inert prose/
planning and rejected-output inspection. The full three-panel workspace belongs to
P5, manual version editing to P6 and mutation approval to P7/P8. Source/context refresh
retains old targets and proposals. Scope/account/navigation changes cancel current
presentation and cannot adopt late output into a new session.

See the P4 status evidence for 468 executed tests, both affected builds, actual editor
bundle comparisons, static actual-handler Razor render checks and isolated rebuilt
Windows startup. Deterministic provider/controller/interop fixtures establish those
scoped behaviors; live provider, signed-in interactive browser/native, SQL Server,
packaged deployment and audio acceptance remain separate gates.

## Implemented prompt 5 workspace

Both manuscript editor hosts expose Brainstorm in their existing main pane. The shared
layout retains the same editor component while presenting the existing outline on the
left, manuscript and read-only proposal in the middle, and session/context/conversation
on the right. Narrow pane tabs and desktop collapse do not remove manuscript/session
DOM. Existing resize handles/module gain bounded outline/conversation sides.

`BrainstormingWorkspace` uses each host's real session/context/capability/workflow
adapters. Its paginated picker lists scoped cached conversations; deliberate server
inspection of mapped sessions reuses P4 ownership/recovery. Remote-only discovery/import
is part of P9 integration. Reopen restores composer/source choices/provenance but requires
fresh context review. Different scene/document/account selection cannot retarget old
drafts or adopt late generation events. Old private caches remain intact.

Shared conversation adds accessible multiline input, focus/caret restoration, explicit
retry only as a new operation after known failure/cancellation, long transcript expansion
and actual availability/operation states. Middle prose is encoded inert text with original
target navigation and provenance. It has no manual edit or apply action yet. P6 adds
versions/manual review; P7/P8 add approved mutations. The ordinary manuscript editor and
checked navigation/save/refusal paths remain authoritative for pending writing.

See [brainstorming-p05-uat.md](brainstorming-p05-uat.md) and the status for 476 regressions,
both builds, real Edge editor/resize/focus checks and isolated rebuilt Windows startup.
Fixtures, signed-in interactive browser/native, live provider/usage, SQL Server, packaging
and speech have separate acceptance scopes. Current handoff is prompt 6.

## Implemented prompt 6 version review

The shared `BrainstormingProposalPreview` now reviews persistent generated/manual
versions with previous/next navigation, captured-source comparison, original target,
constraints, facts, contradictions, parent/instruction provenance and independent
application state. It uses the existing common shipped editor via the shared proposal
module; the web bundle exposes that existing factory, and the device uses its existing
static module. Neither editor callback invokes a manuscript save.

`BrainstormingDraftEdit` is a bounded local recoverable buffer pinned to a candidate
fingerprint; `CheckpointDraftAsync` creates a new immutable author version. Checkpoint,
send, workspace exit and session switch preserve edits before changing review. The
selected candidate/optional verified passage remain in the scoped cache. A next
revision sends that exact manual rich/plain candidate through the metered conversation
route. Exploration/clarification and cancelled partial answers do not create versions.

Selected passages pin exact candidate ID/fingerprint, UTF-16 mapping offsets and quote.
Server/workflow recheck the owned candidate before dispatch. Fresh same-location context
keeps the original target ID; source/position changes refuse revision. New-scene requests
use the newly reviewed destination without adopting old prose as its parent. Explicit
old-passage selection cannot silently follow the new scene. Account/document epochs
reject delayed private captures/callbacks; proposals are read-only during generation.

P6 adds optional version-1 review/edit/selection/warning JSON fields but no migration or
document/sync schema. Match rebuilt server/client because older strict backends reject
new fields. Shared read-only projection/HTML compatibility reuses existing AngleSharp
1.8.2/common allowlist rules; old incompatible inert snapshots remain retained. Actual
editor vocabulary is unchanged and both bundles were rebuilt.

See [P6 acceptance](brainstorming-p06-uat.md) and the implementation status for 495
regressions, both builds, real web/device proposal modules/IndexedDB and isolated
rebuilt Windows startup. Interactive native/browser, live provider, SQL Server,
packaging and speech remain separate gates. Current handoff is prompt 7, which must
pin the exact reviewed candidate fingerprint/content and bridge authoritative mutation
receipts into session/history. P6 creates no committed manuscript/planning record.

## Implemented prompt 7 manuscript approval

Keep now pins a bounded exact candidate/HTML/target/source review and stable record
identities before an explicit writing dispatch. The session cache retains an independent
application journal; Unknown precedes dispatch and requires inspection before retry.
No open/reconnect path applies writing. Both host callbacks flush pending editor work,
verify private identity/source/order and confirm the actual shipped editor transaction.

The server ledger has a unique session/candidate identity and serializable sync-clock
transaction for page/new filled section/outline ordering, linked scene mirror, receipt,
existing AI history and authoritative session application. SQLite and SQL Server Keep
migrations are present. Device history v6 stores Applying/Applied plus exact before/after
snapshots around the existing atomic aggregate CAS save; inspection proves saved state
without replay. Guarded undo/redo changes only affected text/order and refuses later
authored changes. Original-copy recovery preserves current writing. Local receipts
remain distinct from server manuscript commits; full cross-device receipt transfer is P9.

Page-start insertion and one top-level paragraph/heading replacement use portable DOM
mapping plus actual ProseMirror verification. Unsupported nested images/markup/ranges
and anchored server destinations conservatively refuse with original rich content and
comments retained. Multi-page manuscript remains separate from linked scene content.
The ordinary page-save mirror was extracted and reused, preserving other planning.

See [P7 acceptance](brainstorming-p07-uat.md) and implementation status for 529 scoped
regressions, both builds, 24 Keep module/review groups and isolated native startup.
Actual signed-in browser/native interaction, SQL Server, provider quality and installed
package evidence remain separate. Current handoff is prompt 8. An unrelated expanded
targeted-quality diagnostic failed and remains explicitly recorded for reproduction.

## Implemented prompt 9 recovery and synchronization

Both hosts have explicit owned mapped-document session discovery and recovery
history exchange. Manual draft buffers and conflicting local/server discussions
remain inspectable; either discussion may be forked without replaying original
application operations. Current private journals and pending saves remain in their
original cache. Server history is read from durable ledgers, while immutable uploaded
device copies retain their original namespaces and are labeled unconfirmed recovery
evidence. Revoked access clears active review and rejects late adoption while
preserving local writing. Missing generation inspection cannot borrow prior success.

Optional cache Recovery and nullable server RecoveryJson have bounded version-1
contracts; both database migrations add only the nullable column. Standalone
synopsis/notes transfer through additive sync v5 under ordinary document concurrency,
with exact server-context equality checks. Older backends receive an actionable
upgrade refusal rather than silently dropping authored planning. Project planning
continues through its existing wire. No editor schema/asset changes in P9.

See [P9 acceptance](brainstorming-p09-uat.md) and implementation status for 716 scoped
regressions, actual two-host Chapter 2 Send/Keep/Undo callbacks, Edge modules/recovery
reload, measured contract limits, both builds and isolated native startup. Interactive
signed-in/native, live SQL/provider and installed-package gates remain explicit.
P10 now implements real microphone capture and configured transcription into the
existing composer; live microphone acceptance remains separate. Current handoff is
prompt 11. See the P10 entry below.

## Implemented prompt 10 dictation

Real web AudioWorklet/PCM16 and Windows MediaCapture adapters feed the same editable
composer only after an explicit microphone action. Language is explicit en/sv;
permission/device/silence/recognition/connectivity errors retain typed text. Authenticated
backend speech uses configured Audio routing, entitlement/quota, token usage and
owned operation inspection. Audio is transient; typed outcomes and hashes alone
enter the new BrainstormingTranscriptions table and optional Dictations cache.
Lost/uncertain operations cannot automatically reupload/rebill or append twice.

An explicit destination action creates AuthorDictated proposed prose with reviewed
context/target and an owned completed transcription operation. It does not create
an AI request or apply writing. Existing exact-version Keep and Undo remain separate.
Capture and late result guards cover cancellation, identity/scope change and disposal;
Windows also cancels on background/closing and web on pagehide/visibility change.
No playback, monitoring or spoken approval orchestration exists yet.

See [P10 UAT](brainstorming-p10-uat.md) and status for scoped checks, matching migrations,
provider configuration and actual user English/Swedish microphone reproductions.
Automation uses labeled synthetic audio/transport; isolated Windows startup is not
native microphone acceptance. Prompt 11 must retain this boundary and same text memory.

## Implemented prompt 11 spoken dialogue

Explicit push-to-talk turns use P10's configured STT and the same checked text
conversation. Concise completed answers and an optional version-bound draft opening
use installed/local platform TTS; no second assistant memory or remote TTS credential.
Typed follow-ups, spoken exploration/revision and exact reviewed Keep/planning retain
the existing session, context/target, metering, receipt and recovery boundaries.

An exclusive audio floor, explicit interrupt/mute/end, background/navigation suspension,
hidden playback refusal and stale-ID guards prevent automatic playback capture/restart.
Unknown/poor quality, negation, questions, mixed typed prefix and changed reviews require
focused confirmation. Clear final speech can enter only the original exact visible
review's checked handler. Read opening aloud stops on edit/version/identity changes.
Optional recognition quality extends P10 typed JSON; no P11 migration/editor schema.
Multi-turn pull clocks now retain valid imported request revision provenance.

See [P11 UAT](brainstorming-p11-uat.md) and status for current checks, official API
sources and pending real human English/Swedish microphone/model, audible output,
physical echo and interactive native/browser gates. Current handoff is prompt 12;
earlier dated “no playback”/prompt 11-next statements describe the P10 baseline.
