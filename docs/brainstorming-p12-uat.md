# Brainstorming prompt 12 integrated acceptance

Date: 2026-10-07. Implementation and available verification completed in the same
uncommitted checkout. Required hands-on native, live AI, microphone/playback and
SQL Server acceptance remain open. **The feature is not release-ready.**

## Product profile and reproducible data

Prosa is a manuscript-aware web/WASM and Windows desktop AI workflow. Role: Author.
The running web fixture uses the existing Development `LocalDev` identity `dev-oid`
and a disposable Professional entitlement; it does not establish External ID login
or a real subscription. The server uses a fully migrated disposable SQLite database,
ordinary controllers, session mapping, checked editor mappings and durable history.
No HTTP route or provider response is intercepted in this running application test.
Restored prose and its discussion are explicitly synthetic acceptance data.

`BrainstormingAcceptanceTests` creates Chapter 1 departure and Chapter 2 arrival,
two pages per section, linked scene/chapter nodes, Unicode and strong/emphasis HTML,
a synopsis, authored character/place notes, and a conflicting optional page note.
It also creates a separate standalone manuscript. Export refuses existing database
files, adapts only the copied fixture to LocalDev, and exports stable IDs and every
original page. A server session contains a synthetic authored proposal for discovery,
manual edit and actual Keep/planning acceptance without external generation.
Project metadata is initialized before source capture. Canon is declared not loaded;
authored synopsis notes are not passed off as extracted character/place canon.

Reproduce using a **fresh** artifact database name, from the repository root:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p12 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
$env:WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB = "$PWD/artifacts/brainstorming-p12/acceptance-fresh.db"
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter FullyQualifiedName~BrainstormingAcceptanceTests
Remove-Item Env:WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB
./scripts/start-brainstorming-acceptance.ps1 -SeedDatabase artifacts/brainstorming-p12/acceptance-fresh.db
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
$env:BRAINSTORMING_WEB_SEED = 'artifacts/brainstorming-p12/acceptance-fresh.json'
$env:BRAINSTORMING_LIVE_AI = '0'
node Tests/brainstorming-live-web.mjs
```

The launch helper checks the loopback port and requires an artifact database. Child
configuration is restored after launch; Stripe and Mailgun are disabled, speech is
disabled, provider fallback is disabled. The browser runner refuses non-loopback
hosts and verifies the original pages. The output records server PID/start time,
binary SHA256, browser version, selected model, UI captures, actual cache/journals,
and three served WASM asset hashes compared with this build's files. Credentials
are never written. Close only the process launched by this helper after confirming
its recorded PID/start time; preserve any existing user applications.

## Flow packets

| Flow | Steps and actual handler | Expected and observed scope | Evidence |
|---|---|---|---|
| F01 Context and destination | Open actual document route → Brainstorm → new conversation → new first section, Chapter 2 → prepare. `ToggleBrainstorm`, `PrepareBrainstormingContext`, context adapter/source controller/policy. | Multi-page manuscript route remains reachable; preceding chapter and synopsis included, optional conflicting note omitted and disclosed; original HTML untouched. No paid generation. | `live-web/context-wide.png`, `probe.json`; context/regression suites. |
| F02 Versions and Keep | Discover server fixture → reopen → edit actual proposal rich editor → checkpoint → inspect → exact Keep review → approve → Undo/Redo. Shared draft handlers, `PrepareBrainstormKeep`, Keep controller/service, editor mapping, ordinary history. | Authored checkpoint survives; exact reviewed HTML gets one new section before existing Chapter 2; original pages retained; actual receipt and history recorded. Host remount retains the owned conversation and requires fresh context for another send. | `probe.json` versions/receipts/cache, `kept-wide.png`; Keep/recovery host and editor suites. |
| F03 Planning | Type one story decision → choose Notes → review selected fields → approve → inspect/Undo. Shared planning panel, host planning capture/apply, planning controller/transaction. | Only approved Notes field changes with turn/context/version/source provenance; Undo uses actual durable operation. Other planning kinds and partial fields are covered by the existing controller/host/browser fixtures. | `probe.json`, planning/regression suites. |
| F04 Offline, reload and standalone | Loaded web goes offline → unsent composer text → reconnect without sending → full page reload/reopen → open standalone editor/context. | Cached proposal/receipt/composer retained without automatic replay. Standalone session remains document-owned. Device unmapped/mapped/conflict cases use real local files and authenticated TestServer fixtures, not physical native interaction. | `probe.json`, `standalone.png`; recovery/sync/standalone regressions. |
| F05 Layout and keyboard | 1600×1000 → 390×844 → Conversation tab → composer focus → Tab; inspect actual layout and CSS responses. | One visible conversation pane, usable width, hidden alternate panes, no document horizontal overflow, status/log semantics and keyboard focus. Physical screen-reader announcements still require manual acceptance. | `conversation-narrow.png`, `probe.json` layout/asset hashes; workspace browser suite. |
| F06 Speech and exceptional states | Speech lifecycle, recognition quality, voice command review, cancellation, auth/quota/provider failure suites; rebuilt Windows startup. | Automated synthetic transports/callbacks pass. No user microphone, audible English/Swedish output, physical echo or native interaction is claimed. No live provider request has been dispatched for P12. | Dictation/voice logs, speech/voice/conversation regressions, `native-startup.json`; gates below. |

## Defects fixed and rechecked

| ID | Severity | Defect and fix | Regression / outcome |
|---|---|---|---|
| P12-01 | P1 | Linked section route redirected to scene-card content, preventing manuscript context/Keep. Multi-page routes now stay in manuscript editing; Brainstorm from a scene has an explicit checked manuscript route, preserving it across section selection. | Running web context and exact Keep; prior scene-route rejection remains intact. |
| P12-02 | P0 | Combined scene save could put all content in page 1 and empty later pages. Server refuses content replacement for multi-page linked sections; language-only save does not rewrite pages. | Two SQLite/controller checks preserve every page and refuse search rewriting; actual multi-page web journey retains original rich pages. Multi-page scene editing must use manuscript pages. |
| P12-03 | P1 | Unedited notes flush created empty notes and invalidated a proposal's source during Keep. Only dirty/queued notes now flush. | No-network/no-renderer no-op test and actual Keep/source recheck. |
| P12-04 | P1 | Application reload remounted the workspace and closed its conversation. Both hosts supply an owned resume scope; shared UI verifies current partition before reading the cache, without reusing context authority. | Same-account remount and different account/backend rejection tests; actual Keep/Undo/Redo and planning reloads. |
| P12-05 | P2 | Release host under Development selected Debug CSS fallback. Fallback follows the binary's build configuration. | Actual stylesheet responses, served WASM hashes and responsive panel inspection. |
| P12-06 | P2 | App navigation consumed nearly half a narrow Brainstorm viewport. Shared shell uses a horizontal navigation row for narrow Brainstorm only. | Actual 390px panel width/overflow checks and screenshot; other workspace shell rules preserved. |
| P12-07 | P1 | Inspection and Keep could race their IndexedDB cache exchanges. Inspection now locks related actions and honestly announces inspection until its exchange completes. | Delayed exchange test rejects duplicate inspection/send, preserves composer and releases busy state; running inspect→Keep journey. |

## B01–B23 acceptance matrix

All rows have implemented contracts. A fixture or module pass establishes only its
stated scope; every row still needs the applicable hands-on native gate.

| ID | Automated evidence | Running web interaction | Remaining acceptance |
|---|---|---|---|
| B01 | Context, conversation, recovery host tests | Actual Chapter 2 context and restored proposal | Real provider drafting; native UI |
| B02 | Explore intent and no-mutation tests | External generation not run | Real provider exploration in both surfaces |
| B03 | Parent/version/provider-payload tests | Synthetic saved version used | Real provider conversational revision |
| B04 | Draft/editor/recovery tests | Rich manual edit, checkpoint, inspection/reload | Live revision from manual parent; native |
| B05 | Atomic order, deduplication, history tests | Exact manual Keep and original-page preservation | Native interaction |
| B06 | Rich range, unsupported-node and replacement tests; real editor module checks | Full route replacement not exercised | Interactive replacement in both hosts |
| B07 | Divergence/recovery-copy tests | Actual Keep Undo/Redo | Later-edit refusal in native/live UI |
| B08 | All four planning kinds, partial fields, stale/dedup/Undo tests | Actual Notes review/save/Undo | Interactive scene/synopsis/character destinations and native |
| B09 | Stale/deleted/order/selection tests | Captured destination displayed | Native/live navigation and stale-target refusal |
| B10 | Ambiguous target/intent tests | No ambiguous provider turn run | Text and spoken clarification with live provider |
| B11 | Exclusion, explicit conflict, summary/budget tests | Included chapter/synopsis and visible omitted note | Live inclusion/conflict explanation; native |
| B12 | Pending/local-ahead and authorization adapter tests | Separate actual server fixture | Native local-unmapped and mapped divergence |
| B13 | Pending generation, journal, receipt and restart tests | Server discovery, manual inspection and full reload | Interactive send/stream/crash boundaries; native |
| B14 | Offline files/IndexedDB and recovery tests | Loaded offline composer/review, no reconnect replay | Native offline restart/reconnect |
| B15 | Real SQLite/TestServer and file sync/conflict tests | Real server session discovery/receipts | Two actual authorized devices and live SQL sync |
| B16 | Foreign owner/account/backend/revocation/late-event tests | LocalDev identity only | Real External ID sign-in/out/revocation and native |
| B17 | Metering/quota, malformed stream, cancellation, timeout tests | Provider requests disabled | Live provider failure/cancellation/native handling |
| B18 | Capture/PCM/transport/quality lifecycle fixtures | Synthetic module checks only | Human English/Swedish microphone and transcription |
| B19 | Same-session voice/echo-floor/interruption fixtures | Synthetic output events only | Human two-way speech, physical echo and interruption |
| B20 | Voice/text session and proposal tests | Synthetic voice switching only | Human voice→text→revision |
| B21 | Clear/uncertain/negated/mixed/stale-review approval tests | Synthetic finalized transcripts only | Real noisy/partial/negated spoken Keep and planning |
| B22 | Version-bound output/edit/disconnect/disposal tests | Synthetic playback events only | Audible output and physical resource disposal |
| B23 | 500-turn bound, 40-message paging, large chapter and cancellation tests; editor/resize/focus modules | Actual narrow pane, keyboard focus and semantic status/log | Native keyboard/narrow; screen-reader and long live sessions |

## Evidence boundaries and remaining gates

| Area | Result |
|---|---|
| Implementation | P1–12 implementation exists; verified P12 fixes above. No new P12 migration or sync format. |
| Automated | Final regression count/logs in status; original writing, rich content, planning, save/sync/history, auth and usage filters remain included. SQLite migrations and both provider model/script contracts pass. |
| Browser | Ten production module/render suites plus the actual running LocalDev server/WASM editor journey. Restored proposal is synthetic; no provider route interception. |
| Native | Rebuilt Debug Windows binary starts with isolated data as Prosa. Interactive automation could not initialize: Windows Computer Use node kernel failed twice with `apply deny-read ACLs`; no alternative UI automation bypass was used. |
| Live AI | Open approval gate. Automatic approval review rejected sending even the disposable fixture to OpenAI without explicit payload/destination approval. The bounded three-request approval question remains pending. Credentials alone are not authorization or evidence. |
| Live microphone/playback | Open human acceptance gate. No human EN/SV capture/listening participation established. Speech remains disabled in this fixture host. Synthetic audio is not microphone acceptance. |
| Database/sync | Real migrated disposable SQLite plus real local files/TestServer mapped/unmapped/conflict regressions pass. Live SQL Server, migration-index and actual device/web exchange remain open. |
| Packaged | Optional installed/MSIX acceptance not run. Development executable startup is not package acceptance. |

To finish live AI after explicit approval, use another fresh exported fixture and
the same launch helper. Set `BRAINSTORMING_LIVE_AI=1` only for that authorized run.
The runner sends exactly three fictional text prompts: opening exploration, a short
Chapter 2 draft, and revision of the manually edited candidate. It checks actual
nonzero usage, validated operation completion and draft ancestry before the same
reviewed Keep path. Do not retry a failed/unknown operation blindly; inspect its
original generation/usage receipt first. No audio is included in those requests.

For native and audio acceptance, launch the exact P12 executable recorded in
`native-startup.json` against isolated data, create the same two chapters/four pages,
and retain an unmapped local copy plus an authorized server-mapped copy. Follow F01–F05,
including B06 replacement and all four B08 planning kinds. Configure an entitled
backend speech model/key as described in P10/P11 UAT; verify installed/local EN/SV
voices. The author must explicitly start recording, speak English then Swedish,
correct the final transcript, and send in the same session. Start spoken dialogue,
interrupt an answer, switch to text, revise a draft, read its opening, then edit and
navigate away. Check microphone indicator release and audible stop, physical echo,
negative/uncertain spoken approval, actual operation/history receipts and unchanged
writing during exploration. Use Narrator/NVDA and keyboard alone for announcements,
focus, tabs and review controls. Record model/voice, latency, usage, device, transcript,
binary hash and UI evidence. A kernel/startup/module check cannot close these gates.

For database/device acceptance, apply the checked SQLite and SQL Server migrations
through P10 plus P9 RecoveryJson and sync v5 to an isolated approved SQL database.
Create the fixture through owned routes, map a disposable Windows copy, synchronize
sessions/planning/writing, and exercise concurrent branches, unknown operation
inspection, account revocation and metadata-only sync acknowledgments. Retain each
source namespace and authoritative receipt. Optional package acceptance installs a
new disposable profile and repeats the same core flows.

Next copyable acceptance prompt is in the current status handoff. Preserve unrelated
targeted-quality diagnostics and the P14 list-ending normalization gate. Required
gates above must remain open until their actual evidence is recorded.
