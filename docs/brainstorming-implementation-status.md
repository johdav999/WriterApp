# Prosa brainstorming implementation status

Updated: 2026-10-10. Plan: [brainstorming-prompts.md](brainstorming-prompts.md).

Latest response correction: **continuing the manuscript does not require an
existing Brainstorm proposal**. The reported model response had valid new prose
but set `continuation=true` with no parent draft. The shared validator now starts
a reviewable proposal from that prose; Keep still adds it at the reviewed manuscript
destination. Existing-parent appends and explicit rewrite checks remain intact.
See [root cause, exact retained-response replay and runtime handoff](brainstorming-manuscript-continuation-uat.md).

Latest placement correction: **Review & keep lets the author choose where to add
the approved draft**. Continuation defaults to the end of the section's last page;
a named new section can be placed after a chosen section; a saved paragraph boundary
can be selected with a manuscript preview. The shared web/desktop GUI highlights
the exact addition, validates titles, and exposes View in manuscript and Undo.
525 broader regressions and 46 final focused checks passed; production web replay
and actual editor mapping passed; Release server/web and Windows desktop builds pass.
See [placement behavior, evidence and runtime handoff](brainstorming-placement-uat.md).
The existing author desktop still requires the updated build to be launched.

Previous correction: **typed requests survive voice background events; interrupted
requests and reopened conversations inspect their saved server outcome**. The
reported desktop cache retained only Accepted while its server request was
already Cancelled with two saved turns. Voice interruption now stops only a
generation owned by a spoken send. Automatic recovery reads the original
operation, restores saved turns/proposals, and never dispatches it again.
Offline and genuinely Unknown operations remain pending. See
[uncertain-request root cause, evidence and runtime handoff](brainstorming-uncertain-request-uat.md).
The author desktop remains open on its current binary; rebuild/restart its normal
output to activate this UI correction. The server requires no change for it.

Previous correction: **strict structured conversation responses and readable failure
messages**. A response missing `contradictions` was rejected, then its raw JSON was
incorrectly displayed as assistant chat. The provider now requests a strict schema;
failed responses stay in inspection, including old cached failures. 428 relevant
regressions, seven focused failure cases and server/web plus desktop builds pass.
See [response-format root cause and handoff](brainstorming-response-format-uat.md).
Its build/runtime evidence is recorded in the linked handoff.

Previous correction: **new story beats append by default; explicit rewrites replace
the working draft**. The model distinguishes these semantically and quotes the
current-author edit clause for a replacement. The shared validator defaults to
append when that evidence is absent, protecting earlier prose even if the model
omits or mislabels its continuation flag. 488 relevant regressions and server/web
plus desktop builds pass. See [append-intent UAT and runtime handoff](brainstorming-append-intent-uat.md).
Live model/native interpretation remains unverified.

Latest delivery: **continuous working draft, automatic generation history and
optional named alternatives**. The shared reference-based UI exposes Version
history, preview/compare, non-destructive restore, change highlighting and Review
& keep. Continuations preserve earlier prose and formatting. 857 combined checks,
ten browser suites, actual rebuilt-app desktop/tablet/phone checks and both host
builds pass. See [working-draft UAT and current handoff](brainstorming-working-draft-uat.md).
Both backend and desktop need rebuilding/restarting for this delivery; older
runtime instructions below describe previous fixes. Author processes and caches
were preserved. Native/live-provider acceptance remains separate.

The follow-up after Keep correction is implemented. The first scene was committed
to the manuscript, invalidating its reviewed writing snapshot, but the chat still
showed Context ready. Native readiness now tracks authored writing, and failed
host preflight revokes shared readiness. Explicit fresh preparation retains the
kept draft immutably and uses the current manuscript for a new proposal. The
mounted first-send/Keep/refresh/second-send flow passes alongside 853 regressions;
no diagnostic live model request was sent. See
[second-send UAT and handoff](brainstorming-second-send-uat.md).

Second-send final evidence: all ten browser suites and both host builds pass;
fourteen focused plus six updated fixture checks also pass. The running author
desktop/backend remain unchanged. A separate rebuilt native executable using
normal Prosa data is ready; close the old desktop window and run
`pwsh -File .\artifacts\brainstorming-second-send\start-fixed-prosa.ps1` from this
checkout. All 19 saved conversation files are byte-for-byte unchanged. Normal
desktop output is still the old running build; the guarded launcher selects the
new separate output. No agent listener was started. Exact path/hashes and remaining
native/live-provider gates are recorded in the second-send UAT and
artifacts/brainstorming-second-send/final-verification.json.

Latest runtime state: the author's next launch failed because the isolated agent
backend still occupied port 5387. That verified agent process (PID 34268) is now
stopped; no listener remains on 5387. No backend was restarted. The author can
launch the normal Debug backend. Prior active-runtime receipts are historical.
Evidence: artifacts/brainstorming-prose-intent/author-launch-port-release.json.

Runtime follow-up: the agent-launched normal-output backend PID 30980 caused
MSB3026/MSB3027/MSB3021 file locks during the author's next build. That verified
agent process is stopped. The backend now runs from separate runtime build output
at localhost:5387 (PID 34268), health 200. A normal Debug build while this backend
was running passed with zero warnings/errors. All 18 original cache files remain
unchanged. See artifacts/brainstorming-prose-intent/build-lock-verification.json.

The focused draft reference screen is implemented in shared web/desktop presentation:
a dedicated proposal surface, Draft/Compare/Details views, compact real formatting
controls, collapsed facts, retained manuscript navigation, and persistent insertion
review actions. Rebuilt web checks cover desktop/tablet/phone, draft retention and
exact review/cancel without manuscript changes; both shipped editor factories and
the Windows build pass. All 408 brainstorming tests passed, followed by focused
regressions after layout refinements. Native Windows interactive acceptance remains
open. See [focused draft UAT and evidence](brainstorming-focused-draft-uat.md).

The prose-request correction is implemented without keyword or regex routing.
Saved metadata confirms the reported scene request was classified Explore and had
no draft. The backend now gives the model semantic instructions to fulfill scene
creation with actual prose in the existing editable proposal editor; Keep separately
commits the reviewed text. A later saved draft is preserved. Latest verification:
852 regressions, ten browser suites and both host builds pass. The rebuilt normal
backend at localhost:5387 returned health 200 (PID 30980 at finalization). The revised
instruction has not had a new live model test. See
[prose intent UAT and handoff](brainstorming-prose-intent-uat.md).

The subsequent generation failure is also corrected: SQL Server's retry strategy
now wraps both acceptance and terminal transactions, with provider dispatch and
usage settlement outside database retries. Real SQL red/green checks cover
generation, lost commit acknowledgments and immutable replay without duplicate
calls/charges/turns. Nine SQL groups and 849 regressions pass. The fixed backend is
running at localhost:5387; the retained cache file is unchanged. The author must
inspect the rejected operation, prepare context and explicitly Send. Actual
native/provider completion remains open. See [conversation SQL UAT](brainstorming-conversation-sql-uat.md).

The live conversation storage failure is resolved: after explicit user approval,
the exact six Brainstorm migrations were applied to configured Azure SQL Prosa.
All six tables/history entries and RecoveryJson are verified. The original running
backend Read/Inspect probes now return ordinary 404 instead of HTTP 500; health is
200. Ten unrelated migrations remain pending, and existing non-Brainstorm columns
and document/page/project counts are unchanged. The author's composer/operation
were preserved and no AI request was sent. Actual native/provider Send acceptance
remains open. See [backend schema fix and application](brainstorming-backend-schema-fix-proposal.md).

The reported desktop preparation reset has an implemented, regression-verified
identity fix, explicitly approved by the user with message recovery. Brainstorm now
uses the authenticated backend's canonical ID, retaining native generation guards
and strict server ownership. Prior native discussion/composer copies recover into
the verified namespace without replaying operations or modifying original files.
Distinct-ID preparation/Send and isolation checks pass; the literal destination
binding is also fixed. Live Microsoft-account/native interactive acceptance remains
open. See [native identity UAT](brainstorming-native-identity-uat.md).

Prompts 1–12 are implemented in this checkout, with required acceptance gates open.
The feature is not release-ready. Both editors have the shared reachable
three-panel Brainstorm workspace, durable session creation/reopening, checked
targets/context, authenticated metered streaming and send/stop/inspection/explicit
retry controls. Discussion stays on the right and unapplied prose appears in the
middle with its original destination. Both hosts now provide persistent version
review, rich manual proposal editing, checkpoints and selection-based follow-ups.
Both hosts provide explicit exact-version manuscript Keep review, durable operation
inspection, guarded undo/redo and original-copy recovery. Both hosts now provide
reviewed selected-field scene ideas, story/character notes and synopsis saves, with
durable operation inspection and guarded undo. Configured English/Swedish dictation
fills the same editable composer; explicit author-prose placement remains a proposal
until separate Keep approval. Explicit spoken turns use the same conversation;
local synthetic answers, interruption/mute/end and version-bound draft read-aloud
are implemented. Clear finalized spoken approval enters only the same exact visible
Keep/planning review; uncertain, negated or changed reviews require confirmation.
Project planning uses ordinary sync; standalone synopsis/notes now transfer through
owned sync v5 with ordinary document conflicts and checked current server context.
Both editors expose explicit session discovery, synchronization, retained conflict
branches and recovery history. Server ledger receipts remain authoritative; transferred
device journals/buffers are labeled recovery evidence in their original namespaces,
with original operation inspection required before replay. Revocation/sign-out clears
active private review while retaining local writing. Real web/Windows capture and
authenticated transcription code exist; live user microphone/provider acceptance
remains open. Speech defaults to disabled until a backend model is configured.
Pending/local writing requires saving/synchronizing and explicit fresh server-context
review before AI generation; it is never silently replaced with older cloud writing.
The disabled-Send follow-up corrects the primary Windows context action to prepare
checked server context. Both hosts place Send and preparation beside the composer
and disclose blockers there. See [Send fix UAT](brainstorming-send-fix-uat.md).
The subsequent [preparation reset fix](brainstorming-prepare-reset-uat.md) addresses
desktop token notifications that incorrectly cleared active review on every request.
Same-account token acquisition now retains the conversation; actual identity/access
changes still invalidate it. The newer Chat/Context/History design is preserved.
The latest [pending context correction](brainstorming-pending-context-uat.md) handles
unsaved/unsynchronized writing and stale open-editor acknowledgments. Desktop
preparation offers an explicit destination-specific Save, sync & prepare confirmation,
then uses ordinary save/recovery/sync before the exact source review. No automatic
AI send or local-only cloud activation is introduced.

## Prompt progress

| Prompt | Scope | Implementation | Verification |
|---|---|---|---|
| 1 | Baseline, contracts, capabilities | Implemented | 64 contract/capability checks passed; affected builds passed; integration map recorded. |
| 2 | Durable sessions and mapping | Implemented | 32 persistence/sync/adapter checks passed; 349 combined tests passed; real Edge storage reload checks, both builds and isolated native startup passed. Live SQL Server gate remains. |
| 3 | Target resolution and context | Implemented | 36 context/adapter checks; 412 combined tests; actual editor projection and safe responsive Razor render fixtures; both builds and isolated rebuilt Windows startup passed. Actual authenticated/native UI UAT remains separate. |
| 4 | Conversational AI and streaming | Implemented | 468 combined tests; both builds; real editor projection/browser render checks and isolated rebuilt Windows startup. Live provider and interactive browser/native UAT remain separate gates. |
| 5 | Shared desktop/web workspace | Implemented | 476 combined tests; both builds; actual shared layout/editor/resize/focus modules in Edge at wide/390px; isolated rebuilt Windows startup. Signed-in interactive browser/native and live-provider acceptance remain separate gates. |
| 6 | Drafts, revisions, version editing | Implemented | 495 combined tests; both builds; both shipped proposal editor factories, rich edits/selection/IndexedDB recovery and wide/390px comparison in Edge; isolated rebuilt Windows startup. Live-provider and signed-in interactive browser/native acceptance remain separate gates. |
| 7 | Reviewed manuscript apply and undo | Implemented | 529 combined checks, including 32 Keep checks and two ordinary page/history checks; both builds; 24 actual web/device editor/review groups in Edge; isolated rebuilt Windows startup. Native interaction, live SQL/provider and packaged acceptance remain separate gates. |
| 8 | Reviewed planning changes | Implemented | 694 combined checks; both builds; six planning review/IndexedDB browser groups plus all prior module suites; rebuilt isolated Windows startup. Interactive native/browser, live SQL/provider and packaged gates remain. Standalone transfer dependency resolved by P9. |
| 9 | Recovery, sync, text acceptance | Implemented; rechecked | Latest: 821 regressions, eight browser suites, both builds, rebuilt Windows startup and actual LocalDev web Keep/Undo/Redo/planning/offline/reload journey. Access revocation and SQL retry transaction fixed. Local SQL Express migrations/CAS/recovery/index probe passes; real account/native/physical exchange/provider and SQL mutation acceptance remain open. Original evidence retained; installed package is optional. |
| 10 | Real microphone dictation | Implemented | 743 combined tests (23 new speech checks and four existing routing checks); both builds; five production capture/render browser groups using synthetic audio plus all prior suites; rebuilt Windows startup. Live English/Swedish microphone/provider, interactive native/browser, SQL and package gates remain separate. |
| 11 | Spoken dialogue and read-aloud | Implemented | 784 combined tests; zero-error server/web and Windows builds; ten browser suites including six voice groups in Edge; rebuilt isolated Windows startup. Real English/Swedish microphone/provider/playback, physical echo, signed-in interaction, SQL and package gates remain separate. |
| 12 | Integrated acceptance | Implemented; required acceptance gates open | 792 regressions, ten browser suites, actual LocalDev server/WASM fixture journey, both builds and isolated rebuilt Windows startup. Seven verified defects fixed. Live AI approval, human EN/SV microphone/playback, native interaction, real account/device/SQL acceptance remain open; installed package is optional. See P12 UAT. |

## Acceptance scenarios

Create isolated test data with Chapter 1 containing multiple sections/pages, Chapter 2 with existing writing, a synopsis, character/place notes, rich text, and an optional note that contradicts the synopsis. Give the records stable identities and record expected context, order, and content before each mutation. Use actual scene/section relationships from the repository. Include a standalone document and local/server mapped copies.

| ID | Scenario | Expected behavior |
|---|---|---|
| B01 | Request the first section of Chapter 2 based on Chapter 1 | Target resolves; preceding chapter is included in reading order or a disclosed summary path is chosen; synopsis and selected sources appear in the actual context manifest; prose appears as a proposal. |
| B02 | Ask for three opening ideas, then discuss one | Conversation advances; manuscript, planning, and current proposed prose remain unchanged until an explicit drafting/revision request. |
| B03 | Ask for a more suspicious protagonist and understated dialogue | A new version uses the preceding draft and selected context; both versions remain inspectable. |
| B04 | Edit a proposed paragraph, reopen, and request another revision | Manual proposal edits survive and are the source for the next revision. |
| B05 | Keep an opening before an existing Chapter 2 section | Exactly the reviewed content is inserted once; existing prose and structure remain ordered and intact. |
| B06 | Keep a replacement for an existing passage | Exact reviewed range is changed; supported rich nodes, formatting, annotations, and unrelated content remain intact. Unsupported application retains the source. |
| B07 | Undo/redo, then attempt undo after editing affected content | Safe undo/redo works; divergent writing is preserved with an actionable refusal/recovery path. |
| B08 | Save one idea as a scene, note, synopsis field, or character note | Each approved target persists with provenance; unrelated fields remain intact; scene ideas do not silently create manuscript prose. |
| B09 | Change selected scene or reorder/delete the captured target | Subsequent requests use the new selection; prior proposals retain their target and cannot silently apply elsewhere. |
| B10 | Duplicate chapter titles or ambiguous voice/text target | Prosa requests a focused clarification and preserves all writing. |
| B11 | Exclude a source, include a conflicting source, or exceed context limits | Excluded source content is absent; material conflict/omission is visible; mandatory chapter context is not silently truncated. |
| B12 | Local writing differs from server writing | The actual checked local context is used through an authorized contract or the unavailable path is explicit; old cloud text is not presented as current. |
| B13 | Restart/reload during send, streaming, draft editing, or application | Accepted turns and candidates survive; interrupted/unknown operations remain inspectable; no duplicate billing, turns, insertion, or false success occurs. |
| B14 | Offline review followed by reconnection | Cached conversation/proposals and local writing remain usable; generation/application is not automatically replayed. |
| B15 | Synchronize sessions between mapped desktop/web copies with concurrent edits | Identities map correctly; retries deduplicate; conflicting branches/edits are retained for deliberate resolution. |
| B16 | Account/backend change, revoked access, or unauthorized source ID | No private context leaks or late event adopts into the new session; writing stays intact. |
| B17 | Timeout, quota failure, malformed output, stream cancellation, late completion | Composer and prior candidates survive; incomplete output cannot be applied; usage and operation states are accurate. |
| B18 | English and Swedish live dictation | Real microphone input creates an editable final transcript in the existing composer; partial recognition does not submit a turn. |
| B19 | Real spoken dialogue and interruption during an answer | Transcript and audio follow the same session; interruption stops stale output; playback/echo never becomes a user command. |
| B20 | Switch text/voice, then request a draft revision | No session/context fork; revised prose remains an unapplied candidate. |
| B21 | Say Keep this version, Don't keep this version, or an uncertain/partial phrase | Only an unambiguous finalized approval for the visible reviewed candidate reaches the checked application path; ambiguity requests clarification. |
| B22 | Read aloud, edit the draft, disconnect audio, or navigate away | Playback follows an identified version and stops cleanly; resources release; transcript, session, and writing survive. |
| B23 | Long conversation, narrow window, keyboard-only operation | Panels remain accessible; bounded context/pagination work; composer, focus, and save behavior remain reliable. |

## Evidence requirements

For each completed prompt, add a dated entry with:

- Implemented production behavior and changed files.
- Contract/schema/migration changes and compatibility requirements.
- Exact commands, checks, results, data used, and defects fixed.
- Browser/server versions and desktop executable/output path actually exercised.
- Separate implementation, automated, browser, native, live-provider, microphone/playback, database/sync, and packaged statuses.
- Unavailable gates with concrete reproduction instructions and the next copyable prompt.

Record test fixtures as fixtures. A successful build or mocked conversation is not evidence of live provider, actual microphone, real playback, native interaction, or packaged acceptance.

## Current handoff

Continue in `C:\Users\Johan\source\repos\WriterApp` with all uncommitted work intact.
First read [second-send UAT](brainstorming-second-send-uat.md). Keep changed the
manuscript without revoking chat readiness; the correction tracks native authored
writing and revokes readiness after a shared preflight failure. Fresh preparation
retains the committed first draft and starts the next proposal from current
manuscript context. Unkept revision/source/destination guards remain intact.
853 regressions pass, including actual mounted Keep/sync/context/follow-up handlers
with a synthetic provider. The author-owned desktop/backend were running during
verification; do not claim those processes loaded rebuilt assemblies or stop them
without preserving their work. No agent background runtime was started for this fix.
All ten browser suites, host builds and a normal-data isolated desktop build pass.
Use artifacts/brainstorming-second-send/start-fixed-prosa.ps1 after the author closes
the old window, or rebuild normal desktop output once unlocked. Restarting the old
normal executable alone will not load the new correction. The existing backend may
remain running for this native fix. All 19 cache files remain unchanged; see the
final-verification.json receipt.
Latest state supersedes the active-runtime wording below: agent PID 34268 is stopped
and port 5387 was verified free for the author's normal launch. Do not leave another
agent background host on that port after verification; preserve author-controlled
hosts. The build already passed, including while the isolated runtime was active.
The latest backend runs from artifacts/brainstorming-prose-intent/runtime-build/
Debug/net10.0 at localhost:5387 (PID 34268 at verification). Normal Debug output is
free for builds. PID 30980 below is superseded: it was stopped after causing the
reported build lock. A normal build passed while the isolated runtime was active.
Do not launch another localhost:5387 listener over the existing one; stop only the
verified owned runtime if taking over that port for an author-controlled launch.
First read [prose intent UAT](brainstorming-prose-intent-uat.md). Preserve semantic
model intent inference, unchanged author input and the structured draft contract;
there is no keyword classifier or extra classification request. Editable prose
belongs in the middle proposal editor and separate Keep commits its reviewed
version. Saved metadata confirms the original reported request was Explore with
no draft, and a later draft already exists. Do not automatically replay or duplicate
it. Latest verification is 852 regressions, ten browser suites and both host builds.
The rebuilt normal backend at localhost:5387 is healthy, PID 30980 at finalization;
its verified launch receipt is in artifacts/brainstorming-prose-intent. Collect actual
author-driven model/native proposal acceptance next; the fictional provider review
packet is prepared but unsent. Preserve all original cache files and pending operations.
The following 2026-10-08 runtime IDs/evidence are historical.
First read [conversation SQL UAT](brainstorming-conversation-sql-uat.md). The post-
migration generation failure was reproduced with the production SQL retry strategy
and corrected in acceptance/terminal storage. Provider dispatch and usage charging
must stay outside transaction retries. Lost acceptance commits are inspection only;
lost terminal commits must not duplicate assistant turns. Fixed localhost backend
PID 13616 is active at 5387; normal backend Debug output is rebuilt. Nine local SQL
groups and 849 regressions pass. Live read-only metadata confirms the retained
operation was not accepted; cache bytes remain unchanged. Let the author reopen/
inspect that saved conversation, prepare current context and explicitly Send.
No automatic replay, cache surgery or diagnostic provider request was performed.
Read the completed [backend schema update](brainstorming-backend-schema-fix-proposal.md):
the user explicitly approved the migration and all six Brainstorm migrations/tables
are now installed in live Azure SQL Prosa. Original localhost Read/Inspect probes
return 404 instead of 500; health is 200 and backend PID 2916 was preserved. Ten
unrelated migrations remain pending. Before/after evidence verifies unchanged
non-Brainstorm columns, document/page/project counts and existing migration history.
See artifacts/brainstorming-save-read/migration-application.json and
migration-runtime-after.json; retain runtime-evidence.json as the pre-update baseline.
Do not rerun an unrestricted database update. Preserve the author's cached composer
and exact pending operation; let the author explicitly retry Send in the retained
conversation. No diagnostic dispatched AI. Native/provider Send remains unverified.
Also read [native identity UAT](brainstorming-native-identity-uat.md). The user
approved canonical backend-ID resolution and discussion/composer recovery after
automatic review rejected the earlier patches. The narrower device-only fix is now
applied and verified; shared/server identity equality remains strict. All 846 combined
regressions, 14 device authentication checks, ten browser suites, both builds and
isolated startup pass. The normal desktop launch folder is rebuilt with matching
device/UI/shared assemblies and ordinary storage. Reopen the same document/account
in that build, then History restores recovered discussion and unsent text. Collect
live Microsoft-account/native interactive preparation and Send evidence; do not
claim that startup or the synthetic distinct-ID fixture proves that gate. Keep every
original operation immutable and never replay recovered discussion as authority.
The production-parent destination binding includes `@` and is covered directly.
Read [pending context UAT](brainstorming-pending-context-uat.md) first. Preserve the
destination-specific save/sync confirmation and cancellation retention. The mounted
desktop regression must start with advancing pending DOM text and run actual
save/recovery, sync v5 API/controllers and device authentication before context and
explicit Send. Already-synced fixture tests alone missed the reported blocker.
Implementation through P12 and independent verification are finished. Required
acceptance remains open; do not call the feature release-ready. Read
the latest [preparation reset root cause](brainstorming-prepare-reset-uat.md): do not
forward every account display/token notification as a private review invalidation.
Canonical identity/generation/access changes still clear review. Preserve the real
device authentication handler in mounted preparation/send regressions. Read
the 2026-10-08 [Send fix UAT](brainstorming-send-fix-uat.md) and preserve its checked
server preparation default, adjacent Send/preparation actions and explicit blockers.
Preparation retains the typed message and never submits it automatically. Read
the prompt 9 recheck below and [P9 UAT](brainstorming-p09-uat.md) for the latest text
recovery evidence and access-loss fix. P10–12 implementation remains intact; do not
restart or remove it when completing text acceptance. Read
[P12 UAT](brainstorming-p12-uat.md) for B01–B23, fixed defects, exact reproductions,
and the boundary between actual browser interaction and synthetic/provider/native
fixtures. Next acceptance prompt:

```text
Preserve the 2026-10-09 second-send correction in docs/brainstorming-second-send-uat.md. Native authored-writing fingerprints revoke context readiness after Keep/undo/edits; shared preflight failure exposes preparation and retains the composer. After explicit fresh preparation, a committed draft already present in the changed manuscript remains immutable and is not silently reused as an unapplied parent. The next proposal uses current manuscript context. Unkept changed-source revisions and changed locations remain guarded.853 regressions pass, including mounted production first-send/Keep/sync/context/second-send and device authentication/revocation checks with a synthetic provider. No diagnostic provider call or real manuscript mutation was performed. Preserve the author's completed draft, Keep receipt and follow-up; require explicit Send after preparation, never auto replay. Rebuild/restart the exact desktop before native acceptance; preserve author-owned processes and leave port5387 to the author. Verify remaining live provider/native/audio/physical-exchange gates independently.
Latest runtime correction: agent PID34268 has been stopped after its listener blocked the author's normal launch. Port5387 is verified free; no backend was restarted. The author should launch their own normal Debug backend. Do not restart an agent background host on this port at handoff. All active-runtime wording below is historical; exact release evidence is artifacts/brainstorming-prose-intent/author-launch-port-release.json.
Preserve the build-lock follow-up: the verified agent backend PID 30980 was stopped and replaced by PID 34268 from artifacts/brainstorming-prose-intent/runtime-build/Debug/net10.0 at localhost:5387. Health is200; a normal Debug build passed with zero warnings/errors while this runtime was active. Use separate output for agent background hosts so normal builds remain unlocked. Verify PID/path ownership before stopping any host, and release the existing owned listener before taking over port5387. Earlier PID30980 references are historical. All18 original conversation cache files remain unchanged; evidence is artifacts/brainstorming-prose-intent/build-lock-verification.json.
Preserve the 2026-10-09 semantic prose correction in docs/brainstorming-prose-intent-uat.md. The reported scene instruction completed as Explore with no draft; a later draft is saved and must remain intact. The model now infers creation intent semantically, without keyword/regex classification or an extra API request. Keep the original author input, checked destination, structured prose response, editable proposal and separate manuscript Keep. Latest evidence is 852 regressions, ten browser suites, both host builds and normal Debug backend health 200 at localhost:5387 (PID 30980 at finalization). The revised instruction has not had a new live model test; synthetic transport/component checks prove routing, not actual model classification. The exact review-only fictional payload and configured model/destination are recorded in artifacts/brainstorming-prose-intent/provider-review.json; diagnostic transmission requires explicit authorization. Collect author-driven generation/native proposal acceptance, preserve all caches and inspect uncertain operations. Previous runtime IDs below are historical. Do not automatically replay completed requests or duplicate the later draft.
Preserve the 2026-10-08 generation SQL correction in docs/brainstorming-conversation-sql-uat.md. Both acceptance and terminal transactions must use SQL Server's execution strategy and clear tracking on retry. Preserve exact immutable operation/snapshot checks; never wrap provider dispatch, usage charging or metering in a retriable transaction. Lost acceptance acknowledgment must remain inspection only, with no redispatch; lost terminal acknowledgment must not duplicate the assistant turn or session revision. New local SQL Express checks reproduce the original error and pass all four conversation cases plus prior storage/recovery groups (nine total), using a synthetic provider and owned disposable data. All 849 regressions and host builds pass. Fixed backend PID 13616 runs at localhost:5387; normal Debug backend output is rebuilt. Retained author cache bytes are unchanged and read-only SQL confirms no ledger for the rejected pending operation. Collect author-driven Reopen / inspect saved conversation, fresh preparation and explicit Send acceptance next. Do not send real writing diagnostically or clear an uncertain accepted operation. Preserve the completed six-migration update and ten unrelated pending migrations.
The 2026-10-08 backend schema failure is resolved after explicit migration approval. Read docs/brainstorming-backend-schema-fix-proposal.md and artifacts/brainstorming-save-read/migration-application.json / migration-runtime-after.json. Six Brainstorm migrations are installed in configured Azure SQL Prosa; original running-backend Read/Inspect probes now return404 instead of500 and SQL health is200. The ten unrelated pending migrations, existing non-Brainstorm columns, document/page/project counts and prior migration history are unchanged. Preserve all existing work and the retained composer/operation; no diagnostic AI request was sent. Latest verification is849 combined regressions, ten browser suites, isolated/normal builds and isolated startup plus approved live SQL metadata/route verification. Collect explicit author-driven native/provider Send acceptance next; inspect uncertain operations before retry. Do not repeat migration application or treat these read-only route probes as end-to-end generation acceptance.
Preserve the latest 2026-10-08 pending-context correction in docs/brainstorming-pending-context-uat.md. Desktop preparation must explicitly confirm its configured-backend Save, sync & prepare action, retain composer/cancel behavior, use ordinary planning/manuscript save and recovery/sync v5, refresh only matching writing metadata, and check exact current server sources. Do not auto-enable cloud sync for local-only manuscripts. Keep offline/conflict/rejection/save failure/concurrent typing blockers safe and visible. The mounted regression now begins with advancing pending DOM text, runs the actual desktop save/preparation/pre-send handlers and real authenticated sync v5/Brainstorm HTTP controllers, then explicitly sends through a deterministic provider. Latest verification: 840 combined regressions, 14 device authentication checks, ten browser suites, three rebuilt LocalDev web groups with zero generation requests, both builds, isolated native startup and matching normal desktop device/UI assemblies in artifacts/brainstorming-p12-pending-context. Restart the rebuilt normal desktop executable, reopen the retained conversation, click Prepare context for sending, confirm the displayed Save, sync & prepare action, and verify Context ready and Send. Real MSAL/native/provider/audio/device/SQL gates remain open; no user writing was used for verification.
Preserve the 2026-10-08 preparation reset correction in docs/brainstorming-prepare-reset-uat.md. DeviceBrainstormingSessions must notify review changes only for canonical backend/account/generation/access changes; routine same-account token acquisition must preserve the active conversation, draft and context. Keep the actual DeviceAuthenticatedHandler in the mounted context/send regression. All four sign-out/reauthentication/rejected-token/revoked-source cases pass without weakening access invalidation. Latest verification: 832 combined regressions, 14 device authentication checks, ten browser suites, rebuilt LocalDev redesign journey, both builds, isolated Windows startup and matching normal desktop device assembly in artifacts/brainstorming-p12-prepare-reset. Preserve the Chat/Context/History redesign and all prior send/context checks. Restart/reopen the existing desktop conversation for hands-on acceptance; no live provider request has been made.
Retain the 2026-10-08 disabled-Send fix in docs/brainstorming-send-fix-uat.md. Windows primary context preparation must prefer checked server context, with offline local review explicit. Preserve the adjacent preparation/Send actions, disclosed blockers, retained composer and failed-refresh readiness invalidation. Historical first Send-fix verification: 828 tests, ten browser suites, thirteen actual LocalDev web checks, both builds, rebuilt isolated Windows startup and matching rebuilt normal Debug desktop UI in artifacts/brainstorming-p12-send-fix. Restart the rebuilt desktop for hands-on confirmation; preparation never automatically sends. Existing provider/native/audio/account/device/SQL gates remain separate.
Complete the remaining P12 brainstorming acceptance gates in docs/brainstorming-p12-uat.md and docs/brainstorming-implementation-status.md. Preserve all uncommitted P1–12 and unrelated work in C:\Users\Johan\source\repos\WriterApp. Read the prompt pack, P12/P11/P10/P09 UAT, baseline, device-development and editor-content-compatibility. Reuse the P12 fresh disposable fixture exporter, launch helper and running web runner; never replace an existing fixture database or touch real manuscripts. Retain the shared conversation access-revocation fix and explicit access-loss responses, alongside the multi-page scene save guard, explicit manuscript route, no-op notes flush, owned session remount, inspection lock, binary-matched CSS and narrow shared shell fixes. Obtain explicit approval before sending the fictional fixture and three bounded text prompts to OpenAI: automatic approval review rejected this transmission without payload/destination approval, and no P12 provider calls have occurred. Record actual metered exploration, drafting and revision from a manual parent in the same session; inspect failed/unknown operations before any retry. Run rebuilt Windows interactive text/Keep/rich replacement/Undo/planning flows, local-unmapped and authorized mapped copies, real account changes, offline/restart/conflict recovery and actual device/web exchange. Windows Computer Use kernel initialization failed twice with apply deny-read ACLs; startup is not native interaction. Human EN/SV microphone and audible local voice/physical echo acceptance remain open. Configure the entitled speech model/key and installed/local voices using P10/P11 instructions; only explicit user actions may record or play. Exercise interruption, noisy/negated/partial approval, version-bound read-aloud, text/voice switching and resource disposal; verify actual reviewed receipts/history and unchanged writing during exploration. Complete keyboard/Narrator or NVDA and long/narrow native acceptance. Local SQL Express migrations/session CAS/recovery/index checks pass. Retain the EF execution-strategy boundary around session saves; complete SQL Keep/planning mutation acceptance and physical authorized device/web exchange using the matching P10/P9 migration chain and sync v5. Deployed SQL acceptance remains separate. Installed/MSIX package acceptance is optional and separate. Historical P9 text recovery baseline: 821 tests, eight prompt 9 browser suites, both builds and isolated Windows startup in artifacts/brainstorming-p09-recheck; actual LocalDev web journey passes with a synthetic restored proposal and real server Keep/Undo/Redo/Notes receipts. Preserve unrelated targeted-quality valid/429 diagnostic and P14 list-ending normalization gate. Fix verified defects, recheck affected flows, stop only owned hosts and update exact binaries/models/voices, evidence, gate matrix and handoff. Do not mark release-ready with required gates open.
```

## Dated implementation evidence

### 2026-10-09 — Semantic prose request correction finalized

The reported scene request was saved with Explore intent and no draft. The model
instruction now treats actionable scene creation as a request for actual prose,
while respecting discussion, planning, negations and reviewed revision references.
No keyword classifier, forced input mode or extra API call is introduced. The real
adapter fixture and mounted Web/Device proposal fixtures pass alongside all 852
regressions, ten browser suites and both host builds. Normal Debug backend build
and exact-process health verification pass. Saved originals and the later draft
are retained. New live model interpretation and native interaction remain unverified.
See [prose intent UAT](brainstorming-prose-intent-uat.md) for exact evidence and continuation.

### 2026-10-08 — Generation SQL retry transactions and active backend corrected

The post-migration Send hit a second defect: direct acceptance/terminal transactions
were incompatible with the configured SQL retry strategy. A real SQL Express
generation check reproduced the exact exception before dispatch. Both transaction
boundaries now use the execution strategy with durable idempotency checks, keeping
provider calls and charging outside retries. Nine local SQL groups pass, including
four normal/transient/lost-acknowledgment conversation cases. 849 regressions and
Release/Debug host builds pass. The verified staged backend replaced the old host
at localhost:5387 (PID 13616); SQL health is 200. The user's retained cache bytes
are unchanged. No author message was dispatched and no further migration applied.
See [conversation SQL UAT and recovery](brainstorming-conversation-sql-uat.md).

### 2026-10-08 — Approved live Brainstorm migration applied and verified

The user explicitly approved the exact migration. The runner verified the reviewed
SHA-256, target database and pending inventory, then executed only the six additive
Brainstorm migrations. Live Azure SQL now has all six Brainstorm tables/history
entries and RecoveryJson. Ten unrelated migrations remain pending; all prior
history entries, non-Brainstorm table columns and document/page/project counts are
unchanged. Original running-backend Read/Inspect probes changed from 500 to ordinary
404 for nonexistent IDs; SQL health is 200. Backend PID 2916 was not restarted.
No cached author message/operation or manuscript was modified or sent to AI.
Application and before/after evidence: artifacts/brainstorming-save-read/
migration-before.json, migration-application.json and migration-runtime-after.json.
Preserve the pre-update runtime-evidence.json and existing 849-test/browser/build
evidence. Native/provider interaction remains an explicit acceptance gate.

### 2026-10-08 — Actual SQL backend missing Brainstorm schema

The author's next Send reaches the retained initial conversation save, then receives
the generic save/read error. Read-only localhost probes reproduce HTTP 500 for
nonexistent session/operation IDs; SQL health is good. Read-only schema inspection
finds zero Brainstorm tables/applied migrations. The retained payload validates
against the running backend's shared assembly; there is no pending generation.

Generated an idempotent script containing exactly the six additive Brainstorm SQL
migrations, excluding ten unrelated pending migrations. That exact script executes
twice in a fresh local SQL database, with six storage/concurrency/recovery/ownership
acceptance groups passing and owned fixture cleanup complete. Capabilities now block
unmigrated Brainstorm storage; 500/503 guidance identifies a backend/database failure
while preserving composer and the exact operation. 849 combined regressions, ten
browser suites, server/client Release and isolated/normal desktop Debug builds pass;
isolated Windows startup passes. All 200 initial Git status entries remain intact.
At this pre-approval checkpoint, automatic review rejected adding a live-database
write mode; the rejected patch was unapplied and no live schema/manuscript/operation
had been modified. Subsequent explicit approval and completed application are
recorded above. Details:
[backend schema fix proposal](brainstorming-backend-schema-fix-proposal.md).

### 2026-10-08 — Approved native identity fix and message recovery verified

User approval authorized canonical backend-ID resolution and recovery. The device
adapter now resolves authenticated `api/auth/me` before forming online scopes,
guarded by native account/generation/backend identity. A local hash cache supports
offline reopening but grants no remote authority. Discussion/proposals/composer
are copied into the verified namespace; originals, receipts, mappings and pending
operations remain unchanged. Recovery is idempotent, excludes foreign namespaces
and deleted sessions, removes authority/outboxes, and requires fresh context.

846 combined regressions and 14 device authentication checks pass. Distinct native
and backend IDs survive actual mounted save/sync/prepare and explicit deterministic
Send; real invalidation behavior remains intact. All ten browser suites and
server/client Release plus isolated/normal desktop Debug builds pass. Isolated
Windows startup passes; normal device/UI/shared binary hashes match that build.
All 197 initial Git status entries remain. No user process/document was touched.
Actual message recovery occurs on the next authenticated Brainstorm scope opening;
live native/MSAL/provider acceptance remains open. Evidence and continuation:
[native identity UAT](brainstorming-native-identity-uat.md).

### 2026-10-08 — Live confirmation still resets the conversation

The user confirms the upper Save, sync & prepare action returns to the welcome
screen. A new test uses `author.native-tenant` for native identity and `author` for
the authenticated backend owner, reproducing exactly that loss of selected scope
in all four mounted cases. The prior 840-pass result did not cover distinct identity
namespaces and does not establish this live flow. The compiled parent also exposed
a missing Razor `@` on the string destination parameter, which is corrected.
Proposal, rejection boundary and reproduction logs:
[native identity fix proposal](brainstorming-native-identity-fix-proposal.md).

### 2026-10-08 — Pending writing still disabled Send

The desktop primary preparation flushed DOM but did not save or synchronize it;
the context service correctly rejected pending writing. Open-editor sync metadata
could also be stale after an acknowledgment. Added a configured-backend confirmation
followed by ordinary save/recovery/sync and matching-metadata refresh, retaining
source equality and concurrent-edit/account checks. Cancel and failed preparation
retain writing and composer. Local-only cloud activation remains explicit.

The mounted regression now covers advancing pending DOM, actual save and sync v5
controllers through `DeviceAuthenticatedHandler`, checked preparation and explicit
deterministic Send, plus all four genuine identity/access invalidations. Eight added
sync cases cover stale acknowledgments and safe failures. See
[pending context UAT](brainstorming-pending-context-uat.md) for exact evidence and limits.
Final verification: 840 combined regressions, 14 device authentication checks, ten
browser suites, three actual rebuilt LocalDev web groups with zero generation
requests, server/client Release and isolated/normal desktop Debug builds (zero
warnings/errors), isolated Windows startup and matching normal device/UI assemblies.
Evidence: `artifacts/brainstorming-p12-pending-context`. All 195 initial Git status
entries remain; user manuscripts and processes were not touched. Required live
native/account/provider/audio/device/SQL acceptance stays open.

### 2026-10-08 — Preparation returned desktop to Start a conversation

Confirmed the real desktop authentication handler caused same-account token
acquisition to fire a review-identity event, clearing the active workspace. The
earlier direct TestServer client bypassed this handler. Device review notifications
now compare canonical identity/generation/access epoch; genuine invalidation remains.
Mounted actual-handler preparation/send fails before and passes after the change.

832 combined regressions, 14 existing device authentication checks, ten browser
suites and the running LocalDev redesign journey pass. Both builds and isolated
Windows startup pass; normal desktop rebuilt with matching device assembly. No live
provider request or user manuscript mutation. Root cause, exact evidence, preserved
redesign, limits and reproduction: [preparation reset UAT](brainstorming-prepare-reset-uat.md).

### 2026-10-08 — Disabled Send follow-up

Fixed Windows' primary local-only context action by preferring the existing checked
server delegate. Both hosts expose preparation, Send and exact blockers beside the
retained composer. Preparation never generates or applies writing. Failed refreshes
invalidate previous readiness; mapping, current-writing, identity, source-choice,
budget and uncertain-operation checks remain intact. Explicit offline review remains.

Verification: **828 passed, zero failed/skipped**, ten browser suites, thirteen
running LocalDev server/WASM checks, zero generation requests, both affected builds
and isolated Windows startup. Standard Debug desktop output rebuilt with matching
shared UI and normal data configuration. Existing user processes and documents
preserved. Reproduction, screenshots, final hashes, retry evidence and limits:
[Send fix UAT](brainstorming-send-fix-uat.md). Evidence:
`artifacts/brainstorming-p12-send-fix`. Earlier evidence and required P12 gates remain.

### 2026-10-07 — Prompt 9 recheck after P12

Re-audited the requested P9 and its common requirements in this same checkout.
Existing P9 recovery/sync v5, P10–12, migrations and unrelated uncommitted work are
preserved. Fixed an additional private-review revocation defect in shared
`BrainstormingConversationHttp`: HTTP 401/403 and explicit owned-source/session
access loss now invalidate active review during source/context reads, sending,
inspection and stopping. `BrainstormingConversationController` and
`BrainstormingStorySourcesController` return the existing
`brainstorming.access_revoked` discriminator for lost access. Missing backend routes
retain owned review; operation absence is still distinct and never authorizes replay.
No new schema, migration, editor asset, provider API or sync version.

The live local SQL Express probe uncovered a second defect: session writes used a
user transaction outside EF's configured retry strategy. `BrainstormingSessions`
now executes the entire save inside that strategy, clearing failed-attempt tracking
and looking up the exact durable operation receipt on retry. No generation or
manuscript application is repeated. The opt-in console under
`Tests/Brainstorming.SqlServerAcceptance` proves the SQL failure is fixed, migrations
apply, concurrent revision writes retain one conflict, and repeated recovery copies
do not manufacture commits.

Added 33 regression cases: transport access/unsupported-backend boundaries, mounted
workspace authentication loss across five actions, and actual authenticated routes
for wrong account versus revoked document ownership. Tests retain cache/writing,
clear active private UI, reject false operation absence and prove zero dispatch,
charges or application history on access failure. Before the fix, 11 focused checks
failed; all 24 initial focused checks and ten actual access-route checks then passed.
`verify-brainstorming.ps1` now accepts an evidence suffix, preserving historical
artifacts, and correctly selects P11 voice checks. The disposable launch helper and
web runner accept a matching artifact directory/framework path without changing
their P12 defaults.

```powershell
./scripts/verify-brainstorming.ps1 -Phase p09 -EvidenceSuffix recheck -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

`artifacts/brainstorming-p09-recheck`: **821 passed, zero failed/skipped**, eight
Edge 154.0.4258.62 browser suites, clean server/WASM Release and Windows Debug
builds. Existing test-only BL0006/xUnit2031 warnings remain. The retained
500 × 1,000-character contract fixture is 811,910 bytes; ten validation iterations
measure 256.2663 ms. The excess turn refuses with original writing retained; this is
contract timing, not UI/provider latency. `git diff --check` passes.

Rebuilt Windows startup: PID 35588 at 2026-10-07T19:20:55.2991595Z, isolated
`native-validation-data`, live Prosa window, no prior user editors; only this PID
closed. Executable:
`WriterApp.Desktop/artifacts/brainstorming-p09-recheck/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`,
SHA256 `2733E6C7F0C93D8E5DD60F302FB1503E993422F61CDA03E89D98C2802DE9F0D3`.
Its rebuilt `WriterApp.Shared.dll` SHA256 is
`B1221AC970AD366D14D919BEBC2169ADF3D8669FD345050C371CA48B6A64C7F9`.
Native interaction remains open; startup is not approval/keyboard acceptance.

Fresh actual LocalDev web journey: `text-acceptance-final.db/json`, two chapters/four
original rich pages, synopsis/character/place notes and omitted conflicting source;
synthetic restored proposal, actual server application/history receipts. Server
`artifacts/brainstorming-p09-recheck/release-build/Release/net10.0/BlazorApp.dll`,
SHA256 `31EB0064A864C3D019604DB7D5EA3ED9C2CE3B2F90956917AD9F0DEA06369560`,
owned PID 21524 started 2026-10-07T19:22:06.7240147Z, loopback port 5399; stopped
after verification. Browser session `93dec3ed-86c7-4bfc-83b9-0245513a2da7`,
finished 2026-10-07T19:23:10.413Z. Twelve checks pass: original pages/context,
manual checkpoint, exact ordered Keep, Undo/Redo, ordinary applied history,
selected Notes save/Undo, offline composer/no replay, full reload and standalone
isolation. Three served WASM hashes match this rebuild; 390px conversation is
366px wide, no overflow/page errors/failed stylesheets. Narrow screenshot inspected.
See `live-web/probe.json`, captures, `web-host-config.json` and
`web-interaction.log`; commands and gate matrix are in [P9 UAT](brainstorming-p09-uat.md).

Local SQL Express 16.0.1000.6 completed five groups at
2026-10-07T19:18:27.1777299Z: all 28 matched migrations including RecoveryJson/P10,
session receipt deduplication/restart, concurrent CAS, concurrent recovery exchange/
restart, foreign-account refusal, unchanged rich writing/history and actual unique
candidate indexes. `sqlserver-acceptance.json/log` records the scope. Disposable
`WriterApp_BrainstormingP09_436db1df5b8b4d0591bfdb94f7c0b57f` was removed; SQL
verified zero remaining probe databases. This is live local SQL storage evidence,
separate from SQL Keep/planning mutation or deployed database acceptance. Command:

```powershell
dotnet run --project Tests/Brainstorming.SqlServerAcceptance/Brainstorming.SqlServerAcceptance.csproj -p:BaseOutputPath=artifacts/brainstorming-p09-recheck/sql-build/ -- artifacts/brainstorming-p09-recheck/sqlserver-acceptance.json
```

Required real account/device exchange, interactive Windows, SQL mutation and live
AI acceptance remain open. Prior automatic approval review rejected sending the
fictional fixture to OpenAI without explicit payload/destination approval; that
question remains pending and this recheck sent zero provider requests. Real audio
and installed package evidence are separate from P9; P10–12 gates remain recorded.
The feature is not release-ready. Current handoff above retains the latest acceptance
prompt and all later implementation.

### 2026-10-07 — Prompt 12

Implemented available integrated verification and fixed seven observed defects in
this original checkout, preserving all prior/unrelated work. [P12 UAT](brainstorming-p12-uat.md)
contains the product profile, six flow packets, issue ledger, B01–B23 matrix and
concrete reproductions for every remaining gate. The feature is not release-ready.

Production fixes: `ProjectSceneContentController` refuses combined replacement of
multi-page manuscript sections and preserves pages on language-only save. Web
`DocumentEditor` keeps multi-page routes in manuscript editing, offers an explicit
linked manuscript route for Brainstorm, and flushes only dirty notes. Both editor
hosts pass a resume scope to shared `BrainstormingWorkspace`; it verifies the active
account/backend partition before restoring the cache and requires fresh context.
`BrainstormingConversationPanel` locks inspection against concurrent send/review
and releases its busy state without generation. `Program.cs` selects scoped CSS
fallback by build configuration. Shared `shell.css` gives narrow Brainstorm a
horizontal app navigation row. Other workspace/navigation rules remain intact.

No P12 migration, rich schema, provider API, quota contract or sync format changed.
Existing P9 RecoveryJson, P10 transcription migrations, sync v5 and matched hosts
remain required. Multi-page scene content edits must use individual manuscript pages;
the server returns an actionable 409 rather than flattening their saved contents.

Verification: `scripts/verify-brainstorming.ps1 -Phase p12 -Browser -PlaywrightModule
'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
-NativeStartup`. Final results are in `artifacts/brainstorming-p12`: 792 tests,
zero failures/skips, ten browser suites, zero-warning/error Release server/WASM and
Debug Windows builds, and isolated rebuilt native startup. Tests add the integrated
fixture, multi-page save/language protection, unedited notes no-op, current versus
foreign remount and delayed inspection serialization checks. Existing writing,
planning, storage/sync, auth, AI metering/history and editor regressions remain in
the filter. Existing test-only BL0006/xUnit2031 warnings remain; production builds
are clean. `git diff --check` passes.

Actual web launch: Release `artifacts/brainstorming-p12/release-build/Release/net10.0/BlazorApp.dll`,
SHA256 `63DB36AE8A3907A091092AF77D1907BC55A7F9BD4FB96BC6EA162C3015246641`,
PID 30984, started 2026-10-07T18:47:44.8229716Z at `http://127.0.0.1:5399`.
Owned host was stopped after verification. Headless Edge 154.0.4258.62, actual
Development LocalDev `dev-oid`, synthetic entitlement, fully migrated
`interactive-seed13.db/json`; no External ID login is claimed. Successful fixture
session `fb642482-66d7-46ef-bde2-9858e6292b14`, finished 18:48:15.051Z.
`Tests/brainstorming-live-web.mjs` verifies the running UI without route/provider
interception: two chapters/four rich pages, synopsis and omitted optional conflicting
note; discover/reopen synthetic proposal; real manual checkpoint; exact Keep and
Undo/Redo preserving every original page; real selected Notes save/Undo; offline
composer/no auto replay; full reload retaining candidate/receipt/composer; separate
standalone session. At 390×844 the conversation width is 366px, document width 390px,
other panes hidden, no failed stylesheet/page errors/alerts. Three served WASM
hashes match the rebuilt Client/UI.Shared/Shared files. See `live-web/probe.json`,
screenshots, host configuration/logs and `web-interaction.log`.

Native output remains
`WriterApp.Desktop/artifacts/brainstorming-p12/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`;
isolated data is `artifacts/brainstorming-p12/native-validation-data`. The final PID,
time and startup result are in `native-startup.json`; only the script-owned native
process is stopped. Interactive Windows automation failed to initialize twice with
the sandbox `apply deny-read ACLs` kernel error. Startup does not close native UAT.

| Area | P12 result / gate |
|---|---|
| Implementation / automated | Available work complete; seven observed defects fixed, regression/build/module evidence above. |
| Running browser interaction | Passed within LocalDev/synthetic restored draft scope; actual server application/planning receipts and ordinary history. Real account acceptance is open. |
| Native interaction | Open; rebuilt isolated startup passed, Windows automation kernel unavailable. |
| Live AI | Open approval gate. Automatic review rejected external OpenAI fixture transmission without explicit payload/destination approval. Approval question remains pending; zero P12 provider requests. |
| Live microphone/playback | Open human EN/SV capture/listening/echo/interruption gates; simulated audio callbacks are labeled. Speech disabled in the fixture host. |
| Database/sync | Real migrated SQLite, local files and authenticated TestServer mapping/conflict checks pass. Live SQL Server/migration-index and physical authorized device/web sync remain open. |
| Package | Optional installed/MSIX acceptance not run. |

All changes remain uncommitted in this same checkout. Earlier prompt evidence is
historical; this entry and the current handoff supersede their next-prompt wording.

### 2026-10-07 — Prompt 11

Implemented in this same original checkout, preserving P1–10 and unrelated work.
Profile, five flow packets, official API sources, bounded output/recognition behavior,
issue ledger and concrete live reproductions are in [P11 UAT](brainstorming-p11-uat.md).

Production changes:

- Shared `BrainstormingVoice.cs`: versioned bounded playback source, portable output,
  exclusive microphone/output floor, stale-ID rejection, cancellation and once-only
  review tickets bound to the original scope/hash. `BrainstormingSpeech.cs` adds
  optional typed recognition quality; no audio persistence or P11 DB migration.
- `BrainstormingConversationPanel.Voice.cs` and shared panel/workspace: explicit
  Start/Speak next/Finish/Interrupt/Mute/End, ordered final transcripts in the same
  composer, existing checked Send/Stop, concise saved-answer output, elapsed time
  and visible playback/error states. No automatic listening/retry or sound on render.
  The shared workspace JS registers visibility/pagehide suspension and disposes its
  callbacks; native deactivation/closing signals the coordinator's same suspension.
  Both cancel pending voice generation, preventing a late answer from resuming audio.
  Windows shares the coordinator as a singleton across native App and Blazor WebView
  scopes so the lifecycle signal reaches the actual conversation panel.
- `BrainstormingProposalPreview.razor` and `BrainstormingPlanningPanel.razor`:
  version-bound explicit read opening aloud, stop on proposal edit/version/identity,
  registration of the exact visible Keep/selected-field review and entry into the
  identical checked approval handlers. Commit success remains its real receipt.
- Web `WebBrainstormingSpeechOutput.cs` / `brainstorming-speech-output.js`:
  English/Swedish local-only Web Speech output, stop/disposal/pagehide and pending
  discovery guards. Windows `WindowsBrainstormingSpeechOutput.cs`: installed
  SpeechSynthesizer/MediaPlayer with transient bounded streams and actual playback
  events. Host DI/capability services and native App lifecycle stop/release audio
  while preserving existing document saves; portable DeviceCore keeps Unsupported.
- `OpenAiSpeechProvider.cs` / `BrainstormingDictationService.cs`: supported 4o
  logprobs request and conservative recognition metadata; absent/invalid evidence
  never authorizes voice apply. Existing model routing, entitlement/quota/usage and
  owned STT inspection remain. `BrainstormingConversationAction.cs` requests concise
  answers in the author's current instruction language without constraining prose.
- `BrainstormingSessionService.cs`: fixed pull clock that rejected a third completed
  conversational generation when server acceptance/completion revisions outpaced
  local pulls. Preserve original request provenance and advance the local snapshot
  to at least the imported clock; no conflicts or author edits are discarded.

Verification:

| Check | Result and limits |
|---|---|
| Combined C# regressions | **784 passed, 0 failed/skipped**. Includes 27 voice tests, real authenticated migrated server and both production host callbacks, spoken exploration/revision/typing, generation interruption/background suspension and exact reviewed Keep/planning persist/Undo. Capture/provider/output responses are synthetic. Existing test-only BL0006 and xUnit2031 warnings retained. |
| Focused boundary run | 86 passed before five added invalid-playback contract cases; final combined run includes them. |
| Server/web Release and Windows Debug | Both passed with **0 warnings/errors**, sequential isolated output. Shared/UI/Device/Client/server compiled; no new Windows dependency in portable assemblies. |
| Browser | Edge **154.0.4258.62**, all **ten suites** passed: IndexedDB, context/editor, conversation, workspace, draft editing, Keep, planning, recovery, dictation and voice. Six new voice groups exercise production Razor/render fixtures and JS with synthetic Web Speech voices/events. Wide and 390px EN/SV screenshots inspected; no clipped controls. Physical audio and signed-in navigation are not established. |
| Native | Rebuilt isolated executable alive as Prosa with isolated document directory at **2026-10-07T17:52:39.940Z**, owned PID **38028** closed. Build/startup repeated after sharing the coordinator across MAUI/WebView scopes; zero warnings/errors. Startup only, not microphone/playback/UI acceptance. |
| Contracts/migrations/content | No new P11 migration or editor assets/schema; matching hosts required for optional Quality JSON. Existing P10 transcription and prior P9 recovery/sync compatibility preserved. All original rich editor/application suites passed. `git diff --check` passed. |

Command: `./scripts/verify-brainstorming.ps1 -Phase p11 -Browser -PlaywrightModule
'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
-NativeStartup`. Rerunnable tests/scripts and ignored artifacts are under
`artifacts/brainstorming-p11` (TRX/logs, browser result JSON/screenshots, native-startup.json).

Rebuilt executable:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p11\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated data: `C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p11\native-validation-data`.

P11-G01/G02 carry real user English/Swedish microphone, live model recognition,
installed/local audible output, physical echo and actual spoken apply in both hosts.
They remain open; default STT is disabled, no user recording/live provider call was
performed, and synthetic engines/startup do not satisfy them. P11-G03 retains live
SQL, signed-in interactive and installed/package gates. Prior targeted-quality
diagnostic and P14 normalization gate remain separate. Required live gates prevent
release-ready acceptance; current handoff is prompt 12.

### 2026-10-07 — Prompt 10

Implemented in the same original checkout, preserving P1–9 and unrelated AI/editor
changes. Product profile, five flow packets, issue ledger, provider setup, privacy
limits and live English/Swedish reproductions are in
[brainstorming-p10-uat.md](brainstorming-p10-uat.md).

Production changes:

- Shared `BrainstormingSpeech.cs`, `BrainstormingSpeechHttp.cs` and
  `BrainstormingDictationWorkflow.cs`: versioned scoped PCM/result contracts,
  microphone/transcriber abstractions, checked authenticated leases, durable
  original-operation journal and once-only CAS composer adoption. No persisted audio.
- Web `WebBrainstormingMicrophone.cs` and shared `brainstorming-microphone.js` /
  `brainstorming-microphone-worklet.js`: explicit getUserMedia permission, bounded
  mono PCM16 WAV, final-only callback, stop/cancel/error, late permission and
  visibility/pagehide cleanup. Availability never starts capture.
- Desktop `WindowsBrainstormingMicrophone.cs`, DI, `App.xaml.cs` and Windows manifest:
  real audio-only MediaCapture on the UI thread, in-memory stream, device/permission
  errors, bounded duration/bytes, cancellation/disposal and background/closing release.
  Native close still preserves its existing save/flush boundary and user sessions.
- AI `AiModality`, provider capabilities/options/router/registry, Program and
  `OpenAiSpeechProvider`: configured real Audio transport over server-only HTTPS
  multipart, explicit model-specific en/sv language field, bounded JSON/text and
  reported token accounting. No audio fallback to text/mock providers.
- Server `BrainstormingDictationService`, controller and transcription record/model:
  owned active document/session authorization, AI policy/quota preflight, timeout,
  serialized acceptance/dedup and unresolved-operation guard, settlement/Unknown
  recovery, original operation inspection, no conversation turn or manuscript write.
- Shared conversation panel/partial, workspace and both actual editor adapters:
  visible language/start/listening/stop/transcribing/error/final-text controls;
  corrected existing composer, explicit Send and separately placed author proposal.
  Identity/scope epochs cancel and refuse late adoption. Unavailable speech leaves
  ordinary typing usable; iOS retains a portable Unsupported adapter.
- Session/draft contracts, persistence/validation, session services and proposal
  preview: optional Dictations cache and AuthorDictated origin/operation provenance.
  New author proposals require an owned completed transcription ledger, create no
  fictitious AI request/assistant turn and retain checked context/target. Identical
  repeated placement deduplicates; existing Keep/Undo paths remain authoritative.

SQLite migration `20261007164538_BrainstormingTranscriptions` and SQL Server
`20261007164539_BrainstormingTranscriptionsSqlServer` add only SessionId/OperationId,
hash/result JSON and restricted session FK. Matching snapshots have no pending model
changes; SQL script asserts no WAV/audio column. Real SQLite migration/restart passes;
live SQL execution remains separate. New strict JSON cache/draft fields require
matching rebuilt hosts. P10 changes no editor schema or shipped editor assets.

Verification command:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p10 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

| Check | Result/scope | Evidence in `artifacts/brainstorming-p10` |
|---|---|---|
| Combined regressions | 743 passed, zero failed/skipped: prior 716, 23 speech checks and four existing routing checks. Migrated owned server, actual shared Start/Stop/correct/place/Send with both production adapters, provenance, exact Keep/Undo, dedup/restart, malformed/oversize/token accounting, quota/auth/cancel/timeout/disconnect/poor recognition, late lease and lost-response/cancel/overfull journal recovery. | `combined-regressions.log`, `combined-regressions.trx`. |
| Server/web Release and Windows Debug | Both passed with zero warnings/errors; isolated outputs and native data. Test-only prior BL0006 and existing xUnit2031 warnings remain in the test build. | `server-build.log`, `desktop-build.log`. |
| Edge browser suites | All nine suites passed on Edge 154.0.4258.62: eight prior storage/context/conversation/workspace/drafts/Keep/planning/recovery suites and five new dictation groups. Actual production worklet captures synthetic oscillator; permission/no-device/short/late-cancel/pagehide and 60-second bounds pass. Both production Razor fixtures allow correction, keyboard controls and unclipped 390px layouts; wide/narrow captures visually inspected. | `dictation-browser.log`, `browser/dictation-result.json`, four dictation screenshots; all prior browser result packets. |
| Rebuilt Windows executable | Isolated startup remained alive as Prosa and initialized its isolated document directory. Agent-owned PID 33740 checked at 2026-10-07T17:08:44Z, then closed; no pre-existing editor PID was stopped. No microphone/native UI recognition claim. | `native-startup.json`. |
| Migration/model/whitespace | Real SQLite tests, SQL Server generated script/snapshot checks and `git diff --check` passed. | Combined TRX, verification script. |

Rebuilt executable:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p10\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated data: `C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p10\native-validation-data`.

Speech setup remains explicit: checked repository backend configuration has Speech
disabled/no selected model; a server key exists but no live speech provider was
called and no user microphone was captured. P10-G01 real English/Swedish recognition,
correction/Send/prose review and lifecycle on browser/rebuilt Windows remains open.
P10-G02 live SQL, interactive signed-in and installed-package microphone permission
gates remain separate. P11 spoken dialogue/playback is not implemented. Carry forward
the original P14 normalization and targeted-quality diagnostic gates.

### 2026-10-07 — Prompt 9

Implemented in the original checkout with P1–8 and unrelated changes retained.
Product profile, five flow packets covering B01–17/B23, issue ledger, measured limits
and concrete outstanding gate reproductions are in
[brainstorming-p09-uat.md](brainstorming-p09-uat.md).

- `BrainstormingRecovery.cs` adds versioned bounded original-namespace copies and
  authoritative ledger history. Optional cache `Recovery` is validated against its
  owned mapping. Cache CAS writes retain private immutable Keep/planning journals
  and previously synchronized copies. `Application/AI/BrainstormingRecoveryHistory`
  and the authenticated session recovery endpoint use existing ownership checks,
  serializable transactions and sync clock; client reports never create committed
  server receipts or application records. Repeated exchange deduplicates exact copies.
- `BrainstormingSessionService` preserves manual proposal buffers when pulling
  conflicting branches, provides explicit owned import, and forks either discussion
  while leaving original pending/journal state intact. Forked discussion has no
  replayable approval/application or active generation. Web/device session adapters
  provide paged mapped-document discovery and explicit history exchange; supporting
  local documents must be mapped first. Accountless data is not implicitly transferred.
- Shared `BrainstormingWorkspace`, `BrainstormingRecoveryPanel` and proposal review
  are reachable from both actual editor routes. They expose discovery, explicit sync,
  deliberate local/server discussion forks, original content and distinct server/device
  evidence. Transferred candidate application history blocks another Keep review/confirm
  and points to original operation inspection. Current local journal still supports
  normal Inspect/Undo. Recovery text is encoded and keyboard-accessible at 390px.
- Remote leases and both adapters now revoke active review by identity epoch on
  authentication/access revocation, refusing late response adoption. Original local
  cache/manuscript is retained. Missing generation inspection now records the inspected
  send as failed/not-dispatched rather than borrowing prior success; composer retained.
- `DocumentSyncContracts`, `DeviceSyncApi`, `DeviceSyncMapping` and server sync
  service/controller add optional typed standalone `Synopsis` on sync v5. Genuine
  standalone classification ignores incidental planning-container state. Authored notes
  use normal version conflicts/transactions and enter current server context exactly;
  older uploads cannot omit an existing synopsis. Missing v5 refuses upgrade without
  discarding local changes/queue. Project planning wire and normal prose rules remain.
  This resolves P8-G03's standalone transfer dependency. Private local operation markers
  remain local; transferred history is evidence, not server mutation confirmation.
- Nullable `BrainstormingSessionRecord.RecoveryJson` has migrations
  `20261007154702_BrainstormingRecoveryHistory` and SQL Server
  `20261007154719_BrainstormingRecoveryHistorySqlServer`, plus both model snapshots.
  Each migration adds only the nullable column. Matching rebuilt hosts/migrations are
  required. Actual SQLite migrations/restart exercised; SQL Server model/script checks
  remain distinct from live SQL execution. No P9 editor schema/asset change.

Verification from repository root:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p09 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj '-p:BaseOutputPath=artifacts/brainstorming-p09/build/' --filter 'FullyQualifiedName~BrainstormingStandaloneSynopsisSyncV5' --nologo -v:minimal
```

Evidence in ignored `artifacts/brainstorming-p09`:

- Combined **716 passed, zero failed/skipped**. New recovery/standalone tests cover
  actual Web/Device shared Send/Review/Keep/Undo through authenticated routes,
  retained manual-source revision, preserved Chapter 2 existing pages, expected three
  provider calls/charges, mapped discovery, repeated exchange, server restart, pending
  device Unknown copies, no forged commit/replay, retained conflicts/forks and active
  revoked review. Earlier P1–8, ordinary save/history, device sync/CAS, planning/scene
  and rich-editor regressions remain included. Final focused standalone case passed
  after adding exact included current server synopsis assertion. Initial combined run
  found one old v4 route expectation; production upgrade explanation and regression
  now require v5. Test-only BL0006 and existing xUnit2031 warnings remain.
- Both server/web Release and isolated Windows Debug builds: zero warnings/errors.
  All eight real Edge suites passed in **154.0.4258.62**: storage, context/common
  editor, conversation, workspace, draft, Keep, planning, recovery. Three recovery
  groups verify origin labels/read-only evidence, actual IndexedDB reload/foreign
  partition and keyboard/no horizontal overflow at 390px. Wide/narrow recovery
  screenshots visually inspected. Prior six planning and 24 Keep groups, plus 109
  common editor checks, pass. Fixtures remain distinct from signed-in interactive UI.
- 500 × 1,000-character messages serialize to **811,919 bytes**; ten deserialize/
  validation iterations measured **388.2562 ms**. The 501st message refuses and the
  original persists. Implemented context/history/cache bounds and summary/omission
  behavior are listed in P9 UAT; measurement is not a provider/UI latency claim.
- Rebuilt executable:
  `WriterApp.Desktop/artifacts/brainstorming-p09/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
  Isolated data: `artifacts/brainstorming-p09/native-validation-data`. Startup PID
  **34272** at **2026-10-07T16:14:12.0780388Z** was alive with `Prosa` window and
  initialized isolated document directory; only that owned PID was closed. Native
  startup is not interactive selection/approval, microphone or installed acceptance.
- `git diff --check` passed. No reset, clean, commit, deployment, real-manuscript
  conversion or user editor shutdown. The unrelated targeted-quality diagnostic and
  P14 list-ending normalization remain separate documented gates.

Implementation and scoped automated/browser/native-startup evidence are complete.
Signed-in interactive browser/native, live SQL Server/provider and installed package
acceptance remain open with P9-UAT reproductions. Microphone/audio are not implemented;
next prompt is 10.

### 2026-10-07 — Prompt 8

Implemented in the original checkout, retaining P1–7 and unrelated uncommitted work.
No commit, reset, clean, deployment or real-manuscript conversion. Product profile,
five flow packets, issue ledger and concrete gate reproduction are in
[brainstorming-p08-uat.md](brainstorming-p08-uat.md).

- Portable `WriterApp.Shared/Brainstorming/BrainstormingPlanning.cs` adds version-1
  owned snapshots, edited selected before/after fields, immutable approval/hash,
  operation receipt and scoped cache journal. Complete turn/context and optional
  draft identity, source revisions and exact approved content remain inspectable.
  Changed schema, bad ownership, oversized groups and unsettled generation refuse.
  Unknown is durable before dispatch and must inspect before explicit retry.
- Shared `BrainstormingPlanningPanel.razor` and conversation contextual actions let
  the author select a retained message or type a decision, choose an owned destination,
  edit fields, inspect before/after, partially approve and cancel. Separate journal
  review exposes original recovery content, inspection and guarded Undo after reopen.
  Identity changes clear private state and reject late captures. Provider text is
  encoded. Scene proposals and saved unlinked scene cards have explicit labels.
- Real web `DocumentEditor.BrainstormingPlanning.cs` and device
  `DocumentWorkspace.BrainstormingPlanning.cs` save pending editor changes and guard
  writable editing through receipt adoption. Only confirmed persistence reloads
  outline/planning. The next story-context review is invalidated; old turn manifests
  are retained. Web project scopes now use the actual saved outline identity.
- Authenticated `BrainstormingPlanningController` and
  `Application/AI/BrainstormingPlanningSaves.cs` use existing owned session checks,
  checked sources, field-level synopsis commands, serializable execution strategy and
  document sync clock. Save, ledger and session provenance commit together. Only
  selected synopsis fields change. Missing/foreign/stale targets cannot mutate.
  Server Undo restores approved fields only or refuses later affected changes.
- `LocalBrainstormingPlanning.cs` uses existing local planning commands, repository
  CAS and atomic document-envelope operation markers scoped to account/backend/
  document. Exact approved review lives in the private session journal. Lost local
  responses inspect the saved marker; later affected edits refuse Undo. Offline
  owned local planning remains available without replay on reconnection.
- Story notes and named character notes use the actual authored synopsis `Notes`
  field. They do not overwrite extracted character canon or bypass bible approval.
  Project synopsis uses its existing store. Standalone desktop synopsis persists in
  `ExtensionData.authoredSynopsis`, visible in the ordinary Synopsis panel, included
  in local context and the real-edit sync fingerprint. **It is local-only until P9's
  owned wire transfer.** Server context preparation refuses omission of these values.
- Scene ideas use a marked unlinked project scene node/card/notes without creating
  sections, pages or scene prose. `LocalProjectStructure` and
  `DocumentSyncService.Projects` accept only explicitly marked unlinked ideas and skip
  prose mirroring. Ordinary project sync preserves scene mapping/card/notes/tags;
  existing unmarked scene-link validation remains. Later links/tags/status/order or
  writing prevent destructive scene Undo. Existing unrelated metadata is preserved.
- `Data/AI/BrainstormingPlanningSaveRecord.cs`, `AppDbContext` and migrations
  `20261007150526_BrainstormingPlanningSaves` / SQL Server
  `20261007150543_BrainstormingPlanningSavesSqlServer` add a session/operation-keyed
  planning ledger, restricted session FK and unique session/candidate index. Matching
  server/shared/client/device rebuilds and migrations are required; existing caches
  use optional compatible fields and real manuscript content is not converted.
  Migrated SQLite actually exercised; SQL Server model/migration-script checks only.

Verification command, from repository root:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p08 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence lives in ignored `artifacts/brainstorming-p08`:

- Combined suite: **694 passed, zero failed/skipped**. New tests cover all four
  destinations in server/local stores and both actual UI hosts, partial approval,
  reopen, immutable duplicate, missing/deleted/stale/private target, delayed identity
  change, malformed scope, SQLite rollback/retry, uncertain HTTP/local response,
  guarded Undo, old manifests, standalone context and genuine-edit fingerprint.
  Ordinary planning/scene approval/project sync, earlier Brainstorm, AI routing,
  save/history/auth/structure and common rich-content regressions are included.
  Initial focused planning run: 28 passed. Final combined run includes six later
  regressions as well. Test-only BL0006 renderer and existing xUnit2031 warnings remain.
- Server/web Release and Windows Debug isolated builds: zero warnings/errors.
  No P8 editor source changes; existing actual shipped editor mapping, draft and Keep
  suites pass. Six new planning browser groups in Edge **154.0.4258.62** verify exact
  selected-field review/cache/old context reload, Unknown journal/foreign partition,
  encoded provider-shaped text, keyboard focus and unclipped 390px layouts.
  Wide web and narrow device screenshots visually inspected. Production host tests
  invoke actual Review/Approve/Inspect/Undo callbacks with synthetic interop/auth;
  rendered browser fixtures are not signed-in end-to-end UI acceptance.
- Rebuilt Windows executable:
  `WriterApp.Desktop/artifacts/brainstorming-p08/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`.
  Startup uses `artifacts/brainstorming-p08/native-validation-data`, confirms a live
  `Prosa` window and isolated document store, then closes only the launched owned PID.
  Final run: PID **22272**, **2026-10-07T15:33:53.8568979Z**, alive with isolated
  document store; no prior user editor PID. Exact output/data are retained in
  `native-startup.json`. This is startup evidence; interactive native approval remains open.
- `git diff --check` passes. No live provider request was needed by planning saves.
  Signed-in interactive web/native, live SQL Server, provider quality, microphone/
  playback and packaged acceptance remain distinct gates with reproduction in P8 UAT.
  Standalone authored planning transfer and cross-device receipt/history recovery are
  explicit prompt 9 work. The unrelated targeted-quality diagnostic remains below
  and was not changed to make P8 pass.

### 2026-10-07 — Prompt 7

Implemented in the original checkout, preserving P1–6 and unrelated uncommitted work.
No commit, reset, clean, deployment or real-manuscript conversion. See the product
profile, five flow packets, issue ledger and gate reproduction in
[brainstorming-p07-uat.md](brainstorming-p07-uat.md).

- Shared `BrainstormingKeep.cs` adds bounded version-1 writing snapshots, exact
  candidate/HTML/target review, pinned operation and created record identities,
  review hash, receipt, undo/redo transition and cache journal. The portable checked
  DOM mapping preserves rich nodes/marks and UTF-16 quotes. Page-start insertion and
  a checked passage inside one top-level paragraph/heading are supported; incompatible
  markup/nested images or ambiguous ranges refuse with originals retained.
- `BrainstormingKeepWorkflow.cs` saves approved recovery evidence and Unknown intent
  before dispatch. An uncertain operation must be inspected before explicit retry;
  known committed/undone candidates do not dispatch again. No reconnect/open apply.
  The private HTTP lease and bounded receipt reader recheck account/backend. Owned
  missing-record inspection returns Failed; a foreign/missing session cannot establish
  absence or expose private evidence.
- `Application/AI/BrainstormingKeeps.cs`, controller, record and DbSet persist one
  immutable application per session candidate. A serializable transaction with the
  existing sync-clock write lock verifies original revisions/order/ownership, changes
  only the page or creates filled section/page/scene and ordering, commits receipt and
  existing web history, then bridges confirmed status into the session. SQLite and
  SQL Server migrations: `20261007102844_BrainstormingKeeps` and
  `20261007102900_BrainstormingKeepsSqlServer`, with both snapshots updated.
- `ManuscriptSceneMirror.cs` extracts the ordinary page-save mirror from
  `PagesController` and reuses it inside Keep/undo transactions. Multi-page manuscript
  pages remain separate; linked scene content receives their existing ordered mirror.
  New-section undo refuses later writing/planning/annotations/order changes. Existing
  annotated server destinations conservatively refuse Keep/undo rather than damage
  anchors; author comments are retained. Server recovery creates a repeatable owned
  copy of the captured original writing in a separate project.
- Device `LocalBrainstormingWriting.cs` uses the existing atomic aggregate CAS store
  and `LocalAiStore` version-6 Applying/Applied/Undoing/Redoing recovery evidence.
  Only confirmed atomic saves mark history Applied and the scoped cache receipt
  Committed. Inspection after interruption proves before/after or preserves divergence;
  it reconciles history metadata without replaying writing. Existing local history
  commands provide guarded undo/redo and snapshot-copy recovery. Account-bound entries
  remain scoped to their original account/backend. Accountless cached approved writing
  can commit locally offline; mapped server-source review must first prove freshness.
- Shared proposal review and both `DocumentEditor.BrainstormingKeep.cs` /
  `DocumentWorkspace.BrainstormingKeep.cs` production handlers flush/checkpoint, show
  original destination and exact content/mode, explicitly approve, inspect, retry,
  undo/redo and recover. Web mutation/inspection freezes pending writing through receipt
  adoption; identity epochs reject late review/cache adoption. Common editor
  `previewBrainstormingKeep` independently checks the actual schema transaction.
  Both shipped bundles were regenerated. Manuscript capability is available; planning
  and speech remain unavailable. No new provider call or billing action is introduced.

Reproduce with `npm run build` in `WriterApp.Client`, then:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p07 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Ignored evidence: `artifacts/brainstorming-p07`.

| Check | Result and scope | Evidence |
|---|---|---|
| Combined regressions | 529 passed, zero failed/skipped: 32 Keep cases, two ordinary page/history cases, 495 existing contract/writing/auth/sync/history/provider-policy regressions. Real migrated SQLite and atomic device files; synthetic authentication/provider/interop; actual shared approval plus both production host handlers. | `combined-regressions.log`, TRX, filter in script. |
| Builds | Server/web Release and isolated Windows Debug passed with zero warnings/errors. Existing LocalStoryboard xUnit2031 analyzer warning remains in test compilation. | `server-build.log`, `desktop-build.log`. |
| Actual editor/browser | Edge 154.0.4258.62: 24 Keep groups passed for empty/insert/replace/create, manual rich/Unicode content, restart rendering, preserved list/table/link, stale/forged/unsupported refusal, production Razor review at 1400×950 and 390×844 with keyboard focus. Existing storage, 109 editor checks, context/conversation/workspace and proposal module checks passed. Screenshots visually inspected. | `browser/keep-result.json`, `keep-web/device-wide/narrow.png`, all six browser logs/reports. |
| Rebuilt Windows | Isolated executable startup and local document directory initialized; only the owned PID closed. Startup is separate from interactive Keep acceptance. | `native-startup.json`. |
| Whitespace/compatibility | `git diff --check` passed; new SQLite migration exercised; matching SQL Server migration generated. No editor schema or document/sync wire-version change. | Script/TRX/migrations. |

Rebuilt executable:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p07\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated data: `C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p07\native-validation-data`.
Final startup: PID 32720 at `2026-10-07T11:00:29.5369717+00:00`, alive as Prosa,
isolated documents initialized. No preexisting desktop PIDs were present; only the
owned process was closed.

Separate diagnostic: an overly broad `DesktopAiWebPersistence` filter also selected
unrelated targeted-quality retry tests. The `valid` and `429-between` cases failed
(`true` versus `false`, and expected two requests versus one). That expanded run was
stopped and retained as `expanded-quality-diagnostic.log`; no baseline attribution
or fix is claimed. The final filter adds only the changed ordinary page/history
tests. Reproduce the targeted-quality cases separately before drawing conclusions.

Gates: signed-in interactive browser/native Keep with pending edits/selection and
mapped local/server copies; live SQL Server migrations and provider quality; installed
packaging; speech. Existing SQLite multi-manuscript index-migration and P14 list-ending
editor normalization gates remain. P7 local receipts/history are local writing evidence;
they are deliberately separate from server mutation claims and full cross-device receipt
transfer remains P9. Matching client/server is required for new strict JSON cache fields.
Next implementation: prompt 8.

### 2026-10-07 — Prompt 6

Implemented in the same checkout, retaining uncommitted prompts 1–5 and unrelated
work. No commit, reset, clean, deployment or conversion of real manuscripts.
Generated and author-edited proposals remain inert conversation data. No manuscript,
planning, structure, canon or AI mutation-history entry is written by these flows.

- `WriterApp.UI.Shared/BrainstormingProposalPreview.razor` and stylesheet now present
  previous/next persistent versions, captured manuscript-source versus candidate
  comparison, constraints, introduced facts, contradictions, original destination,
  parent/instruction/context provenance and independently derived application status.
  The mounted manuscript editor remains separate. Shared proposal editing reuses the
  existing shipped common TipTap editor and compatibility rules via
  `wwwroot/brainstorming-proposal.js`; provider markup remains literal text and authored
  snapshots are validated before mounting.
- `BrainstormingSessionService` saves a bounded recoverable draft-edit buffer separately
  from the immutable draft history. Explicit checkpoint, next AI send, workspace exit
  or deliberate conversation switch checkpoints it into a new authored version. It
  preserves original request/assistant/context/target provenance and warnings, assigns
  a new ID/parent/revision/time and does not overwrite an earlier candidate. Empty,
  unsupported, mismatched or stale manual edits refuse with source/buffer retained.
  Normal editor opening does not itself create a manual version.
- New portable `BrainstormingDrafts.cs` supplies typed reviewed-candidate, edit-buffer
  and selected-passage contracts. Selection pins candidate ID/fingerprint, exact quote,
  UTF-16 offsets and `prosa-quality-utf16-v1`; both workflow and authenticated server
  check the owned selected draft before dispatch. Invalid ranges, lost quotes,
  wrong candidates/fingerprints and split surrogate pairs cannot become guessed input.
- `BrainstormingConversationPanel`, workspace, workflow and both actual editor hosts
  bind the reviewed candidate and pre-send checkpoint to P4's real authenticated
  transport. A later revision uses the manual checkpoint, not an older generated draft.
  Same-destination fresh review keeps the original target ID; changed source/position
  refuses revision. Selecting a different scene uses its fresh target without retargeting
  the older draft; explicitly selected old passages require returning to that candidate.
  The author can also deliberately discuss the reviewed destination without the candidate.
- `BrainstormingConversationAction` includes the exact previous rich/plain candidate
  and verified selection in quoted conversation data, retaining intent inference through
  the existing metered provider route. Explore/Clarify leave drafts unchanged. Only a
  validated terminal Draft/Revise creates a new generated version; its revision follows
  its parent and per-version contradiction warnings remain inspectable. Unknown,
  cancelled and invalid output cannot become a partial version or automatically replay.
- Proposal edit capture is bound to the original candidate/scope/epoch, including delayed
  JS callbacks and account/document changes. The proposal editor is read-only during
  generation. Navigation, ordinary writing and existing DOM/source/auth guards remain
  in effect. Session changes release the host's generation presentation state.
- `BrainstormingDraftContent.cs` extracts the existing server read-only text mapper and
  reuses the common HTML allowlist/nesting rules for authored edit snapshots. Shared
  adds the already-used portable AngleSharp 1.8.2 dependency. Old inert legacy draft HTML
  is retained; unsafe/incompatible/mismatched snapshots refuse editing/revision instead
  of being flattened. No manuscript/editor schema, sync-format or database migration.
- Version-1 JSON gains optional `DraftReview`/`DraftEdit` cache fields, optional send
  `Selection`, and per-draft `Contradictions`. Older caches select their latest draft by
  default; edited rich versions/parent IDs remain supported. Strict older backends cannot
  accept P6 fields: rebuild/deploy the matching server/client together; a rejected sync
  preserves the local candidate. No optimistic application capability is enabled.
- `WriterApp.Client/src/tiptap-editor.ts` exposes the existing common editor factory as
  `tiptapEditor.createBrainstormProposal`. `npm run build` regenerated shipped assets;
  no common editor source/schema changed. `BrainstormingDraftTests.cs` adds 19 focused
  cases and exports validated fixtures. `Tests/brainstorming-drafts.mjs` exercises both
  actual editor factories and IndexedDB; verification script adds p06.

Reproducible checks (PowerShell 7.6.5, .NET SDK 10.0.400, Node 24.13.1):

```powershell
# In WriterApp.Client:
npm run build
# In repository root:
./scripts/verify-brainstorming.ps1 -Phase p06 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Ignored evidence: `artifacts/brainstorming-p06`. Product profile, flow packets,
fixed-issue ledger and gate reproduction: [brainstorming-p06-uat.md](brainstorming-p06-uat.md).

| Check | Result and scope | Evidence |
|---|---|---|
| Combined regressions | 495 passed, 0 failed/skipped, including 19 P6 cases and existing P1–5/writing/auth/content/sync/history/quota boundaries. Production host checkpoint handlers, shared edit/version/send handlers, real atomic device files, SQLite owned session/generation services and deterministic provider. Existing xUnit2031 LocalStoryboard warning only. | `combined-regressions.log`, TRX; exact filter in script. |
| Both affected builds | Server/web Release and Windows Debug passed, 0 warnings/errors. Isolated outputs/data; shared/device portability retained; full iOS build outside scope. | `server-build.log`, `desktop-build.log`. |
| Existing browser regressions | Edge 154.0.4258.62: six IndexedDB groups, 109 editor checks, nine context groups, eight server projections plus both rendered conversations, four workspace groups passed. | `browser/result.json`, `context-result.json`, `conversation-result.json`, `workspace-result.json`. |
| Proposal editor/browser | Both real web/device factories passed rich marks/Unicode editing, exact UTF-16 selected quote, generation read-only state, 1400×950 and 390×844 comparison/layout/focus, unsupported markup refusal and real IndexedDB edit-buffer/review reload. Static real-handler Razor and .NET-validated synthetic storage/interop fixtures, not signed-in app journeys. Screenshots visually reviewed; no browser errors. | `browser/draft-result.json`, `draft-web/device-wide/narrow.png`, `drafts-browser.log`. |
| Rebuilt Windows executable | Passed isolated startup as Prosa with isolated document directory; only the owned process closed. Native interaction remains separate. | `native-startup.json`. |
| Compatibility/whitespace | Existing migration/model and actual editor/source mapper regressions passed. No P6 migration. `git diff --check` passed. | Script/TRX. |

Rebuilt executable:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p06\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated data:
`C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p06\native-validation-data`.
Final startup: PID 21312 at `2026-10-07T10:01:08.1748813+00:00`, alive as Prosa,
isolated documents initialized. No preexisting desktop PIDs were present; only this
owned process was closed. Complete report is in `native-startup.json`.

Separate pending gates: signed-in interactive web/native full sample journey,
configured live-provider quality/intent/selection-following and usage/cancellation,
P7/P8 approved mutations, P9 cross-device remote-only discovery/sync/recovery,
live SQL Server and preexisting SQLite multi-manuscript migration-index acceptance,
packaged/MSIX and speech. Tests establish the stated production-handler/module scopes;
no live provider, microphone or native interaction is claimed. Current next step is P7.

### 2026-10-07 — Prompt 5

Implemented in the same checkout with prompts 1–4 and unrelated work retained.
No commit, reset, clean, deployment or real-manuscript conversion was performed.

- `WriterApp.UI.Shared/EditorWorkspace.razor` and stylesheet keep the manuscript
  component in its original render position while adding host-supplied outline on
  the left and conversation on the right. Desktop collapse and narrow pane tabs
  keep all mounted editor/session state. Ordinary context remains mounted while
  hidden. The Brainstorm entry in both actual manuscript editors uses lazy first
  activation so ordinary writing does not initialize conversation dependencies.
- `BrainstormingWorkspace.razor`/stylesheet use the actual host session service,
  scoped cache, context preparation and P4 conversation workflow. New/reopen and
  paginated saved-session choices are deliberate. The picker lists conversations
  cached in the current device/browser/account/backend/document partition; mapped
  sessions can be inspected against the owned server. Discovery/import of sessions
  existing only in another browser/device is a P9 integration gate. No automatic
  session exchange, paid generation or manuscript/planning application occurs.
- `BrainstormingContextReview.razor` restores included/excluded sources, summary
  provenance and selected support on reopen, labels the captured original target,
  and requires fresh review before send. Selecting another scene invalidates next
  request readiness without changing earlier proposals. Device generation retains
  P4's explicit synchronized server review and pending-writing refusal.
- `BrainstormingConversationPanel.razor`/stylesheet retain the multiline composer,
  ordered messages, stopped/uncertain/error/quota/offline/unsupported states, bounded
  40-message presentation with deliberate earlier-message expansion, inspect and
  explicit new-request retry after known failure/cancellation. `brainstorming-workspace.js`
  restores focus/caret after a send attempt. Opening cache temporarily makes the
  composer read-only to prevent late restoration overwriting new input. Scope/account
  changes discard active private presentation, cancel old work and retain old caches.
- `BrainstormingProposalPreview.razor`/stylesheet show safe inert prose in the middle,
  constraints, version/context/parent provenance, original destination and explicit
  navigation. A document guard prevents another manuscript's cache appearing. It is
  read-only; version editing belongs to P6 and application to P7/P8.
- Both `DocumentEditor.Brainstorming.cs` and `DocumentWorkspace.Brainstorming.cs`
  bind the actual outline and checked page navigation. Web entry retains the same
  `PageEditor`; desktop flush captures DOM into pending local writing without forcing
  a save. Navigation reuses existing save/refusal paths. Old proposal targets never
  follow the new selection. Document changes release busy state and clear active
  private cache; older asynchronous loads cannot clear a newer operation's state.
- `PanelResizeHandle.razor.css` and `wwwroot/panel-resize.js` extend existing keyboard,
  pointer, bounds and storage behavior to outline/conversation handles, positioned
  at pane edges. `BrainstormingWorkspaceTests.cs`, production adapter tests and
  `Tests/brainstorming-workspace.mjs` cover the new flows; the verification script
  gains p05. No wire/schema/migration, rich-text vocabulary or editor bundle change
  in P5; P1–4 contracts, additive migrations and ownership rules remain in force.

Reproducible verification (PowerShell 7.6.5, .NET SDK 10.0.400):

```powershell
./scripts/verify-brainstorming.ps1 -Phase p05 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence under ignored `artifacts/brainstorming-p05`; durable flow packets, issue
ledger and interactive reproduction are in [brainstorming-p05-uat.md](brainstorming-p05-uat.md).

| Check | Result and acceptance scope | Evidence |
|---|---|---|
| Combined regressions | 476 passed, 0 failed/skipped. Includes seven new workspace cases, authenticated HTTP Stop → inspect → explicit new request, both production editor entry/scope/send/context/navigation safety handlers, restored choices/composer, document change, unchanged mounted editor, long safe transcript and existing P1–4/writing/storage/sync/auth/quota/history/content boundaries. Real SQLite and authenticated TestServer with deterministic provider, synthetic interop/editor fixtures. Existing LocalStoryboardTests xUnit2031 test warning only. | `combined-regressions.log`, `.trx`; exact filter in script. |
| Server/web Release and Windows Debug | Both passed, 0 warnings/errors; isolated build output. Shared/device libraries retain portable dependencies; full iOS build outside scope. | `server-build.log`, `desktop-build.log`. |
| Real browser storage | Six production IndexedDB groups passed with validated synthetic .NET session data, reload/account partition/offline/concurrency coverage. | `browser/result.json`. |
| Real editor bundles and rendered review | Headless Edge 154.0.4258.62: 109 existing editor checks, nine context groups, eight server projection comparisons and web/device rendered conversation checks passed. | `browser/context-result.json`, `conversation-result.json`. |
| Three-pane browser modules | Four groups passed: real keyboard/pointer resizing/bounds, safe long transcript/original target presentation, 390px access to every pane/multiline/focus/caret, exact rich Unicode pending device HTML retained through resizing/pane changes. Screenshots visually inspected; no browser errors. Pane click binding is a fixture over actual rendered Razor; C# production controls tested separately. | `browser/workspace-result.json`, `workspace-wide.png`, `workspace-narrow.png`. |
| Rebuilt Windows executable | Passed isolated startup: alive as Prosa, isolated documents initialized; only this run's owned PID closed. No interactive native journey claim. | `native-startup.json`. |
| Whitespace/migration compatibility | `git diff --check` passed; existing migration/model checks in regressions. No P5 migration. | Script and combined TRX. |

Rebuilt executable:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p05\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated data: `C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p05\native-validation-data`.
Final startup: PID 31892 at `2026-10-07T09:32:54.3414897+00:00`, alive as Prosa,
isolated documents initialized; no preexisting desktop PIDs were present. Only the
owned validation process was closed.

Separate remaining gates: signed-in interactive browser/native workspace journey,
configured live-provider streaming/cancellation and usage settlement, cross-device
remote-only discovery/sync/recovery acceptance (P9), live SQL Server and the preexisting
SQLite multi-manuscript migration-index issue recorded under P2. Microphone/playback,
packaged/MSIX and distribution acceptance were not run. P5 fixtures prove production
handlers and modules in their stated scopes; native startup does not prove interaction.
Current next step is P6's persistent version and manual proposal review, not application.

### 2026-10-07 — Prompt 4

Implemented in the same checkout, preserving uncommitted prompts 1–3 and unrelated
work. No commit, reset, clean, deployment or real-manuscript conversion was performed.
Generation saves conversation and inert proposals; manuscript, outline, synopsis,
canon, planning and application/history records are unchanged.

Production changes and files:

- `AI/Actions/BrainstormingConversationAction.cs`, `AI/Core/AiOrchestrator.cs`:
  registered dedicated action with bounded prior accepted dialogue (30 turns / 24,000
  characters), approved decisions, checked target, optional preceding draft, exact
  included context and current instruction. Natural language selects typed Explore,
  Clarify, Draft, Revise or ProposePlanning; no mode selector or mutation tools.
  The generic action catalogue/execution/streaming route refuses this action so it
  cannot bypass checked context, operation persistence and quota settlement.
- `Application/AI/BrainstormingGenerationContext.cs`: server recaptures owned current
  writing, selected support, selected canon entitlement/freshness/version, grouped
  page provenance and source choices before dispatch and before accepting completion.
  Ready flags/hashes are not authorization. Author summaries retain their exact
  original revision/hash; required omissions remain explicit. Its read-only HTML text
  projection matches the shipped web/device quality mapper for the executed fixtures,
  including preserved whitespace, empty blocks, image parsing and horizontal rules.
- `Application/AI/BrainstormingConversationService.cs`,
  `Controllers/BrainstormingConversationController.cs`, `Program.cs`: authenticated
  context review, NDJSON stream, owned inspection and stop endpoints; existing router,
  usage policy, quota admission/charge and usage meter. Scope, operation, request,
  assistant-turn and monotonic sequence travel with every event. Answer preview,
  draft-checking status, validated terminal output and usage are separate. Capacity
  admission precedes a paid request; one active/unknown generation reserves the
  session slot. Server timeout defaults to 180 seconds and is bounded to 1–300.
- `Application/AI/BrainstormingSessions.cs`, `Data/AI/BrainstormingGenerationRecord.cs`
  and `Data/AppDbContext.cs`: acceptance commits user turn/request and unique operation
  ledger atomically before dispatch; terminal assistant/draft/planning and ledger
  commit atomically. Public session CRUD cannot forge/change generation-ledger turns,
  requests or generated drafts, or alter a session while generation is active/unknown.
  An identical operation replays saved events only; a changed duplicate is rejected.
- `AI/Providers/OpenAI/OpenAiProvider.Brainstorming.cs`, `OpenAiProvider.cs`,
  `AI/Abstractions/AiStreamEvent.cs`, `WriterAiOptions.cs`: dedicated Responses streaming
  adapter using configured server credentials/model/base URL. It requests no tools,
  disables provider storage, bounds SSE/output and requires a real completed response
  with terminal usage. Failed/incomplete responses, tool output, reordered events and
  EOF without completion cannot become successful proposals. The other existing AI
  paths retain their behavior. Current [official streaming documentation](https://developers.openai.com/api/reference/resources/responses/streaming-events)
  was checked; synthetic HTTP events do not establish live-provider acceptance.
- `WriterApp.Shared/Brainstorming/BrainstormingConversation.cs`,
  `BrainstormingConversationHttp.cs`, `BrainstormingConversationWorkflow.cs` and
  `BrainstormingPersistence.cs`: strict version-1 response/stream/send/usage contracts,
  bounded NDJSON and identity leases, duplicate/order validation, durable client
  pending-send identity, retained composer, terminal inspection/pull and no automatic
  generation retry. Response intent uses validated string enums for provider JSON;
  existing session wire enums stay compatible. Additive optional cache fields retain
  pending and last generation. Unsupported/unknown future payloads remain rejected.
- `WriterApp.Client/Services/WebBrainstormingSessions.cs`,
  `Pages/DocumentEditor.Brainstorming.cs` and editor Razor integration:
  current auth/source leases, active DOM HTML check and explicit mapping before send.
  Unsaved web HTML differing from the current owned server page blocks generation.
- `WriterApp.Device.Shared/Services/DeviceBrainstormingSessions.cs`,
  `DeviceBrainstormingContext.cs`, `Pages/DocumentWorkspace.Brainstorming.cs` and editor
  Razor integration: explicit **Review synchronized server context for AI** action
  verifies synced local documents, section/page identity and content against newly
  captured owned server sources, then maps context/targets back to local identities.
  DOM/whole-source/account checks run before and after capture and before send.
  Standalone documents retain a null server project scope. Pending writing is kept
  locally and blocked; preparation does not save/sync it or substitute old cloud text.
- `WriterApp.UI.Shared/BrainstormingContextReview.razor`,
  `BrainstormingConversationPanel.razor` and stylesheet: minimal reachable shared
  Send/Stop/Reopen-inspect controls in both existing editors, durable multiline
  composer, ordered encoded text, answer preview, usage, contradictions, inert prose
  and planning details, and incomplete/rejected output for inspection. Auth/navigation
  leases discard late presentation. This is the P4 integration surface; P5 still owns
  the complete three-panel layout, session selection and fuller interaction states.

Contracts and migrations:

- Send is bounded to 8,000 instruction characters; dialogue to 24,000; provider output
  to 150,000; prose to 100,000; answer to 50,000; planning to 20 entries; stream journal
  to 512 application events. Unknown/duplicate JSON properties and inconsistent
  intents/prose/planning are rejected. Only a validated Completed outcome with settled
  usage creates a complete assistant turn or generated proposal. Prose stays plain
  text with constraints/facts, server-created identity, context/target and parent.
- SQLite `20261007080002_BrainstormingGenerations` and SQL Server
  `20261007080044_BrainstormingGenerationsSqlServer`, designer files and snapshots add
  only the durable generation table, session FK and unique `(SessionId, OperationId)`
  with payload fingerprint and revision. SQLite full migrations and existing-data
  preservation ran; SQL Server generated script/model checks ran without a live DB.
- Usage uses provider terminal tokens where present and the existing policy's
  estimation fallback when stopped/interrupted without usage. Settling is persisted
  before calling non-idempotent quota/meter services. A provider-disconnect or uncertain
  settlement retains Unknown and blocks another generation in that session. A worker
  lost on restart is inspected as Unknown; neither inspection nor duplicate send
  restarts it. Confirmed owned missing operations and known terminal failure allow an
  explicit new operation; generic route/access 404 does not clear a pending send.
  No manual reset/reconciliation workflow is shipped in P4. Retain the original ledger
  for provider/accounting reconciliation; do not delete it to enable retry.

Executed verification:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p04 -Browser `
  -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' `
  -NativeStartup
```

- **468 tests passed, 0 failed/skipped**: 177 brainstorming checks and 291 existing
  writing/structure/history/sync/auth/AI/quota/usage/editor checks. P4 adds 31 service/
  transport/recovery cases, four authenticated/host-handler/navigation cases, ten provider/
  response/projection cases and one generic-route guard case. Final evidence:
  `artifacts/brainstorming-p04/combined-regressions.trx` and `.log`.
- Deterministic provider + synthetic HTTP/SSE: exploration without prose, clarification,
  typed Draft/Revise ancestry, owned planning without mutation, exact-operation replay,
  concurrent request blocking, explicit retry after known failure, quota/policy refusal,
  stale/forged/unauthorized context, owner revocation, malformed/partial/tool output,
  disconnect, stop, timeout, late completion, usage settlement failure, restarted worker,
  ordering/duplicate changes and auth loss. Actual OpenAI adapter executes against a
  synthetic handler with a synthetic key; no live credential/provider request occurred.
- Actual production ASP.NET authenticated controllers and HTTP adapters execute against
  migrated SQLite; wrong-document/account/anonymous access and owned missing operation
  are exercised. The actual web `DocumentEditor.BeforeBrainstormingSend`, device
  `DocumentWorkspace.PrepareServerBrainstormingContextAsync`/`BeforeBrainstormingSendAsync` and shared Send
  handlers run with production persistence/mapping and HTTP. DOM interop, auth and
  provider are explicitly test fixtures. Reopen checks persisted turns/composer and
  dirty writing refusal; real local `ApplySyncAsync` creates the mapped device source.
- Release `BlazorApp.csproj` (including client/shared/UI) and Debug rebuilt Windows
  desktop passed with **0 warnings/errors**. Existing unrelated test analyzer warning
  xUnit2031 in `LocalStoryboardTests.cs` remains. Builds run sequentially with isolated
  output; no user executable/output is replaced.
- Headless **Edge 154.0.4258.62**: six real production IndexedDB reload/isolation/race/
  offline/corruption groups; 109 existing real editor checks plus nine source/context
  groups; eight server projection cases compared to both actual editor bundles, and
  both actual C# handler Razor renders checked for encoded text, controls, keyboard
  focus and 390 px overflow. No browser errors. Browser artifacts/logs and narrow
  screenshots live under `artifacts/brainstorming-p04/browser`. Static Razor fixtures
  are not interactive signed-in browser/native UI acceptance.
- Rebuilt executable
  `C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p04\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`
  launched for seven seconds, alive with title Prosa and initialized isolated documents
  in `artifacts/brainstorming-p04/native-validation-data`. Agent-owned PID 32860 alone
  was closed; no prior editor PIDs existed. Timestamp `2026-10-07T08:49:50Z`.
  `native-startup.json` proves startup only. `git diff --check` passed.
- Browser comparison found and fixed server/editor text differences for inline image
  omission and preserved inter-block whitespace. A stream-disposal regression found
  that async-iterator cleanup could leave a Settling operation; terminal settlement
  now runs in one awaited ordinary task, with a regression proving known failure and
  explicit subsequent send. Browser render fixtures now request Blazor's normal final
  post-handler render instead of exporting the last busy preview. A production shared
  Send/navigation regression also proves that a scope change cancels the old request,
  releases the new workspace's busy state, preserves the old composer and prevents
  late cleanup from disposing the new workspace's cancellation handle.

Remaining gates and reproduction:

- **Interactive signed-in browser/Windows + live provider:** run the built server and
  rebuilt desktop against isolated owned synchronized writing using normally configured
  server credentials. Review context explicitly, ask for opening ideas, then a draft
  and revision; confirm answer/prose separation and retained version/target identities.
  Stop one request, interrupt another, reopen/inspect, and verify the durable outcome,
  quota/meter and unchanged writing. Do not reissue an Unknown operation. Also navigate/
  change account during output and retain unsent local writing. No live paid request
  or native interactive UI was exercised here. P5/P6 supply the fuller workflow.
- **Live SQL Server / packaged/deployed:** apply additive migrations in an isolated
  authorized database, inspect existing data and run owned endpoint/concurrency cases.
  Generated script and SQLite do not prove SQL Server runtime; no packaging/deployment
  was attempted. Preserve the separately documented pre-existing SQLite multi-manuscript
  unique-index defect; P4's migrated fixtures do not repair or prove that gate.
- **Uncertain usage reconciliation:** inspect the retained original operation and
  provider/accounting evidence before any future reviewed reset. P9 must define a
  deliberate reconciliation path; automatically clearing Unknown, charging twice or
  starting the same paid operation again is forbidden.
- **Later scope:** full workspace/session navigation P5; draft editing/selection/version
  comparison P6; checked application/history/undo P7; planning-field approval P8;
  integrated recovery P9; microphone/playback P10–11; final acceptance P12. Older
  response contradictions are retained with their generation ledger; draft viewers
  must inspect the original request, not use the newest generation's warnings.

No unresolved brainstorming failure remains in the executed checks. Status is
implemented with the independent gates above; the same-checkout prompt 5 handoff is
copyable above.

### 2026-10-07 — Prompt 1

The starting checkout was clean at `09352ded5c4f115578329323cf48569f141babd6`.
Work stayed in this checkout without reset, clean, worktree transfer or commit.
Repository/ancestor inspection found no applicable AGENTS.md. The common requirement
documents were read. [Baseline and design](brainstorming-baseline-and-design.md)
records the actual chapter/node/section/page relationship, multi-document and
standalone behavior, unsaved source capture, synopsis/notes/canon, checked writing,
identity mapping and recovery paths. In particular, the linked scene-content PUT
flattens a linked section into its first page and blanks later pages; it is excluded
from precise brainstorming insertion/replacement. Provider streaming abstractions
exist but no production caller reaches either UI; no real speech service or deployed
Yjs authority was found. These are source-inspection findings, not live acceptance.

Changed files:

- `WriterApp.Shared/Brainstorming/BrainstormingContracts.cs`, `BrainstormingValidation.cs`
  and `BrainstormingCapabilities.cs`: version-1 identities, targets, context, ordered
  turns, request/event lifecycle, draft ancestry, planning, approval/receipt states,
  bounded strict JSON and capability transport. Manual proposal HTML is inert and
  separately pinned in approval hashes; native canon entity keys are strings.
- `Controllers/BrainstormingCapabilitiesController.cs`,
  `WriterApp.Client/Services/WebBrainstormingCapabilities.cs`,
  `WriterApp.Device.Shared/Services/DeviceBrainstormingCapabilities.cs` and host
  registrations: authenticated additive discovery, actionable old-server fallback,
  account/backend/cancellation leases and unsupported speech states. Session storage
  was initially NotImplemented; prompt 2 now reports it Available. Generation,
  streaming, application and speech remain unavailable.
- `Tests/WriterApp.Tests/BrainstormingContractTests.cs` and
  `BrainstormingCapabilityTests.cs`: 49 + 15 checks, including strict size/schema,
  wrong identities, standalone targets, duplicates/order/ancestry, excluded context,
  unknown/cancelled/incomplete results, exact reviewed content, old servers, actual
  authenticated capability endpoint and both production discovery adapters.
- `docs/brainstorming-baseline-and-design.md` and this status/handoff.

Shared contracts compile for both hosts; final verification is the combined prompt
1/2 run below. No editor schema, generated editor assets, manuscript schema or device
sync protocol changed in prompt 1. The separate realtime plan was not implemented.

### 2026-10-07 — Prompt 2

Implemented production behavior:

- Authenticated owned-session create/read/update/tombstone endpoints, bounded session
  summaries and turn pagination, immutable accepted history/proposal provenance,
  document/project/source authorization, revision CAS and durable exact-operation
  receipts. A snapshot and receipt commit in one EF transaction. Saving a conversation
  never writes manuscript/planning/history, dispatches AI or bills usage.
- Atomic version-1 device cache and durable IndexedDB web cache for ordered unsent
  messages, composer text, enabled sources, targets/context, draft/manual versions,
  planning, requests, application references, explicit mapping and immutable outbox.
  Both hosts register a shared adapter/service boundary for later presentation.
- Explicit inspect-before-retry sync, acknowledgements, local/server ID conversion,
  account/backend guards, mapping rechecks during HTTP, tombstones and deletion
  acknowledgement. New in-flight writing/composer changes survive an old response.
  Unchanged acknowledgements do not post another revision. Reconnect has no automatic
  send/generation/application path.
- Remote conflicts retain both snapshots; local CAS failures retain the losing
  candidate in a separate inspectable archive. Explicit forks get new IDs/mappings
  and preserve the original history. Deletion racing a remote edit keeps its tombstone
  and both histories. Anonymous installation-owned sessions are not adopted on sign-in.
- Offline device review is account-scoped; browser review after reload requires a
  previously authenticated tab's review grant when auth bootstrap is unresolved and
  the browser is offline. That grant cannot authorize network exchange. Logout revokes
  it before IndexedDB cleanup. Invalid/future files and capacity failures retain bytes.
- Conversation CRUD refuses new Committed/Undone application claims. Prompt 7 must
  implement the authoritative mutation receipt bridge; an unrelated AI history receipt
  cannot establish that a brainstorming draft was applied.

Changed files beyond prompt 1:

- `WriterApp.Shared/Brainstorming/BrainstormingPersistence.cs`,
  `BrainstormingIdentityMap.cs`, `BrainstormingSessionService.cs`,
  `BrainstormingHttpRemote.cs`; optional EnabledSources/BranchedFromSessionId on the
  version-1 session and DeletionAcknowledged on its local cache envelope.
- `Data/AI/BrainstormingSessionRecord.cs`, `Data/AppDbContext.cs`,
  `Application/AI/BrainstormingSessions.cs`,
  `Controllers/BrainstormingSessionsController.cs`.
- SQLite `Migrations/20261007060407_BrainstormingSessions.cs` and designer/snapshot;
  SQL Server `Migrations/SqlServerMigrationsDb/20261007060409_BrainstormingSessionsSqlServer.cs`
  and designer/snapshot. Generated using the repository EF contexts. Up creates only
  two empty tables and a scope index; no authored data conversion. Session roots do
  not add manuscript purge foreign-key constraints.
- `WriterApp.Device.Shared/Storage/LocalBrainstormingStore.cs`,
  `Services/DeviceBrainstormingSessions.cs`, `Services/DeviceServiceCollectionExtensions.cs`.
- `WriterApp.Client/Services/WebBrainstormingStore.cs`, `WebBrainstormingSessions.cs`,
  `ClientLogoutService.cs`, `WriterApp.Client/wwwroot/js/brainstorming-store.js`,
  `WriterApp.Client/Program.cs`.
- `Tests/WriterApp.Tests/BrainstormingPersistenceTests.cs`, `BrainstormingSyncTests.cs`,
  `BrainstormingWebAdapterTests.cs`, `Tests/brainstorming-store.mjs`,
  `scripts/verify-brainstorming.ps1`, baseline/design and this status file.

Compatibility and limits are documented in the design. Session storage uses a bounded
snapshot root plus receipt rows instead of the initial normalized-row proposal, so
ordering and ancestry commit atomically. Session v1 is bounded to 2 MB/500 entries;
local v1 cache to 8 MB, 10 remote conflicts, 10 local CAS conflicts and 10,000 receipts.
Listing returns 1–50 summaries; local directory listing supports 1,000 sessions.
No eviction or schema downgrade is implicit. Device data sits under the existing
document root's `brainstorming` directory, isolated from authored document JSON.
Server standalone documents have a backing project GUID even when their local
counterpart has no authored project; those are distinct scope representations.

Final reproducible verification (PowerShell 7.6.5, .NET SDK 10.0.400):

```powershell
./scripts/verify-brainstorming.ps1 -Phase p02 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

The script runs these checks sequentially and retains logs under
`artifacts/brainstorming-p02` (ignored generated evidence; rerunnable from tracked tests):

| Check | Result and scope | Evidence |
|---|---|---|
| .NET focused and existing regressions | 349 passed, 0 failed/skipped: 96 brainstorming (64 P1, 32 P2), 253 existing writing/outline/history/storage/sync checks. One existing xUnit2031 warning in LocalStoryboardTests; no new compiler warnings. | `combined-regressions.log`, `combined-regressions.trx`; exact filter is in the script. |
| Server/web Release build | Passed, 0 warnings/errors. Builds Shared, UI.Shared, Publishing, Client/Blazor output and server. | `server-build.log`; `dotnet build BlazorApp.csproj -c Release -p:BaseOutputPath=artifacts/brainstorming-p02/release-build/ --nologo -v:minimal` |
| Windows Debug build | Passed, 0 warnings/errors; isolated development data/output. Shared/device use portable code; no iOS platform references added. | `desktop-build.log`; exact desktop command in script. |
| Real browser storage | Six groups passed in headless Microsoft Edge 154.0.4258.62: validated synthetic .NET session reload, draft ancestry/manual HTML/composer, dedup, namespace/document isolation, two-tab CAS/retained loser, offline review/logout, malformed/future input preserves bytes. Actual production IndexedDB module. | `browser.log`, `browser/result.json`; `node Tests/brainstorming-store.mjs` with PLAYWRIGHT_MODULE. |
| Production HTTP/adapter boundaries | Actual authenticated controllers through ASP.NET TestServer + SQLite and production BrainstormingHttpRemote; CRUD, turn pagination, revision conflict, exact receipt retry, wrong account/document and authentication. Production device registration/adapter and web adapter guards exercised with explicitly synthetic identity/interop/transport fixtures. | BrainstormingSyncTests, BrainstormingWebAdapterTests and TRX. Interop fixture is not browser UI proof. |
| Database migration | Real SQLite engine migrated from WebScopedPlanningRecovery to latest; existing Unicode rich writing and document sync version preserved. SQL Server idempotent migration script generated and inspected; both models report no pending changes. | BrainstormingPersistenceTests and TRX. No live/deployed SQL Server was changed. |
| Native executable startup | Rebuilt Windows executable launched with hidden window, stayed alive as Prosa and initialized isolated document data. Only agent-launched PID was closed; pre-existing user editors were preserved. Startup only, not brainstorming interaction. | `native-startup.json`; executable below. |
| Whitespace | `git diff --check` passed. | Verification script. |

Rebuilt executable exercised:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p02\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Its development validation data is
`C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p02\native-validation-data`.
TestServer uses synthetic owned standalone/project-backed manuscript data; no provider,
real account, paid request or real manuscript was involved. Rich Unicode source pages
were checked unchanged and AI application history/usage remained empty. Existing
editor assets were unchanged; application/editor interaction belongs to prompts 6/7.

Remaining gates and follow-up:

- Prompt 3 target/context capture, prompt 4 provider/streaming, prompt 5 reachable
  three-panel UI, prompts 6–8 application and prompt 9 integrated recovery remain
  unimplemented. The persistent proposal fixtures do not claim a generated live draft.
- Brainstorming browser/Razor and native UI journeys are not yet reachable. Native
  startup, compiled adapters and browser storage checks do not establish those gates.
- Live SQL Server migration/transaction/CAS/pagination acceptance remains unverified.
  Use a disposable database and these supported migration contexts, apply only the
  additive migration after backup, then exercise authenticated CRUD/retry/conflict/
  revocation/tombstone on synthetic data. Do not run against tenant manuscripts.
- Live provider, microphone, audio playback, packaged/MSIX/distribution and full iOS
  build were not run. iOS is outside acceptance scope; its shared managed dependencies
  compiled through the Windows/tests builds and gained no Windows-only APIs.
- Existing SQLite multi-manuscript migration defect discovered by the richer mapping
  fixture: `20260208163000_AddDocumentProjectRelationship` creates unique partial index
  `IX_Documents_ProjectId_Manuscript` for DocumentKind=0, but
  `20261002061051_MultiDocumentProjects` drops `IX_Documents_ProjectId_DocumentKind`
  instead. The old unique index survives a full migration chain and a second
  manuscript in one project fails with `UNIQUE constraint failed: Documents.ProjectId,
  Documents.DocumentKind`. This predates brainstorming and remains unchanged. Rich
  mapped session acceptance uses one manuscript plus an explicitly selected Notes
  document in the same project, with actual migration triggers and unchanged writing
  clocks. Fix the old-index mismatch in a separate additive migration before claiming
  full migrated SQLite multi-manuscript acceptance; do not rewrite applied migrations
  or discard existing data. The EnsureCreated sync regressions do not prove that gate.
- Explicit anonymous-to-account transfer and interactive conflict resolution/archive
  UI are later reviewed workflows; there is no implicit adoption/merge/pruning now.
  Use listed original scopes for archives, preserve outboxes/receipts, and fork with
  new scoped identities. Keep schema and concrete receipt bridge decisions visible
  when extending these services.

No unresolved brainstorming failure remains in the executed checks; the separate
pre-existing SQLite index defect above remains a migration gate. The files stay uncommitted for
the next prompt. The current handoff above now reflects the subsequent P3 work below.

### 2026-10-07 — Prompt 3

Implemented in the same checkout, preserving uncommitted P1/P2 and unrelated files.
No manuscript, outline, planning, canon, history or usage mutation is performed by
preparation. No new database migration was required: versioned context metadata is
stored in P2's bounded session envelope. Additive optional fields are validated;
older clients that cannot read them must update instead of using unchecked data.

Production changes and contracts:

- `WriterApp.Shared/Brainstorming/BrainstormingStoryContext.cs`: typed owned raw
  snapshots, checked page projections, target queries/resolutions, explicit source
  choices, character policy, bounded supporting-document pages and preparations.
  Depth-first saved node order plus section/page identity resolves chapter number,
  title or ID, section number/title/ID and scene title/ID. Duplicate/missing or
  contradictory references and separate scene-content routes clarify before any
  preparation. Existing insertion pins the first saved page at UTF-16 offset zero;
  new-section preparation pins the chapter parent and existing section/node neighbors.
- `BrainstormingContracts.cs`, `BrainstormingValidation.cs`, `BrainstormingIdentityMap.cs`:
  availability is separate from inclusion; manuscript facts, planning intent,
  extracted canon and supporting data are separate kinds of evidence. Sources retain
  exact excerpts, source/document identities, hashes/revisions, freshness, reasons,
  ranges and version-pinned summary attribution. More than 80 required pages are
  grouped into complete chapter sources with every page/section ID, revision, content
  hash and range retained; mapping and server storage authorize every grouped part.
  No page is truncated to fit the source-count limit. Unknown old page choices after
  grouping require a fresh explicit review.
- Shared context policy prioritizes the preceding chapter and destination; defaults
  to 60,000 UTF-16 characters, accepts 100–90,000, and permits at most 100 manifest
  sources. Missing/over-budget mandatory text blocks generation. Authors may supply
  a reviewed summary pinned to the full source revision/hash or explicitly review and
  omit a required source. No hidden AI summary request is made. Additional unselected
  inventory is disclosed without including its text. The character budget covers
  excerpts, titles and reasons; transport/provenance metadata and provider token
  admission remain separately bounded and must be checked by P4.
- `BrainstormingContextPolicy.GenerationData` emits only included source data and an
  explicit quoted-data instruction. Excluded/stale text cannot enter this payload.
  It refuses unresolved mandatory omissions, absent policy and the explicit newer
  local/pending server-generation limitation. It is a payload builder, not server
  authorization. P4 must recheck authoritative sources, identity, ownership and quotas.
- The deterministic policy surfaces conflicting explicit `[fact: key=value]`
  assertions and warns when authored prose coexists with planning/canon/supporting
  intent. This does not claim semantic detection of every natural-language conflict;
  P4's bounded conversational action must assess those sources and report material
  contradictions before drafting. Quoted instructions remain inert story data.
- `Application/AI/BrainstormingStorySources.cs` and
  `Controllers/BrainstormingStorySourcesController.cs`: authenticated read-only
  source snapshot and bounded owned document-title listing, active document/project
  and deleted-user checks, current `WebAiSourceService` checks before/after capture,
  strictly document-owned nodes/sections/pages/notes/synopsis. Canon text is not read
  through a new entitlement bypass; selected canon uses existing checked bible APIs.
  Legacy project nodes with null DocumentId are not guessed into a manuscript.
- `WriterApp.Client/Services/WebBrainstormingContext.cs`: existing `WebCheckedAi`
  leases, authorized selected supporting documents, bounded strict reads, actual
  editor projection and checked active-page HTML overlay. Pending writing is shown
  as pending and cannot be silently replaced by the saved cloud text. Identity,
  supporting sources, source fingerprint and pending HTML are rechecked after capture.
- `WriterApp.Device.Shared/Services/DeviceBrainstormingContext.cs`: real repository
  and `LocalEditorSession.Snapshot()` in local identities, offline preparation,
  explicitly selected local support and current-account cached canon. Stale,
  interrupted, differently mapped or missing canon is excluded/disclosed. Newer local
  writing has an explicit authorization limitation; no network fallback uploads or
  substitutes older cloud writing. Whole raw capture is bounded to 4 million characters,
  1,000 pages/references and ten selected supporting documents; web transport is 8 MB.
- `WriterApp.UI.Shared/BrainstormingContextReview.razor` and stylesheet: shared
  reachable review in both actual manuscript editor routes. Chapter/current-section
  and existing/new-first-section choices, titled author-selected support, context
  budget, safe quoted excerpts, freshness/availability, summary/omission review,
  provenance and refresh. This is P3's source review, not the P5 three-panel workspace.
  Per-source summaries are authored/reviewed here; later generated summaries must use
  the same source-version attribution. Canon snapshots are explicitly enabled as a
  selected kind; future workspace refinement may select individual opaque entity keys.
- `DocumentEditor.Brainstorming.cs` and `DocumentWorkspace.Brainstorming.cs`: real
  production handlers flush/capture and recheck the active editor, invoke the adapters
  and persist exact context/targets through `RememberPreparationAsync`. Device capture
  flushes again after projection to detect intervening DOM edits. Old contexts,
  discussion, composer, versions and approvals remain intact. Source changes mark
  old draft previews obsolete; no preview is automatically rebased or applied.
- Session adapters expose review identity notifications; web `AuthStateService`
  now notifies on reset or account change. Shared review clears active private context
  and supporting choices without erasing original caches, issuing paid requests or
  changing writing. Host DI registers both context adapters. Capability reporting adds
  optional ContextPreparation; generation/application/speech remain unavailable.
- `WriterApp.Client/src/tiptap-editor.ts` exposes the existing checked
  `qualityPlainText` mapper as `tiptapEditor.brainstormingPlainText`. `npm run build`
  regenerated the actual web bundle; the device bundle already contained this mapper
  and remained byte-identical. No rich-text vocabulary or application path changed.

Reproducible final verification (PowerShell 7.6.5, .NET SDK 10.0.400):

```powershell
./scripts/verify-brainstorming.ps1 -Phase p03 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence is under ignored `artifacts/brainstorming-p03`; tests and scripts are
repository-local and rerunnable. The script builds sequentially with isolated output
and desktop data, and stops only its own launched PID.

| Check | Result/scope | Evidence |
|---|---|---|
| Focused P3 boundaries | 36 checks passed: saved multi-page chapter and reorder, existing/new-first target, ambiguity/scene route, Unicode, standalone, exact payload/exclusions, stale canon, oversized context/summary/hash, 121-page full chapter parts and mapping, bad schema/null/hash/projection/scope, refresh and obsolete previews. Real SQLite, authenticated TestServer, both reachable production editor preparation handlers and shared Razor refresh/identity handler exercised with labeled synthetic transport/interop/editor fixtures. | `BrainstormingContextTests.cs`, `BrainstormingContextAdapterTests.cs`, TRX. |
| Combined regressions | 412 passed, 0 failed/skipped: 132 brainstorming and 280 existing writing/outline/storage/sync/history/logout/canon/auth/content checks. Existing LocalStoryboardTests xUnit2031 warning only. | `combined-regressions.log`, `combined-regressions.trx`; exact filter in script. |
| Server/web Release | Passed, 0 warnings/errors, including Shared/UI/Client/server. | `server-build.log`; BaseOutputPath `artifacts/brainstorming-p03/release-build/`. |
| Windows Debug | Passed, 0 warnings/errors. | `desktop-build.log`; BaseOutputPath `artifacts/brainstorming-p03/native-build/`, isolated ProsaValidationDataDirectory below. |
| Browser storage | Six existing real IndexedDB module groups passed on validated synthetic .NET session data. | `browser.log`, `browser/result.json`. |
| Actual editor bundles/browser render | Headless Edge 154.0.4258.62: 109 existing editor checks plus nine new groups passed. Web/device rich Unicode, lists/table reading order, empty source, inert instructions, read-only HTML projection, actual pending device DOM snapshot, unsupported rich nodes/attributes refused, safe encoded Razor/excluded notes, 390px layout and keyboard details. | `context-browser.log`, `browser/context-result.json`, `context-review-wide.png`, `context-review-narrow.png`. |
| Native rebuilt executable | Isolated Windows executable stayed alive as Prosa and initialized its isolated document directory; only the launched PID was closed. Startup acceptance only. | `native-startup.json`; rebuilt path below. |
| Migration/model and whitespace | Existing real SQLite migration and both provider model/script checks passed in combined suite; no new P3 migration. `git diff --check` passed. | Combined TRX and script. |

Rebuilt executable checked:
`C:\Users\Johan\source\repos\WriterApp\WriterApp.Desktop\artifacts\brainstorming-p03\native-build\Debug\net10.0-windows10.0.19041.0\win-x64\WriterApp.Desktop.exe`.
Isolated development data:
`C:\Users\Johan\source\repos\WriterApp\artifacts\brainstorming-p03\native-validation-data`.

Acceptance boundaries and continuation (at P3 completion; P4 evidence above supersedes
the generation/streaming boundary):

- Handler/interop fixtures and static rendered Razor in Edge establish their stated
  scopes, not authenticated browser navigation or interactive native acceptance.
  For actual host UAT, use disposable synthetic manuscripts in the rebuilt native
  output and local authenticated web editor. Open Brainstorm story context, select
  Chapter 2 and existing/new-first-section, inspect all Chapter 1 pages, enable notes
  and explicitly selected support, edit during capture, then refresh. Confirm writing
  remains and context/targets persist. A live account/native UI interaction was not
  automated here; do not call startup proof a full native context journey.
- P4 provider/token admission, stream lifecycle and semantic contradiction review,
  P5 full workspace/session reopening, P6–8 generated drafts and approved application,
  and P9 end-to-end recovery remain pending. No provider call, microphone/audio,
  deployment, packaged/MSIX or distribution acceptance was performed. Full iOS build
  remains outside scope; shared dependencies compiled without new Windows-only APIs.
- Existing live SQL Server and SQLite multi-manuscript migration-index gates recorded
  under P2 remain unchanged. P3's migrated source-route fixture uses a synthetic
  standalone manuscript; it does not establish multi-manuscript migration acceptance.
- All changes remain uncommitted in this same checkout. P3 handed off P4, now completed
  above. Current handoff points to P5; retain the explicit pending-writing limitation
  and newly checked synchronized server-context review requirement.
