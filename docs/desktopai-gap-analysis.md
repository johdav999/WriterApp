# Desktop AI gap analysis and release handoff

Latest integrated acceptance: **2026-10-06, AI parity prompt 14**, including adopted prompts 10–13. Scope: the current uncommitted checkout; HEAD alone does not identify this implementation. The earlier comparisons and prompt-specific counts below are historical. Fresh commands, evidence and environment limits are in [desktopai-uat.md](desktopai-uat.md).

## Integrated G01–G12 acceptance — 2026-10-06

This audit freshly traced reachable controls through request construction, backend execution, review/approval, saved state and recovery. Evidence is in `artifacts/ai-parity-p14`: initial dirty-file hashes/status, focused TRX, browser/editor reports, actual Development WASM journeys, build logs and the acceptance profile/ledger. Deterministic provider contracts and compiled desktop controls establish the scopes below. Native loading, external identity/provider quality and installed/deployed acceptance remain pending; there is **no blanket parity or release approval**.

| Finding | Reachable flow and persistence/recovery trace | Current result and actual evidence scope |
|---|---|---|
| G01 / P1 — targeted replacements | Both Quality controls construct checked repetition/sentence/passive requests; shared validators and shipped editor preview/apply use unique complete anchors and preserve marks. Explicit Apply persists the approved page through host history; stale/ambiguous/embedded targets refuse. | Implemented / verified in focused real-handler tests and both shipped editor harnesses. Fresh complete-clock-to-chime proof preserves the unchanged bold word. Native/live-provider gate remains. |
| G02 / P2 — full style review | Both Quality panels expose goal selection and full review, dispatch complete `StyleQualityOptions` rather than targeted-only generation, then review the checked candidate/subset before atomic Apply and saved history. | Implemented / verified by `ClientStyleReview`, `WebStyleInput`, `StyleQuality` and local Quality suites, 14 compiled-control layouts and 23 additional shipped-editor checks. Full signed-in/native/provider acceptance remains pending. |
| G03 / P2 — recommendation execution | Shared strategy descriptors feed both Writing menus; supported recommendation callbacks build checked selection/section/continuation/list requests. Review handles prose and list insertion before scoped persistence and recovery. | Implemented / verified by recommendation/actual client/local Writing suites, 20 layouts and eight additional editor checks. Full native/live quality remains pending. |
| G04 / P2 — recovery scope | Client Writing → Writing tools exposes server-scoped planning and aggregate snapshot recovery; backend compares affected fields/pages and commits snapshots/replays transactionally. Desktop retains local exact snapshots and original-copy recovery. Legacy comparisons cannot become replayable; translated copies remain retained. | Implemented / verified in planning/aggregate/local recovery suites, 12 layouts and actual client reload → Undo → Redo. **New intent-resurrection defect P14-N03 fixed and verified.** Intentional difference: local and backend-scoped identifiers are not cross-host portable recovery IDs. |
| G05 / P2 — scene partial approval | Story scene review and storyboard inspector collect explicit all/clear/subset choices. Checked source/fingerprint and durable reviewed intent reach the scene endpoints; raw unapproved fields and linked mirrors are preserved. | Implemented / verified by scene approval/boundary/coaching suites and 24 layouts. Actual Development client approved only Narrative Intent, reopened, then scoped Undo/Redo with exact unrelated fields/pages. Existing mock provider required compatible synthetic reference IDs; unresolved IDs correctly refused. Native/external quality remains pending. |
| G06 / P2 — glossary quality | Client uses owned saved glossary; desktop Quality refreshes the mapped cache and discloses verified-empty, cached/offline or unavailable state. Shared rules and cache identity include terms; review and any supported fix remain checked and source-bound. | Implemented / verified by glossary/cache/rule/quality suites and 12 layouts, including informational near matches. Full native/authenticated backend freshness journey remains pending. |
| G07 / P2 — other-page consistency | Both Advanced/consistency flows capture ordered section pages, locate a unique primary page/anchor, save before navigation, review there and persist only the checked replacement. Unlocatable/ambiguous findings retain writing and recovery. | Implemented / verified by client/local consistency, primary-passage, checked-source and editor suites plus six layouts. Scene routes intentionally retain their single-scene target; complete live-provider/native navigation remains pending. |
| G08 / P2 — cancellation | Both hosts expose pending Cancel and carry an owned token through preparation/generation/retry, supersession and late-result cleanup. Cancelled proposals cannot Apply; new work may start immediately. | Implemented / verified by actual client/local cancellation suites and six layouts. Actual WASM Cancel of held HTTP dispatch preserved all saved pages and allowed the next request. Server/provider cancellation races have deterministic test evidence; no live-provider abort claim. |
| G09 / P3 — durable dismissal | Desktop journals exact account/backend/document/page source occurrences, shows delivery/unmapped state and Restore. Backend mapping/queue respects current full-page/glossary/rule identity; changed sources become eligible again. | Optional prompt 10 adopted / verified by dismissal/mapping/cache suites and 16 layouts. Intentional limitation: selection decisions stay local; legacy decisions without source evidence are disclosed, not invented mappings. Native/reconnect against an external account remains pending. |
| G10 / P3 — bounded targeted retry | Both Quality flows expose the same default-off opt-in and quota notice. Shared eligibility permits at most one extra strict generation after a supported semantic failure; auth/quota/network/cancel/stale/anchor/embedded failures cannot retry automatically. | Optional prompt 11 adopted / verified by actual adapter request-count tests, eight layouts and 12 additional editor checks. Invalid second result cannot Apply. Live-provider prose quality remains pending. |
| G11 / P3 — preset continuity | Both libraries expose origin, explicit send/import/copy/refresh, receipts, retry and conflict comparison. Cloud refresh does not replace an edited local preset without an explicit import/conflict choice. | **Intentional documented manual workflow**, optional prompt 12 adopted. Actual client-loader/two-device-library synthetic transfer/refresh journey, library/transfer tests and 14 layouts pass. **New account-reload teardown race P14-N01 fixed.** Automatic synchronization remains outside scope. |
| G12 / P3 — tones | Both reachable menus use six shared descriptors including Executive; checked request retains literal `Executive`, `Rewrite (Executive)`, Same length and preserve terms. Typed saved custom tones remain accepted. Review precedes Apply/history. | Optional prompt 13 adopted / verified by shared/endpoint/actual client/local Writing tests and ten layouts. Actual WASM menu and Executive request/discard/apply/reopen/Undo/Redo pass with mock-text. Native and provider prose quality remain pending. |

### Newly reproduced regressions and remaining prerequisites

| ID / priority | Reproduction and root cause | Current state |
|---|---|---|
| P14-N01 / P2 | Sign out while Library or Writing account refresh is reading the local store, then dispose its renderer/store. Previously an untracked refresh could retain `.store.lock` during teardown. | Fixed: components asynchronously await all started account refreshes before teardown completes. Two gated real-store regressions and the integrated rerun pass. Initial failure is retained in `focused-initial.trx`. |
| P14-N03 / P1 | Approve only Narrative Intent from an empty card, reopen, then Writing → Writing tools → Undo change. Pre-recovery full save materialized the DTO's legacy-purpose projection; successful replay cleared raw intent but load reconstructed it from purpose. Unrelated manual saves had the same risk. | Fixed: unchanged editor cards skip saves; both scene endpoints preserve raw legacy purpose when resolved role/intent did not change. Three regressions cover preflush, pending manual edits, both endpoints, Undo/Redo/reopen and unrelated summary retention. Actual full client journey passes after rebuild. Initial failing regression is retained in `intent-initial.trx`. |
| P14-N02 / P3 | Open a saved page ending in a list without typing, navigate away/reopen and compare HTML. StarterKit's trailing-node insertion emits an editor update and autosave appends `<p></p>`. | **Remaining defect** outside AI Apply: opening/navigation can normalize saved HTML. No authored prose, marks or list content was lost in the reproduced case. `navigation-normalization-initial-failure.json` and the fresh navigation report record it separately; subsequent AI comparisons use the exact saved pre-request graph. Follow-up: distinguish initial editor normalization from writer edits, then verify list/table-ended documents in both hosts and restart/autosave. |

Available verification passes in its stated scope; P14-N02 and the environment gates prevent an unqualified acceptance claim. To finish native acceptance, launch the changed Windows candidate with the isolated validation directory and exercise full style review, recommendation execution, scene subset, glossary freshness, other-page consistency, cancel/retry and recovery across reopen. An external signed-in backend/provider and an installed/deployed build are separate prerequisites. Do not reuse unit tests, static layouts or mock prose as evidence for those gates.

## Fresh client / desktop AI parity audit — 2026-10-05

Implementation follow-up: [AI parity fix prompts](ai-parity-fix-prompts.md)
maps G01–G12 to bounded implementation tasks and a fresh acceptance check.
The P3 workflow policies are optional and explicitly stated in that file.

This is a new comparison of the current checkout, including the explained
desktop style review and targeted-formatting repair. It compares reachable
commands, request construction, review, application, persistence and recovery;
server action registration alone is not counted as a usable host feature. The
older comparison below remains historical evidence. This audit does not verify
the deployed client or run every signed-in native/provider journey.

Full parity is still incomplete, with gaps in both directions. The reproduced
G01 formatting-loss defect is repaired for supported ranges by prompt 1 below;
G08 client request cancellation is implemented and verified by prompt 2 below;
native/provider acceptance remains open. P2 identifies material functionality gaps;
P3 identifies lower-impact workflow differences, rather than missing AI actions.

| ID / priority | AI function | Current web client | Current Windows desktop | Remaining gap |
|---|---|---|---|---|
| G01 / P1 | Targeted fixes on formatted passages | Prompt 1 uses a shared minimal-change transaction, exact checked ranges and complete-result validation. Quality and consistency keep unchanged marks; unsafe/stale results refuse without dispatch. | Targeted preview shares that transaction and retains its stricter paragraph-split preflight. | Implementation and synthetic shipped-editor verification pass (109 browser checks, 77 focused .NET tests). Signed-in full-shell, native and live-provider acceptance remain open. |
| G02 / P2, implemented | Full Style & quality AI review | Prompt 6 adds selection/current-page explained review alongside targeted findings. Shared correction/preference, reason/tradeoff, edit checkboxes and approved-only preview feed one atomic rich transaction, checked save and durable page Undo. Cancellation, stale/invalid results and save recovery retain writing. | Existing shared report/review supports selection/current-page review, individual selection and partial approval with local recovery. | Available production-handler, shared-control, desktop-reference and shipped-editor checks pass. Full signed-in browser, live-provider and rebuilt native acceptance remain separate gates; see dated prompt-6 UAT. |
| G03 / P2, implemented | Intent-recommended writing tools | Prompt 7 executes all 15 catalog tools through a versioned, checked recommendation contract with their actual user/system templates. Section/opening revisions use rich multi-page review and checked approval; paragraph tools append at the verified section end. Headlines and summaries offer selection/copy. | Actual Recommended writing tools now execute the same catalog contracts and output targets, with local review, scoped persistence and recovery. Continuation craft focus remains a separate next-paragraph operation. | Available catalog-wide handler/executor/provider-format, persistence, browser and build checks pass. Client append requires opening the last section page; supported revisions retain existing blocks/run boundaries. Full signed-in browser, live-provider and rebuilt native acceptance remain separate gates; see dated prompt-7 UAT. |
| G04 / P2, implemented | AI history Undo/Redo | Prompts 8–9 add server-captured exact planning fields and versioned rich multi-page snapshots, reachable Undo/Redo, atomic receipts and scoped drift checks. New checked replace actions recover on the same owned backend graph. Translated copies remain intact with original-copy recovery; legacy entries are comparison-only. | Existing local snapshots recover writing, section revisions, translations, selected scene-card fields and synopsis fields. Device identifiers are not used as cloud replay IDs. | Available persistence, reload/retry/failure, compiled UI/browser and builds pass. Same-backend recovery is explicit; no portable cross-host replay was introduced. Full signed-in browser, rebuilt native, live provider and deployed migrations remain independent gates. |
| G05 / P2, implemented | Scene-card approval granularity | Prompt 3 uses shared SceneCoachingReview in the editor and storyboard inspector, with arbitrary field selection, approve-all/clear and one checked partial save. The server preserves unselected raw fields and records the exact approved subset in durable history. | Whole-card review lets the writer approve an arbitrary subset of changed fields before one save; shared all/clear controls now also apply here. | Prompt 8 adds scoped planning Undo/Redo, including exact affected mirror fields. Available implementation and shared-control checks pass; authenticated browser/live-provider and native acceptance remain separate. |
| G06 / P2, implemented | Glossary-aware quality findings | Owned glossary snapshot supplies the same saved, ordered terms. Server finding caches now include glossary terms; informational near matches remain informational after cache reuse. | Explicit quality checks refresh an account/backend/document-mapped cache and pass those terms to the shared rules. Offline/failure states disclose cached freshness; verified empty and unavailable remain distinct. | Prompt 4 implementation, scoped parity/cache/approval tests and component browser evidence pass. Actual rebuilt native, authenticated full-shell and deployed acceptance remain separate gates. |
| G07 / P2, implemented | Consistency primary passages across pages | Captures ordered section pages and checked revisions; unique primary evidence maps to a page and local anchor. Jump/review opens that page after saving, and approved fixes use checked persistence plus the formatting-safe editor helper. Ambiguity, source drift and failed saves retain writing/recovery. | Captures every current-section page and resolves/navigates a primary finding to its exact page. | Prompt 5 scoped handler, ownership, recovery and browser/editor checks pass. Existing conflicting evidence and intentional decisions remain usable. Scene-content routes retain their separate single-scene target. Full signed-in, live-provider and rebuilt native acceptance remain separate gates. |
| G08 / P2, implemented | Cancel an in-flight AI request | Prompt 2 gives writing, targeted-quality and consistency one owned request token through preparation/generation/retry, pending Cancel, immediate reuse and guarded late-result cleanup. Structured translation retains its existing cancellation. | Writing, quality and advanced coaching panels expose cancellation and pass a request token through preparation/generation. | Available implementation checks pass; full authenticated browser/live-provider and native acceptance remain separate gates. Completed-proposal Dismiss remains distinct. |
| G09 / P3, implemented | Dismiss quality findings | Existing client decisions retain their lifetime. An additive source-bound device contract filters verified dismissals in ordinary page runs/listing; the existing Restore route also clears a current scoped decision. | Account/backend/document/page journals retain exact-source occurrences across reruns/restarts. Review dismissed findings offers Restore, delivery state and inactive-source disclosure. Current mapped full-page decisions exchange with the backend. | Prompt 10 adopts the optional policy. 142 focused checks, 16 browser layouts and client/desktop builds pass. Selection decisions stay local; all page-text/glossary/rule changes invalidate suppression. Legacy client decisions lack source evidence and are disclosed rather than imported. Reconnect retry requires a fresh full-page check; full signed-in/native/deployed acceptance remains open. |
| G10 / P3, implemented | Handling an invalid targeted AI result | Prompt 11 replaces implicit retry with the shared explicit, default-off choice. Eligible unchanged/non-improving repetition, sentence-length or supported passive-voice output can receive exactly one strict additional generation. | Same choice, validator, eligibility and bounded orchestration; deterministic fixes remain local. | Optional policy adopted. 187 focused tests verify actual adapter counts, cancellation, source/anchor/embedded failures and approval/recovery. 121 shipped-editor checks, eight compiled-control browser layouts and client/desktop builds pass. Authentication, quota, network, cancellation and source failures cannot cause automatic extra generation. Full signed-in/native/live-provider acceptance remains separate; see dated prompt-11 UAT. |
| G11 / P3, intentional manual workflow | Reusable-prompt continuity | Direct cloud-library edit/run/pin/delete, with shared storage/transfer help, cloud origin labels and explicit Refresh cloud presets. Save/delete status explains effects on desktop copies. | Shared help and per-preset local/imported origin, retained snapshot status and confirmed receipts explain existing import/copy/send/retry/conflict actions. Full existing comparison remains before conflict resolution. | Prompt 12 adopts and explains explicit transfer; automatic synchronization is intentionally outside scope. 137 focused checks, 14 compiled UI/browser layouts and synthetic actual-client refresh/two-desktop-library journey pass. Existing transfer/storage behavior is preserved; project/account/backend context must match. Full signed-in/native/deployed acceptance remains separate; see dated prompt-12 UAT. |
| G12 / P3 | Exact tone preset choices | Tone presets now derive from the shared supported descriptors; existing labels and request values remain. | Shared WritingOptions derives from the same descriptors: Neutral, Formal, Casual, Friendly, Technical and Executive. | **Implemented prompt 13 (2026-10-06):** adopted the optional policy. Executive selection rewrite preserves the existing client `tone=Executive`, `instruction=Rewrite (Executive)`, Same length and preserve-terms settings on desktop. Existing five values and arbitrary saved custom tones remain compatible. 125 focused checks, 10 browser layouts and isolated client/desktop builds pass; full signed-in/native/live-provider acceptance remains separate. See dated prompt-13 UAT. |

Current feature families found implemented in both hosts, within their supported
targets: selection rewriting, expansion/shortening, tone and show-don't-tell;
mapped section writing; next-paragraph continuation; selection/section/document
translation and translated copies; character/place/timeline canon refresh;
scene suggestion/refinement/open questions; synopsis suggestion/evaluation/
questions; all four storyboard analyses and suggested-scene creation; reusable
presets; and cover concepts plus variation/darker/brighter/cinematic/minimal.
The approval/recovery differences above still prevent declaring these identical
end-to-end workflows.

### Source evidence for the new findings

- G01: [web payload normalization](../WriterApp.Client/Components/Editor/PageEditor.razor),
  [web quality replacement](../WriterApp.Client/wwwroot/js/tiptap-editor-patch.js)
  (`applyQualityIssueFixDetailed`) and [desktop targeted preview](../WriterApp.Client/src/device-editor.ts)
  (`previewTargetedQualityRevision`). Fresh probe:
  `artifacts/ai-parity-2026-10-05/editor-parity-result.json`.
- G02: [client quality UI](../WriterApp.Client/Pages/DocumentEditor.razor)
  (`StyleQualityOptions TargetedOnly`) versus [desktop workspace](../WriterApp.Device.Shared/Pages/DocumentWorkspace.razor)
  (`StyleQualityRevisionReview`) and [shared report contract](../WriterApp.Shared/StyleQualityReview.cs).
- G03: [shared recommendation templates](../WriterApp.Shared/WritingRecommendations.cs),
  [client recommended request builder](../WriterApp.Client/Pages/DocumentEditor.razor.cs)
  (`CreateRecommendedToolOption`), [client preflight](../WriterApp.Client/Pages/DocumentEditor.CheckedWriting.cs)
  (`CaptureStructuredWriting`), [typed recommendation contract](../WriterApp.Shared/RecommendedWriting.cs)
  and [desktop execution/recovery](../WriterApp.Device.Shared/Services/LocalWriting.cs).
- G04: [web replay eligibility](../Application/AI/EfCoreAiActionHistoryStore.cs)
  (`TargetKind is Page or SceneContent`), [aggregate copy recovery](../WriterApp.Client/Pages/DocumentEditor.Translation.cs),
  [web synopsis](../WriterApp.Client/Pages/Synopsis.razor),
  [desktop scoped replay](../WriterApp.Device.Shared/Services/LocalAiHistoryActions.cs)
  and [cloud-only desktop entries](../WriterApp.Device.Shared/Services/DeviceAiHistoryService.cs).
- G05: [client scene review/apply](../WriterApp.Client/Pages/DocumentEditor.razor.cs)
  (`ApplySceneAiProposalAsync`), [client inspector](../WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor)
  and [desktop selection/review](../WriterApp.Device.Shared/Components/LocalAiPanel.razor)
  (`SceneCoachingReview`, `_approvedSceneFields`).
- G06: [server glossary context](../Application/Documents/Quality/QualityCheckService.cs)
  (`RunChecksAsync`) versus [local quality context](../WriterApp.Device.Shared/Services/LocalQualityChecks.cs)
  (`Analyze`, empty glossary argument).
- G07: [client consistency request/range](../WriterApp.Client/Pages/DocumentEditor.razor.cs)
  (`ExecuteContinuityActionAsync`, `BuildContinuityApplyRangeAsync`),
  [client comparison evidence](../WriterApp.Client/Pages/DocumentEditor.CheckedCanon.cs),
  [desktop complete section](../WriterApp.Device.Shared/Services/LocalConsistencyContext.cs)
  and [page-aware resolution](../WriterApp.Device.Shared/Services/LocalConsistencyRevisions.cs).
- G08/G10: [client request handler](../WriterApp.Client/Pages/DocumentEditor.razor.cs)
  (`PostAiActionAsync`), [targeted retry adapter](../WriterApp.Client/Pages/DocumentEditor.TargetedQualityRetry.cs),
  [shared policy](../WriterApp.Shared/TargetedQualityRetry.cs)
  versus [desktop quality](../WriterApp.Device.Shared/Components/LocalQualityPanel.razor),
  [writing](../WriterApp.Device.Shared/Components/LocalWritingPanel.razor)
  and [advanced coaching](../WriterApp.Device.Shared/Components/LocalAiPanel.razor).
- G09: [server dismissal](../Application/Documents/Quality/QualityCheckService.cs)
  (`DismissIssueAsync`) versus [desktop current-check dismissal](../WriterApp.Device.Shared/Components/LocalQualityPanel.razor).
- G11/G12: [client presets/menu](../WriterApp.Client/Pages/DocumentEditor.razor.cs),
  [desktop transfers](../WriterApp.Device.Shared/Components/LocalPromptPanel.razor),
  [transfer service](../WriterApp.Device.Shared/Services/DevicePromptLibrary.Transfers.cs)
  and [shared tone options](../WriterApp.Shared/WritingActions.cs).

### Fresh verification and scope

166 focused tests passed against freshly rebuilt current assemblies. Coverage
included style review/workflows, both quality and consistency handlers, scoped
history, translation handlers, scene/synopsis coaching, reusable-prompt transfers
and writing endpoints. Evidence: `artifacts/ai-parity-2026-10-05/tests.log` and
`results/parity.trx`. These tests establish the supported synthetic workflows;
they are not proof that every source-audited gap has a failing automated case.

The baseline headless Edge probe loaded both shipped editor bundles and the
client patch with synthetic prose. It used the same `rewrite` → `replace`
normalization as PageEditor, applied a complete sentence fix and confirmed that
the client dropped `<strong>clock</strong>` while desktop retained it. No user
document was read or changed by this probe, and no provider call was made.

No deployed release, live provider-quality comparison or complete native
client/desktop UAT was run for this audit. The earlier desktop activation remains
separate evidence. Unsupported rich content, provider/model capabilities and
owned static-PNG cover limits remain supported-scope constraints rather than
new one-host features. Registered outline-generation endpoints have no matching
reachable typed host journey found here; they are not counted as client features
missing on desktop. Intentional-consistency decisions remain browser/device
local in both hosts.

### Prompt 1 implementation and fresh verification — 2026-10-05

The maintained probe is now `WriterApp.Client/tests/verify-targeted-revisions.mjs`;
fresh evidence is `artifacts/ai-parity-p01/`. Both rebuilt shipped editors retain
the unchanged bold **clock** while the last plain **clock** becomes **chime**.
The shared adapter expands the changed span to whole words/graphemes, uses source
marks, validates the complete resulting text and creates an undispatched schema
transaction. Unsupported source blocks/leaves, incompatible changed marks and
Unicode boundaries refuse. Pure deletion can remove mixed marks without assigning
new marks. Client Apply refuses editor-appended transactions that would change
unrelated nodes (including a not-yet-normalized trailing table).

Client Apply no longer relocates stale ranges to nearest anchors or retries with
freshly read expected text. Review resolution accepts an exact range or a unique
anchor; ambiguous/missing recovery anchors refuse. Existing checked source,
approval, save/history and Undo paths remain. A separate synthetic consistency
fixture exercises its real resolver and the same replacement helper; this proves
that supported path, rather than claiming the quality probe reproduced a separate
end-to-end consistency defect.

Verification: **109 real-editor checks**, **77 focused .NET tests**, TypeScript
adapter check, both Vite builds, Release client and isolated Debug desktop builds.
Client tests exercise actual ProseMirror Apply/Undo/Redo and reopen; device editor
checks exercise preview/Apply/source restoration/reopen, with durable device
Undo/save-failure recovery covered separately by production workflow tests.
Screenshots at 1280 and 480 pixels and the updated bold-clock probe result are
in that evidence directory. Full authenticated shell, live provider, actual native
relaunch/interaction and packaged/iOS acceptance remain separate gates. No user
writing, deployment, external identity or database migration was changed.

Next concrete task: prompt 2 (client in-flight cancellation). Prompt 5 may use
this verified replacement helper when adding cross-page consistency targeting.

### Prompt 2 implementation and fresh verification — 2026-10-05

**G08 implementation and available verification pass.** The client owns one
cancellation lifetime for ordinary writing, quality checks/targeted revisions
and consistency checks/targeted revisions. It covers save-before-request, checked
source/canon/outline/structured-writing preparation, generation, supported editor
reads and bounded strict retries. The shared pending control exposes **Cancel AI
request**; explicit cancellation immediately detaches the owner so another request
can begin. Late responses cannot restore proposals or clear a newer request's
status. Navigation, document/page changes, account invalidation and disposal cancel
the relevant work. Already-started authored saves finish independently; Apply uses
the reviewed candidate and never performs hidden generation.

Evidence in `artifacts/ai-parity-p02/`: **194 scoped .NET passes**, including **33
new cancellation cases**, **109 shipped-editor checks**, six compiled cancellation
control layouts at 1280/480 pixels, actual rendered button-event dispatch, final
client Release and isolated Windows Debug builds. Synthetic provider/authorized
SQLite tests cover preparation, canon refresh, generation, strict retry, retained
history preparation, all three fresh-request races, late editor reads, navigation,
disposal, account/backend changes, error recovery, advisory usage refresh and
durable save completion. The existing structured-translation and desktop reference
flows pass their relevant regression cases. The legacy tighten retry helper shares
the owner token; ordinary checked-outline writing intentionally retains its existing
retry exclusion so proposal identity still names the reviewed result.

Cancellation prevents local retention/application and signals supported HTTP/JS
calls. A dispatched remote request may still finish and consume quota; this is not
a refund or proof of remote provider termination. Full signed-in browser lifecycle,
live-provider behavior and native interaction/relaunch were not exercised. No new
content contract, schema, migration, dependency, editor algorithm or generated
editor asset was needed. See the dated [UAT evidence](desktopai-uat.md#ai-parity-prompt-2--client-request-cancellation-2026-10-05).

Next concrete task: **prompt 3**, client scene-card partial approval, in this same
uncommitted checkout. Earlier next-prompt statements above remain historical.

## Historical comparison through 2026-10-04

Most desktop AI workflows now have reachable review, explicit application and durable recovery. **Full client/desktop parity is not accepted.** Prompt 14 implements checked broader web translation Apply with atomic persistence and original-copy recovery; legacy plain-text proposals remain blocked. Prompt 15 connects desktop guidance to the shared server-owned demo policy, scoped sync and explicit completion. Prompt 16 adds authenticated owned remote static-PNG materialization, immutable scoped caching and offline publishing without changing cover metadata before explicit Save. Prompt 17 aligns bounded saved writing outline context and invalidates changed planning approval in both hosts. Prompt 18 implements real supported owned cover edits with shared review and durable Save/recovery. Prompt 19 adds checked production web generation and scoped confirmed saves, including recoverable aggregate section writing. Prompt 20 closes the functional one-shot applied-reporting gap with a durable scoped browser outbox and owned persistence-derived ordered receipts. Native Windows interaction and authenticated provider/storage/deployed-backend acceptance remain open independently of passing automated checks.

“Implemented” below means the listed supported workflow exists and passed the current automated checks. “Partial” identifies a remaining functional or cross-host limitation. It does not measure provider quality or establish a parity percentage. No live quality or latency measurements were taken.

## Final comparison table

The web writing, translation, quality, canon, consistency, prompts and history reference is [DocumentEditor code](../WriterApp.Client/Pages/DocumentEditor.razor.cs) and [its UI](../WriterApp.Client/Pages/DocumentEditor.razor). Synopsis uses [Synopsis](../WriterApp.Client/Pages/Synopsis.razor); covers use [ProjectCoverStudio](../WriterApp.Client/Components/Covers/ProjectCoverStudio.razor); onboarding uses [Onboarding](../WriterApp.Client/Pages/Onboarding.razor). Each test class named below was executed in the final, unfiltered Release suite, rather than inferred from action registration. Browser evidence supplements these tests; it does not replace their persistence checks.

| Original benchmark feature | Final desktop/client behavior and source evidence | Status | Current automated evidence | Independent native / live acceptance |
|---|---|---|---|---|
| Rewrite | [Shared WritingOptions](../WriterApp.UI.Shared/WritingOptions.razor) supplies client tone/length/preserve-term choices; [LocalWritingPanel](../WriterApp.Device.Shared/Components/LocalWritingPanel.razor) requests a pinned selection and reviews before Apply. Grammar is disclosed as neutral rewriting. | Implemented | `LocalWritingPanelTests` (38), `LocalWritingTests` (36), `WritingEndpointTests`; shipped selection/rich-content checks. | Windows and authenticated prose quality open. |
| Expand / shorten | Dedicated selection and complete structured-section actions; [LocalWriting](../WriterApp.Device.Shared/Services/LocalWriting.cs) preserves ordered pages and text-node identities. | Implemented | Same writing suites; ordered/malformed/stale section tests; actual two-page review fixture. | Native multi-page application and live output open. |
| Change tone / Show, don't tell | Dedicated selection/section commands and shared typed settings, rather than a custom-instruction substitute. | Implemented | Dedicated action-key/target/settings cases in `LocalWritingPanelTests`; `WritingEndpointTests`. | Native/live open. |
| Next paragraph | Dedicated `propose.next-paragraph`, saved section/scene guidance, one inert paragraph appended after the final page. Existing rich nodes remain. [LocalWriting](../WriterApp.Device.Shared/Services/LocalWriting.cs). | Implemented | Continuation review/Apply/history tests and shipped-device/client reopen tests. | Native/live semantic continuation open; the established provider context-tail limit still applies. |
| Custom instructions | Existing editor instructions retained through [DeviceAiActions](../WriterApp.Device.Shared/Services/DeviceAiActions.cs); reusable custom selection/section presets additionally use the typed review/application path. | Implemented within supported targets | `DeviceAiActionTests`, `LocalWritingPanelTests`, `LocalPromptPanelTests`, custom-preset real-editor checks. | Native/live open; unsupported structure receives guidance. |
| Selection translation | Shared language/options/result presentation, exact captured selection, explicit Apply and guarded recovery. [LocalTranslationPanel](../WriterApp.Device.Shared/Components/LocalTranslationPanel.razor). | Implemented | `LocalTranslationPanelTests` (10), `TranslationEndpointTests`, real-editor selection mapping/inert-output checks. | Native and real language quality open. |
| Section / document translation | Desktop [LocalTranslation](../WriterApp.Device.Shared/Services/LocalTranslation.cs) and [web handlers](../WriterApp.Client/Pages/DocumentEditor.Translation.cs) capture every ordered page/run. Web replace/duplication uses owned atomic approvals/receipts and explicit original-copy recovery; desktop retains local Undo/Redo. Legacy plain-text web proposals remain blocked. | Supported workflows implemented in both hosts | Prompt-14 continuation (2026-10-04): 1,483 passed; production handlers/SQLite transactions, replay/restart/recovery, migrations, source validation, late-result/backend guards and both editor bundles. See dated UAT evidence. | Automated P13-003 closed; native/deployed/live language quality open. |
| Individual style & quality issues | [LocalQualityPanel](../WriterApp.Device.Shared/Components/LocalQualityPanel.razor) and [LocalQualityChecks](../WriterApp.Device.Shared/Services/LocalQualityChecks.cs) offer findings, Jump, targeted supported fixes and generated revision review; preserve other writing. | Implemented within supported issue/range capabilities | `LocalQualityTests` (23), panel tests (10), quality capability/normalization/output suites, real-editor exact-range tests. | Native highlight/focus and live generated fixes open. Unsupported or ambiguous ranges require manual revision. |
| Consistency Coach | [LocalConsistencyContext](../WriterApp.Device.Shared/Services/LocalConsistencyContext.cs) includes validated canon/planning; [LocalAiPanel](../WriterApp.Device.Shared/Components/LocalAiPanel.razor) supports severity, evidence, Jump and individual fix review/Apply. | Implemented | Context tests (21), panel tests (13), revision tests (4), continuity/server tests and shipped-editor safe replacement. | Native/live bible-aware findings and fix quality open. |
| Character / place / timeline bibles | [DeviceBibleService](../WriterApp.Device.Shared/Services/DeviceBibleService.cs) and [LocalBibleStore](../WriterApp.Device.Shared/Storage/LocalBibleStore.cs) implement extraction/update/view, source tokens, cached offline read and isolated versioned storage. | Implemented for actual client read/refresh scope | `DeviceBibleTests` (21), `DocumentBiblesControllerTests` (13), refresh/patch tests; actual cached/offline canon fixture. | Native/live extraction/update open. Neither client has a corresponding manual authored-bible editor to benchmark. |
| Scene-card suggestion / refinement | [LocalSceneCoaching](../WriterApp.Device.Shared/Services/LocalSceneCoaching.cs) and [SceneCoachingReview](../WriterApp.UI.Shared/SceneCoachingReview.razor) cover whole card or pinned fields, validated references/tags/status, selected-field approval and durable recovery. | Implemented | `LocalSceneCoachingTests` (40), panel tests (13), scene normalizer/preservation/controller suites; editor/storyboard review fixtures. | Native/live coaching and resolved canon-link acceptance open. |
| Synopsis field improvement | [LocalSynopsisCoaching](../WriterApp.Device.Shared/Services/LocalSynopsisCoaching.cs) uses all ten source fields and notes, pins the chosen field and saves only approved text. A synced project manuscript is required; manuscript pages/scenes are not. | Implemented | Local coaching tests (36), panel tests (20), device API/server tests, interrupted save and scoped history checks. | Native/live field quality open. |
| Synopsis evaluation / guiding questions | Dedicated evaluation and questions modes share [SynopsisCoachingFeedback](../WriterApp.UI.Shared/SynopsisCoachingFeedback.razor); feedback is inert and cannot become a field Apply. | Implemented | Same synopsis suites; three actual mode fixtures and malformed/wrong-mode/source/account tests. | Native/live analysis quality open. |
| Four storyboard AI actions and suggested scenes | [LocalStoryboardData](../WriterApp.Device.Shared/Services/LocalStoryboardData.cs) retains next-scene, missing-scenes, subplot continuity and POV-balance actions. Approved suggested-scene creation retains identity/source checks and separate history outcomes. | Implemented | `LocalStoryboardTests` (19), including all four owned/versioned request paths and stale creation refusal; scene preservation suites. | Native board interaction and real provider suggestions open. Interrupted creation must be inspected before retry; it is not a reversible cloud command. |
| Reusable prompts | [DevicePromptLibrary](../WriterApp.Device.Shared/Services/DevicePromptLibrary.cs), [transfer orchestration](../WriterApp.Device.Shared/Services/DevicePromptLibrary.Transfers.cs), shared editing and production Writing review support built-in/custom presets, parameters, categories, pin/edit/delete and scopes. Transfers use explicit CAS/retry/conflict/tombstone handling. | Implemented for explicit transfers | `ReusablePromptContractTests` (23), `ReusablePromptTransferTests` (15), panel tests (6), reusable-writing endpoints and real-editor custom runs. | Native/live and deployed migrations open. Automatic background two-way merging is not implemented or promised. |
| AI history / undo / redo | [DeviceAiHistoryService](../WriterApp.Device.Shared/Services/DeviceAiHistoryService.cs) retains authoritative local outcomes. [WebAiHistoryOutbox](../WriterApp.Client/Services/WebAiHistoryOutbox.cs) retains immutable approval before checked Save and reconciles owned receipts after reload. Page/SceneContent Undo/Redo requires current source and expected HTML. | Implemented within explicit reporting/recovery scope | Existing device/local/controller tests plus 41 prompt-20 cases, actual editor/card handlers, crash/ordering/ownership/rejection/upgrade tests and real two-tab IndexedDB checks. | Native/deployed/full signed-in browser acceptance open. Browser receipts do not become device entries. Planning cards and aggregate snapshots cannot authorize manuscript Undo; retained inspection/original-copy recovery applies. |
| Cover concept generation / saving | [DeviceCoverStudio](../WriterApp.Device.Shared/Services/DeviceCoverStudio.cs) retains review, explicit Save, metadata CAS, prior-cover recovery and normal project sync. [Owned assets](../Application/Covers/CoverAssetService.cs) resolve only owned saved project references or server provider results; validated static PNGs up to 2 MiB retain exact bytes/hash/provenance in backend/account/project-scoped cache for offline preview/publishing. Materialization leaves the saved remote URL unchanged until explicit Save. | Supported owned remote PNG workflow implemented; other remote codecs unsupported | Prompt 16: 1,579 full-suite passes, production SQLite HTTP/asset policy/storage/migration and actual Cover Studio/Publishing handler checks, 116 integrated panel renders and both shipped editors. See dated UAT. | Automated P13-005 closed for supported PNG sources. Configure trusted account/container paths and matching migrations; real provider/storage/TLS, native and physical publishing remain open. JPEG/WebP/GIF/SVG conversion is unavailable. |
| Cover variations and adjustments | Shared controls and labelled Original/Proposed review in both studios route the selected owned PNG through the real provider edit adapter. Five operations, source/hash/revision guards, server-issued proposal receipts, explicit selection/Save/Dismiss, offline scoped previews and prior-cover recovery. | Implemented for advertised supported PNG/provider configurations | Prompt 18: 56 new cases; 1,671 full-suite passes, 138 integrated Razor renders, both editor bundles and five clean builds. Intercepted multipart routing and normal owned SQLite HTTP/component lifecycles pass. | Real-provider darker/brighter/composition quality, native/deployed and physical export remain open. Unsupported codecs/models remain unavailable. |
| Guided AI onboarding | [DeviceOnboarding](../WriterApp.Device.Shared/Services/DeviceOnboarding.cs), [LocalOnboardingStore](../WriterApp.Device.Shared/Storage/LocalOnboardingStore.cs) and shared guides retain local/guest practice and offer explicit owned online demo creation/reopen, scoped sync, Run Demo, normal review/Apply/history and completion reconciliation. | Implemented supported server-demo workflow | Production SQLite HTTP/device bootstrap, lost acknowledgements, second device/window, ownership, account/backend, no-reseed, expiry/used policy, zero-quota demo versus ordinary denial, stale Apply and migration checks pass in the fresh full suite. Actual shared/component fixtures and both shipped editors pass; see prompt 15 UAT. | Native/live account/provider demo gate open. Only the server grant permits the exact section/action; local labels grant no allowance. Local guide completion, request use/success, Apply and online completion remain separate. |
| Explicit review / Apply / recovery | Device lifecycle and checked production web flows save before generation, reject stale source/planning/account/backend and preserve unsupported content. Web aggregate translation and section writing retain durable approved intent and original-copy recovery. | Implemented for supported targets; explicit limitations below | Full suite, local and actual web handler/revision/recovery/sync/account/malformed/cancellation tests; owned non-demo SQLite HTTP persistence with host restart. | Actual close/relaunch, real accounts, deployed backend and provider acceptance open. |

Additional limitations found while tracing the reference:

- **Former client limitation implemented in prompt 18:** variation, darker, brighter, cinematic and minimal use the real owned PNG image-edit adapter and reachable review/selection/Save/recovery in both studios. Unsupported configurations remain unavailable before transport. Synthetic routing/lifecycle checks do not establish real image-quality acceptance.
- **Saved writing context implemented in prompt 17:** both hosts send the same bounded owned canonical outline through selection, section, continuation and reusable writing. Generation/Apply reject changed planning. Provider semantic equivalence remains a separate live gate; unrelated outline generation is outside this workflow.
- Legacy version-0 web requests do not automatically acquire the device's version-checked source, canon, synopsis and aggregate recovery guarantees. Normal owned web persistence is tested separately below.
- iOS uses prompt 21's native MSAL adapter. Its public-client registration remains unconfigured; managed compilation does not establish signed-in AI, a native bundle or device usability.

## Acceptance profile and exercised flows

Surfaces: actual Razor components/handlers, production device services/storage with isolated repositories and synthetic transport, production MVC authorization/repositories on SQLite and disposable local SQL Server databases, both shipped editors in headless Edge, and the complete Development LocalDev web shell with a synthetic provider. LocalDev is existing development authentication, not deployed OIDC acceptance. No live provider, installed native app, macOS/iOS simulator or customer distribution was exercised. See the dated prompt 22–23 evidence below for package and export status.

Seven representative flows were exercised, together with their existing failure suites:

| Flow | Required observable result | Evidence |
|---|---|---|
| 1. Guide → writing presets → section revision/continuation | Skip/resume preserves the sample; review is inert; selection/section/append targets remain pinned; Apply/Dismiss and local history reflect actual saves. | Onboarding/writing/preset tests; `p06`, `p09`, `p12`; real editor practice application/reopen. |
| 2. Translation at all three scopes | Complete ordered page/run mapping, supported rich structure, replace/copy ownership, interruption recovery and guarded Undo/Redo; unsafe legacy web broader writes rejected. | Local/server translation tests, client safety tests and editor harness. |
| 3. Canon → consistency/style finding → targeted fix | Correct source/canon versions and account, readable evidence/Jump, unique supported anchors; wrong/stale/malformed content never applies. | Bible, consistency and quality suites; `p02`–`p04`. |
| 4. Scene/storyboard → field review or suggested scene | Approved fields only; valid links/tags/status; all four board actions and stale-source creation refusal; authored notes and other nodes retained. | Scene and storyboard suites; `p07`. |
| 5. Synopsis → evaluation/questions/field improvement | Feedback has no Apply; selected field save preserves all other planning and rich manuscript; approved interrupted saves resume without generation. | Synopsis suites; `p08`. |
| 6. Prompt transfer/history → offline/conflict/account change | Stable IDs, retry receipts, Keep both/conflict/tombstones, cached cloud history, local outcomes and guarded recovery stay independent. | Preset/history/sync suites; `p09`, `p10`. |
| 7. Cover → concepts → selection/save → offline/recovery | No cover change before Save; metadata conflicts reject overwrite; cache survives offline; original bytes preserved through sync/publishing tests. | Cover/concurrency/sync/publishing suites; `p11`. |

Fixtures include Unicode (`Åsa`, Japanese and emoji), multiple sections/pages, supported marks/headings/lists/tables/hard breaks, scene cards, synopsis and bibles. These are isolated equivalent **test inputs**, not a real account's documents. Panel orchestration tests use synthetic editor interop where needed; real browser tests separately verify the shipped mapping/schema algorithms. Their combination is not a live end-to-end provider run.

Failure coverage includes offline transitions, stale selections/writing/planning/canon, conflicts, plan/quota denial, cancellation and ignored late responses, malformed results, account/backend switching, save/reopen, interrupted saves, scoped Undo/Redo, immutable recovery and ownership. Fresh store/host instances establish reopen behavior; a native process close/relaunch remains untested.

## Prompt 13 results and evidence (historical)

Evidence root: `artifacts/desktopai-p13/` (ignored local captures; regenerate with the commands below). Historical prompt captures were not overwritten.

| Check | Current result | Evidence |
|---|---|---|
| Final unfiltered Release .NET suite | **1,434 passed; 0 failed/skipped** | `final-tests.log`, `final.trx`, `test-summary.json` |
| Initial integrated suite | 1,420 passed / 1 failed; availability regression subsequently fixed | `full-test.log`, `full.trx`; not counted as current success |
| Ordinary non-demo web persistence | Production authorization, page PUT/GET and SQLite storage pass; restart retains Unicode/rich content, all page/section IDs and other pages, project identity, scene metadata and scene mirror. Anonymous 401 and another owner 404. | `DesktopAiWebPersistenceTests`, `normal-web-persistence.json`; synthetic test authentication, not live OIDC/browser acceptance |
| SQLite version history | UTC-offset ordering, retention/count pruning and cross-owner refusal pass | Second `DesktopAiWebPersistenceTests` case |
| Real editor/browser harness | **60 passed**, no page errors; both shipped bundles | `browser-check.log`, `browser-results.json` |
| Actual Razor panel presentation | **42 fixtures × 2 viewports = 84 renders**; readable status/results, enabled native controls reached by keyboard, no horizontal overflow | `browser-results.json`, `p02`–`p12`, `screenshots/` |
| Equivalent shipped device/client editor inputs | **4 checks** at 1280×720 and 1920×1080; review inert, explicit application, supported structure/run IDs and reopening retained | `browser-results.json`, `screenshots/{device,client}-editor-*.png` |
| Release server, client and shared device builds | Passed, each **0 warnings/errors** | `server-build.log`, `client-build.log`, `device-build.log` |
| Release Windows build | Passed, **0 warnings/errors**; candidate inspected, not launched | `windows-build.log`, `native-path-inspection.json`, `native-candidate.json`, `native-candidate-sha256.json` |
| iOS managed target, `iossimulator-x64` | Passed, **0 warnings/errors** on Windows; compilation only | `ios-build.log`; no Mac bundling/signing/simulator or physical device acceptance |

Panel screenshots use the actual compiled UI.Shared/Device.Shared scoped CSS with a minimal fixture host. There are comparable **browser fixtures**, including the shared client translation settings, at both requested viewport sizes; these are not native screenshots or a full web-shell comparison. Exact viewport images use `*-1280x720.png` / `*-1920x1080.png`; `*-full.png` additionally captures long review content. Keyboard checks complement actual Razor handler tests but do not establish native drawer focus, selection or assistive-technology acceptance. Solid-color cover fixtures verify image selection/bytes, not illustration quality.

The test build encountered the existing unavailable NuGet vulnerability feed (`NU1900`) and existing `LocalStoryboardTests` analyzer warning (`xUnit2031`). Explicit final builds were clean. No dependency/security-feed freshness claim is made.

## Defect ledger and open gates

Prompt 14's current evidence (2026-10-04) is `artifacts/desktopai-p14-continuation/`: 1,483 unfiltered Release tests passed, 62 shipped-editor checks passed, 86 actual Razor fixture renders at the two requested viewports passed, and four equivalent device/client application/reopen comparisons passed. The production web handlers and SQLite HTTP endpoints exercise checked source, every replace/duplicate mode, durable approval, injected transaction rollback, lost acknowledgement, receipt replay, reload, original-copy recovery and aggregate history. Twelve continuation cases cover cancellation/context changes during capture/preview, late responses, backend-scoped recovery and mismatched approval acknowledgements. SQLite schema upgrade and SQL Server script/model checks pass. See [the dated continuation entry](desktopai-uat.md#prompt-14-continuation--late-result-and-backend-isolation-2026-10-04) for fresh checks and independent live gates. The original `artifacts/desktopai-p14/` evidence and prompt-13 historical table remain intact.

Severity: P1 blocks safe use/release of its stated workflow; P2 is an interaction regression or functional limitation. A closed automated check does not close its native/live gate.

Latest integrated evidence before prompt 17 is prompt 16 (2026-10-04), `artifacts/desktopai-p16/`: **1,579 unfiltered Release tests, 62 shipped-editor checks, 116 actual Razor fixture renders and four equivalent device/client Apply/reopen checks passed**. All five Release targets built with zero warnings/errors. Production asset endpoints, SQLite upgrade and exact offline publishing bytes passed with synthetic authentication/provider/DNS/transport seams; real storage TLS and native/physical export remain separate gates. See [the dated UAT](desktopai-uat.md#prompt-16--owned-remote-cover-materialization-2026-10-04). Earlier prompt-specific counts below describe their dated closure evidence, not the latest suite total.

| ID | Severity / expected → observed | Fix or outstanding work | Verification / status |
|---|---|---|---|
| P13-001 | P2: writing actions should enable after explicit cloud enrollment/sign-in without reopening → stale availability and outdated test adapter left them disabled. | Parent passes cloud enrollment; panel refreshes on cloud/account changes and cancels/discards older availability responses. Test uses authenticated availability seam and waits for child refresh. | Sync regression + two sign-in/late-availability cases pass in final suite. Automated fix closed; native acceptance open. |
| P13-002 | P1: normal web save with enabled version history should complete on SQLite → pruning orders `DateTimeOffset` in SQL and throws after checkpoint save. | Filter the owned page in SQL, order timestamps in memory as existing listing/latest lookup already do; retain deterministic ID tie-break. Reject pruning another owner's page. | Non-demo HTTP save/restart and mixed UTC-offset/count/retention/ownership regression tests pass. Automated fix closed; deployed SQL Server/SQLite acceptance open. |
| P13-003 | P1: broad web translation must preserve all pages/formatting and confirm actual persistence. | Prompt 14 replaces production broader generation/review/Apply with the checked page/run contract, durable server approval, serializable aggregate commit, idempotent receipts and original-copy recovery. Unsafe first-page helpers removed. Legacy previews retain their refusal; selection/copy/discard remain. Unknown commit outcomes pause editing until reconciliation. Aggregate history uses terminal receipts without page-HTML Undo snapshots. The 2026-10-04 continuation pins async capture/preview/commit and recovery to their account/document/backend context. | Implementation/available automated checks closed: 1,483 full-suite passes; 62 real editor checks, 86 renders and 4 reopen comparisons. SQLite upgrade and rollback/replay pass; SQL Server script/model checks pass. Native, deployed SQL Server and live language quality remain open. |
| P13-004 | P2: matching first-use experience → desktop guidance/sample lacks server demo eligibility/completion synchronization. | **Implemented in prompt 15:** owned version-1 server grant/status/progress receipts, separate durable local/cloud mapping, explicit demo-only sync and normal writing review/Apply. No local provenance entitlement. | **Closed for supported implementation and available automated checks:** 1,510 full-suite passes plus fresh integrated browser evidence. Real account/provider/native demo acceptance remains independently open. |
| P13-005 | P2: owned remote client cover source → desktop/offline asset availability. | **Implemented in prompt 16 for static PNG:** additive authenticated source/asset receipts, exact trusted storage paths, redirect/DNS/size/encoding validation, immutable owned server cache, scoped local bytes/provenance and existing explicit Save/recovery/sync. No arbitrary URL request. Unsupported codecs preserve the prior cover with guidance. | **Closed for supported implementation and available automated checks:** 69 new cases, 1,579 full-suite passes, 116 actual Razor renders and both editor bundles. Real trusted storage/provider/TLS, native and physical export acceptance remain open; no broader codec parity claim. |
| P13-006 | P2: equivalent saved writing outline context. | Implemented in prompt 17: shared canonical owned snapshot, bounded escaped provider context, original-context retries and generation/Apply guards in both hosts. | Supported implementation and automated checks closed: 36 new cases, 1,615 full-suite passes, 62 editor checks, 116 renders, four reopen comparisons and five clean Release builds. Native/deployed/provider semantic review remains open. |
| P18-001 | P2: former client cover operation stubs left variation/darker/brighter/cinematic/minimal missing in both clients. | Prompt 18 implements a real owned provider edit adapter and both reachable host journeys, shared comparison UI, immutable proposals and durable web approval/Save/restore receipts. | Supported implementation and automated checks closed: 56 new cases; 1,671 full-suite passes, 138 renders, 62 editor checks, four reopen comparisons, five clean builds. Live image quality, native and deployed gates remain open. |
| P19-001 | P2: reachable web AI requests and Apply paths lacked consistent checked writing/planning/canon source contracts. | Prompt 19 adds owned content fingerprints, before/after generation checks, account/backend/expiry checks and serializable scoped saves; unsupported mappings explicitly refuse. Per-flow evidence is below. | Supported implementation and available automated checks closed: 1,773 unfiltered passes, 103 added cases replacing one redundant case, 64 editor checks, 142 renders, four reopen comparisons and five clean builds. Durable general applied-event reporting and native/live gates remain open. |
| P13-G01 | Native Windows gate | Confirm executable/backend/data directory, rebuild and launch isolated data, exercise keyboard/drawers/selection/loading/offline/Apply/Undo/Redo/save-close-relaunch; capture both requested sizes. | Native computer APIs disabled in this session. No native screenshots, relaunch or acceptance claimed. |
| P13-G02 | Authenticated deployed provider gate | Authorized isolated account/project, real backend/provider configuration, matching capabilities/migrations, plan/quota fixtures, second account and controlled network/failure testing. | No authorized authenticated live environment available; synthetic tests do not close this gate. |
| P21-001 | P2: iOS used the unavailable identity scaffold and Windows-only callback settings. | iOS MSAL system presentation/native Keychain, scoped selected-account restore, guarded callback/resume/cancellation, explicit public configuration and immediate shared sign-out/revocation generation invalidation. | Supported implementation and available checks closed: 38 new cases, 1,852 full-suite passes, 156 Razor renders and five clean builds. Registration/native acceptance remains open. |
| P13-G03 | iOS/package gate | Authorized iOS client registration/API allowlist, Mac/Xcode, signing/Keychain entitlements, simulator/device and package installation/update checks. | Prompt 21 implements the native adapter and lifecycle; tracked iOS ClientId stays empty until authorized configuration is supplied. Managed compilation/seam tests pass; actual signed-in callback/Keychain/device/package acceptance remains open. |
| P20-001 | P2: general web applied reporting was one-shot and could lose a saved outcome or mark history before content persistence. | Version-1 account/backend-scoped IndexedDB approval intent, immutable owned server receipts, atomic checked-save proof, ordered reporting/reconciliation and inert inspectable delivery UI. | Functional gap closed: 41 new cases, 1,814 unfiltered Release passes, 70 editor checks, 148 Razor renders, four reopen comparisons, real two-tab storage and five clean builds. See prompt-20 evidence below. |
| P13-G04 | Database/web/publishing gate | Deployed supported database, normal non-demo web browser save/reload and physical cover export verification. | Prompt 22 adds actual LocalDev non-demo shell, disk DOCX/EPUB/cover-byte checks and local SQLite/SQLEXPRESS upgrade/concurrency evidence. Local facets pass; deployed migration/OIDC/remote cover and native reader/dialog acceptance remain open. |

## Prompt 19 per-flow checked-source contracts and evidence (2026-10-04)

All listed web generation paths require an affirmative typed version-1 `web-source` response after saving. [WebCheckedAi](../WriterApp.Client/Services/WebCheckedAi.cs) pins account/backend/owned target, confirms content before and after transport and requires echoed source and a valid proposal date before review. [WebAiSourceService](../Application/AI/WebAiSourceService.cs) fingerprints saved writing, project/outline, notes, scene cards/content, synopsis and canon. The server verifies before and after provider execution; sync acknowledgement/timestamp changes alone do not change this fingerprint. Existing strict device/legacy version checks remain. [WebAiMutationFilter](../Application/AI/WebAiMutationFilter.cs) verifies checked scoped saves inside a serializable transaction and returns an explicit confirmation header. Older external callers may omit additive fields where previously supported; their requests do not acquire checked semantics.

| Audited reachable production web flow | Request, review and Apply contract | Current evidence and explicit limits |
|---|---|---|
| Selection writing, rewrite, continuation and reusable presets | Saved page/scene and owned `WebAiSource`; shared saved outline and reusable-prompt contracts; echo/action/date/local-editor checks; scoped checked Save before an Applied label. | `WebCheckedSourceServerTests`, `WebCheckedSourceHandlerTests`, `WebCheckedMutationTests`, existing writing/preset/continuation tests. Provider context is rebuilt from the owned saved source. Unsupported rich mappings refuse without replacing content. |
| Expand/tighten/change-tone/show-don't-tell section and reusable section prompt | Complete typed writing structure with stable pages/runs, real editor capture/preflight, saved outline and universal source. Explicit Apply reuses prompt-14 durable operation/receipt/original-copy recovery with `IsWriting`, preserving language. | Five actual Razor handler actions commit three mapped pages and reopen recovery (`WebCheckedSourceHandlerTests`); two new real-editor cases cover rich Unicode/whitespace/blank runs and unsupported later-page preflight. A section tool without a reusable typed prompt, unmapped section summary or unsupported scene aggregate refuses. |
| Selection and section/document translation | Selection checked scoped Save; broader translation retains prompt-14 typed complete structure, ownership, language/source checks, atomic durable Apply and recovery. The common checked request adds writing/planning/canon fingerprint checks; metadata-only acknowledgements compare unchanged content. | Existing translation handler/server/recovery suites remain in the unfiltered run, plus universal source tests. Legacy unchecked proposals cannot Apply. |
| Page/selection quality and generated fixes | Save before checked issue run; bounded issue response and target echo; retain a lease per report; recheck before fix generation/review/Apply, then confirmed exact-range rich Save. | `WebCheckedQualityTests` exercises actual quality handlers and services, late source/account changes and invalid targets. Cached legacy reports remain readable but require a new checked run before Apply. Scene-route quality refuses and directs the user to its linked manuscript page. |
| Consistency report and individual revisions | Typed owned current canon plus checked saved planning/writing, source receipt for report and fixes; pinned editor HTML/anchors and confirmed scoped Save. | `WebCheckedConsistencyTests`, consistency/canon/revision suites and shipped editor range checks. Web evidence anchors retain the current-editor-page scope; this does not establish multi-page anchored report parity. |
| Character/place/timeline canon refresh | Shared `DeviceBibleRefreshRequest.WebSource`, owned checked source before extraction and after provider inside commit; typed current canon reads; existing expected canon versions. | `WebCheckedCanonTests` exercises all three real refresh endpoints, concurrent pre/post changes and unchanged metadata acknowledgements. A stale/oversized/unavailable canon refuses coaching with refresh/update guidance. |
| Scene suggest/refine/open questions in editor and storyboard inspector | Save authored card using expected normalized card fingerprint; bind typed coaching v1 and owned entity/canon IDs; verify source/date/local card before review and scoped Apply. Card/source CAS and acknowledgement protect Save; failed saves retain review and authored fields. | `WebCheckedSceneHandlerTests` exercises all three actual editor handlers and Scene/Section card CAS. `WebCheckedSourceServerTests` exercises typed builders and foreign entity rejection. Inspector implementation uses the same contract; native/full signed-in inspector interaction remains an acceptance gate. Authored links/tags/status and unrelated fields are preserved. |
| Synopsis suggest/evaluate/questions | Save before all three dedicated typed modes, complete ten-field/focus snapshot and `WebAiSource`; echo/mode/date/account/current-source checks; explicit field-only Apply with source header and expected saved synopsis CAS. | `WebCheckedSynopsisTests` tests actual Razor modes and stale Apply; dedicated server tests mutate writing/canon during provider execution. Analyses/questions remain inert. Ordinary synopsis Save also uses expected-content CAS. |
| Four storyboard actions and approved scene creation | Server constructs authoritative owned planning context for all four actions. Each retained proposal has its own source/date, account/backend/project/document identity; validation cannot borrow another analysis's fresh receipt. Checked first creation requires source CAS and acknowledgement. | Four actual adapter cases and four actual provider builders in `WebCheckedSourceHandlerTests`/`WebCheckedSourceServerTests`; exact-proposal expiry in `WebCheckedMutationTests`; existing `LocalStoryboardTests`. Interrupted multi-step creation still requires inspecting saved state before retry. |
| AI history Undo/Redo and old context-drawer actions | Checked web page Undo/Redo validates current source, exact stored expected HTML and target before scoped persistence; ordinary editor Undo remains available. Removed unused raw unchecked drawer requests expose manuscript-editor guidance. | Existing history regressions plus checked page mutation/ack tests. Scene AI-history cloud replay is explicitly unavailable; use ordinary editor Undo or retained original-copy recovery. General web applied-event delivery remains one-shot and is prompt 20. |

Supported implementation and available automated checks close **P19-001**; limits in this table remain visible rather than using unchecked or flattening fallbacks. Response bytes, source entity/content counts, provider context and proposal lifetime are bounded as documented in [development setup](device-development.md#desktop-ai-prompt-19--checked-production-web-ai-2026-10-04). No Device.Shared persistence/lifecycle code was imported into the web client. No prompt-19 persisted entity or migration was added.

Latest integrated evidence: `artifacts/desktopai-p19/`, **1,773 unfiltered Release passes**, **64 real editor checks**, **142 actual Razor renders** at both requested sizes, **four equivalent Apply/reopen comparisons**, and all five Release targets with zero warnings/errors. The suite adds 103 cases and replaces one redundant old outline-only case (net +102). New review screenshots were inspected; provider markup remains inert text. Static Razor renders and synthetic transport/authentication do not establish full signed-in browser, native, provider semantic or deployed SQL Server acceptance. The owned fixture listener is stopped; Windows candidate path/hash and preservation audit are recorded. See [fresh UAT and reproduction](desktopai-uat.md#prompt-19--checked-production-web-ai-2026-10-04).

## Reproduction and release handoff

Continue in this same checkout: its uncommitted changes include prompts 1–21. Do not reconstruct the state from HEAD. Prompt 21 retains all 371 preexisting readable dirty files and all six preexisting deletions; its audit records unchanged/extended hashes and new/previously clean changes separately. No commit, deployment, live migration, external registration, provider call or user document mutation was performed. The commands below retain the historical p13 reproduction profile; use the dated prompt-21 UAT for current fixture/output roots.

Run the full suite with fixture directories prepared, then build hosts **sequentially** to avoid shared intermediate races:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p13'
New-Item -ItemType Directory -Force $evidenceRoot | Out-Null
foreach ($prompt in 2..12) {
    $fixtureRoot = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $fixtureRoot | Out-Null
    [Environment]::SetEnvironmentVariable('WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE', $fixtureRoot)
}
$env:WRITERAPP_P13_EVIDENCE = $evidenceRoot
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p13/ --logger 'trx;LogFileName=final.trx' --results-directory artifacts/desktopai-p13
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p13/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p13/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p13/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p13/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:RuntimeIdentifier=iossimulator-x64 -p:BaseOutputPath=artifacts/desktopai-p13/
```

No editor source or generated bundle changed in p13. If modifying editor algorithms later, regenerate both tracked bundles with `npm run build` in `WriterApp.Client` before verification. In a separate terminal run `node tests/serve-device-editor.mjs` from `WriterApp.Client` (port 5179, allowlisted fixtures/assets only). From the repository root run:

```powershell
# Playwright must be installed/resolvable; optionally use the absolute bundled module path.
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node WriterApp.Client/tests/desktopai-acceptance.mjs
```

The runner is [checked in](../WriterApp.Client/tests/desktopai-acceptance.mjs). `PLAYWRIGHT_MODULE` is a local runtime override; use `playwright` or the available installation on another machine. `DESKTOPAI_EDITOR_HOST` can select another bounded local fixture port. Stop only the fixture server you started. The server from this session was stopped and its port probed afterward.

Before a native run inspect `Get-Process -Name WriterApp.Desktop` and its `Path`; no such running process was found in the p13 pre-build snapshot. Existing `bin/Debug`, `bin/Release`, published and validation executables are older outputs and were not replaced. The new unlaunched candidate is:

`WriterApp.Desktop/artifacts/desktopai-p13/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`

For actual native acceptance, build a separate **Development** candidate with an authorized local/test backend, absolute isolated storage and native authentication configured per [device-development.md](device-development.md). For example, after preparing that backend:

```powershell
$nativeData = Join-Path (Get-Location) 'artifacts/desktopai-native/data'
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release -p:BaseOutputPath=artifacts/desktopai-native/ -p:ProsaEnvironment=Development -p:ProsaApiBaseUrl=http://localhost:5387/ -p:ProsaValidationDataDirectory="$nativeData"
```

Confirm the resulting executable, environment/backend and data directory before launching; do not use the production candidate to validate isolated data. Do not replace authentication registrations or live data to satisfy this gate.

Live prerequisites: authorized owned non-demo Unicode project with at least two sections/three pages, supported rich formatting, authored cards/synopsis and extracted bibles; second isolated owner/account; enabled writing/history/image plans and available quotas, plus a denied-plan/exhausted-quota account. Configure the real provider **on the server** using the existing provider/authentication configuration. Confirm `/api/ai/status` advertises the required checked-source/structured writing/translation/canon/synopsis/cover/history capabilities and device sync supports the current version-4 project envelope. Missing capability must remain an actionable refusal.

Matching backend deployment must include the additive preset/history migrations for its configured database: SQLite `20261003125036_ReusablePresetTransfers` and `20261003145238_DeviceAiHistoryReporting`, or SQL Server `20261003125046_ReusablePresetTransfersSqlServer` and `20261003145251_DeviceAiHistoryReportingSqlServer`. Use the repository's configured-context migration/deployment process; these migrations were not applied to an external database here.

Execute the seven flows above on web/native equivalent copies. Export persisted before/after content and IDs, metadata/canon versions, history receipts and cover bytes; compare those values after reload/relaunch, not Saved labels. Test Apply/Dismiss, Undo/Redo, stale proposal, conflict, real account switch, offline transitions, quota denial and cancellation. Capture comparable web/native screenshots at both sizes, keyboard selection and readable results. Human-review actual prose, canon and translation meaning; record provider/model and actual observed timing only when measured. Validate normal web persistence outside demo mode. Prompt 14 enables P13-003 only for new checked proposals; old plain-text proposals remain disabled. Its backend also requires SQLite `20261003183221_WebTranslationOperations` or SQL Server `20261003183237_WebTranslationOperationsSqlServer`. These were not applied to an external database.

Next concrete implementation prompt:

Executable continuation prompts **22–23**, with dependencies and the original common requirements, are saved in [desktopai-prompts.md](desktopai-prompts.md#remaining-gaps-after-prompt-13). Prompts 14–21 close their supported implementations and available automated checks. Next execute **prompt 22** for available Windows/web/provider/database/physical export acceptance; prompt 23 covers native iOS/package acceptance when its authorized environment is available.

Prompt 16 additionally requires SQLite `20261004072914_OwnedCoverAssets` or SQL Server `20261004072915_OwnedCoverAssetsSqlServer` and explicitly configured `CoverAssets:TrustedStoragePrefixes`. The empty default disables remote fetch. No external migrations/configuration were changed. Keep immutable server asset identities/bytes and scoped local recovery evidence while retry/recovery is offered; no pruning was introduced. See [development setup](device-development.md#desktop-ai-prompt-16--owned-remote-cover-assets-2026-10-04) and [fresh UAT](desktopai-uat.md#prompt-16--owned-remote-cover-materialization-2026-10-04).

> Execute prompt 22 in docs/desktopai-prompts.md, including the common requirements. Continue in this same dirty checkout and preserve prompts 14–21, including iOS callback/Keychain/scoped-selection and shared account invalidation guards. Exercise available Windows/web/provider/database/physical export gates with owned isolated hosts/data; repair scoped regressions and append current evidence. Leave unavailable credentials/native/provider/database/physical checks explicitly open. Do not change external authentication registrations or deploy without separate authorization.


Prompt 17 latest evidence (2026-10-04): **1,615 unfiltered Release passes**, 62 editor checks, 116 renders, four equivalent Apply/reopen comparisons and all five clean builds. Evidence is in `artifacts/desktopai-p17/`; see the dated UAT entry. Bounds and missing-capability guidance are documented in device-development.md. No live semantic equivalence or native acceptance is inferred.

Historical prompt-18 evidence (2026-10-04), `artifacts/desktopai-p18/`: **1,671 unfiltered Release passes**, 62 real editor checks, 138 actual Razor renders, four equivalent Apply/reopen comparisons and all five clean builds. The p18 audit retains 284 preexisting readable dirty files (263 unchanged, 21 extended, zero missing) and all six prior deletions. Earlier counts are their dated closure evidence; prompt 19 above is current. P18-001 closes implementation/available automated checks for supported configurations; live image semantics remain open. New proposal/Save receipt tables require the exact matching migrations listed in [development setup](device-development.md#desktop-ai-prompt-18--owned-cover-variations-and-adjustments-2026-10-04); none was applied externally. See [prompt-18 UAT and reproduction](desktopai-uat.md#prompt-18--real-cover-variations-and-adjustments-2026-10-04).
## Prompt 20 — durable web history contracts and evidence (2026-10-04)

This section supersedes historical prompt-19 one-shot reporting and scene-content replay limitations. It closes the functional reporting facet of P13-G04 without closing its deployed/full browser/publishing acceptance gate.

| Actual path | Durable evidence and supported outcome | Verification |
|---|---|---|
| Selection/custom writing, continuation, translation, generated quality and consistency revisions | PageEditor stores immutable approval before checked Page/SceneContent Save; content and committed receipt persist atomically; reconciliation reports only committed outcomes. | `WebAiHistoryEditorTests` exercises actual PageEditor Save, offline/storage failures and lost save/report acknowledgements. Existing actual revision handler suites remain in the full run. |
| Page/SceneContent cloud Undo/Redo | Read-only transition proposal, exact expected HTML/current source, stable ordered intent before content persistence; report follows commit, predecessor first. | `WebAiHistoryOutboxTests` ordered Undo/Redo and `WebAiHistoryBoundaryTests` scene identity/later edit/refused replay/SQLite concurrency. |
| Scene/section-card Apply | Same durable pre-save approval and checked commit receipt, typed planning original/request/response, authored field preservation. No manuscript replay. | Extended actual scene-handler assertions confirm reported planning receipts and absence of a manuscript Undo transition. |
| Complete section/document writing/translation | Durable reporting intent references the exact prompt-14 owned aggregate operation; committed receipt proves outcome even after later edits. Explicit original-copy recovery remains. | Aggregate uncommitted/lost-ACK/later-edit cases; existing multi-page atomic persistence/restart/recovery suite. |
| History view and retry | Shared inert delivery component shows Apply intent or saved outcome separately from pending/rejected/confirmed delivery; account-scoped reload/manual/timer reconciliation never reapplies drafts. | Three actual Razor state fixtures at both viewports, typed cached-receipt validation, auth/backend/cancellation/deletion/plan rejection tests and two real browser tabs. |

Fresh integrated evidence: **1,814 Release tests passed**, 41 added prompt-20 cases; **70 real shipped-editor checks, 148 actual Razor fixture renders, four equivalent device/client Apply/reopen comparisons**, real two-tab IndexedDB preservation, and all five clean Release builds. Tests use synthetic provider/authentication transport with normal owned SQLite controllers; browser checks use actual IndexedDB and shipped editors/static compiled Razor fixtures, not a complete authenticated shell. Neither constitutes live provider/native acceptance. Exact logs, schema upgrade checks, reproduction and preservation audit are in [prompt-20 UAT](desktopai-uat.md#prompt-20--durable-web-applied-history-2026-10-04) and `artifacts/desktopai-p20/`.

Matching deployment requires the SQLite/SQL Server WebAiHistoryOperations migration and additive receipt routes documented in [development setup](device-development.md#desktop-ai-prompt-20--durable-browser-applied-history-2026-10-04). No external migration occurred. Unavailable authorization/deleted targets retain unresolved browser evidence; unsupported planning/aggregate replay remains an explicit limitation. Proceed to prompt 21 in this same checkout.

## Prompt 21 — native iOS identity implementation (2026-10-04)

[IosDeviceIdentityClient](../WriterApp.iOS/Authentication/IosDeviceIdentityClient.cs) implements the actual `IDeviceIdentityClient` registration, backed by [IosMsalSession](../WriterApp.iOS/Authentication/IosMsalSession.cs), native iOS Keychain and main-thread system authentication. [IosIdentityLifecycle](../WriterApp.iOS/Authentication/IosIdentityLifecycle.cs) handles creation/resume/background/destruction without duplicating an active sign-in. Callback routing, bundle/redirect/entitlement consistency, secure environment/backend-selected account, cancellation, silent renewal, tenant/account/expiry checks and sign-out are implemented. Shared account generation immediately invalidates old approvals and pending acquisitions; ordinary writing/recovery remains available. The UI no longer identifies every native host as desktop.

`IosIdentityTests` adds **38** platform-seam/configuration/lifecycle/actual Account-menu and existing production AI approval-guard cases. Fresh integrated evidence is **1,852 Release passes, 70 real editor checks, 156 actual Razor renders, four equivalent Apply/reopen comparisons, real two-tab browser outbox**, and all five clean builds. Production iOS-specific SDK APIs compile on Windows; native Keychain/system presentation/OAuth callbacks are not executed by these tests. No editor algorithm/assets, database schema, deployed backend allowlist or external app registration changed.

The tracked iOS ClientId is empty until a separately authorized public-client registration with the fixed iOS redirect, API scope/consent and deployed `NativeAuth:AllowedClientIds` entry is supplied. This keeps default guest/offline operation usable and avoids treating the Windows registration as authorized iOS configuration. See [development prerequisites](device-development.md#desktop-ai-prompt-21--native-ios-identity-2026-10-04) and [dated UAT](desktopai-uat.md#prompt-21--native-ios-identity-2026-10-04). **P21-001's implementation/available checks are closed; P13-G03 native/configured/device/package acceptance stays open.** Prompt 22 is the next same-checkout action.

## Prompts 22–23 — current local and native gates (2026-10-04)

Fresh current evidence is `artifacts/desktopai-p22/` and `artifacts/desktopai-p23/`: **1,863 unfiltered Release passes**, including real local SQL execution, **71 shipped-editor checks, 156 actual Razor renders, four equivalent editor Apply/reopen comparisons**, two-tab IndexedDB and three save-lifecycle JS checks; **all five sequential Release builds pass with zero warnings/errors**. The actual complete LocalDev WASM shell passes **16** checks with a mock-text provider. Both disk-saved DOCX and EPUB open as valid archives and retain complete writing/Unicode plus exact source cover bytes and project cover metadata. Independent reader rendering remains untested.

| Scoped regression | Repair | Fresh verification |
|---|---|---|
| P22-001: normal multi-page section opened under one first-page ID | Preserve real page list; keyboard page picker saves before switching, retains failed drafts; preview alone joins all pages; version restore updates matching ID. | Actual helper/save-refusal tests and complete web shell navigation/Apply/Dismiss/Undo/Redo/offline/reload retain all IDs and unrelated pages. |
| P22-002: actual WASM render lock on input/window blur | Defer/coalesce lifecycle interop, handle only actual window blur, cancel replaced registrations; settle unchanged saved coordinator state. | Three JS lifecycle checks and actual full-shell run without page errors. |
| P22-003: destroyed TipTap view accessed by queued pagination/resize | Disable pagination interop, clear timers and cancel queued/debounced observer work on destroy; regenerate bundles. | Real shipped-editor destroy regression plus actual repeated page switching/reload without page errors. |
| P22-004: fresh SQL migration chain referenced missing cover/scene fields | Discover missing cover migration with existing-column guard; conditional scene-card column migration. | Two fresh/existing-column local SQLEXPRESS upgrades, complete mapped-column audit, retained writing/cover/sync version and concurrent receipt replay tests pass. Deployed migration remains open. |
| P22-005: SQLite legacy scene routing threw on DateTimeOffset SQL ordering | Filter owned matching nodes in SQL, compare UTC instants in memory. | Actual MVC mixed-offset/newest/anonymous/foreign/missing target regression passes; existing linked-section redirect preserved. |
| P22-006: async panel teardown could retain a local store lock | Async disposal drains already-started document refresh reads. | Former full-suite teardown failure repaired; current unfiltered 1,863-case suite passes. |

| Host / release facet | Available result | Independent remaining gate |
|---|---|---|
| Original seven flows + translations, onboarding/demo, covers/adjustments, outline and history | Actual shared/device/web handlers, local stores, synthetic authorized transport/provider and shipped editors pass the complete suite and real Razor fixtures. The full-shell browser run directly exercises writing/history/offline/export. | The other journeys have no complete native/deployed live-provider acceptance; consult the per-flow profile rather than extrapolating the writing run. |
| P13-G01 Windows UI | Rebuilt candidate path/hash inspected; no desktop process was running or launched. | Actual native control/screenshots, keyboard/selection/lifecycle and close/relaunch on isolated data. |
| P13-G02 live provider | No authorized provider/account environment used. | Approved non-demo accounts, real model/configuration and human prose/translation/canon/image review. No latency inferred. |
| P13-G04 local database/browser/exports | SQLite + disposable SQLEXPRESS execution, normal LocalDev full shell, history reconnect/reload retry, physical disk files and exact inline cover bytes pass. | Deployed migrations/OIDC, trusted remote/provider covers, independent readers/native file dialogs. |
| P13-G03 Windows package | Test-signed Development x64 MSIX `0.1.0.1`: 247 block-map files/CMS signature, actual manifest/packaged assembly/environment/backend/isolated data path verified. | Temporary signer is untrusted (signtool exit 1); no package installed/launched. Approved stable trusted channel, clean install and higher same-family upgrade required. |
| P13-G03 iOS simulator | Real native adapter source/configuration tests and Windows managed iOS compilation pass. | Authorized iOS registration, compatible Mac/Xcode and actual installed/launched simulator bundle with native Keychain/callback/UI checks. |
| P13-G03 physical iOS | No device/signing environment exercised. | Authorized Apple team/profile/device and native lifecycle/touch/accessibility/upgrade acceptance. |
| Distribution | No release/update feed published, no trust or external registrations changed. | Stable signing/version/feed/channel and relevant prompt-22 live acceptance. |

Prompt-23 production-store integration additionally verifies representative document envelopes 1–3 and guide v1 migration through interrupted retry, exact backups, unchanged writing/IDs, pending preset/history events, account/backend-scoped canon/covers and approved recovery. This closes available storage checks independently of installed-package/iOS data preservation.

**Available prompt-22/23 work is complete; full parity and release acceptance are not approved.** Continue in this same dirty checkout with the precise open native/provider gates in [desktopai-release-checklist.md](desktopai-release-checklist.md). Earlier prompt counts and next-prompt sentences remain dated historical evidence, not current acceptance.
