# Brainstorming prompt 9 recovery and text acceptance

Date: 2026-10-07. Checkout: `C:\Users\Johan\source\repos\WriterApp`.
Original uncommitted prompts 1–8 and unrelated AI/editor work retained.

## Latest recheck after prompts 10–12

Rechecked P9 on 2026-10-07 without removing any later implementation or unrelated
work. Original evidence below remains historical. Latest output is separately stored
in `artifacts/brainstorming-p09-recheck`; the current status/handoff names this run.

An additional access-loss defect is fixed: shared conversation source/context reads,
send, inspection and stop now revoke active review on HTTP 401/403 or explicit lost
source/session access. The server uses the existing `brainstorming.access_revoked`
code, distinguishing access loss from an unavailable backend. Cached writing and
private recovery evidence remain; no provider/application retry occurs. The transport
is shared by web and device. New regression cases exercise five mounted workspace
actions, HTTP boundaries and actual server routes with wrong-account and revoked
document ownership. Eleven focused checks failed before the fix; all 24 initial
focused checks and ten actual access-route checks pass afterward. No schema, editor
bundle, provider contract or sync format changes.

Live SQL Express also exposed a session-save transaction outside EF's retry strategy.
`BrainstormingSessions.WriteAsync` now retries the entire transaction from fresh
tracked state and checks durable operation receipts before writing. The explicit
local-only console probe creates a new GUID-named database, applies the actual SQL
migration chain, verifies revision/recovery races and removes only its own database.
It runs independently of ordinary unit tests:

```powershell
dotnet run --project Tests/Brainstorming.SqlServerAcceptance/Brainstorming.SqlServerAcceptance.csproj -p:BaseOutputPath=artifacts/brainstorming-p09-recheck/sql-build/ -- artifacts/brainstorming-p09-recheck/sqlserver-acceptance.json
```

Five groups pass on SQL Express 16.0.1000.6: 28 actual migrations through P10,
restart/duplicate session receipts, competing CAS writes with one retained conflict,
concurrent/restarted recovery copy deduplication, wrong-account refusal, unchanged
rich manuscript/history and live unique candidate indexes. Completion:
2026-10-07T19:18:27.1777299Z. The disposable database was removed, and a separate SQL
catalog query confirms zero remaining probe databases. See
`sqlserver-acceptance.json/log`. This closes the local SQL migration/session/recovery
portion; real SQL Keep/planning mutation and physical authenticated device/web
acceptance still need the gate reproduction below.

```powershell
./scripts/verify-brainstorming.ps1 -Phase p09 -EvidenceSuffix recheck -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
$env:WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB = "$PWD/artifacts/brainstorming-p09-recheck/text-fresh.db"
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-build -p:BaseOutputPath=artifacts/brainstorming-p09-recheck/build/ --filter FullyQualifiedName~BrainstormingAcceptanceTests
Remove-Item Env:WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB
./scripts/start-brainstorming-acceptance.ps1 -SeedDatabase artifacts/brainstorming-p09-recheck/text-fresh.db -EvidenceDirectory artifacts/brainstorming-p09-recheck
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
$env:BRAINSTORMING_WEB_SEED = 'artifacts/brainstorming-p09-recheck/text-fresh.json'
$env:BRAINSTORMING_EVIDENCE = 'artifacts/brainstorming-p09-recheck/live-web'
$env:BRAINSTORMING_WEB_FRAMEWORK = 'WriterApp.Client/artifacts/brainstorming-p09-recheck/release-build/Release/net10.0/wwwroot/_framework'
$env:BRAINSTORMING_LIVE_AI = '0'
node Tests/brainstorming-live-web.mjs
```

Always export a fresh database; the exporter refuses replacement. Use the recorded
PID and start time to stop only the helper-owned host. This run used
`text-acceptance-final.db/json`, actual LocalDev `dev-oid` and synthetic entitlement.
Edge 154.0.4258.62 completed session `93dec3ed-86c7-4bfc-83b9-0245513a2da7` at
2026-10-07T19:23:10.413Z. Two chapters/four original rich pages, synopsis and
character/place notes, conflicting optional source, and standalone manuscript were
exercised. The restored proposal is synthetic; HTTP routes/provider responses are
not intercepted. Actual manual checkpoint, ordered Keep/Undo/Redo and Notes
save/Undo persist real server receipts and ordinary AI history. Offline composer,
full reload and standalone isolation pass. All original page HTML stays exact.
Three served WASM hashes match this build. At 390px, the conversation is 366px wide
with no horizontal overflow, page errors or failed styles; the narrow capture was
visually inspected. See `live-web/probe.json` and captures for all twelve checks.

| Area | Latest result and remaining gate |
|---|---|
| Implementation | P9 recovery/sync v5 intact; additional access-revocation and SQL session retry fixes complete. P10–12 remain intact. No new migration. |
| Automated | 821 tests, zero failed/skipped; eight real module/render suites in Edge. Both production builds have zero warnings/errors. Existing test-only BL0006/xUnit2031 warnings remain. |
| Browser interaction | Running rebuilt LocalDev server/WASM journey passes within synthetic restored-proposal scope. Real account authentication remains open. |
| Native | Rebuilt Windows startup passes with isolated data; PID 35588 at 19:20:55.2991595Z, only owned PID closed. Interactive Windows remains open; Computer Use kernel initialization previously failed twice with sandbox ACL errors. |
| Live AI | No provider request during recheck. Earlier automatic approval review rejected the external fictional fixture transmission without explicit payload/destination approval; approval remains pending. Synthetic providers establish recovery/metering checks only. |
| Database/sync | Fully migrated SQLite, local files, authenticated TestServer mapping/conflict/recovery and live local SQL Express migration/session CAS/recovery/index checks pass. Physical authorized device/web exchange and real SQL Keep/planning mutation acceptance remain open. |
| Audio/package | Outside this text recheck. P10–12 implementation and their human microphone/playback gates remain; installed package is optional and unrun. |

Exact server/native paths, hashes, PIDs and timestamps are in the latest status entry,
`web-host-config.json` and `native-startup.json`. At HEAD
`09352ded5c4f115578329323cf48569f141babd6` plus uncommitted P1–12/recheck work,
the retained 500 × 1,000-character session measures 811,910 bytes and ten validation
iterations 256.2663 ms; the 501st turn refuses without changing the original. This
is contract timing, not interactive latency. Required gates below and the current
P12 acceptance packet remain applicable. **The feature is not release-ready.**

## Product profile and evidence scope

Prosa serves an author using a standalone manuscript or a structured book on web
and Windows. Discussion, proposed prose and approved writing/planning are separate.
Restart, offline review and explicitly synchronized copies must retain the author's
work and explain uncertain operations. Correctness, ownership, rich-content
preservation, keyboard access and recovery take priority in these flows.

The web routes are `/documents/{documentId}/sections/{sectionId}` and
`/projects/{projectId}/scenes/{sceneNodeId}`; the device route is
`/documents/{documentId}`. C# tests mount production components and invoke actual
editor callbacks/adapters through authenticated TestServer routes, migrated SQLite
and isolated device files. Authentication, provider responses and native interop in
these tests are synthetic. Edge uses actual shipped editor/storage modules and
production Razor render fixtures. This is distinct from signed-in interactive UI,
live-provider quality, native interaction, live SQL Server and installed packaging.

## Flow packets and scenario coverage

| Flow | Action and data | Required outcome | Evidence |
|---|---|---|---|
| P9-01 Chapter 2 text journey (B01–07) | Structured Chapter 1 with preceding writing, Chapter 2 with existing rich pages; explore, save/reopen a manual candidate, revise, review Keep before the existing section, then Undo. | Discussion leaves writing/candidates unchanged; revision uses exact saved manual content and Chapter 1; one filled section is inserted at the reviewed position, other pages remain exact; Undo restores safely. | New Web/Device `ChapterTwoConversationManualRevisionKeepAndUndoThroughProductionRoutes`; production shared Send/Review/Confirm/Undo and actual host callbacks over authenticated HTTP; prior P6/P7 version, replacement, duplicate and divergent undo checks; both shipped editor factories in Edge. Three expected provider calls/charges only. |
| P9-02 Checked sources and planning (B08–12) | Explicit supporting documents and exclusions, ambiguous/stale targets, reviewed field subsets and unlinked scene ideas; standalone synopsis/character notes authored locally. | Authorized selected sources only; old target/manifests retained; unrelated planning/formatting preserved; scene ideas create no prose; saved standalone notes transfer and appear in included current server context. | P3/P8 context, actual host planning and project-sync regressions; new v5 real sync service upload/download with two local identity maps, stale/foreign refusal and checked server context assertion. |
| P9-03 Restart and offline outcomes (B13–14, B17) | Retain composer, accepted/unknown generation, draft buffer, pending exact save, approved/Unknown Keep/planning journal; reload/reopen or lose a response. | Inspect known operation instead of replaying generation/application. No false success, duplicate insertion/turn/billing or borrowed previous generation result. Offline review preserves current local manuscript. | Existing generation/Keep/planning restart, failure and dedup tests; new missing-operation inspection and server-restart device recovery tests; actual IndexedDB reload/offline/foreign partition suites. |
| P9-04 Explicit owned sync and conflicts (B15–16) | Discover same owned session on mapped web/device copies, explicitly synchronize history, concurrently edit conversation/manual candidate, fork either retained discussion, then revoke/sign out. | Original identities and local pending journals remain; both branches retained; fork is discussion rather than operation replay; device reports cannot manufacture server commits or authorize a second Keep; active private UI clears, local writing remains. | New production adapter discovery/repeated exchange, manual-buffer conflict/local-and-server fork, transferred application duplicate guard and revoked mounted workspace tests; prior mapping/CAS/offline/conflict tests; actual two-tab IndexedDB race. |
| P9-05 Bounded history and narrow keyboard review (B23) | Validate/persist 500 retained turns, attempt 501; reopen recovery copies and exact original fields; focus history controls at 1400px and 390px. | Oversize refusal retains original; long histories have bounded pages; private origin/outcome remains visible; no horizontal clipping. | Measured C# limit test; three new actual Edge recovery render/storage groups; prior workspace resize/focus and context/contract budget checks. Wide/narrow recovery screenshots visually inspected. |

The combined verification passed **716 tests, zero failures/skips**, followed by a
passing focused standalone context check after adding its final included-source
assertion. Both Release server/web and Debug Windows builds have zero warnings/errors.
Test-only BL0006 renderer and pre-existing xUnit2031 warnings remain. All eight
browser suites passed in Edge **154.0.4258.62**; no page errors in the new recovery
suite. The Keep suite retains its 24 groups and 109 common editor regressions;
planning retains six groups; recovery adds three groups.

## Issue ledger

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| P9-I01 | P1 | Standalone authored synopsis/notes had no owned sync field; older cloud context could omit the local values. | Fixed with additive optional typed synopsis and sync v5, ordinary version conflict checks, preserved extension data and exact current-context comparison. P8-G03 standalone transfer dependency resolved. |
| P9-I02 | P1 | A pull could replace the candidate beneath an uncheckpointed manual edit. | Fixed: retain both branches; fork retains local manual buffer, original journals and pending operations. Server branch can also be deliberately forked through checked mappings. |
| P9-I03 | P1 | Session discovery did not expose recovery history on another authorized mapped copy; copying client receipts into applications would misstate authority. | Fixed: explicit paged discovery and history exchange; server ledger separate from immutable device-reported copies in original namespaces. No client-provided committed ledger/application result. |
| P9-I04 | P1 | Missing generation inspection could reuse the prior completed generation's success. | Fixed: failed/not-dispatched result for the inspected send, retained composer, no automatic provider call. |
| P9-I05 | P1 | Server revocation could leave private active review visible. | Fixed: lease revocation epoch clears active workspace/review and rejects late adoption; original cache and manuscript remain. Sign-out/account/backend guards retained. |
| P9-I06 | P1 | Imported application history could offer a fresh Keep for the same candidate. | Fixed: show history/origin and block new review/confirmation until the original operation is inspected; original local journals retain normal inspection/undo. Server and device-report regressions prove no dispatch. |
| P9-I07 | P1 | Conversation stop/authentication loss, forbidden responses and lost source access could leave active private review visible. | Fixed during recheck: shared transport revokes on 401/403/explicit access loss across all conversation actions; actual server response codes and mounted workspace tests preserve cache/writing without replay. |
| P9-I08 | P1 | SQL retry configuration refused the session-save user transaction. | Fixed during recheck: execute whole save under EF retry strategy, reload tracking and inspect exact operation receipt. Live SQL migration/concurrent CAS/recovery/index probe passes with disposable database removed. |
| P9-G01 | P2 | Signed-in browser and interactive native journey not exercised. | Open acceptance gate with reproduction below. |
| P9-G02 | P2 | Live SQL Server, configured real provider quality/usage and installed package evidence absent. | Open separate acceptance gates. |
| P9-D01 | P2 | Unrelated targeted-quality `valid`/`429-between` diagnostic and P14 list-ending initialization/normalization remain. | Preserved; see P7 UAT and editor-content compatibility. No claim that Brainstorm tests resolve these separate workflows. |

## Recovery authority and compatibility

Version-1 caches gain optional `Recovery`. Explicit synchronization stores bounded
immutable copies of private Keep/planning journals and authored draft buffers in a
nullable `BrainstormingSessions.RecoveryJson` column. SQLite migration
`20261007154702_BrainstormingRecoveryHistory` and SQL Server migration
`20261007154719_BrainstormingRecoveryHistorySqlServer` add only this column;
matching model snapshots and rebuilt hosts are required. Real migrated SQLite and
server restart are tested; SQL Server model/script checks are not live execution.

`POST api/brainstorming/sessions/{sessionId}/recovery?documentId={documentId}`
checks owned session/backend and uses the existing serializable execution strategy
and document sync clock. Authoritative history is read from actual Keep/planning
ledgers. Client copies retain original mapping, receipt hash and origin and never
enter those ledgers or session applications. Transfers are read-only recovery
evidence; original-device operation inspection/undo remains scoped to its original
account/device. Another copy can inspect/copy exact original content without
replaying an approval. Active composer is not uploaded through recovery history.
History exchange rechecks identity/mapping and preserves newer local cache changes.

`api/sync/v5/documents` carries optional top-level `Synopsis` for genuinely
standalone documents; projects continue using their existing project synopsis.
Null optional fields retain older wire JSON. Matching device/server code is required;
older routes cannot omit an existing standalone synopsis during upload. Missing v5
returns an actionable upgrade refusal with queued/local work retained. There is no
automatic downgrade that drops notes. Ordinary document version/clock/transaction
and dedup rules apply; conflicting writes retain their existing sync recovery path.
Downloads preserve private local operation markers and unrelated extension data.
Context preparation still requires current saved/synchronized writing and exact
authored synopsis equality, so an old cloud value cannot become current AI context.
No editor schema or shipped editor asset changed in P9.

## Measured and enforced limits

`recovery-limits.json` records **500 turns × 1,000 characters**, a serialized
**811,919-byte** session, and **388.2562 ms** for ten deserialize/validate iterations
on this run. This is an isolated contract measurement, not UI/provider latency.
The 501st turn is rejected and the original 500-turn file remains intact.

| Boundary | Implemented limit and behavior |
|---|---|
| Session | 500 messages, 2,000,000 serialized bytes; conversation UI shows older messages in groups of 40. |
| User instruction / draft | 8,000 instruction characters; 100,000 draft text/HTML characters. |
| Context | At most 100 sources and 100,000 contract characters. Preparation defaults to 60,000 and permits budgets 100–90,000; required context over budget prevents readiness, with reviewed summary/omission paths. Supporting document selection is explicit, at most ten. |
| Dialogue for provider | Bounded 24,000 characters; retained history is separate from the sent dialogue window. |
| Recovery | 6,000,000-byte bundle, 100 immutable copies, 500 server Keep and 500 server planning operations; cache bound 8,000,000 bytes. Exceeding a bound refuses transfer and retains originals. Display expands ten entries at a time. |

## Gate reproductions

For P9-G01 use isolated two-chapter/synopsis/character-note data in a matching
authenticated Development host and the exact rebuilt Windows executable/data path
recorded in the status. Open the real routes, Brainstorm, prepare Chapter 2 context
with Chapter 1 and one explicitly selected supporting document. Explore, draft,
edit/checkpoint, reopen, revise, review exact Keep, inspect and Undo. Save each
planning destination. Reload during streaming and after a lost application response;
inspection must not increment provider usage or repeat writing. Repeat offline review.

Map the manuscript on a second authorized copy, synchronize ordinary document data,
then explicitly discover/synchronize conversations. Concurrently edit a candidate
on each copy and confirm both branches/original journals survive; deliberately fork
each discussion without replaying operations. Save standalone Logline/character Notes,
sync v5 and prepare fresh included server context; verify exact approved text. Attempt
an old-version upload, revoke access/sign out during delayed capture, and ensure private
active UI clears while local manuscript remains. Test Tab/Shift+Tab and 390px layout.
Preserve user-owned editor processes/data. Native startup establishes startup only.

For P9-G02 apply the applicable migration chain, including RecoveryJson, to an
authorized isolated SQL Server database. Repeat concurrent duplicate history exchange,
session CAS conflicts, actual Keep/planning approval, transaction rollback and lost
response inspection. Separately use a configured real provider for exploration and
manual-draft follow-up, record usage/quality and ensure replay inspection adds no
charge. Run these flows in an installed package separately. Real microphone and
audio remain prompts 10–11; no speech acceptance is claimed.

The original next implementation was prompt 10. Prompts 10–12 now exist and remain
intact; continue remaining text/native/live acceptance using the current copyable
handoff in implementation status, preserving their composer/session/approval contracts.
