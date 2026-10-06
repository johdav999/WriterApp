# Desktop AI implementation and acceptance evidence

This is the append-only evidence and handoff for [desktop AI prompts](desktopai-prompts.md). Implementation, automated verification and live acceptance are separate statuses. Append subsequent prompt results; do not replace this baseline with a later success claim.

## Prompt 1 — Current baseline, 2026-10-03

**Status: baseline task complete; native/authenticated/provider acceptance remains open.** No new AI features were implemented in this prompt. The existing desktop AI paths compiled and the exercised automated checks passed. The next implementation task is prompt 2, durable character/place/timeline bibles.

Source baseline: commit `6dafbbfa65414f83b38414cdda0d26009fc3536b` plus the existing uncommitted working tree. In particular, consistency suggestion application and recent storyboard/shared presentation work were already modified or untracked before this run. They were included in verification and preserved. SHA-256 comparison confirmed all 19 initially modified tracked files remained unchanged (`artifacts/desktopai-p01/preservation-check.json`); no application/test source was edited. This report describes this checkout, not an installed release.

### Feature matrix

**Implemented** means the named workflow is connected in source at its stated scope. **Partial** means a working desktop path exists but has less scope than the client. **Missing** means no equivalent reachable desktop workflow. Automated evidence below verifies particular boundaries, not the entire user journey. **Open** in the live column means native, authenticated backend and provider acceptance was not performed in this run.

| Feature | Client reference / desktop implementation and gap | Implementation | Automated evidence this run | Live acceptance |
|---|---|---|---|---|
| Rewrite | Client has multiple tone/length presets; desktop sends Neutral/Same/preserve-terms and replaces the captured selection. W1/D1. | Partial | DeviceAiActionTests; real editor selection Apply/undo and inert-output checks. | Open |
| Expand / shorten | Client supports selection/section commands; desktop has selection-only dedicated actions. Section custom requests do not establish those workflows. W1/D1. | Partial | DeviceAiActionTests request mappings and section-context checks; ReviseActionsTests. | Open |
| Change tone / Show, don't tell | Client has dedicated presets/commands; desktop custom instructions are a workaround. W1/D1. | Missing dedicated workflows | Existing backend rewrite/revise tests pass; no desktop workflow to accept. | Open; implementation first |
| Next paragraph | Client reaches `propose.next-paragraph` and appends a normalized continuation; desktop has no dedicated command. W1/D1. | Missing | Existing action/orchestrator tests do not prove a desktop continuation workflow. | Open; implementation first |
| Custom instructions | Desktop editor replaces selected text, or reads a complete section and appends the result to the current page. Saved-library execution is separately analysis-only. W1/D1/D2. | Implemented for editor instructions | DeviceAiActionTests, CustomTransformActionTests and real editor append/stale/restore checks. | Open |
| Selection translation | Desktop language picker sends `translate.selection`, shows a proposal and explicitly applies it. W1/D1. | Implemented | AdvancedDeviceAiTests selection/language/target checks; real editor selection and Apply boundaries. | Open |
| Section / document translation | Client has both scopes and dedicated Apply paths; desktop has selection translation only. W1/D1. | Missing | No desktop multi-page translation implementation exercised. | Open; implementation first |
| Style & quality | Client has individual quality issues, highlights, Jump and targeted fix proposals; desktop offers selected-text or current-page revision through `custom_transform`. W1/D1/Q1. | Partial | DeviceAiActionTests style targets, AdvancedDeviceAiTests rejection of obsolete analysis route, client quality suites; real editor style selection/page Apply/undo/staleness. | Open |
| Consistency Coach | Client supplies character/place/timeline canon and has severity filtering/Jump/fix review. Desktop checks a saved section without bibles, renders findings and supports individual revision review/Apply. W1/D2/B1. | Partial | 12 presentation + 4 local revision tests; continuity/proposal suites; 3 real-editor consistency replacement checks. | Open |
| Character / place / timeline bibles | Client loads and refreshes all three via owned document endpoints; desktop has no bible cache, refresh or view adapter. W1/B1. | Missing | 7 refresh + 7 patch + 1 controller tests pass for backend behavior; no desktop adapter exists. | Open; implementation first |
| Scene-card suggestion / refinement | Client offers whole-card and individual-field coaching, including links/tags/status. Desktop editor suggests; storyboard also refines. Apply updates summary, purpose, role, intent, emotional beat, key events and open questions, retaining authored links/tags/status. W1/D2/S1. | Partial | AdvancedDeviceAiTests, SceneCardApplyRegressionTests, normalizer/presentation suites and storyboard tests. | Open |
| Synopsis field improvement | Desktop uses all ten local synopsis fields, user notes, `synopsis.story_coach` and explicit field Apply. Requires a project with an active readable scene/page. Client uses its specialized synopsis suggest endpoint. Y1/D2. | Implemented within desktop prerequisites | AdvancedDeviceAiTests typed target and prose/other-field preservation; LocalAiHistoryActionsTests field undo/restart; SynopsisTests. | Open |
| Synopsis evaluation / questions | Client has evaluate/questions endpoints and feedback-only results; desktop exposes field improvement only. Y1/D2. | Missing | SynopsisTests cover existing backend behavior; no desktop modes to accept. | Open; implementation first |
| Four storyboard AI actions | Suggest next scene, detect missing scenes, subplot continuity and POV balance use the shared board/insights and desktop adapter, with explicit suggested-scene creation. S1. | Implemented | 19 LocalStoryboardTests, including parameterized cloud identity/version checks for all four actions and stale suggestion refusal. | Open |
| Reusable prompts | Client supports editable built-in/custom presets, parameters/categories and selection/section execution. Desktop saves new local plain templates, copies/imports eligible account-wide templates and runs library results as analysis. No edit/delete/pinning/full preset adapter or automatic synchronization. P1/D2. | Partial | AdvancedDeviceAiTests copy/account/interruption checks, durable local prompt restart; PromptPresetsControllerTests. | Open |
| AI history / undo / redo | Desktop has local comparisons, target-scoped guarded undo/redo and separate recovery copies. Client lists backend history and records applied events. Desktop does not load cloud history or reconcile/report local applications to it. H1/D1/D2. | Partial | 7 LocalAiHistoryActionsTests plus DeviceAiActionTests durable backup; real editor Apply/undo/redo and guarded restore. | Open |
| Cover concept generation / saving | Client generates concepts through `api/covers/generate` and saves a chosen project cover. Desktop has local publishing-cover selection, without an AI studio. C1. | Missing | AiImageFlowTests cover existing backend action/cover persistence boundaries, not the client cover studio or a desktop journey. | Open; implementation first |
| Guided AI onboarding | Client onboarding state/overlays and an authorized first-AI demo are connected; desktop has no equivalent guided journey. O1. | Missing | Onboarding eligibility/bootstrap/demo/preservation suites pass for existing code; no desktop journey exists. | Open; implementation first |
| Explicit review / Apply / recovery | Desktop writing/planning proposals have explicit review, account/source guards, durable history/backup and recovery. Those boundaries must survive all later prompts. D1/D2/H1. | Implemented for existing targets | Device/advanced AI, local revision/history/session/compatibility suites; real editor stale/inert-output/restore checks. | Open |

Desktop also retains the explicitly non-destructive **Summarize section** action: every readable page contributes to context and the result appends to the active page after approval. There is no equivalent dedicated summary button in the inspected client preset list; do not remove this desktop capability while aligning the other actions.

### Source map and traced paths

- **W1 — Client editing/coaching:** [DocumentEditor markup](../WriterApp.Client/Pages/DocumentEditor.razor), [handlers](../WriterApp.Client/Pages/DocumentEditor.razor.cs). Relevant paths: action presets/catalogue → `OnAiActionSelected`/request → `PostAiActionAsync` → pending proposal → `OnApplyPendingAiProposal`; translation has its own scope and Apply branches. Scene coaching uses `RunSceneAiAsync`/`RunScopedSceneAiAsync` → typed proposal → `ApplySceneAiProposalAsync`. Quality has individual issue/proposal/highlight handlers. The client builds richer outline/scene context than the desktop's generic writing request, whose `OutlineText` is null.
- **D1 — Desktop writing:** [DocumentWorkspace](../WriterApp.Device.Shared/Pages/DocumentWorkspace.razor), [DeviceAiActions](../WriterApp.Device.Shared/Services/DeviceAiActions.cs), [editor source](../WriterApp.Client/src/device-editor.ts). UI → current selection/page capture → local save/sync → `DeviceAiRequests.Build` → `DeviceAiService.ProposeAsync` → comparison → `ApplyAiAsync` → backup → editor command → durable save/history. Style captures only the current selection, falling back to the whole page when empty; it does not reuse an old collapsed selection.
- **D2 — Desktop planning/analysis:** [LocalAiPanel](../WriterApp.Device.Shared/Components/LocalAiPanel.razor), [AdvancedAiRequests](../WriterApp.Device.Shared/Services/AdvancedAiRequests.cs), [LocalConsistencyRevisions](../WriterApp.Device.Shared/Services/LocalConsistencyRevisions.cs), [shared result presentation](../WriterApp.UI.Shared/AiResultPreview.razor), [consistency report](../WriterApp.UI.Shared/ConsistencyReport.razor). Saved/synced document → typed prepared target → request → freshness check → stored reviewed result. Scene/synopsis Apply changes planning only. Consistency review resolves a verified anchor or unique evidence quote, previews a supported HTML revision, rechecks the saved source and persists only the target page with undo evidence.
- **S1 — Storyboard:** [shared insights](../WriterApp.UI.Shared/Projects/StoryboardInsights.razor), [board](../WriterApp.UI.Shared/Projects/StoryboardBoard.razor), [desktop adapter](../WriterApp.Device.Shared/Services/LocalStoryboardData.cs), [desktop page](../WriterApp.Device.Shared/Pages/Storyboard.razor), [client adapter](../WriterApp.Client/Services/HttpStoryboardData.cs). Both hosts use the shared action UI. Desktop maps cloud request identities and guards source changes and suggested-scene creation. Its summary/role/structure coaching calls the existing scene coach, with structure refinement using `scene.refine`.
- **B1 — Canon:** [DocumentBiblesController](../Controllers/DocumentBiblesController.cs), [BibleRefreshService](../Application/Continuity/BibleRefreshService.cs), [models](../Application/Continuity/BibleModels.cs), [store contract](../Application/Continuity/IBibleStore.cs), [continuity actions](../AI/Actions/ContinuityActions.cs). Client `LoadBibleSnapshotsAsync` reads all three; `OnUpdateAllBiblesAsync` sequentially refreshes them. Section checks include `character_bible_json`, `place_bible_json`, `timeline_bible_json`. Desktop currently supplies none of these.
- **Q1 — Quality:** [page controller](../Controllers/PageQualityChecksController.cs), [scene controller](../Controllers/SceneQualityChecksController.cs), [analysis implementation](../Application/Documents/Quality), [capabilities](../WriterApp.Shared/QualityIssueCapabilities.cs), [fix helpers](../WriterApp.Shared/QualityFixClientHelpers.cs). Distinguish deterministic issue analysis from AI generation of a targeted rewrite; a generic desktop style revision does not transfer these capabilities.
- **Y1 — Synopsis:** [client page](../WriterApp.Client/Pages/Synopsis.razor), [controller](../Controllers/DocumentSynopsisController.cs), [context builder](../Application/Synopsis/SynopsisAiContextBuilder.cs), [desktop picker](../WriterApp.Device.Shared/Pages/Synopsis.razor), [planning panel](../WriterApp.Device.Shared/Components/LocalPlanningPanel.razor). Client modes are evaluate/questions/suggest; desktop redirects to the Story synopsis panel and uses its generic revision-checked action adapter for field coaching.
- **P1 — Prompts:** [client handlers](../WriterApp.Client/Pages/DocumentEditor.razor.cs), [controller](../Controllers/AiPresetsController.cs), [device transfer](../WriterApp.Device.Shared/Services/DevicePromptLibrary.cs), [local store](../WriterApp.Device.Shared/Storage/LocalAiStore.cs). Transfer accepts only account-wide `custom` templates with no extra parameters and local size limits. Upload creates a new copy; interrupted creates are not blindly retried.
- **H1 — History:** [backend actions/history](../Controllers/AiActionsController.cs), [local actions](../WriterApp.Device.Shared/Services/LocalAiHistoryActions.cs), [local store](../WriterApp.Device.Shared/Storage/LocalAiStore.cs), [durable writing backup](../WriterApp.Device.Shared/Storage/DeviceAiUndoStore.cs), [shared panel](../WriterApp.UI.Shared/AiHistoryPanel.razor). Local undo/redo checks affected values and preserves unrelated later edits; recovery creates detached copies. Local history IDs are distinct from server proposal IDs and need reconciliation in prompt 10.
- **C1 — Covers:** [studio](../WriterApp.Client/Components/Covers/ProjectCoverStudio.razor), [API client](../WriterApp.Client/Services/CoverApiClient.cs), [controller](../Controllers/CoversController.cs). Concept generation calls the service; save calls the project-cover endpoint. `GenerateVariationsAsync`, darker/brighter/cinematic/minimal only call `SetPendingActionAsync`. `SurpriseMeAsync` rotates form values and updates UI state. These are client limitations, not accepted AI generation features.
- **O1 — Onboarding:** [client onboarding](../WriterApp.Client/Pages/Onboarding.razor), [service](../WriterApp.Client/Services/OnboardingService.cs), [state](../WriterApp.Client/State/OnboardingStateStore.cs), editor onboarding/demo handlers, [controller](../Controllers/OnboardingController.cs), [eligibility](../Application/AI/OnboardingDemoEligibilityService.cs). Preserve the existing server demo eligibility and authored-content preservation rules in prompt 12.

No reachable client/shared presentation use of `GenerateOutline`/`generate-outline` was found in the inspected source. Backend outline actions/tests exist; that is not a missing desktop workflow relative to this client benchmark.

### Contracts and reusable seams for later prompts

1. **Backend AI capability:** [Program status endpoint](../Program.cs) exposes `/api/ai/status`, including `SupportsDocumentVersionChecks = true` in this source. [Shared DTOs](../WriterApp.Shared/AiActionDtos.cs) carry `ExpectedDocumentVersion`, returned `SourceDocumentVersion`, proposal identity, typed scene card and text operations. `AiActionsController.Execute` checks the owned document's sync version before and after execution and can return `ai.stale_source`. This is source evidence, not confirmation that a deployed backend has this contract.
2. **Device request gate:** `DeviceAiService` requires connectivity/sign-in, enabled UI/AI, remaining quota and version-check support. It has a 90-second linked timeout, cancellation and late account-change checks, action/proposal/output validation and a 100,000-character output bound. Apply checks account generation and 30-minute proposal expiry. Keep these production boundaries when adding adapters.
3. **Identity and local persistence:** [LocalDocument](../WriterApp.Device.Shared/Storage/LocalDocument.cs) separates local document/section/page IDs from server counterparts; [DeviceSyncMapping](../WriterApp.Device.Shared/Services/DeviceSyncMapping.cs) uses server IDs in uploads and retains local IDs on download. [LocalDocumentCodec](../WriterApp.Device.Shared/Storage/LocalDocumentCodec.cs) currently writes **schema 4** and reads recognized older envelopes. Do not use the historical schema-1/3 paragraphs as current migration instructions.
4. **Freshness:** `AdvancedAiRequests.RequireFresh` compares writing/planning and cloud identity/version while tolerating local revision/timestamp-only sync acknowledgements. Local consistency approval uses its saved source and refuses ambiguous targets or changed payloads. Do not regress these into strict timestamp-only checks or permissive whole-document replacement.
5. **Presentation reuse:** `AiTextComparison`, `AiResultPreview`, `AiHistoryPanel`, `ConsistencyReport`, `SynopsisFields`, board/insights and existing coach/navigation components are available in `WriterApp.UI.Shared`. Keep request/persistence orchestration outside that library. Existing DI registration in [DeviceServiceCollectionExtensions](../WriterApp.Device.Shared/Services/DeviceServiceCollectionExtensions.cs) wires scoped account/AI/transfer services and durable local stores.
6. **Tests and browser seams:** `IDeviceAiApi` supports synthetic usage/response/failure/cancellation tests; request builders and typed Apply are directly testable. Local repositories/stores use isolated test directories. `HtmlRenderer` exercises shared result components. The shipped-editor harness imports both real bundles and covers capture, inert provider text, stale Apply, undo, compatibility and consistency replacements. Extend these seams when a later prompt adds behavior rather than creating a parallel fake editor.
7. **Canon contract gap for prompt 2:** `RefreshBibleRequest` currently carries only `FullRebuild` and `ActiveSectionId`; `BibleSnapshotDto` includes content/stats/time, but no document identity, source document version or snapshot concurrency token. The service tracks section hashes/source hash internally and validates/patches before saving. There is no exposed manual bible-edit endpoint in this controller. Add an explicit, compatible identity/version guarantee for the device; timestamps or ChangedSections counts alone are insufficient to prove that a long-running refresh analyzed the intended revision. Reinspect storage/concurrency before designing retry or automatic sync.
8. **Other future contract gaps:** specialized synopsis requests lack the generic expected-version field; preset DTOs have timestamps but no exposed version/idempotent-create contract; history returns proposal/text/application status rather than a complete device undo target; local history records do not retain the server proposal ID. Cover generation's prompt is separate from project-cover saving. These need scoped contract work in their respective prompts; do not reuse the generic AI guard as proof that every specialized endpoint is guarded.
9. **Content boundary:** [compatibility contract](editor-content-compatibility.md) is version 2 and currently supports tables, images and several advanced marks/attributes. Unknown rich attributes remain protected. Do not repeat the older claim that all tables/images are unsupported, and do not equate a whole-range plain-text AI rewrite with preservation of every mark inside the replaced range.

### Automated checks actually run

Evidence directory: `artifacts/desktopai-p01/` in this checkout. Build output was isolated beneath each project's `artifacts/desktopai-p01/`; no desktop binary was launched and no provider request was sent.

| Check | Result | Evidence |
|---|---|---|
| Focused AI/scene/history/consistency/quality/synopsis/onboarding tests | 342 passed, 0 failed, 0 skipped | `focused-tests.log`, `results/focused.trx`, `results/focused-suites.json` |
| Additional device content/session boundaries | 35 passed, 0 failed, 0 skipped | `editor-boundaries.log`, `results/editor-boundaries.trx`, `results/editor-boundaries-suites.json` |
| Distinct .NET checks | 372 distinct tests; 377 executions because 5 checks matched both filters | `test-summary.json` |
| Real shipped-editor harness in headless Microsoft Edge, 1280×720 viewport | 34 passed, 0 failed, no page errors | `browser-check.log`, `browser-results.json`, `browser-results.txt`, `editor-harness-1280.png` |
| Release client build and referenced Shared/UI.Shared | Passed, 0 warnings, 0 errors | `client-build.log` |
| Release Windows build and referenced Shared/Publishing/UI.Shared/Device.Shared | Passed, 0 warnings, 0 errors | `windows-build.log` |
| Server/shared/device/client compilation through focused test build | Passed; two nonblocking warning categories below | `focused-tests.log` |

Relevant focused suite counts include DeviceAiActionTests 23, AdvancedDeviceAiTests 19, AiActionsControllerTests 14, LocalAiHistoryActionsTests 7, LocalConsistencyRevisionTests 4, ConsistencyReportPresentationTests 12, LocalStoryboardTests 19, SceneCardApplyRegressionTests 4, SceneCardProposalPresentationTests 9, StyleQualityWorkflowTests 2, SynopsisTests 3, BibleRefreshServiceTests 7 and BiblePatchApplierTests 7. The filter also selected incidental method names such as `Fails`; the TRX files, rather than the label "focused", define the exact executed set.

The first test compilation reported `NU1900` because the NuGet vulnerability service index was unreachable, and `xUnit2031` in the pre-existing modified `LocalStoryboardTests.cs:75` (filtering before `Assert.Single`). Neither caused a build/test failure; the vulnerability warning means this run did not obtain a fresh advisory check. No test or application source was changed to suppress warnings.

Commands, from the repository root:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p01/ --filter 'FullyQualifiedName~Ai|FullyQualifiedName~AI|FullyQualifiedName~Scene|FullyQualifiedName~Synopsis|FullyQualifiedName~Continuity|FullyQualifiedName~Bible|FullyQualifiedName~Quality|FullyQualifiedName~Consistency|FullyQualifiedName~LocalStoryboard|FullyQualifiedName~DeviceEditor|FullyQualifiedName~EditorContent|FullyQualifiedName~PromptPreset|FullyQualifiedName~Onboarding' --logger 'trx;LogFileName=focused.trx' --results-directory artifacts/desktopai-p01/results

dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-build --no-restore -p:BaseOutputPath=artifacts/desktopai-p01/ --filter 'FullyQualifiedName~DeviceContentCompatibility|FullyQualifiedName~LocalEditorSession' --logger 'trx;LogFileName=editor-boundaries.trx' --results-directory artifacts/desktopai-p01/results

dotnet build WriterApp.Client/WriterApp.Client.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p01/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p01/

# In one terminal; serves only the harness and its allowlisted assets.
$env:PORT='5193'
node WriterApp.Client/tests/serve-device-editor.mjs

# In another terminal; uses the installed bundled Playwright and Edge.
node artifacts/desktopai-p01/verify-editor.mjs
```

The editor assets were tested as shipped in the working tree. No editor source changed in this prompt, so no asset rebuild was needed. This is a browser test of the real JavaScript/editor boundaries, not a test of the native Razor-to-WebView workflow or normal authenticated web persistence. The full test suite, iOS managed compilation and paired product screenshots remain prompt 13 work.

### Native launch and environment boundary

The initial CIM process-path query returned Access denied. A fallback `Get-Process -Name WriterApp.Desktop` returned no process; no running desktop executable/path was observable through that probe. This does not establish which shortcut or package the user normally launches.

The newly built candidate executable exists at `WriterApp.Desktop/artifacts/desktopai-p01/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. It was **not launched**. The Release build defaults to Production and `https://app.prosa-app.com/`; no live backend access was made. Source `WriterApp.Desktop.csproj` sets the Debug default to Development and `http://localhost:5387/`, with the existing Debug-only runtime API override. Development-only `ProsaValidationDataDirectory` must be an absolute path to isolate native acceptance storage.

No claim is made that the installed/native application loaded current code. Before native verification, identify the actual executable, rebuild that output or explicitly launch an isolated Development candidate, and inspect persisted synthetic writing after reopen/relaunch. Avoid using the user's ordinary document directory as test data.

### Regressions, corrections and remaining gates

- No failing test or new data-loss regression was reproduced in the exercised baseline checks. This is limited to the executed service/component/browser boundaries.
- Corrected two stale current-setup statements in `docs/device-development.md`: Debug backend URL and the local schema version. Added links to this AI baseline and its prompt sequence. Historical dated evidence remains unchanged.
- **Client limitations:** cover variation/adjustment controls do not generate media; no reachable client outline-generation workflow was found. Do not turn either into a false desktop parity requirement.
- **Implementation gaps:** the matrix's Missing/Partial entries remain feature work. In particular, the canon identity/version contract and durable device cache are prompt 2, not completed by this baseline.
- **Live gate L1 — Native host:** isolated test data, correct executable/output, WebView interaction, keyboard, viewport/focus and restart acceptance. Prerequisite: a controllable native host and confirmed launch path. Browser harness evidence does not close this gate.
- **Live gate L2 — Identity/sync/backend compatibility:** registered native identity and an authorized test account/backend with v2 sync and revision-checked AI. Verify sign-in, enabled sync, source version, account switching, conflict/offline transitions and normal web save/reload with non-demo documents. No credential readiness was inferred or changed here.
- **Live gate L3 — Provider:** configured text/image provider and authorized test budget. Rehearse plan/quota, timeout/cancel, real proposal review/Apply/undo/restart/sync and actual translation/coaching quality. No provider calls were made for this baseline.
- **Later integrated gate L4:** complete relevant suites, iOS managed regression build, paired native/client product screenshots and cross-host journeys in prompt 13. Package/device/distribution acceptance remains separate from compilation.

### Concrete handoff to prompt 2

Continue in this same checkout; the starting uncommitted consistency/storyboard work is needed for this baseline. Start with `Implement prompt 2 in docs/desktopai-prompts.md, including the common requirements.`

1. Read this source map and the current controller/service/store/model code; establish the additive owned-document and snapshot/source-version contract before implementing a cache that claims freshness. Keep full rebuild versus incremental section-hash refresh and invalid-payload preservation.
2. Decide the narrow durable storage boundary for bible snapshots and refresh state, keyed by account/backend plus server document/type, with explicit local document mapping. Reuse the atomic storage and DI patterns; do not introduce unrestricted manual canon editing when the reference workflow only refreshes canon.
3. Implement all three read/initial-build/update flows and offline cached viewing, then add focused identity/revision/cancellation/invalid-output/restart tests. Existing backend test seams are available but do not yet certify device or concurrent refresh behavior.
4. Expose typed canon context suitable for prompt 3 while retaining the existing desktop section-only consistency explanation until those snapshots are actually included. Do not delete or replace current consistency review/Apply, page style revisions, scene proposal preservation or the four storyboard actions.
5. Append prompt 2 evidence and precise remaining live gates here. Source readiness permits prompt 2 implementation; this report does not approve a deployed/native release.

## Prompt 2 — Durable character, place and timeline bibles (2026-10-03)

Implementation and available automated checks are complete. Native signed-in WebView, deployed SQL Server/identity/sync, and live provider acceptance remain separate gates. Existing unrelated uncommitted work remains in this checkout.

### Implemented behavior and reference scope

| Capability | Result | Evidence boundary |
|---|---|---|
| Character/place/timeline extraction, saved read and stale update | Implemented for all three types using the existing bible extraction/refresh policies | SQLite controller tests and device HTTP service tests |
| Update all, individual update and explicit full rebuild | Implemented in Writing → Consistency; save/lock/sync first; three independent sequential saves | Actual Razor panel orchestration test with synthetic HTTP/sync |
| Cached offline view and restart | Durable version-1 atomic cache with stable native account/backend scope and local/cloud document identity | Fresh service/store instances, offline tests and process-interruption fixture |
| Ownership and concurrency | Additive device revision/snapshot contract plus concurrent legacy-client write protection | Owned SQLite graph, stale source/snapshot checks, late source/canon changes, invalid section/foreign owner |
| Invalid/partial output and interruption | Previous server/cache data retained; pending state does not qualify as current context; retry reads first | Malformed JSON, incomplete entries, invalid references, cancellation, disconnect, account switch and local edit tests |
| Readable safe presentation | Shared nested canon reader; empty/current/stale/interrupted/offline/unavailable disclosures; encoded provider text | HtmlRenderer and static rendered output in headless Edge |
| Manual authored bible editing | No corresponding client edit/save workflow found; desktop implements read/refresh scope | Client DocumentEditor handlers and server route inspection |
| Bible-aware section consistency | Deferred to prompt 3; current section-only disclosure and existing individual suggestion Apply retained | Existing consistency, history and editor regression checks |

Source paths: `WriterApp.Shared/Canon/DeviceBibleContracts.cs`, `Controllers/DeviceBibleEndpoints.cs`, `Application/Continuity/PreparedBibleRefresh.cs`, `BibleRefreshService.cs`, `DocumentBiblesController.cs`, `WriterApp.Device.Shared/Storage/LocalBibleStore.cs`, `Services/DeviceBibleService.cs`, `Components/DeviceBiblePanel.razor`, `WriterApp.UI.Shared/StoryCanonView.razor` and `CanonValue.razor`. The Windows MSAL adapter supplies the home-account ID; normal token renewal preserves cache scope. Signing out/changing account clears visible canon and invalidates in-flight requests. Display names, access tokens and provider credentials are not cache keys or persisted cache fields.

The new API is additive and requires the expected cloud manuscript version for reads and writes, plus the last server snapshot fingerprint for refresh. Provider work is prepared without persisting, then committed after ownership/source/snapshot rechecks inside a short serializable transaction. The transaction is wrapped in EF's execution strategy for configured SQL Server retry support. The old web refresh DTO remains compatible; its commit also rechecks source and snapshot. Removed sections now cause a full extraction rather than a skipped or empty-delta update. Existing timeline fallback/character repair policies remain in place; repaired/fallback output must still pass device structure validation.

Cache freshness is deliberately scoped: source hash matching confirms the checked synchronized manuscript version; cached data cannot certify later cloud changes. Stored canon with unconfirmed/stale source remains readable and excluded from typed current context. Entity reference shapes and duplicate IDs/JSON fields are validated. Unknown target names/IDs produce explicit unresolved-reference warnings; they are never silently attached to authored scene links. Bounds and rollout requirements are described in device-development.md.

### Verification and artifacts

Evidence directory: `artifacts/desktopai-p02/`. All test content/accounts are synthetic. No actual document directory, live provider or deployed backend was used.

| Check | Result | Artifact |
|---|---|---|
| AI/scene/history/consistency/quality/editor plus authentication/story-tab regressions | 426 passed, 0 failed/skipped | `regression.trx`, `regression-test.log` |
| Final focused canon controller/device checks | 34 passed, 0 failed/skipped, including one added deleted-scene check after the broader run | `canon-final.trx`, `canon-final.log` |
| Distinct .NET checks across those two runs | 427 distinct checks (33 overlap); 460 executions | `test-summary.json` |
| Real Razor canon panel | Update all for three types; busy disabled actions; token renewal and unrelated repository notifications; offline cache; sign-out clearing; disconnect preservation | `DeviceBibleTests.RealPanelUpdatesAllAndHandlesLoadingTokenRenewalOfflineErrorAndAccountSwitch` in the TRX files |
| Headless Edge, static real Razor output + shared canon CSS | 7 assertions passed; keyboard expand, three cards, offline disabled actions, entity reference and narrow layout | `canon-browser-results.json`, `canon-browser.log`, `canon-offline-1280.png`, `canon-offline-480.png` |
| Windows Release build, referenced Shared/UI.Shared/Device.Shared | Passed, 0 warnings/errors | `windows-build.log` |
| Client Release build, referenced Shared/UI.Shared | Passed, 0 warnings/errors | `client-build.log` |
| Server/device/shared compilation through final test build | Passed; two pre-existing warning categories | `canon-final.log` |
| Unrelated initial tracked modifications | Hashes preserved; only intentional LocalAiPanel integration and development documentation additions changed within that initial set | `initial-hashes.json`, `preservation-check.json` |
| Diff whitespace check | Passed | `diff-check.log` |

The test build reported `NU1900` because the NuGet advisory service was unreachable, and the pre-existing `xUnit2031` in LocalStoryboardTests.cs. No warning was suppressed in source. The initial panel fixture exposed an asynchronous-render assertion timing issue; final tests drive the actual component event lifecycle and await the resulting state. No failing exercised implementation checks remain.

Commands from the repository root:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Ai|FullyQualifiedName~AI|FullyQualifiedName~Scene|FullyQualifiedName~Synopsis|FullyQualifiedName~Continuity|FullyQualifiedName~Bible|FullyQualifiedName~Quality|FullyQualifiedName~Consistency|FullyQualifiedName~LocalStoryboard|FullyQualifiedName~DeviceEditor|FullyQualifiedName~EditorContent|FullyQualifiedName~PromptPreset|FullyQualifiedName~Onboarding|FullyQualifiedName~DeviceAuthentication|FullyQualifiedName~DeviceStoryTabRegression|FullyQualifiedName~DeviceContentCompatibility|FullyQualifiedName~LocalEditorSession' --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p02

dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~DocumentBiblesControllerTests|FullyQualifiedName~DeviceBibleTests' --logger 'trx;LogFileName=canon-final.trx' --results-directory artifacts/desktopai-p02

dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p02/
dotnet build WriterApp.Client/WriterApp.Client.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p02/

# Optional fixture export for the browser check; uses only synthetic test output.
$env:WRITERAPP_P02_EVIDENCE = (Resolve-Path artifacts/desktopai-p02).Path
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~DeviceBibleTests.RealPanelUpdatesAll'
node artifacts/desktopai-p02/verify-canon.mjs
```

No editor source/asset changed in prompt 2; the existing editor regression checks passed, so no editor asset regeneration was needed. Browser screenshots show a standalone rendered canon fixture, not the running MAUI app or normal authenticated web editor. The Windows build candidate is `WriterApp.Desktop/artifacts/desktopai-p02/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; no WriterApp/Prosa process was returned by the process-name/path inventory, and this candidate was not launched.

### Remaining live gates and prompt 3 handoff

- **Native gate:** launch an isolated Development build from a verified output path with synthetic data; exercise real WebView scrolling, keyboard actions, loading/error/disconnect, sign-out/switch and reopen/relaunch. Native computer-control APIs are disabled in this session; static/browser/component evidence does not close this gate.
- **Backend/identity gate:** deploy the additive canon routes beside the current sync v4 infrastructure, then use an authorized registered native test account to verify real document/source versions, account switching, conflict handling and the legacy web read/refresh workflow. Production SQL Server transaction/retry behavior remains unverified by SQLite tests.
- **Provider gate:** real character/place/timeline extraction/update quality, entitlement/quota, character repair/timeline fallback and cancellation after remote commit require a configured provider and authorized live test budget. None was invoked here.
- **Host limitation:** a host without a stable native account identifier gets an unavailable capability message; it cannot reuse a display-name cache. Offline restart can view canon only once native account restoration succeeds. iOS/provider/package acceptance remains prompt 13 work.

Continue in this same checkout with `Implement prompt 3 in docs/desktopai-prompts.md, including the common requirements.`

1. Consume `DeviceBibleService.ContextAsync(LocalDocument)` and `DeviceCanonContext` at the saved, synchronized revision; it returns only Ready/exists/source-matched snapshots. Keep account generation/backend scope and document identity guards throughout request preparation.
2. Show current/stale/missing/interrupted canon explicitly. Resolve `UnresolvedReferences()` without silently binding unknown IDs. Decide the disclosed reduced-context run versus refresh path, then include only the intended validated snapshots in the existing consistency request.
3. Extend the existing issue filters/Jump/review/Apply path, retaining LocalConsistencyRevisions' changed-source/unique-match checks and durable local history/undo/recovery. Prompt 2 does not inject bibles or claim bible-aware checks.
4. Preserve the new canon controls, sequential partial-completion semantics, old client DTO compatibility, interrupted retry read-first behavior and the starting uncommitted editor/storyboard work. Append current evidence and remaining native/provider gates.

## Prompt 4 — Individual style and quality findings, 2026-10-03

**Status: implementation and available automated checks complete. Native, authenticated backend and provider acceptance remain open.** Prompt 4 depends on prompt 1. Prompt 2's canon work is preserved; prompt 3's bible-aware consistency implementation remains pending. This entry does not change those statuses.

### Implemented behavior and reference

Writing → Style & quality retains the existing selected-text/current-page AI revision and adds `LocalQualityPanel`. Users can check the current page or current selection offline, filter by severity/rule, select a finding, Jump to its exact passage, dismiss a finding for the current check, and review an available targeted fix before explicitly approving Apply. Readability and timeline findings without a supported fix are informational. Dismissal changes the current check only; it is not a cloud dismissal or a persisted preference.

The interaction reference is the client's quality toolbar/finding list in `DocumentEditor.razor`, its `RunQualityChecksAsync`, `OpenQualityProposalAsync`, `CanApplyQualityIssue`, and repeated-word/sentence/passive rewrite handlers in `DocumentEditor.razor.cs`. `PageQualityChecksController` supplies owned page checks, fixes and persisted server dismissals. `SceneQualityChecksController` lists separately stored owned scene hints; it does not generate page style findings. The desktop UI discloses that cloud glossary terms and those stored scene hints are omitted. Passive voice detection has the client's English heuristic; it is not a language-independent grammar guarantee.

The pure engine, analyzer, models and eight rules now live in `WriterApp.Shared/Quality`, retaining their existing namespace. The server and device instantiate the same `QualityRuleCatalog`; no device/presentation reference to the web client was introduced. Trimmed sentence/paragraph offsets now match their actual source, including whitespace preceding passive phrases. The server's content hash includes catalog cache version 2 so existing cached offsets are recomputed on the next run. Ownership/authorization and glossary persistence routes are unchanged.

Quality capture deliberately uses two newlines between ProseMirror blocks and one for a hard break, matching the client's paragraph distinction. General AI/consistency capture retains its existing mapping. Detection, selection offsets, highlights, Jump and quality preview share this quality mapping. Checks bind to the exact captured HTML/plain text and local document/page identity; page size is bounded at 500,000 characters, results at 200 issues and proposal text at 100,000 characters.

Adjacent duplicates are collapsed locally only within their exact duplicate range. Existing rule-provided name casing and paragraph splitting are local fixes. Non-adjacent repetition, long sentences and passive voice use the existing `rewrite.selection` endpoint with an issue-specific instruction, the input language, stable cloud identities and expected document revision. Save/flush precedes work, sync precedes generation, and `DeviceAiService` retains entitlement, quota, capability, echoed-version, timeout and account checks. No provider credentials or new provider endpoint were added.

Proposals are inert encoded text with an Original/Proposed comparison, explicit Approve/Apply and Dismiss. JSON/fenced metadata, instructions, HTML, unchanged/empty results and unsafe repeated-word outputs are rejected. Applying a generated fix also verifies that the provider request/result matches the reviewed local page, range, source, cloud revision and account. A token renewal does not discard a valid preview; account changes cancel a request and remove its generated preview.

`validateQualityRange` runs before any provider request. A supported replacement stays within one text block with uniform inline marks and complete words/Unicode characters. Mixed inline styles, embedded content and cross-block ranges receive an actionable manual-edit explanation. Phrase fixes retain marks; paragraph splitting retains source block attributes and marks. Surrounding writing/links/tables and all other pages/planning remain intact. No entire-manuscript flattening or automatic Apply occurs.

Typing immediately clears editor decorations without changing content, version or caret. Parameter changes invalidate findings/previews; Review/Apply recheck saved and captured source. Leaving the quality view clears its decorations. After approval, findings must be rerun. Page content/format changes, deletion, conflicts, expiry, invalid targets and stale generated cloud revisions block Apply; local checks tolerate unrelated metadata changes.

`LocalQualityActions` records an immutable version-1 `quality.apply_issue` Applying entry with the original snapshot and intended result before saving; Applied is recorded after commit. Existing target-scoped History Undo/Redo works after restart. Interrupted Applying entries remain recovery evidence and can recover the original into a separate local copy. If the writing commit succeeds but history completion fails, the UI explicitly reports the saved change and retained original. No proposal is automatically resumed after restart.

### Verification

Evidence directory: `artifacts/desktopai-p04/`. All fixtures use synthetic documents, identities, transport and provider responses. No live provider call or external deployment was performed.

| Check | Result | Evidence |
|---|---|---|
| Focused AI/scene/synopsis/continuity/canon/quality/history/editor/authentication regressions | 461 passed, 0 failed/skipped, 461 distinct test IDs | `regression.trx`, `regression.log` |
| New quality data/component/cache boundary checks on final UI | 34 passed, 0 failed/skipped | `quality.trx`, `quality.log` |
| Real shipped device and client editor bundles in headless Edge | 42 passed, 0 failures/page errors; real Enter → Jump and typing assertions also passed | `browser-results.json`, `browser.log`, `editor-harness-1280.png`, `editor-harness-480.png` |
| Actual Razor quality review render in Edge at 1280 and 480 pixels | Original/Proposed, approval, informational state, keyboard filter order and no horizontal overflow passed at both widths | `quality-browser-results.json`, `quality-browser.log`, `quality-review-1280.png`, `quality-review-480.png` |
| Regenerated web/device editor assets | `npm run build` passed | `editor-build.log` |
| Windows Release and referenced shared libraries | Passed, 0 warnings/errors | `windows-build.log` |
| Client Release and referenced shared libraries | Passed, 0 warnings/errors | `client-build.log` |
| Starting tracked and untracked modifications | 45 initial files fingerprinted; only intended editor source/harness/bundle, DI and appended development/UAT documentation changed within that set | `initial-status.txt`, `initial-hashes.json`, `preservation-check.json` |
| Diff whitespace | Passed; existing LF/CRLF normalization notices remain | `diff-check.log` |

The new tests cover page/selection rule equivalence, empty writing, exact whitespace/Unicode anchors, direct local fixes, generated request mapping, informational targets, stale/wrong/expired/conflicting proposals, metadata-only changes, prompt/JSON/HTML leakage, cancellation/account/quota/offline errors, actual component filters/Jump/dismiss/review/approval, restart Undo/Redo, interrupted-save recovery and other-page/planning preservation. The editor checks additionally prove identical-passage disambiguation, paragraph versus hard-break mapping, marked ranges, block attributes, surrounding tables/links, unsupported/mixed formatting, partial words/Unicode, caret stability, immediate decoration invalidation and restoration of legacy text/JSON through the explicit source format without interpreting legacy text as HTML. Workspace refresh passes the restored content format to the editor.

The test build reported the existing `NU1900` advisory-network warning and `xUnit2031` in LocalStoryboardTests.cs; neither was suppressed. Development caught and corrected Razor bindings, malformed-result error handling, paragraph mapping and format-preservation boundaries. No exercised implementation check remains failing. The Razor browser fixture is static output from real component tests, not an authenticated web/desktop application; the editor harness executes the actual shipped bundles.

Reproduction from the repository root:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Ai|FullyQualifiedName~AI|FullyQualifiedName~Scene|FullyQualifiedName~Synopsis|FullyQualifiedName~Continuity|FullyQualifiedName~Bible|FullyQualifiedName~Quality|FullyQualifiedName~Consistency|FullyQualifiedName~LocalStoryboard|FullyQualifiedName~DeviceEditor|FullyQualifiedName~EditorContent|FullyQualifiedName~PromptPreset|FullyQualifiedName~Onboarding|FullyQualifiedName~DeviceAuthentication|FullyQualifiedName~DeviceStoryTabRegression|FullyQualifiedName~DeviceContentCompatibility|FullyQualifiedName~LocalEditorSession' --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p04

$env:WRITERAPP_P04_EVIDENCE = (Resolve-Path artifacts/desktopai-p04).Path
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~LocalQuality|FullyQualifiedName~QualityCacheRevisionTests' --logger 'trx;LogFileName=quality.trx' --results-directory artifacts/desktopai-p04

Push-Location WriterApp.Client
npm run build
Pop-Location
$env:PORT = '5194'
# Run the bounded harness server in a separate terminal while verifying the editor.
node WriterApp.Client/tests/serve-device-editor.mjs
node artifacts/desktopai-p04/verify-editor.mjs
node artifacts/desktopai-p04/verify-quality.mjs

dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p04/
dotnet build WriterApp.Client/WriterApp.Client.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p04/
```

### Open live gates and handoff

- **Native:** no WriterApp/Prosa process was found by the name/path inventory (`desktop-processes.json`). The verified build candidate is `WriterApp.Desktop/artifacts/desktopai-p04/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched. Native computer-control APIs are disabled in this session. In an isolated Development host, verify real quality controls, focus/caret during autosave, Jump, loading/cancel/error/offline, Apply, restart and History from the actual launched output. Browser/component evidence does not close this gate.
- **Authenticated backend/provider:** use an authorized native test account and configured provider to verify real owned page identities, sync conflicts/revision echoes, quota/entitlement, cancellation/account changes and the writing quality of repeated-word/long-sentence/passive revisions. English heuristic findings can be false positives; review remains required. SQL Server/provider, packaged and iOS acceptance remain open.
- **Declared scope:** glossary and stored scene hints are omitted; issue findings and dismissals are ephemeral per check, while approved writing/history is durable. Structure that cannot be preserved safely blocks a targeted fix before generation. No cloud issue/dismissal synchronization or claim of complete client quality-cache parity was added.

Next independent implementation prompt: **5 — section and document translation**, using the existing prompt-1 baseline and explicit multi-page recovery/version contracts. Preserve prompt 4's quality capture mapping, shared rule catalog/cache version, strict range preflight and local history. Prompt **3** remains a separate pending task: consume prompt 2's validated canon in consistency requests and extend its issue interactions; do not infer bible-aware behavior from these page quality checks. Integrated native/provider acceptance and a refreshed final gap table remain prompt 13 work.

## Prompt 3 completion — Canon-aware consistency and issue interaction, 2026-10-03

**Status: remaining implementation and available automated verification complete.** This entry supersedes the earlier prompt-3-pending handoffs. Prompt 2's durable canon and prompt 4's quality changes remain intact. Native Windows, authenticated backend and provider acceptance remain open.

### Implemented behavior and traced reference

The device Consistency Coach now shows exactly which character, place and timeline canon is current, stale, missing or interrupted, and displays unresolved references. It consumes only `DeviceBibleService.ContextAsync` snapshots matching the synchronized manuscript and current account/backend scope. Update all bibles uses the existing sequential, recoverable canon workflow. Incomplete canon requires refresh or an explicit reduced-context choice; the report identifies every included/omitted bible and its source version. Unknown entity references remain unbound and disclosed.

The client reference is `ExecuteContinuityActionAsync`, severity filtering, Jump, `OpenContinuityProposalAsync`, `EnsureContinuityIssueHasRevisedFixAsync`, `GenerateContinuityRewriteAsync` and its explicit approval path. Its check sends three named bible JSON fields; generated prose uses `rewrite.selection`. Inspection found that the continuity action ignored the client's editor text override and used separately mapped saved text. The action now honors that override, and saved story planning is carried through to the continuity provider prompt. Desktop analyzes every saved page in the selected section using exact schema text rather than regex HTML stripping, including supported legacy JSON.

An additive optional typed canon-version request/response contract verifies intended snapshot tokens, exact content, ownership and manuscript source hash before and after provider execution. Devices require the advertised capability and echoed canon versions; unsupported backends fail before a billable request. Reduced-context runs pass `{}` for omitted bibles. Old web DTO callers remain compatible. No authorization, entitlement, quota or provider-credential boundary was weakened; no deployment or live provider call occurred.

Shared issue presentation now supplies severity filtering, readable evidence, affected scene/page, Jump, review, generated-fix review, explicit approval and Applied feedback. Jump binds exact current page text/range and opens the affected saved page without changing analyzed planning. Verified original anchors and unique evidence matching are retained; after an approved fix, remaining findings are matched against current saved writing. A later user edit, missing/ambiguous anchor, invalid target, changed canon, account switch, expiry, invalid prose, cancellation or missing revision echo cannot mutate writing.

Direct prose, `revisedText` JSON and revised-span wrappers remain inert until approval. Findings requiring prose generation use the existing version-checked selection rewrite with bounded finding/guidance and language/voice preservation. Preflight runs before generation and supports whole words in a single block with uniform marks. Mixed marks, embedded content, cross-block and multi-paragraph replacements require manual editing with an explanation. Targeted previews preserve source marks and unrelated writing, links, headings, tables, pages and authored planning. Each Apply retains Applying recovery evidence before the local save, then Applied after commit; history completion failure reports the saved state accurately. Restart Undo/Redo and separate-copy recovery remain available. No proposal is automatically applied after restart.

### Verification

Evidence directory: `artifacts/desktopai-p03-final/`. Tests use synthetic writing, accounts, canon, transport and provider results.

| Check | Result | Evidence |
|---|---|---|
| Focused AI, continuity, canon, quality, scene/planning, history, editor and authentication regressions | 503 passed, 0 failed/skipped; 503 distinct IDs, including 42 new prompt-3 tests | `regression.trx`, `regression.log`, `test-summary.json` |
| Actual device/client editor bundles in headless Edge | 47 passed, 0 failures/page errors; real keyboard consistency Jump, quality Jump and typing also passed | `browser-results.json`, `browser.log`, `editor-harness-1280.png`, `editor-harness-480.png` |
| Real Razor consistency review render in Edge at 1280/480 pixels | Severity-filtered finding, exact comparison, canon disclosure, explicit approval, keyboard order and no horizontal overflow passed at both widths; screenshot inspected | `consistency-review.html`, `consistency-browser-results.json`, `consistency-review-1280.png`, `consistency-review-480.png` |
| Tracked editor asset regeneration | `npm run build` passed | `editor-build.log` |
| Final Windows Release and referenced shared libraries | Passed, 0 warnings/errors | `windows-build.log` |
| Final Client Release and referenced shared libraries | Passed, 0 warnings/errors | `client-build.log` |
| Initial dirty-tree preservation | 65 starting existing files fingerprinted; only intended consistency/editor/presentation and appended documentation changed; unrelated files unchanged | `initial-status.txt`, `initial-hashes.json`, `preservation-check.json` |
| Whitespace validation | `git diff --check` passed; existing LF/CRLF notices retained | `diff-check.log` |

New checks exercise full/reduced canon context, exact-page inclusion, typed wire round trip, unresolved references, size limits, stale/wrong/changed snapshots, owned backend snapshot matching before/after provider execution, immutable writing, malformed prose, generated request binding, revision/capability echoes, account/token renewal, quota/offline/cancel/loading, actual component filters/Jump/review/Apply, sequential findings, later edits, restart Undo/Redo and unrelated planning preservation. Editor checks cover exact list/table/hard-break/legacy mappings, identical-passage disambiguation, unchanged content/version on Jump, marked/structured targeted preview, reopened/restored writing and unsafe ranges. Existing consistency suggestion, canon, quality and history tests were preserved.

Test builds retain the existing `NU1900` advisory-network warning and `xUnit2031` in LocalStoryboardTests.cs. Development corrected Razor syntax, synthetic cache evidence and fixture assertions; no exercised implementation check remains failing. Browser component output is a static real Razor render; its form value is restored from the rendered binding attribute for screenshot fidelity. Interactive component events are exercised by the .NET tests, and the editor harness runs the actual shipped bundles. Neither is native or live-provider acceptance.

Reproduction from the repository root:

```powershell
$env:WRITERAPP_P03_EVIDENCE = (Resolve-Path artifacts/desktopai-p03-final).Path
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Ai|FullyQualifiedName~AI|FullyQualifiedName~Scene|FullyQualifiedName~Synopsis|FullyQualifiedName~Continuity|FullyQualifiedName~Bible|FullyQualifiedName~Quality|FullyQualifiedName~Consistency|FullyQualifiedName~LocalStoryboard|FullyQualifiedName~DeviceEditor|FullyQualifiedName~EditorContent|FullyQualifiedName~PromptPreset|FullyQualifiedName~Onboarding|FullyQualifiedName~DeviceAuthentication|FullyQualifiedName~DeviceStoryTabRegression|FullyQualifiedName~DeviceContentCompatibility|FullyQualifiedName~LocalEditorSession' --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p03-final

Push-Location WriterApp.Client
npm run build
Pop-Location
$env:PORT = '5194'
# Run the bounded harness server separately while verifying the editor.
node WriterApp.Client/tests/serve-device-editor.mjs
node artifacts/desktopai-p03-final/verify-editor.mjs
node artifacts/desktopai-p03-final/verify-consistency.mjs

dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p03-final/
dotnet build WriterApp.Client/WriterApp.Client.csproj --configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p03-final/
```

### Live gates and concrete handoff

- **Native:** process-name inventory returned no WriterApp/Prosa process; CIM path inventory was denied by the environment, so no running-path claim is made. The verified candidate is `WriterApp.Desktop/artifacts/desktopai-p03-final/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched. Native computer-control APIs are disabled. In an isolated Development host, verify scrolling, real keyboard/affected-page Jump, source changes, loading/error/cancel/offline, account switch, explicit direct/generated Apply, restart and History from the actual launch output.
- **Backend/provider:** deploy the additive canon revision capability beside prompt 2's owned canon routes and existing sync revision infrastructure, then use an authorized native account/provider budget to verify real extraction/check versions, canon refresh during analysis, server conflict/quota/entitlement, generated prose quality and legacy web interaction. SQL Server, packaged and iOS acceptance remain separate gates.
- **Declared safe scope:** current findings/filters/previews are session state; approved writing and recovery/history are durable. Automatic revisions preserve one uniform text block; unsafe rich spans require manual review. Reduced runs and unresolved references are disclosed. Stored server canon is revalidated during analysis; changes not yet downloaded after a completed report are discovered by the next canon load/sync. Apply does not issue a live cloud canon lookup while offline; it checks retained source/account/canon evidence, and remote writing changes retain the normal sync conflict boundary.

Next implementation: **prompt 5 — section and document translation**, preserving prompts 2–4, both editor text mappings, typed source/canon checks, markup protections, explicit approval and durable recovery. Continue in this same checkout with `Implement prompt 5 in docs/desktopai-prompts.md, including the common requirements.` Prompt 13 still owns integrated native/provider acceptance and the refreshed final gap table.


## Prompt 5 — Section and document translation (2026-10-03)

**Implementation complete for the declared supported scope; native/provider acceptance remains open.** Selection translation is preserved. Desktop now captures every ordered page for section/document translation through the actual editor schema, reuses the existing backend translation actions and shared language/style/result UI, validates complete typed output, and provides explicit per-page before/after review and Replace/Duplicate choices. Bounded text-run mapping preserves rich content without applying a whole translation blob over unrelated pages. Unsupported rich sources/annotations fail before generation.

Source/version/account/capability checks precede generation and approval. Whole-scope Apply commits one local aggregate behind immutable version-2 Before/After history; version-1 history remains readable. Interrupted saves/confirmations can finish from History after restart. Copies use preassigned detached identities, preserve original writing/planning and cannot duplicate themselves on retry. Targeted Undo/Redo retains later unrelated edits and rejects changed targets. Copy undo uses Trash/restore and keeps the original intact. No deployment, external authentication registration changes or live provider requests were made.

### Client trace and known defect

The client reference is `DocumentEditor.OpenTranslateModal` / `ExecuteTranslateActionAsync`, `GetTranslationApplyOptions`, `ApplyTranslationProposalAsync`, `DuplicateTranslatedSectionAsync`, `DuplicateTranslatedDocumentAsync`, `ReplaceTranslatedDocumentAsync` and `ParseTranslatedSections`. The existing `TranslateSelectionAction`, `TranslateSectionAction` and `TranslateDocumentAction` remain the backend actions. `TranslationProposalPanel` and the scope/source/target/style controls were extracted into `WriterApp.UI.Shared` and are consumed by both hosts.

The legacy client's broader application is incomplete: section replacement flattens its returned text into the current editor; document replacement writes first pages only and silently skips absent section markers; duplicate payload construction can substitute empty content for missing markers. Desktop does not reuse that behavior. It checks every ordered section/page/run identity and preserves the page/schema mapping. Full native/web application acceptance, including repair of that legacy web path, remains a prompt-13 gate.

### Current checks and evidence

Evidence directory: `artifacts/desktopai-p05/` (local generated evidence, not a release artifact).

| Check | Current result | Evidence |
|---|---|---|
| Focused AI, canon, continuity, quality, scene/planning, history, editor/authentication and translation regressions | 547 passed, 0 failed/skipped; 547 distinct IDs; 44 new prompt-5 checks | `regression.trx`, `regression.log`, `test-summary.json` |
| Real shipped editor harness in headless Edge | 52 passed, 0 failures/browser errors, including 5 new translation checks and preserved consistency/quality keyboard Jump/typing checks | `browser-results.json`, `browser-results.txt`, `verify-editor.mjs`, editor screenshots at 1280/480 |
| Actual device/shared-client Razor render fixtures in Edge | Both hosts at 1280/480: language/style controls, three complete page comparisons/explicit approval where applicable, keyboard order, no horizontal overflow; screenshots inspected | `translation-review.html`, `client-translation-options.html`, `translation-browser-results.json`, `verify-translation.mjs`, four translation screenshots |
| Windows desktop Release build | 0 warnings, 0 errors | `desktop-build.log` |
| Client Release build | 0 warnings, 0 errors | `client-build.log` |
| Editor asset regeneration | Both editor bundles generated successfully from source | `assets-build.log` |
| Dirty-tree preservation and whitespace | Initial tracked dirty files fingerprinted; only intended integration/docs/assets changed; existing unrelated changes and initial deletions retained | `initial-status.txt`, `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

Tests cover exact intended scopes, multi-page Unicode, empty/rich/legacy page mapping, missing/duplicate/reordered section/page/run markers, invalid Unicode/spacing, wrong target/language, unsupported sources, input limits, owned backend target/revision checks before/after execution, capability gating, styles/languages, inert provider markup, cancel/loading, account changes, explicit review/Dismiss/Apply, version-1 compatibility, aggregate failure after durable staging, interrupted confirmation, stable copy retry, restart Undo/Redo, planning retention and later-edited targets. Component behavior tests use synthetic API/JS fixtures; the editor checks execute the shipped bundles and actual schema. Static Razor screenshots establish presentation/keyboard order, not native event or provider acceptance.

The test build retains pre-existing NU1900 (NuGet vulnerability feed unavailable) and xUnit2031 in `LocalStoryboardTests`; no new warnings remain. A concurrent build briefly locked a shared intermediate; the Windows build was rerun sequentially and passed. Use sequential host builds when sharing this checkout.

Reproduce the focused suite with the prompt-3 filter recorded above plus `|FullyQualifiedName~Translation`, changing the results directory to `artifacts/desktopai-p05`. Build client/Windows Release with `--no-restore -p:BaseOutputPath=artifacts/desktopai-p05/`, sequentially. Run `npm run build` from `WriterApp.Client`, then the bounded editor harness on port 5194 and `verify-editor.mjs` / `verify-translation.mjs`. Set `WRITERAPP_P05_EVIDENCE` to the absolute evidence directory when running component tests to regenerate the Razor fixtures.

### Live gates and next handoff

- **Native:** process-name inventory found no running `WriterApp.Desktop`; the isolated executable exists at `WriterApp.Desktop/artifacts/desktopai-p05/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe` and was not launched. Native computer-control APIs are disabled. In isolated Development data, verify actual More writing tools controls, keyboard selection, section/document review and copy choices, loading/cancel/offline/unsupported states, account changes, explicit Apply, close/restart and History recovery from the confirmed launch output.
- **Authenticated backend/provider:** publish the additive structured-translation capability/action/controller contract alongside the existing revision-checked sync infrastructure. Verify real entitlement/quota/cancellation/conflict behavior and language quality for Swedish/English, Japanese, RTL and mixed-format prose; inspect semantic placement of translated marked phrases. Output-token limits can require smaller scopes. SQL Server, legacy web broader Apply, packaged and iOS acceptance remain separate gates. A target-language echo does not prove language quality.
- **Safe supported scope:** up to 60,000 UTF-16 text units/2,000 runs, complete ordered pages, supported headings/paragraphs/marks/links/lists/tables/hard breaks and legacy source formats. Images/captions, inline/block code, HTML comments, implicit text-run newlines, unknown editor content and anchored annotations require a suitable selection or text-only scope. Copies start local-only; remote changes use normal explicit sync/conflict handling. Recovering an already approved local save does not generate another model result or charge another account.

Next: **prompt 6 — writing presets, section actions and next paragraph**. Reuse the complete-page capture, bounded structured mapping, aggregate persistence and versioned recovery conventions rather than flattening section edits. Preserve prompts 2–5, consistency/quality range mappings, language catalogue, revision/account/capability guards, local writing/history and unrelated changes. Continue in this same checkout with `Implement prompt 6 in docs/desktopai-prompts.md, including the common requirements.` Prompt 13 owns integrated live acceptance and the final gap table.

## Prompt 6 — Writing presets, section revision and continuation (2026-10-03)

Implemented in the existing dirty checkout. Initial tracked and untracked source files were fingerprinted before editing; prior prompts and unrelated work were retained. The client action catalogue, preset parameters, recommendation resolver, request/Apply handling, revise/continuation actions, provider construction and feature gating were traced. Common architecture, source/version/account guards, bounded inert output, explicit approval and durable recovery requirements were applied.

| Workflow | Desktop implementation and client semantics |
|---|---|
| Rewrite presets | Neutral/Formal/Casual, expand/shorten through length, Friendly/Technical tone, and editable tone/length/preserve terms through shared `WritingOptions`. The client grammar-labelled preset is neutral same-length rewrite with preserve terms, not a specialized grammar guarantee; the desktop explains this. |
| Dedicated selection actions | Existing `expand.selection`, `tighten.selection`, `change_tone.selection`, `show_dont_tell.selection`, plus parameterized `rewrite.selection`; exact saved-page quality mapping, safe range preflight, marked inert replacement and changed-selection guards. |
| Complete section revision | Existing expand/tighten/change-tone/show-don't-tell section actions with additive strict `writing_structure`; every ordered page/run captured, reviewed and atomically saved. Structure/formatting is host-owned; no flat JSON replacement operation is emitted. |
| Next paragraph | Existing `propose.next-paragraph` with saved all-page section context and scene beats, genre and shared client intent/task catalogue as disclosed craft guidance. Exactly one normalized inert paragraph previews and appends after the final page's original nodes. The established leading-echo helper is shared with the client; empty/pure repeats and invalid output are rejected. |
| Existing Summarize section | Preserved, including its non-destructive append behavior and prior tests. |
| Availability | Configured runnable action keys filtered by actual backend feature-tier rules, additive complete-section capability, and existing AI/subscription/quota status. Missing/old/inactive actions are disabled with refresh/plan/backend guidance and rechecked before generation. |
| Review/history/recovery | Explicit per-page Original/Proposed review, Apply/Dismiss, typed version-3 section identity and complete immutable Before/After snapshots. Applying intent precedes one atomic aggregate save. Approved recovery works after restart/offline without another generation. Undo/Redo preserves unrelated writing/planning and refuses changed/moved targets. History presents readable page prose and the new paragraph. |

Whole-section actions preserve supported headings, paragraphs, marks, links, lists, tables, hard breaks and block attributes while retaining page/run boundaries. Structural rewrites, images/captions, code, comments, implicit run newlines, unknown content and anchored annotations require a supported selected passage or manual editing. Selection supports one uniform text block; unsafe mixed/cross-block ranges remain intact. Continuation preserves existing rich nodes, tables/images and legacy formats because it owns only a new paragraph. Limits and contracts are documented in `device-development.md` and `editor-content-compatibility.md`.

Source is flushed and synchronized before generation, with exact local/cloud versions and account generation pinned. The backend retains ownership, entitlements and quota checks and validates complete section/page identities before generation plus source revision afterward. Apply checks document/section/page identity, selection, account/expiry and complete source freshness. Provider HTML remains inert schema text. Invalid targets/output, cancellation, stale writing and account changes never become authored writing.

This run exposed a Windows read/atomic-replace race in existing canon/history paths. Snapshot readers now share deletion, and the atomic writer retries a brief sharing/access-denied rename for at most 775 ms using its already durable staging file. Persistent errors/cancellation still fail without truncation. New concurrent canon/history replacement and controlled Windows-reader tests pass. Navigation retains the editor freeze throughout panel operations; accessible select labels and preset/custom state were corrected during Razor/browser verification.

### Current automated evidence

All artifacts below are under `artifacts/desktopai-p06/`. Counts refer to the final passing run, not earlier attempts.

| Check | Result | Evidence |
|---|---|---|
| AI/scene/synopsis/canon/quality/consistency/editor/history/authentication/onboarding/translation/writing regression suite | **619 distinct tests passed; 0 failed/skipped. Includes 71 new writing/component/controller cases.** | `regression.trx`, `regression.log`, `test-summary.json` |
| Real shipped editor bundles in headless Edge | **58 passed; 0 failed/browser errors.** Includes exact HTML/legacy selection mapping, rich complete-page revision, inert append, original-node retention, empty final pages, unsupported/invalid rejection and reopen through both bundles; keyboard Jump/typing retained. | `browser-results.json`, `browser.log`, `verify-editor.mjs`, editor screenshots at 1280/480 |
| Actual shared/device Razor review markup in headless Edge | Section and continuation at **1280/480**: accessible controls, complete comparisons, approval/dismiss keyboard order, no horizontal overflow; screenshots inspected. | `section-review.html`, `continuation-review.html`, `verify-writing.mjs`, `writing-browser-results.json`, four preview screenshots |
| Windows desktop Release build | **0 warnings, 0 errors.** | `desktop-build.log` |
| Client Release build | **0 warnings, 0 errors.** | `client-build.log` |
| Editor asset regeneration | Both tracked bundles regenerated successfully from source. | `assets-build.log` |
| Source preservation and whitespace | Initial dirty tracked/untracked source fingerprinted, only intended integration/docs/assets changed; initial deletions preserved; `git diff --check` passed. | `initial-status.txt`, `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

Tests exercise preset parameters and actual shared control events, dedicated keys/scopes, complete page identities, owned/current/foreign/stale backend requests, availability/entitlements/capability, changed selection/source, wrong document/section/page, malformed/partial/reordered/duplicate/unknown JSON, invalid Unicode/newlines/whitespace, source echo/meta/size rejection, loading/cancel/account changes, inert review/Dismiss/Apply, original preservation, immutable history, aggregate staging failure, interrupted confirmation, idempotent approved recovery, restart Undo/Redo and later unrelated edits. Component/controller tests use synthetic API/JS/in-memory database seams; editor checks execute the actual shipped schema/bundles. Static Razor screenshots establish presentation and keyboard order, not native application events or provider quality.

The test build retains the existing NU1900 vulnerability-feed connectivity warning and xUnit2031 in `LocalStoryboardTests`; final host builds have no warnings. Earlier invalid Razor expressions, output-validation fixtures, inaccessible select labels and the reproduced Windows file race were repaired before the final passing run. The initial focused `writing.trx` is historical; `regression.trx`/`test-summary.json` contain the final source checks.

Reproduce the regression suite with the prompt-3 filter above plus `|FullyQualifiedName~Translation|FullyQualifiedName~LocalWriting`, using `--no-restore -p:BaseOutputPath=artifacts/desktopai-p06/` and this evidence directory. Set `WRITERAPP_P06_EVIDENCE` to the absolute evidence path to regenerate actual component markup. Build Windows and client Release sequentially with the same isolated output parameter. Regenerate assets from `WriterApp.Client` with `npm run build`; run the bounded `tests/serve-device-editor.mjs` on port 5194 and the two recorded browser scripts. The fixture server was stopped after verification.

### Native/provider gates and next handoff

- **Native:** no running `WriterApp.Desktop` was found. The confirmed isolated candidate is `WriterApp.Desktop/artifacts/desktopai-p06/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched. Native control APIs are disabled. In isolated Development data, verify real presets/caret selection, disabled/offline/empty/loading/error/cancel states, all-page comparisons, append at a final table/image/empty page, approval, close/restart and History recovery/Undo/Redo from that actual launch output. Existing user documents were not used for verification.
- **Authenticated backend/provider:** deploy the additive availability/structured-section/controller/provider contract alongside revision-checked sync before enabling it. Verify live plan/quota/cancellation/account/conflict behavior, section output completeness and semantic placement of marked prose, and continuation voice/POV/scene beats/genre. The validator suppresses exact leading suffix echoes at the established 80-character threshold and pure repeats; it does not prove absence of paraphrased recaps or guarantee grammar/prose quality. Existing recent-context and provider output-token limits remain. No live provider call or deployment occurred.
- **Other retained gates:** legacy web whole-section/document Apply defects recorded in prompt 5, packaged Windows, iOS, SQL Server and integrated cross-host acceptance remain for prompt 13. Full reusable/recommended template workflows remain prompt 9; continuation craft guidance is not counted as their implementation.

Next: **prompt 7 — complete scene-card coaching scope**. Continue in this checkout with `Implement prompt 7 in docs/desktopai-prompts.md, including the common requirements.` Reuse shared recommendations, versioned scoped history, account/source checks and editor preflight; extend existing scene-card suggestion/refinement to individual fields/open questions and validated metadata references. Preserve prompts 2–6, all-page translation/revision, dedicated continuation append, canon/consistency/quality guards, local recovery, authored metadata and unrelated changes. Prompt 13 retains integrated live acceptance and the final gap table.

## 2026-10-03 — Prompt 7: complete scene-card coaching scope

Implemented all 12 actual client field-selector counterparts plus the retained narrative-purpose application and explicit timeline-event/reference targets (15 fields total). Both editor Story/Scene card Coach and storyboard selected-scene coaching reuse `LocalAiPanel`; existing imperative suggest/refine shortcuts remain. Open questions use `scene.find-open-questions`. The shared review shows Original/Proposed per changed field, invalid-link explanations and approval checkboxes. Whole-card proposals require choosing updates; a scoped Apply can own only its selected field. Empty/omitted fields and malformed/unresolved metadata preserve authored data. IDs are exact typed catalogue identities, never guessed from labels or normalized as prose.

Requests capture complete saved page text, current card, owned project planning and current account/backend-scoped canon with manuscript/canon revision checks. The additive version-1 scene contract forwards the full field catalogue and focus to the existing provider adapter without the legacy 4,000-character section truncation. Narrative coaching remains possible with missing canon, but missing/stale/foreign/ID-less canon cannot resolve links. Supported metadata includes status, POV, place, timeline event/marker, subplot tags, tags and typed references. Current source/planning/canon changes invalidate previews; acknowledgement-only document revision/timestamp changes remain tolerated. Supported legacy JSON pages supply schema text without changing original bytes or format.

Application uses local planning and the existing project sync transaction. Version-4 durable history stores immutable original/result/catalogue/approved fields before atomic Apply. Reviewed results remain inert; explicit approved-save recovery is idempotent and offline, with later-change guards. Field Undo/Redo retains unrelated writing, fields and notes; history link Redo checks current canon/planning and explains refresh requirements. History comparisons are readable. Existing version-1–3 records and prompt-2–6 behavior remain intact. The initial dirty checkout was fingerprinted and preserved; unrelated storyboard changes were not reverted.

Verification exposed and repaired two reachable UI issues: imperative storyboard coaching needed explicit child loading/result rerenders, and canon controls were clearing cached cards on every parent rerender. Regression tests now verify displayed results and retained cache entries. Current-canon link validation was also added before history Redo. No editor source/asset regeneration was necessary because this change does not modify editor mutation, selection or schema behavior.

### Final automated evidence

Evidence is under `artifacts/desktopai-p07/`; counts below refer to final passing source checks, excluding earlier diagnostic runs.

| Check | Result | Evidence |
|---|---|---|
| AI/scene/synopsis/canon/quality/consistency/editor/history/authentication/onboarding/translation/writing regression suite | **683 passed, 0 failed/skipped; includes 64 new scene contract/component/controller cases.** | `regression.trx`, `regression.log`, `test-summary.json` |
| All 15 scene fields | Scoped Apply, unchanged unrelated fields/pages, save/reopen, mapped sync card payloads and field Undo/Redo passed. | `LocalSceneCoachingTests` in final TRX |
| Actual device/shared component events | Editor presentation and storyboard imperative entry, checkbox approval, scoped instructions, open-questions key, inert review/Dismiss, stale source/account, invalid output, disconnect, loading/cancel and unconfirmed link Redo passed. | `LocalScenePanelTests`, `editor-review.html`, `storyboard-review.html` |
| Backend scene actions | All three dedicated keys, full 6,000-character source, focus/current card/catalogue and confirmed versions; foreign/stale/malformed/new-contract/late-source rejection passed. | `AiActionsControllerTests.SceneCoachingEndpoints*` / `InvalidOrChangedSceneContracts*` |
| Shipped editor bundles in headless Edge | **58 passed, 0 failed/errors**; prior rich-content, selection, typing, translation, continuation and reopen behavior retained. | `browser-results.json`, `browser.log`, `verify-editor.mjs` |
| Actual Razor review markup in headless Edge | Editor/storyboard at **1280/480**: four field comparisons, three valid approval controls, invalid-link explanation, Apply disabled until approval, native checkbox keyboard interaction, Dismiss focus, no horizontal overflow; screenshots inspected. | `scene-browser-results.json`, `verify-scene.mjs`, four review screenshots |
| Windows/client Release builds | Both **0 warnings, 0 errors**. | `desktop-build.log`, `client-build.log` |
| Dirty source preservation/whitespace | Only intended integrations/documentation changed; prior dirty files and deletions retained; `git diff --check` passed. | `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

The new tests also cover metadata-only proposals, retained ID spelling/underscores, omitted/null/empty values, duplicate/unknown/numeric JSON keys, invalid status/role/types, wrong-kind/foreign/missing links, canonical entity labels versus IDs, approved subsets, cloud planning IDs versus local target IDs, exact legacy-page context, wrong-source canon, immutable approved results/catalogues, cancellation before storage, pre/post-commit interruption, idempotent offline recovery and later related/unrelated edits. Canon freshness compares both version tokens and content. Test compilation retains the existing NU1900 vulnerability-feed connectivity warning and xUnit2031 in `LocalStoryboardTests`; host Release builds are warning-free. Component/controller tests use synthetic API/JS/storage/database seams. Browser review snapshots establish presentation and native HTML keyboard behavior, not running Blazor events, provider quality or native Windows acceptance.

Reproduce using the prompt-3 regression filter above plus `|FullyQualifiedName~Translation|FullyQualifiedName~LocalWriting`, with `--configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p07/ --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p07`. Set `WRITERAPP_P07_EVIDENCE` to the absolute evidence directory to regenerate actual component markup. Build Windows/client Release sequentially with the same output parameter. Run the bounded editor fixture server on port 5194 and `verify-editor.mjs`; run `verify-scene.mjs` for static scene review fixtures. The fixture server was stopped after verification.

### Retained live gates and next handoff

- **Native desktop:** no running desktop process was found. The rebuilt candidate is `WriterApp.Desktop/artifacts/desktopai-p07/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched because native control APIs are disabled. In isolated Development data, exercise all scene fields and both real entry points, keyboard/scrolling, missing/stale/offline/empty/error/cancel states, per-field approvals, close/restart, recovery and History Undo/Redo from that actual output. No existing user manuscript was used for tests.
- **Authenticated backend/provider:** backend and desktop require the additive scene-coaching contract together. Verify real structured field output, whole/scoped quality, exact canon links, plan/quota/account/version/cancel behavior, server planning identity transport and full-section provider limits. No provider call or deployment occurred. Character limits and validation do not establish semantic coaching quality.
- **Client reference defects:** its scoped selector has 12 fields, but the legacy provider prompt does not list summary/status/subplot-tags despite those visible controls; the lenient parser rejects metadata-only outputs, normalizes ID strings as prose and discards malformed references. Its whole-card Apply uses non-empty values without desktop-equivalent catalogue validation/field approval. Desktop version-1 coaching uses the complete provider contract and independent presence-aware typed application, preserving the older parser/application paths for compatibility. Repair and live client parity acceptance remain prompt 13; these legacy behaviors were not copied as acceptance evidence.
- Packaged Windows/iOS, SQL Server, earlier legacy translation client Apply defects and integrated cross-host acceptance remain prompt 13. Local executable builds, mocks and static previews do not establish those gates.

Next: **prompt 8 — synopsis evaluation and guiding questions**. Continue in this same checkout with `Implement prompt 8 in docs/desktopai-prompts.md, including the common requirements.` Extend the existing synopsis coach and retain scene field review, current-canon link validation, scoped history versions 1–4, source/account checks, bible/consistency/quality controls, rich writing/translation/continuation and unrelated storyboard work. Prompt 13 retains final live acceptance and the gap table.

## Prompt 8 — Synopsis evaluation and guiding questions, 2026-10-03

**Status: implementation and available automated checks complete; native/authenticated/provider acceptance remains open.** The existing Story → Synopsis coach now offers evaluation, guiding questions and explicit field improvement. All ten synopsis fields and author notes remain local planning. Generation saves/synchronizes first, uses the existing dedicated synopsis endpoints and confirms the exact owned saved source before/after provider execution. Evaluation/questions remain readable feedback; field improvement shows Original/Proposed text and separate commentary. Shared presentation escapes provider text and supports keyboard controls and narrow layouts. Scenes/pages are not required, empty synopsis guidance is explicit, and standalone/no-cloud/conflict/offline states preserve ordinary writing.

Version-1 request/response contracts add complete source, version, project/document identity and mode/field confirmation, plus an advertised backend capability. Field suggestions use bounded strict JSON; legacy web callers remain compatible. Apply stays pinned to the reviewed field and rejects changed local source, account, expiry and invalid output. Version-5 history saves immutable approved intent before mutation, retains commentary/coaching notes, and supports offline restart recovery and field Undo/Redo while retaining later unrelated edits. Original rich content remains unchanged. Existing history versions 1–4 and prior prompt behavior were preserved. Cloud generation history stores the actual proposed field rather than its commentary.

### Final automated evidence

Evidence is under `artifacts/desktopai-p08/`. Counts refer to final passing checks.

| Check | Result | Evidence |
|---|---|---|
| AI/scene/synopsis/canon/quality/consistency/editor/history/authentication/onboarding/translation/writing regression suite | **764 passed, 0 failed/skipped, including 81 new synopsis cases.** | `regression.trx`, `regression.log`, `test-summary.json` |
| All ten synopsis fields | Approved field-only persistence/reopen, unchanged writing/other fields, pinned selector target, immutable approval, scoped Undo/Redo and retained coaching notes passed. | `LocalSynopsisCoachingTests`, `LocalSynopsisPanelTests` |
| Actual device/shared Razor handlers | All three modes, no-scene/no-page context, headings/questions/comparison, inert analysis, Apply/Dismiss, stale source/account, malformed output, quota/offline, loading/cancel and save/adopt hooks passed. | `LocalSynopsisPanelTests`; `evaluate-review.html`, `questions-review.html`, `suggest-review.html` |
| Dedicated backend and HTTP adapters | Correct routes/actions/notes/all fields; owned source/version/project pre/post checks; foreign/stale/changed/trashed, malformed/downgraded contracts, plan/quota responses and separated history output passed. | `DocumentSynopsisCoachingTests`, `DeviceSynopsisApiTests` |
| Interrupted approval/recovery | Reopen and finish approved save offline before/after document commit; idempotent completion, later-source refusal, unrelated-note preservation and affected-field Undo refusal passed. | `LocalSynopsisCoachingTests.InterruptedApprovalReopensOfflineAndRejectsLaterChanges` |
| Shipped editor bundles in headless Edge | **58 passed, 0 failed/errors.** No editor schema, selection, rich-content or bundle changes were needed. | `browser-results.json`, `browser.log`, `verify-editor.mjs` |
| Actual Razor review markup in headless Edge | All three modes at **1280/480**, ten fields and pinned notes, explicit field-only Apply, read-only analysis, source disclosure/Dismiss keyboard focus, no horizontal overflow; screenshots inspected. | `synopsis-browser-results.json`, `verify-synopsis.mjs`, six screenshots |
| Windows/client Release builds | Both **0 warnings, 0 errors.** | `desktop-build.log`, `client-build.log` |
| Existing dirty checkout/whitespace | Initial 120-file fingerprint baseline, intended edits only, retained pre-existing deletions; `git diff --check` passed. | `initial-status.txt`, `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

Tests use synthetic local account/API/database/JS fixtures and temporary isolated manuscripts. Component events exercise the real Razor handlers; static Edge fixtures establish native HTML keyboard/layout behavior, not running Blazor events or Windows mouse input. Test compilation retains the existing NU1900 vulnerability-feed connectivity warning and xUnit2031 in `LocalStoryboardTests`, with no new compiler warnings. No existing user manuscript, provider credential, deployment or external registration was changed.

Reproduce the prompt-7 regression filter (including Translation/LocalWriting) with `--configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p08/ --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p08`. Set `WRITERAPP_P08_EVIDENCE` to the absolute evidence directory to regenerate component fixtures. Build Windows/client sequentially using that output parameter. Serve the bounded editor fixture on port 5194, run `verify-editor.mjs`, and run `verify-synopsis.mjs` for the static fixtures. The fixture server was stopped after verification.

### Remaining live gates and next handoff

- **Native Windows:** no running desktop process was found. The rebuilt candidate is `WriterApp.Desktop/artifacts/desktopai-p08/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched because native control APIs are disabled. From that exact output, use isolated Development data to exercise the real Story/Synopsis route, all modes/fields, scrolling/keyboard, empty/standalone/no-scene/offline/quota/error/cancel states, close/restart recovery and History Undo/Redo. `environment.json` records the checked host boundary.
- **Authenticated backend/provider:** enable matching synopsis contracts together and verify real evaluation/question usefulness, field text versus commentary, preserved author voice/language/intent, empty-context behavior, all ten fields, plan/quota/account/version boundaries, sync conflicts, cancellation and provider output limits. No live provider call occurred; typed validation does not establish semantic coaching quality.
- **Client reference defects:** `WriterApp.Client/Pages/Synopsis.razor` sends its legacy request without first confirming the edited fields were saved. It applies using the currently selected field rather than the response's pinned field and lacks desktop-equivalent source/account/shape guards. Desktop does not copy those defects; legacy client repair and live comparison remain prompt 13. Production planning sync triggers already notify changes; exact content checks provide an additional source safeguard.
- Packaged Windows/iOS, SQL Server, cloud/local history integration and integrated cross-host acceptance remain the existing later gates. Current local builds and fixtures do not establish them.

Next: **prompt 9 — full reusable prompt workflows and safe synchronization**. Continue in this same checkout with `Implement prompt 9 in docs/desktopai-prompts.md, including the common requirements.` Extend the existing prompt library while retaining synopsis version-1 source contracts and version-5 scoped recovery, scene review/canon safeguards, writing/translation/quality/consistency behavior, history versions 1–5 and unrelated storyboard work. Prompt 10 owns cloud/local history reconciliation; prompt 13 owns final live acceptance and the gap table.

## Prompt 9 — Reusable presets and safe cloud transfers, 2026-10-03

**Status: implementation and available automated checks complete; Windows, authenticated provider and live SQL Server acceptance remain open.** The production **Advanced → Prompt library** now provides offline create/edit/delete, categories, typed builtin revision presets, custom templates/variables, declared selection/section targets, persisted pins and genre recommendations. Shared editor/list/conflict presentation resides in UI.Shared, versioned local persistence and lifecycle in Device.Shared, and typed definitions/transfer contracts in Shared. Existing uncommitted prompts 1–8, history versions 1–5 and unrelated storyboard work were retained.

The client reference has preset CRUD, raw JSON parameters, scope selection and up to three in-memory pins. It does not implement automatic preset synchronization. Desktop adds durable **explicit** cloud copy/update/import/delete with a persisted retry queue, rather than claiming that background sync exists in the client. Supported builtin execution covers Rewrite selection and selection/section Expand, Shorten, Change tone and Show, don't tell. Other action/parameter combinations can transfer losslessly and remain visibly unavailable until repaired or supported by a dedicated target adapter. Legacy client project binding uses the document ID in preset CRUD; desktop validates real project ownership and refuses mismatched project execution instead of reproducing that defect. Client legacy writes remain ordinary CRUD; repairing their concurrency behavior and final action coverage comparison belongs to prompt 13.

Version-1 plain local prompts survive without byte/identity changes on read. Explicit edits/pins/deletes upgrade only that record to version 2, retaining creation time and stable identity. Expected local revisions and an atomic root lock protect competing edits/queues. Tombstones retain deleted authored records. Imports retain category, action, scope, project binding, pins and arbitrary parameter JSON, with stable identities per cloud revision; they never overwrite a locally edited copy or revive a deleted copy on retry. Imported content, caches and queue files are account/backend scoped and disappear immediately from the UI on account change. Authored local presets remain usable offline. Pin writes enforce available slots; no import silently discards a pin. Additional pins made visible by account membership are disclosed, retained and limited to three quick-access buttons.

Execution uses `LocalWritingPanel.RunPresetAsync`, `LocalWriting` and its existing durable writing history/recovery. The workspace saves/freezes writing/planning, captures the exact selection or complete ordered section/page/run mapping, synchronizes and pins source versions. Custom tokens share backend validation, use the declared target as context and cannot override execution controls. Review stays inert; Apply reacquires the intended selection and validates source/account/target/parameters before saving immutable approval. Section custom output uses typed version-1 `writing_structure`, an advertised capability, complete page/run validation and no flat replacement operation. Existing scoped Undo/Redo and interrupted approved-save completion retain rich content and unrelated writing/planning. Original/proposed comparisons, explicit Apply/Dismiss, loading/cancel and actionable errors are exercised in the real Razor handlers.

The additive preset transfer protocol has stable preset/operation IDs, canonical authored payloads, revision tokens and a durable owner-scoped receipt ledger. Serializable mutation/receipt commit and replay return the original result after a lost response, including after subsequent deletion. Reusing an operation with another payload is rejected. Updates/deletes require the current token; divergence becomes a visible owned snapshot or deleted-state conflict. Choices preserve both by queuing a separate stable cloud fork, keep/import the cloud version into a separate local record, or cancel while keeping originals. A pending authored intent cannot be replaced by later local edits; retries replay that exact frozen intent. No missing-route fallback issues an unsafe legacy create. Local deletion and cloud deletion are separate explicit operations.

### Final automated evidence

Evidence is under `artifacts/desktopai-p09/`. Counts below refer to final passing runs, including the authentication, project-binding and malformed stored-preset fixes. Windows and client builds followed the final test compilation.

| Check | Result | Evidence |
|---|---|---|
| AI/planning/canon/quality/consistency/editor/history/authentication/onboarding/translation/writing/preset/migration regression | **837 passed, 0 failed/skipped.** | `regression.trx`, `regression.log`, `test-summary.json` |
| Focused preset/token/writing component and controller checks | **103 passed, 0 failed/skipped**, including existing writing/token cases. | `focused.trx`, `focused.log` |
| Actual production library and shared editor handlers | **6 passed**: offline CRUD, categories, typed scope/tone/length/preserve-term editing, pinning, library-to-writing selection/section Apply, retained upload retry, visible conflict choices, import, immediate account clearing, refreshed project binding and same-account token refresh. | `LocalPromptPanelTests`, `ui.trx`, `ui.log`; `library-*.html` |
| Authorization, revision and replay contract | Owned project/target and pre/post manuscript checks; exact selection/context/parameters; unsupported/partial/stale/foreign/plan refusal; lost upload response, restart replay, simultaneous queue creation, immutable intent, changed/deleted cloud versions, separate conflict resolutions, account/backend isolation and no legacy fallback passed. | `ReusablePromptTransferTests`, `ReusablePromptContractTests`, `ReusablePromptWritingEndpointTests`, `CustomTransformActionTests.Presets` |
| Writing Apply/recovery | Custom/builtin selection/section reviews retain parameters and unrelated writing; durable persistence/reopen and scoped Undo/Redo passed. Existing interrupted-writing recovery remains in the broad suite. | `LocalWritingPanelTests.Presets`, `LocalWritingTests`; four `*-review.html` snapshots |
| Shipped editor bundles in headless Edge | **60 passed, 0 failed/errors**, including two new custom selection/section preview/application/reopen rich-content cases and the existing keyboard/selection/formatting checks. | `browser-results.json`, `browser.log`, `verify-editor.mjs`; editor screenshots |
| Actual Razor markup in headless Edge | **10 fixtures passed**: custom/builtin editor, conflict comparison and selection/section review at 1280/480. Stored template/token/scope/length visible, explicit Apply, keyboard focus/disclosure and no horizontal overflow; screenshots inspected. | `preset-browser-results.json`, `verify-presets.mjs`, ten screenshots |
| Database contract | SQLite upgrade from the preceding multi-document migration preserves an old preset row and its template; SQLite and SQL Server snapshots match current models; generated SQL Server migration script includes the new columns/receipt table. | `ReusablePromptContractTests.SqliteUpgradePreservesOldPresetRowsAndBothProviderSnapshotsMatchCurrentModels`; migration logs |
| Windows/client Release builds | Both **0 warnings, 0 errors**, rebuilt sequentially with isolated output. | `desktop-build.log`, `client-build.log` |
| Dirty checkout and whitespace | Initial fingerprints were audited; intended additions/edits only, no missing pre-existing files, pre-existing deletions retained; `git diff --check` passed. | `initial-status.txt`, `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

Fixtures use synthetic accounts, isolated temporary manuscripts/SQLite databases and mocked transport/provider/editor callbacks. The transfer handler exercises actual controllers with fresh SQLite contexts, including mutation-before-response-loss. The actual executor check confirms mapped custom results produce no flat mutation operation. Static Edge fixtures establish HTML keyboard/layout behavior rather than live Blazor or Windows events; separate shipped-editor checks exercise the real JavaScript adapters. Test compilation retains the existing NU1900 vulnerability-feed connectivity warning and xUnit2031 in `LocalStoryboardTests`; Release builds are clean. No user manuscript, provider credential, external registration, deployment or production database was changed. No editor source/schema or generated bundle changed; only its real harness gained two cases. The owned fixture server was stopped after verification.

To reproduce: run the broad prompt-8 test filter plus `Prompt` and `MigrationSafety`, using `--configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p09/ --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p09`. Set `WRITERAPP_P09_EVIDENCE` to the absolute artifact directory for component HTML. The focused filter is `ReusablePrompt|LocalPromptPanel|LocalWritingPanel|ReusablePresetExecution|CustomTransformActionTests`. Build Windows/client sequentially with the same output parameter. Serve the bounded editor fixture on port 5194 and run `verify-editor.mjs`; run `verify-presets.mjs` for the component layout fixtures. Migration generation used the cached EF 10.0.12 tooling against the isolated server build and did not apply to any live database.

The real authenticated HTTP handler now captures account generation before token acquisition and rejects a principal change before sending a private payload; tests cover both explicit transfer generation and ordinary requests. Same-account token notifications preserve active transfers and writing reviews. The library follows newly acknowledged project identity without reopening. Streaming response limits apply before buffering, and malformed/null/oversized stored cloud parameter JSON yields an actionable error without replacing the previous device cache or server record.

### Remaining live gates and next handoff

- **Backend rollout:** matching versioned transfer/writing routes and SQLite migration `20261003125036_ReusablePresetTransfers` or SQL Server migration `20261003125046_ReusablePresetTransfersSqlServer` are required before real cloud transfers. Retain operation receipts while clients may retry. Live SQL Server parallel update/delete/replay behavior and deployed authentication remain unverified; the model/script and SQLite fixtures passed. Background synchronization is not enabled.
- **Native Windows:** no running desktop process was found. The fresh candidate is `WriterApp.Desktop/artifacts/desktopai-p09/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; it was not launched because native control APIs are disabled. Use that exact output with isolated Development data to verify Advanced/Prompt library, real selection capture and section Apply, window close/restart/approved-save recovery, keyboard/scrolling, offline/expired account, conflicting cloud CRUD and account switching. `environment.json` records this boundary.
- **Authenticated provider/client:** verify real revision quality, strict custom variables, supported rich runs, language/voice preservation, quota/provider limits, cancellation and stale sync/target behavior. Exercise competing real client/device writes/deletes and lost HTTP responses after committed transfers. Legacy client document-as-project binding, ignored raw parameters, in-memory pins and unguarded CRUD require explicit comparison/repair in prompt 13; no live parity claim is made here.
- Packaged Windows/iOS and integrated cross-host acceptance remain later gates. Cloud/local AI history reconciliation and applied-event reporting remain prompt 10; local writing history already retains the immutable preset definition.

Next: **prompt 10 — cloud AI history alongside durable local history**. Continue in this same checkout with `Implement prompt 10 in docs/desktopai-prompts.md, including the common requirements.` Retain all previous manuscript/planning/recovery safeguards, version-2 presets and version-1 transfer intents/receipts. Preserve additive optional `LocalAiHistory.Preset` metadata on version-3 writing records, and do not confuse queue receipts with server AI proposal/applied IDs. Keep explicit preset transfers and scoped conflict resolution intact while implementing cloud history delivery/reconciliation; prompt 13 owns the final gap table and live acceptance.

## 2026-10-03 — Prompt 10: cloud history alongside durable local recovery

Implemented owned cloud AI history inspection and an account/backend-isolated offline cache, using the shared history comparison UI. Origin and check time are visible. Freshness distinguishes the last acknowledged cloud source from unsynchronized local changes. Proposal/document/section/page/action/source identities reconcile corresponding entries; local status and recovery remain authoritative. Older unlinked local records retain their bytes and are not matched by text. Cloud-only results remain inspection-only, with a capability explanation and no local Undo/Redo/Recover action. The combined list and cloud response are limited to 200 entries; overflow is disclosed and originals are retained. The bounded server set is selected by proposal ID, then sorted by creation time, rather than represented as the globally latest history.

Writing/presets/translation, generated style fixes, consistency revisions, scene fields, synopsis and storyboard analyses retain generated proposal identity and typed source/target metadata. Local deterministic style fixes keep their local origin. Suggested-scene creation retains an approved intent and a post-save outcome without inventing reversible cloud operations. Local snapshot/content/structure guards still protect scoped Undo/Redo and recovery copies; linked records additionally require the original account/backend. Later edits are preserved. Interrupted scene creation is inspectable and must not be blindly repeated.

Optional origin and delivery metadata are additive to local history versions 1–5. Durable terminal completion writes its immutable ordered event intent into the same recovery file. Applying/Undoing/Redoing never count as completed cloud outcomes. The scoped reporter attempts delivery after the local file is durable; reporting failure cannot roll back writing. The visible Report saved events action retries the same persisted intent after response loss or restart. Each receipt confirms operation, proposal/local entry, sequence, state and full request fingerprint. Account switches cannot send a stale private payload or store its receipt/cache into another scope; the UI clears prior comparisons before asynchronous refresh. Ordinary same-account token refresh does not cancel a review.

The additive history reporting route checks owned document/proposal, source/action/target identity, entitlement and predecessor sequence in a serializable transaction. Owner/operation receipts and owner/local-entry/sequence are unique. Replays return the same receipt even after later events; altered payloads, stale source identities and out-of-order transitions are refused. Events are kept apart from legacy cloud undo snapshots. The web history listing includes device outcomes while explicit CanCloudUndo/CanCloudRedo flags prevent device-only reports from enabling unsupported web undo/redo. Legacy cloud snapshot behavior and the web client's own new Apply remain compatible; legacy one-shot applied delivery and current-content guarding remain separate integrated acceptance gaps.

### Final available verification

Evidence: `artifacts/desktopai-p10/`. The test build retains only the existing NU1900 vulnerability-feed connectivity warning and xUnit2031 in LocalStoryboardTests. Final Release builds are clean.

| Check | Final result | Evidence |
|---|---|---|
| AI/canon/quality/consistency/scene/synopsis/storyboard/editor/history/authentication/onboarding/translation/writing/preset/migration regression | **869 passed, 0 failed/skipped** | `regression.trx`, `regression.log`, `test-summary.json` |
| New history behavior and actual components | **32 passed, 0 failed/skipped** | `history.trx`, `history.log`; `DeviceAiHistoryTests`, `DeviceAiHistoryPanelTests` |
| Delivery and authorization | Lost committed response, exact restart replay, immutable identities/receipts, sequence and ownership refusals, original account requirement, switched read/report, old backend, plan denial and bad receipts preserve local files and pending events | Actual authenticated handler, fresh SQLite controller contexts and bounded mocked HTTP transport in `HistoryTestFixture` |
| Local application and recovery | Real writing preparation/generation/Apply retains origin; automatic reporting failure leaves completed writing intact. Applying remains unreported. Undo/Redo/restart reports terminal states; stale undo refuses; actual History callbacks create a separate original copy | `DeviceAiHistoryTests`, actual `LocalAiPanel` handlers; broad pre-existing scoped recovery tests |
| Reconciliation and client capability | Local/cloud match produces one combined row, cloud-only comparisons have no undo/recovery, offline cache persists, account switch clears immediately; actual web availability method honors explicit capabilities and permits its own new Apply | `DeviceAiHistoryPanelTests`, `RealClientAvailabilityUsesExplicitCloudCapabilitiesAndKeepsItsOwnNewApplyUsable` |
| Invalid data and retention bounds | Null/duplicate/wrong-document/oversized comparisons refuse cache replacement; oversized stored results and history overflow retain server records; legacy history reads preserve bytes | `DeviceAiHistoryTests` |
| Shipped editor bundles in headless Edge | **60 passed, 0 failures/errors**; existing selection/section application, rich formatting, reopening and keyboard boundaries | `browser-results.json`, `browser.log`, `verify-editor.mjs`; editor screenshots |
| Actual Razor markup in headless Edge | **8 fixtures passed** at 1280/480: combined, offline, reporting error and switched account; disclosure/keyboard, inert stored script text, cloud recovery absence, no horizontal overflow; screenshots inspected | `history-browser-results.json`, `verify-history.mjs`, `history-*.html/png` |
| Database compatibility | Upgrade from prompt-9 SQLite migration retains preset receipt; both provider snapshots match current models; generated SQL Server script contains only additive history ledger/index work | `UpgradePreservesPresetReceiptsAndBothProviderSnapshotsMatch`; migration logs and generated files |
| Windows/client Release | **0 warnings, 0 errors**, sequential isolated builds after final source tests | `desktop-build.log`, `client-build.log` |
| Source preservation | Initial dirty/untracked fingerprints audited; prior deletions retained; only intended integration/docs/migrations/tests changed; `git diff --check` passed | `initial-status.txt`, `initial-fingerprints.json`, `preservation-audit.json`, `diff-check.log` |

These checks use synthetic identities, isolated temporary manuscripts/databases and a mocked provider. HTTP delivery exercises real authentication guards/controllers/transactions, including committed-but-lost responses. Static browser fixtures are actual rendered Razor output with headless keyboard/layout checks, not live Blazor, native Windows or a deployed backend. The editor harness executes shipped JavaScript independently. No editor source/schema or generated bundle changed. No user manuscript, live provider credential, external registration, deployed service or production database was modified. The owned bounded editor fixture server on port 5179 was stopped.

Reproduce: use the preceding broad regression filter with `Prompt`, `MigrationSafety` and all AI/history cases, `--configuration Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p10/ --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/desktopai-p10`. Focused filter: `FullyQualifiedName~DeviceAiHistory`. Set `WRITERAPP_P10_EVIDENCE` to the absolute evidence directory for component fixtures. Build Windows/client sequentially with the isolated output parameter. Run the bounded editor fixture server and `verify-editor.mjs`; run `verify-history.mjs` on the rendered component fixtures. Cached EF 10.0.12 tooling generated migrations without applying them to a live database.

### Remaining live gates and next handoff

- **Backend rollout:** publish the new owned history/reporting routes and apply SQLite `20261003145238_DeviceAiHistoryReporting` or SQL Server `20261003145251_DeviceAiHistoryReportingSqlServer`. Keep event ledger receipts while device retries are possible. Live SQL Server competing delivery/sequence behavior, live history entitlement and deployed authentication remain unverified. Missing routes retain local writing and delivery intents.
- **Native Windows:** no running desktop process was found. The rebuilt candidate is `WriterApp.Desktop/artifacts/desktopai-p10/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; native control APIs are disabled and it was not launched. In isolated Development data, verify History load/cache/error/offline/keyboard, source freshness, real Apply/Undo/Redo/restart/reporting recovery, account switching, close/relaunch and actual suggested-scene creation. The environment record identifies the rebuilt output.
- **Authenticated backend/provider/client:** verify provider-generated source/target identities for all actions, client/device concurrent histories and local application followed by interrupted reporting. Exercise separate client legacy applied/undo/redo/current-content behavior; a server-generated comparison or aggregate applied count is not proof of a reversible cloud change. Cloud-only inspection remains deliberately non-reversible until exact local snapshots and mapping establish safety.
- Packaged Windows, iOS and integrated cross-host acceptance remain prompt 13. Local history has no automatic retention deletion; capped inspection and explicit bounded retries do not discard pending/recovery evidence.

Next: **prompt 11 — AI cover concepts and durable selection**. Continue in the same checkout with `Implement prompt 11 in docs/desktopai-prompts.md, including the common requirements.` Preserve prompts 2–10, local history versions 1–5, optional immutable origin/delivery/preset metadata, version-1 reporting/cache receipts and prompt-9 preset transfer identities. Use existing owned cover endpoints, validated durable asset storage, explicit selection/save/recovery and publishing integration. Do not equate a cover adjustment button or concept preview with persisted selection. Prompt 13 owns final integrated live acceptance and the gap table.

## 2026-10-03 — Prompt 11: AI cover studio, recovery and publishing

**Status: implementation complete at the bounded PNG scope; available automated checks pass. Native, deployed-account and live image-provider acceptance remain open.** The desktop now exposes `/documents/{id}/cover` from a project document and Publishing. Generation → cached concept preview → explicit selection → explicit Save → restart → offline publishing is connected. No live provider request, deployment, database migration, user-document edit or desktop launch was performed.

### Implemented behavior and boundaries

| Area | Implemented behavior |
|---|---|
| Shared presentation | `WriterApp.UI.Shared/CoverBrief` and `CoverConcepts` provide description, genre, mood, visual style, palette, numbered keyboard-accessible selection, selected preview and busy/disabled states. Both clients use them. Client variation/darker/brighter/cinematic/minimal adjustment stubs are removed from the reachable workflow. “Choose another brief” honestly changes choices only. |
| Authenticated generation | The existing `api/covers/generate`/`CoverImageService`/server-owned credentials and image usage policy are reused. The desktop sends additive contract version 1 with cloud project/document identity, acknowledged document version and project metadata revision. Server ownership, association, active lifecycle and revisions are checked before and after generation. The exact source is echoed; missing/legacy/mismatched contracts fail without replacing cached concepts. UI token/plan information is advisory; server image policy remains authoritative. |
| Validated assets | The existing local PNG validator is extracted into `CoverStudioContract` and strengthened with encoding/depth checks, CRCs, complete chunks, no trailing bytes and bounded zlib pixel/filter decoding, including Adam7. 1–4 concepts, each at most 2 MiB decoded, are accepted from bounded 16 MiB JSON. The dedicated native authenticated cover client has a three-minute deadline, no redirects/cookies, and expected account generation captured before token acquisition. Unexpected remote URLs, SVG/HTML, MIME mismatches, malformed base64, invalid PNGs and oversized/interrupted responses fail safely. No external image URL is fetched or given credentials. |
| Durable preview and selection | Version-1 `LocalCoverDraft` assets, stable identity, selected index, project/document association, source revisions and time persist atomically under `cover-studio/<backend-account-scope>/<document-id>.json`. Restart/offline viewing works for the original account. Account switching clears private concepts and cancels work; ordinary token renewal does not. Old-project previews are hidden after movement, with their files retained. Stale previews are disclosed and cannot be saved. Dismiss closes the preview without changing the project; retained concepts can be inspected on reopening. |
| Explicit project save and recovery | `SetProjectCoverAsync` checks current document/project revisions, deletion and conflict state under the existing cross-process lock. A single atomic shared project metadata replacement stores the chosen valid PNG, prior cover (including no cover), stable change ID and incremented dirty metadata revision. Metadata version 1 remains readable; cover saves write version 2. Restore exchanges current/prior covers with the same checks. No manuscript page/content/revision is changed. Completed concept saves are recognized on exact replay. Interrupted/cancelled staging preserves old cover/recovery; a commit already completed remains durable and recoverable. |
| Supported sync | The chosen inline cover uses existing project-aware v4 sync, metadata concurrency, account bindings and operation receipts. The request envelope permits up to 5 MiB, with writing/planning still limited to 2 MiB after removing the validated inline cover; decoded PNG remains limited to 2 MiB. Invalid or oversized payloads cause no server mutation. Prior-cover recovery stays local and survives normal metadata acknowledgement/download. The existing web save route now accepts an expected metadata revision from `ProjectDto` and performs an owned SQL compare-and-swap, detecting competing saves. No new DB schema is required. |
| Publishing and existing covers | Include cover embeds the saved project PNG in HTML, DOCX, EPUB and the existing Windows PDF path without networking. Sibling documents receive the same project asset with their own publishing association. Choosing a local PNG remains supported and explicitly changes the project cover when applicable; standalone/document-local covers remain available. Remote saved covers are preserved and disclosed. Invalid legacy project cover media leaves the local chooser accessible and export fails with guidance until a valid asset is selected. Text/Markdown retain their existing no-cover restriction. |

### Available verification

| Check | Current result / evidence |
|---|---|
| Final AI/editor/project/publishing/cover regression selection | **997 passed, 0 failed, 0 skipped**. `artifacts/desktopai-p11/regression.trx` and `regression.log`. Extends the preceding AI/editor selection with Cover, Publishing, DocumentSync, LocalProject, MultiDocument and StandaloneProject cases. |
| New cover behavior cases in that run | **31 passed** across `DeviceCoverStudioTests`, `DeviceCoverStudioPanelTests`, `CoverProjectSyncTests` and `ProjectCoverConcurrencyTests`. `verification-summary.json` identifies the count. Cases cover owned/stale/changed source checks before/after provider work, exact response contracts, invalid media/checksums/compressed pixels, response interruption/size, account switching during generation and atomic save, cancellation, deletion, changed/moved targets, restart/offline cached selection, sibling project cover sharing, recovery to no cover, CAS competing writers, and real project-sync round trip/conflicts. |
| Export bytes after restart | The exact second selected PNG, different from the prior first concept, is verified in HTML, the EPUB cover entry and the DOCX image part. Writing remains equal before/after cover save. This verifies produced bytes, not native file-dialog saving or Word/ebook appearance. |
| Final focused check after publishing fallback refinement | **14 passed** (local publishing plus actual cover Razor event flows), `final-publishing.trx`/`.log`. A preceding focused cover/publishing/sync iteration passed 127 cases; the final regression above also includes the two later asset-envelope/pixel-data cases. |
| Actual Razor event workflow fixtures | `cover-empty`, `cover-concepts`, `cover-saved`, `cover-offline`, `cover-plan`, `cover-error`, `cover-signed-out`, `cover-loading`. Real component handlers exercise generation, selection, save, restore, cancellation and account clearing against isolated synthetic assets and the real guarded generation controller. |
| Headless Edge layout/keyboard/image checks | **16 passed**: those eight rendered Razor states at 1280 and 480 pixels, all images decode, no page errors/overflow, reachable publishing/brief/cancel controls and correct empty/busy/disconnected/plan/error states. `verify-cover.mjs`, `cover-browser-results.json` and PNG screenshots; narrow concept and wide saved screenshots were visually inspected. These are static rendered fixtures, not a native WebView or live Blazor/provider session. |
| Windows Release build | Successful, **0 warnings / 0 errors**, isolated `WriterApp.Desktop/artifacts/desktopai-p11/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; `windows-build.log`. Not launched. |
| Client Release build | Successful, **0 warnings / 0 errors**, `client-build.log`. Shared/client/server libraries are also compiled by the test build. |
| Existing warnings / preservation | Test compilation retains the preceding unreachable NuGet vulnerability-feed warning and existing `LocalStoryboardTests` xUnit2031 warning. No new build warning. The final audit retains all six pre-existing deletions and all prior phased work; only named prompt-11 integration paths differ. No editor source/schema/generated bundle changed. See `initial-status.txt`, `initial-fingerprints.json`, `preservation-check.json`, `final-status.txt` and `diff-check.log`. |

The PNG fixtures are synthetic blue/red images, and all test document/account/backend data is isolated. No fixture output establishes production image generation. An early fixture typo was rejected by CRC validation and corrected; subsequent failures in new assertions were corrected without weakening validation. The real export test now reads DOCX image parts rather than assuming their package directory. Available checks establish implementation behavior at these exercised boundaries.

### Remaining live gates and next handoff

- **Backend/account/provider:** deploy the new guarded cover contract and cover-aware sync envelope with the preceding prompts' required migrations. In an authorized Professional test account, verify actual image quota/denials, real PNG generation, server source changes while generation runs, provider timeout/cancellation and malformed/remote-only responses. Remote-only generation output has an explicit capability limit; trusted materialization of other media is a separate scope.
- **Native Windows and actual client:** native control APIs are disabled, so the rebuilt desktop candidate was not launched. Relaunch that exact binary with isolated Development data; verify keyboard/focus/loading/cancel/disconnect, save/restart/restore, account switches, auto/manual v4 sync conflicts and actual client concurrent cover saves. Automated component/rendered fixtures do not establish native or authenticated deployed UI acceptance.
- **Physical publishing:** use the native PNG chooser, export dialogs and PDF adapter; open saved HTML/DOCX/EPUB/PDF outputs in their real readers. The PNG/dimensions/byte assertions are not a native save, PDF rendering or final typography/layout gate. Existing remote covers require choosing/saving a valid PNG for offline publishing.
- **Retention:** no automatic cache/recovery deletion was introduced. Dismiss retains the last valid previews; a new successful generation atomically replaces that document/account's preview set. Project recovery retains the immediately previous cover and can exchange it with the current cover. It is not a multi-version asset library.

Next: **prompt 12 — Guided desktop AI onboarding**. Continue in this same dirty checkout with `Implement prompt 12 in docs/desktopai-prompts.md, including the common requirements.` Reuse the writing/synopsis/prompt/history paths and the cover entry `/documents/{id}/cover`; no tutorial opening may generate images or grant entitlement. Preserve version-1 cover preview caches, project metadata versions 1/2, prior-cover recovery, v4 metadata conflicts, all prompts 2–10 and existing local PNG/PDF publishing. Keep skip/resume/restart and isolated starter content durable without reseeding user writing. Prompt 13 still owns final integrated client/native/provider acceptance and the final gap table.

## Prompt 12 — Guided desktop AI onboarding (2026-10-03)

**Implementation complete; available automated checks pass. Native/deployed/provider acceptance remains open.** This work continues the same dirty checkout and preserves prompts 1–11.

### Implemented behavior and client comparison

| First-use capability | Desktop implementation and boundary |
|---|---|
| Reachable first-use guidance | Library banner, persistent navigation and `/ai-guide`; no forced onboarding redirect or sign-in before local writing. Shared `AiFirstUseGuide` supplies five steps. |
| Skip/resume/restart/completion | Version-2 durable local state with stable IDs, step/status, atomic writes, revision checks and process locks. Restart changes guidance only. Completion means guidance completed, not an AI call passed. |
| Starter-content preservation | Explicit creation of a separate labelled practice project; stable ID reserved before creation and provenance checked on retry. Revisit, empty/edited writing, migration, Trash and missing files never reseed a registered practice manuscript. |
| Selection/preset/request | Links reach the normal Writing tools. The author selects a passage/preset or a whole-section action, signs in/connects/enables sync when ready and deliberately submits. Existing save/source/version/provider/entitlement/quota paths are retained. |
| First demonstration authorization | Deliberate practice Shorten section adds the existing `OnboardingAiDemoRequest` flag. Backend eligibility and quota remain authoritative. The local practice marker grants nothing; local sample content lacks the web bootstrap's server-owned demo metadata and normally needs available plan quota. No bootstrap, entitlement override or automatic provider call was added. |
| Comparison/Apply/Dismiss/Undo | Uses the existing production writing review and durable History actions. The guide does not implement a second mutation path. Selection, account/source/expiry guards and recoverable approval remain in force. |
| Optional next actions | Reachable Story/Synopsis, Advanced/Prompt Library and `/documents/{id}/cover` links. None has to generate or complete before finishing guidance. |
| Account switching/offline | Independent guest/account/backend guidance; explicit continuation of a labelled guest practice document after sign-in. Current-scope checks prevent stale navigation/state adoption. Local writing stays usable offline. AI availability/error/cancellation comes from the normal panels. |

Client inspection traced `OnboardingService`, `OnboardingStateStore`, overlay callbacks, `DocumentEditor`'s deliberate tighten demonstration, server bootstrap, eligibility and quota paths, and existing starter-content preservation tests. The web flow owns a server bootstrap project and server profile completion; desktop guidance instead owns local progress and a separate local-first sample. It does not synchronize its completion into the server profile or claim the server's free demonstration allowance for a local marker. Existing client content-preservation behavior was retained rather than copying tutorial-time editor initialization.

The guide lives in the practice editor's existing scrollable context panel. Its Writing/History/Synopsis/Prompt links use existing `panel` and `view` parameters. The normal library, standalone documents, project documents and cover/publishing paths remain reachable. Unknown/corrupt/oversized/mismatched state is preserved and can be retried; migration keeps a source backup and treats existing linked sample IDs as already created. No backend schema migration, editor-bundle regeneration or live document change was required.

### Current verification

Evidence is in `artifacts/desktopai-p12/` (ignored local evidence).

- **Focused onboarding/writing:** 81 passed, 0 failed/skipped in `focused.trx`. The final broad regression includes the later migration/retry refinement. New coverage totals 17 cases across `DeviceOnboardingTests` and the production `LocalWritingPanelTests`: rich/Unicode/empty content, skip/resume/restart/completion, interrupted creation, Trash/missing content, wrong provenance, account/backend isolation, explicit guest continuation, stale-window state, migration backup, invalid/bounded state, actual Razor controls, deliberate demo parameter, explicit review/Dismiss/Apply and durable Undo.
- **AI/editor/planning/sync regression:** final 1,014 passed, 0 failed/skipped in `regression.trx` using the prior prompt's AI/scene/synopsis/continuity/bible/quality/consistency/storyboard/editor/prompt/onboarding/authentication/translation/writing/migration/cover/publishing/document-sync/project/multi-document/standalone filter. The test build still reports the existing unreachable NuGet vulnerability-feed `NU1900` and `LocalStoryboardTests` analyzer warning; these are not new feature failures.
- **Actual Razor render browser evidence:** 27 headless Edge checks, nine states at 1280/480/360 × 720: five steps, skipped, completed, account continuation and recovery error. Every enabled guide link/button was keyboard reachable, focus visible where checked, and there was no horizontal overflow. `verify-guide.mjs`, `guide-browser-results.json` and screenshots retain exact results. The narrow review/undo fixture was visually inspected. These are rendered component fixtures, not a running native desktop or full interactive Blazor host.
- **Shipped editor assets:** 60 existing browser harness checks passed, plus the exact stored practice sample's passage selection, inert mapped review, explicit content adoption and original restoration. `verify-practice-editor.mjs` and `practice-editor-results.json` record the headless Edge host using both shipped device/web editor assets. Durable history Undo was separately exercised through the production .NET actions. The bounded fixture server used port 5179 with a successful bind; listener inventory was denied, so no pre-launch listener inventory is claimed. The owned exec session was stopped after verification.
- **Builds:** final Windows desktop and web client Release builds passed with 0 warnings/errors, sequential isolated `-p:BaseOutputPath=artifacts/desktopai-p12/`. This also builds the affected Device.Shared/UI.Shared libraries. Logs are `windows-build.log`, `client-build.log` and the earlier `device-build.log`.
- **Checkout preservation:** baseline hashes cover 214 already dirty paths. The final audit records 204 unchanged baseline paths, 10 intentionally extended feature/docs paths, no missing baseline paths, nine new or previously clean changed paths, and retention of all six pre-existing deletions. `baseline.json`, `baseline-audit.json`, status files and `diff-check.log` provide the exact audit. No commit or deployment was performed.

Rebuilt candidate: `WriterApp.Desktop/artifacts/desktopai-p12/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. It was **not launched**. Native computer APIs are disabled in this session, and no running-binary inspection or native acceptance is claimed.

### Remaining live gates and next handoff

- Relaunch that exact candidate in an isolated Development data root. Verify real heading focus, selection retention through preset controls, context-panel scrolling at small windows, guide navigation while planning is dirty/AI is pending, restart/recovery, signed-out and disconnected library/editor use, and switching real accounts without stale proposals or guide navigation.
- With an authorized backend/test account, verify deliberate first requests, expired identity, provider/quota denials, cancellation, full review/Apply/Undo, and successful account/backend-isolated history reporting. The web bootstrap allowance must continue to depend on authorized server demo metadata and quota; a locally labelled project must not qualify by its marker alone. No live provider, bootstrap request or external authentication change was made here.
- Preserve preceding native sync/conflict, provider, migration/deployment and physical publishing gates. Prompt 12's automated samples and static fixtures do not close those gates.

Next: **prompt 13 — Integrated client/desktop acceptance and final gap table**. Continue in this same checkout with `Implement prompt 13 in docs/desktopai-prompts.md, including the common requirements.` Reuse `/ai-guide`, normal production writing/History, the optional synopsis/prompt/cover entries and `ai-onboarding` progress. Preserve stable sample identities, no-reseed behavior, source backups and explicit guest continuation. Preserve all prior versioned bibles, planning, presets, history, covers, recovery and v4 metadata conflicts. Keep implementation, automated/browser evidence, native acceptance and authenticated deployed/provider acceptance separate in the final gap table.

## Prompt 13 — Integrated acceptance and final comparison (2026-10-03)

**Available implementation/audit/automated work complete; full parity and release acceptance remain open.** The final [gap analysis and release handoff](desktopai-gap-analysis.md) records every original benchmark row, source pointers, current test evidence, supported scopes, client limitations, stable defect IDs and independent live gates. It contains the repository acceptance profile and seven exercised flows. Historical prompt evidence above remains intact.

### Repairs in this run

- **P13-001, writing availability:** the full initial suite exposed the stale open-editor sync test. Production `DocumentWorkspace` now supplies cloud enrollment to `LocalWritingPanel`, which refreshes on account/enrollment transitions, clears signed-out availability and rejects cancelled/late responses from previous accounts. The test fixture uses authenticated availability and waits for child refresh. New sign-in/late-response checks prove no provider generation is triggered by refresh.
- **P13-002, SQLite web save:** a new normal non-demo production-controller HTTP integration test exposed unsupported `DateTimeOffset` SQL ordering in version-history pruning. The fix filters the owned page in SQL and orders its versions in memory, retaining a deterministic ID tie-break. Another owner's page cannot be pruned. A second SQLite test checks mixed UTC offsets, retention/count and ownership.
- **P13-003, unsafe legacy web broader translation:** source tracing confirmed flattening/first-page writes, missing-section skips and misleading Applied reporting. Web section/document replacement and duplication are now disabled with readable guidance and rejected in both handlers before editor flush, HTTP writes or applied reporting. Preview/copy/discard and selection translation remain. Nine tests verify retained source/proposal and zero requests. **This contains unsafe writes; a complete structured web aggregate Apply/recovery workflow remains a functional gap.**

The shared/device dependency boundaries, provider credentials, account/backend isolation, explicit review, local history and all preceding prompt implementations were preserved. No editor algorithm/generated asset changed. No user document or external authentication registration was edited.

### Fresh checks and scope

Evidence root: `artifacts/desktopai-p13/`, with a checked-in [browser runner](../WriterApp.Client/tests/desktopai-acceptance.mjs). Exact reproduction commands are in the gap analysis.

| Check | Result | Evidence |
|---|---|---|
| Final full Release server/shared/client/device test assembly, unfiltered | **1,434 passed, 0 failed/skipped** | `final-tests.log`, `final.trx`, `test-summary.json` |
| Earlier initial full suite | 1,420 passed, 1 failed; subsequently repaired | `full-test.log`, `full.trx`; retained historical failure |
| Non-demo normal web persistence | Actual authorized page PUT/GET through production MVC/repositories and isolated SQLite; host restart retains rich Unicode, 3 pages/2 sections and IDs, untouched pages, project primary identity, authored scene metadata and scene mirror; anonymous 401/foreign-owner 404 | `DesktopAiWebPersistenceTests`, `normal-web-persistence.json`; synthetic authentication, not deployed web/OIDC acceptance |
| Real shipped device and client editor harness | **60 passed**, no page errors | `browser-results.json`, `browser-check.log` |
| Actual Razor review/state fixtures in Edge | **84 renders**: 42 current fixtures at both 1280×720 and 1920×1080; readable text/status, keyboard access to enabled controls and no horizontal overflow | `p02`–`p12`, `screenshots/`, `browser-results.json` |
| Comparable shipped-editor practice/rich Unicode fixture | **4 checks**: device/client at both sizes; inert preview, explicit application, run/format retention and reopen | `browser-results.json`, `screenshots/{device,client}-editor-*.png` |
| Final Release server, client, Device.Shared and Windows builds, sequential | **All passed, 0 warnings/errors each** | `server-build.log`, `client-build.log`, `device-build.log`, `windows-build.log` |
| Available iOS managed `iossimulator-x64` build on Windows | **Passed, 0 warnings/errors**; no native bundle/signing/simulator/device claim | `ios-build.log` |

The test build encountered the existing unreachable NuGet vulnerability feed (`NU1900`) and `LocalStoryboardTests` analyzer warning (`xUnit2031`); final explicit builds were clean. No live dependency-feed freshness is established. Initial failing fixture logs and the intermediate 1,425-pass suite are retained; the final 1,434 count includes the added web safety tests.

Production Razor component handlers, isolated local/server persistence, synthetic HTTP/provider seams and both real editor bundles were exercised. Fresh repository/host instances verify save/reopen and recovery. Component tests may stub editor interop; browser tests independently exercise the actual shipped schema/mapping code. No real provider, full authenticated web shell or native desktop process was exercised. Screenshot filenames identify fixtures: exact requested viewports plus optional full-page review captures, not native screenshots. Solid-color cover inputs establish selection/storage behavior, not image quality.

### Binary and checkout evidence

Before rebuilding, `Get-Process -Name WriterApp.Desktop` found no matching process. Known existing Debug/Release/published/validation paths were recorded and left untouched. The final isolated Release candidate is `WriterApp.Desktop/artifacts/desktopai-p13/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; its path/timestamp/hash are captured in `native-candidate*.json`. It was **not launched**: native computer APIs are disabled here. A separate Development build with an absolute isolated data directory and confirmed authorized backend is required for native acceptance; do not treat the default production candidate as an isolated-data validation build.

Baseline hashes cover 223 already dirty paths. The final audit retains unrelated baseline files and all six preexisting deletions, and identifies only the deliberate source/test/docs extensions and new or previously clean p13 files. `baseline.json`, `baseline-status.txt`, `baseline-audit.json`, `final-status.txt` and `diff-check.log` retain the exact checkout evidence. No commit, deployment or external database migration was performed. The owned bounded browser fixture server was stopped after verification; its port was probed afterward.

### Remaining work and release handoff

The gap analysis retains these functional limitations: structured web broader translation Apply/recovery (P13-003), server-demo onboarding equivalence (P13-004), remote cover materialization (P13-005) and complete writing outline-context equivalence (P13-006). Cover variation/adjustment operations remain unavailable **in the client reference as well as desktop**. Explicit prompt transfers are not automatic authored merging; cloud-only history is not local Undo authority.

Native Windows keyboard/selection/drawers/loading/offline/save-close-relaunch at both sizes, real account switching, authenticated deployed/provider behavior and prose/language/image quality remain **P13-G01/G02**. SQL Server/deployed migrations, normal non-demo authenticated web browser persistence, durable legacy web applied reporting and physical publishing remain **P13-G04**. iOS native identity, Mac bundle/signing/simulator/device and Windows packaging remain **P13-G03**. Managed compilation, synthetic provider outcomes and generated exports do not close those gates.

Next concrete prompt: **complete P13-003 using the existing complete typed page/run translation contract and recoverable aggregate web persistence, retaining the safety block until verified**, as written at the end of `desktopai-gap-analysis.md`. Continue in this same checkout and preserve all preceding prompts. Execute the documented native/live gates only when the necessary authorized environment is available; append evidence and update statuses rather than rewriting historical checks. Full parity must not be declared while these functional gaps and required acceptance gates remain open.

## Prompt 14 — Safe structured web translation (2026-10-03)

**Supported P13-003 implementation and available automated checks complete. Native/deployed/provider language acceptance remains open.** This run preserves the preceding dirty checkout and the safety block for old plain-text broader proposals.

### Implemented behavior

- Web section/document generation captures every ordered saved section/page/run, including blank pages, with owned account/document/project, language, source version and authored graph fingerprint. Both hosts reuse the actual editor mapping source. New server preflight verifies the web snapshot and saved HTML before provider execution. Unsupported source/backends fail with guidance. Existing authenticated entitlement/quota policies and selection translation remain.
- The shared review shows inert per-page Original/Proposed text. Copy and Dismiss remain available. Replace/duplicate-section/duplicate-document are enabled only for a verified new typed preview; missing/reordered/duplicate/foreign/language-mismatched/malformed results cannot acquire an approval. Settings, account, backend and source are pinned; cancellation/late results cannot restore an older preview. The unsafe first-page/plain-text Apply helpers were removed.
- Owned `WebTranslationOperations` persists an immutable approval and complete original writing recovery evidence before page commit. Serializable transactions use the existing sync-clock lock and compare source version/fingerprint. Owner/operation and owner/proposal uniqueness prevent changed retries or another copy from the same proposal. Replacement preserves all page/section IDs, formatting, titles and unrelated planning. Copies preserve page ordering, blank pages, section purposes, scoped scene metadata/hierarchy and scene mirrors with new IDs; language/translation groups are explicit. The source project's primary manuscript stays intact.
- Commit changes all intended writing and its receipt atomically. An injected failure on the second page leaves every original page unchanged and the approval retryable. Source changes between approval and commit fail closed. Lost acknowledgement pauses editing; Reload recovery reconciles a committed receipt, or Finish approved save retries the original approved operation without another generation/copy. Confirmed terminal receipts, rather than best-effort client reporting, establish aggregate Applied history.
- Recover original as separate document uses a durable reserved ID and the retained complete original graph. Restart/retry does not duplicate or reseed the recovery copy, and recovery never overwrites current writing. This is the explicit recoverable-original-copy option; web aggregate in-place Undo/Redo is not supplied. Aggregate snapshots never enter legacy page-HTML Undo/Redo. Desktop's existing local translation recovery/Undo/Redo is preserved.
- Browser testing exposed a nested-image preflight gap: schema parsing could discard a block image inside a paragraph. Shared capture now inspects an inert HTML template for images/code before parsing, preserving originals and refusing before generation. Both editor assets were regenerated from source; the web cache version was advanced.

### Fresh verification

Evidence: `artifacts/desktopai-p14/` (ignored local captures).

| Check | Result and scope |
|---|---|
| Full unfiltered Release .NET suite | **1,471 passed, 0 failed/skipped**, `full.trx` / `full-tests.log`. Includes prior P13-001/002 regressions and all desktop AI flows. |
| Changed behavior | 37 new cases: production web handler generation/review/Dismiss/Apply for all four modes; malformed/unchecked/cancelled/account/backend/stale responses; failed commit/lost acknowledgement/reload; production SQLite HTTP transactions/replay/host restart/blank pages/IDs/languages/scenes/recovery/history; invalid targets and source; ownership; pre-provider saved-HTML validation; SQLite upgrade and SQL Server script/model; inert per-page review. The nine legacy web safety checks remain passing. |
| Final shared review adjustment | One focused `WebTranslationReviewTests` pass in `review.trx` after adding desktop-size Original/Proposed headings and hiding the inapplicable paragraph/sentence toggle for mapped page review. |
| Shipped editor harness | **62 passed**, no page errors, both regenerated bundles. New checks prove identical web/device capture, inert page mapping, blank/rich reopen and unsupported/incomplete/reordered refusal. `editor-results.json` also verifies actual keyboard input is blocked while web editing is locked and works after unlocking. |
| Integrated browser profile | **86 actual Razor fixture renders** at 1280×720 and 1920×1080, plus **4 equivalent shipped device/client application/reopen checks**, all passing. Fresh p02–p12 fixtures and the new p14 per-page review retain readable status, reachable enabled controls and no horizontal overflow. `browser-results.json`, `browser-check.log`, `screenshots/`. The p14 review was visually inspected. These are component/editor fixtures, not a full authenticated web shell/native host. |
| Release builds | Server, web client, Device.Shared, Windows and available iOS managed `iossimulator-x64` builds pass, each 0 warnings/errors, with isolated `BaseOutputPath=artifacts/desktopai-p14/`. iOS compilation is not native signing/device acceptance. |
| Database | SQLite upgrade from the preceding history migration preserves existing writing and sync version. Production HTTP persistence uses isolated non-demo SQLite, actual MVC authorization/controllers and synthetic authentication/provider seams. SQL Server migration script/model passes without server execution. |

The test build retains the existing inaccessible NuGet vulnerability-feed `NU1900` and `LocalStoryboardTests` `xUnit2031` warning; explicit builds are clean. An intermediate test expected a concrete `ConflictObjectResult` where the controller correctly returned HTTP 409 via `ObjectResult`; that assertion was corrected without weakening the source guard. Earlier whitespace and harness-lifecycle fixture failures were also repaired. No provider quality, latency, live OIDC or deployed SQL Server acceptance is inferred from these synthetic checks.

Reproduction (prepare p02–p12 fixture directories and environment variables as in prompt 13, additionally set `WRITERAPP_P14_EVIDENCE` to the absolute evidence root):

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p14/ --logger 'trx;LogFileName=full.trx' --results-directory artifacts/desktopai-p14
npm --prefix WriterApp.Client run build
$env:PORT = '5184'
node WriterApp.Client/tests/serve-device-editor.mjs
# In another owned session, with PLAYWRIGHT_MODULE pointing to the available runtime:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5184'
node WriterApp.Client/tests/desktopai-acceptance.mjs artifacts/desktopai-p14
```

Build the same five projects sequentially using prompt 13's commands with `BaseOutputPath=artifacts/desktopai-p14/`. The isolated Windows candidate is `WriterApp.Desktop/artifacts/desktopai-p14/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. Process inspection found no running desktop process; the candidate was rebuilt and **not launched**. Native computer APIs are disabled. Use a separately configured Development build with isolated data and an authorized backend for actual native acceptance. The owned browser fixture server is stopped after verification. Baseline hashes, final audit/status and `diff-check.log` retain checkout evidence; no commit/deployment/user-document mutation was performed.

Checkout audit: 217 preexisting readable dirty files were hashed; 199 remain byte-identical, 18 were deliberately extended for this prompt, and none is missing. All six preexisting deletions remain intact. New files and previously clean modified assets are listed in the final status; no commit was made.

### Limitations, live gates and next handoff

Source limits remain 60,000 translated characters / 2,000 runs, 100 sections / 1,000 pages, 500,000 characters per captured page, a bounded 2,000,000-character saved graph, and 5,000 owned planning nodes / 2,000,000 planning-metadata characters. JSON and approval HTTP bodies are bounded. Shared schema/DOM text boundaries must agree; adjacent equivalent mark wrappers or incidental HTML whitespace may require an explicit normalizing editor save or a smaller selection. Images/code/comments/anchored annotations still require selection/text-only scope. Nothing is silently stripped. Review meaning and placement of translated marked phrases still require real language review.

Deploy the matching checked routes/assets and apply SQLite `20261003183221_WebTranslationOperations` or SQL Server `20261003183237_WebTranslationOperationsSqlServer` alongside preceding required migrations. Keep approval/recovery receipts for as long as retry/recovery is available; no pruning was introduced. These migrations were exercised only on isolated SQLite or generated as SQL scripts, never on an external database. Before release, test normal non-demo authenticated web save/reload, SQL Server transactions/retry/deadlock behavior, real quotas/provider cancellation and human-reviewed translation in all four modes. Native Windows, iOS/package and physical export gates remain P13-G01–G04. General web applied-event outbox work remains prompt 20; only these aggregate translations derive history from durable terminal receipts.

Next: **prompt 15 — Server-authorized desktop onboarding demo (P13-004)**. Continue in this same checkout with `Implement prompt 15 in docs/desktopai-prompts.md, including the common requirements.` Preserve checked translation contracts, legacy refusal, server approval/receipt/recovery schema, source/backend/account guards, uncertainty editing lock, shared mapping exports, inert review and all prior desktop local history/sync workflows. Do not grant demo quota from a local practice marker. Full parity/release acceptance remains open pending the other functional prompts and actual live gates.

## Prompt 14 continuation — Late-result and backend isolation (2026-10-04)

Continued prompt 14 in the same dirty checkout after rereading its common requirements, development/content contracts, UAT and gap ledger. The complete structured workflow remains implemented; this continuation repairs asynchronous lifecycle boundaries in the actual `DocumentEditor.Translation.cs` handlers.

- Cancellation/account invalidation, a backend switch or a section switch during the final editor preview cannot restore a proposal. Changes during capture stop before the provider request. Checks retain the captured generation/document/section/backend across editor and HTTP awaits.
- Approval acknowledgements must match the reviewed operation, proposal, source document and supported receipt state before committing. An incorrect acknowledgement leaves the real durable approval recoverable without a commit or false Applied state.
- Recovery receipts retain their backend origin. Finish approved save and Recover original refuse an old receipt before sending a mutation to another backend. Late commit/recovery responses cannot reload writing, navigate or mark a proposal Applied in a changed context.
- An uncertain commit retains its original backend through cancellation. Recovery from another backend cannot consume that operation even if document/proposal/operation IDs match. Returning to the original backend reconciles its existing terminal receipt without regeneration or another commit.

No server journal schema, migration, editor algorithm, generated bundle or shared presentation changed in this continuation. Existing atomic aggregate persistence, rich formatting, all four replace/copy modes, original-copy recovery, legacy plain-text refusal and selection/copy/discard remain covered by the integrated tests.

Evidence root: `artifacts/desktopai-p14-continuation/`.

| Fresh check | Result / evidence |
|---|---|
| Reproduced production-handler lifecycle defects | **8 failures before the initial fix** in `lifecycle-before.trx`; **3 further failures** at capture/cross-backend reconciliation in `capture-before.trx`. Both retained with logs. |
| Focused handler verification | **20 passed** after the first fix, including four application modes and interrupted-save recovery; `lifecycle-after.trx`. The final suite also covers the subsequently added boundaries. |
| Final complete unfiltered Release suite | **1,483 passed, 0 failed/skipped**, including **12 new continuation cases**; `full.trx`, `full-tests.log`, `test-summary.json`. Production MVC/SQLite persistence and synthetic authentication/provider seams are exercised, including migrations, history, ownership, conflicts, transaction rollback, replay and recovery. |
| Final integrated browser profile | **62 shipped-editor checks, 86 actual Razor fixture renders** at 1280×720 and 1920×1080, and **4 equivalent device/client apply/reopen checks** passed; `browser-results.json`, `browser-check.log`, `screenshots/`. The p14 per-page review was visually inspected; Unicode, blank-page and inert-text labels remain readable. |
| Final sequential Release builds | Server, Client, Device.Shared, Windows and available iOS managed `iossimulator-x64`: **all passed, 0 warnings/errors each**; per-project build logs and `build-summary.json`. |

The initial test compilation was blocked by generated restore metadata referencing absent `C:\Users\CodexSandboxOffline\.nuget\packages` assemblies. Restoring against the existing `C:/Users/Johan/.nuget/packages` cache corrected local intermediates without package/version changes. Restore used `--ignore-failed-sources -p:NuGetAudit=false`; no current vulnerability-feed check is claimed. The full test build retains the existing `LocalStoryboardTests` `xUnit2031` warning. The sandbox process helper also failed before commands started while applying deny-read ACLs; approved execution outside that helper allowed repository checks to proceed.

Reproduce with the prompt-14 commands above, using `artifacts/desktopai-p14-continuation/` for both `BaseOutputPath` and evidence, setting `WRITERAPP_P02_EVIDENCE` through `WRITERAPP_P12_EVIDENCE` to their fresh fixture directories and `WRITERAPP_P13_EVIDENCE` / `WRITERAPP_P14_EVIDENCE` to the absolute evidence root. If local restore metadata references an unavailable account cache, first run:

```powershell
dotnet restore Tests/WriterApp.Tests/WriterApp.Tests.csproj --ignore-failed-sources -p:NuGetAudit=false -p:RestorePackagesPath=C:/Users/Johan/.nuget/packages
```

The new isolated Windows candidate is `WriterApp.Desktop/artifacts/desktopai-p14-continuation/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; `native-candidate.json` records its path/hash. Process inspection found no running desktop host. The candidate was **not launched**; native computer APIs are disabled. No live provider, authenticated full web shell, hosted SQL Server, native iOS/package or physical export acceptance is established. The loopback fixture host is stopped after verification. Only the translation handler, its focused tests and the three relevant documentation files were intentionally extended; preservation/status/diff evidence retains unrelated files and the six preexisting deletions. No commit, deployment or user-document mutation occurred.

**P13-003 remains closed for supported implementation and available automated checks**, with native/deployed/live-language gates independently open. The required backend migrations and deployment prerequisites remain those of the 2026-10-03 entry. Next remains **prompt 15 — Server-authorized desktop onboarding demo** in this same checkout; preserve the new capture/preview/receipt/backend guards as well as the original structured-translation safeguards.

## Prompt 15 — Server-authorized desktop onboarding demo (2026-10-04)

Implemented the prompt's common/continuation requirements in the same dirty checkout. Local/guest practice remains offline-capable and separate from a deliberately chosen online demo. Shared presentation offers owned create/reopen, refresh and explicit online completion; the existing production Writing panel offers a distinct Run Demo followed by normal inert review, Apply/Dismiss and History Undo/Redo.

### Implemented boundaries

- Typed version-1 status/bootstrap/progress routes return authenticated owned identities and actual server policy. Version-3 backend/account-scoped local guidance retains distinct local practice/demo IDs, stable bootstrap choice and pending progress operations. Late account results, another backend or another window cannot replace them.
- Server bootstrap reserves a fresh separate workspace once per owner under a transaction/clock lock. It no longer adopts existing projects by title or empty content, and retries cannot reseed altered/empty/missing writing or a deleted/trashed reservation. Existing manuscripts and guest samples remain unchanged. A second device imports the same cloud reservation with its own stable local identity.
- The shared web/device server policy is now a durable owner grant: exact linked section/action, incomplete onboarding, active owned content, one provider attempt and seven-day expiry. Neither a provenance marker nor `onboarding_demo` grants entitlement. Eligibility is rechecked and the grant atomically consumed before provider work; ambiguous/failed attempts remain consumed. Successful proposal identity is separate from use, Apply and completion. Ineligible flags cannot suppress ordinary billing; ordinary AI retains actual plan/quota.
- Additive demo-only routes use the production sync service and immutable operation receipts, scoped to the reserved cloud document. Free desktop sync additionally requires a persisted explicit choice, even when web created the grant first. Normal v4 entitlement and other-document refusal remain. Local unlinked writing is never enrolled by the demo choice; original guest content is byte-preserved.
- Explicit online open records step 2 after import; explicit online completion records step 10. Profile revision CAS, serializable transactions, immutable request-hash receipts and monotonic web step writes preserve completed progress. Lost acknowledgement retains the same operation; receipt replay is followed by fresh status. Local guide completion and ordinary AI/Apply never manufacture server completion.
- Bootstrap/progress HTTP bodies, device status parsing and local guide files are bounded to 16 KiB. Existing v1 migration backups and v2 practice identities are preserved; v2 upgrades atomically to v3 on explicit update. Unknown/corrupt backend/storage contracts remain actionable refusals. Missing/deleted server or local identities require restoration rather than recreation.

### Fresh verification

Evidence root: `artifacts/desktopai-p15/` (ignored local captures).

| Check | Result and scope |
|---|---|
| Final unfiltered Release .NET suite | **1,510 passed, 0 failed/skipped**; `full.trx`, `full-tests.log`. Includes all previous integrated preservation/recovery/translation checks and 27 new prompt-15 cases. |
| Production onboarding/device checks | Actual MVC authorization, controllers, bootstrap/eligibility/sync services, file-backed SQLite and device file/journal storage with synthetic authentication/provider seams. Covers first/repeated bootstrap, lost bootstrap/progress/sync acknowledgement, second window/device, guest sign-in, account/backend switching, authored/empty/trash/missing samples, expiry/used/ineligible status, ordinary zero-quota denial, explicit demo review/Apply/Undo, stale Apply and web completion reconciliation. |
| Database upgrades | SQLite migration from the preceding web-translation migration preserves rich Unicode writing and sync version and creates no retroactive demo grants. SQL Server idempotent script/model checks pass without a database connection/execution. |
| Integrated browser profile | **62 shipped-editor checks, 98 actual Razor fixture renders** at 1280×720 and 1920×1080, and **4 equivalent device/client Apply/reopen checks**, all passing. Fresh p02–p12/p14 fixtures plus p15 signed-out/available/used/completed/loading and writing review: readable status, keyboard-reachable controls, no horizontal overflow/page errors. `browser-results.json`, `browser-check.log`, `screenshots/`. New demo availability and writing review screenshots were visually inspected. |
| Final sequential Release builds | Server, Client, Device.Shared, Windows and available iOS managed `iossimulator-x64`: **all pass, 0 warnings/errors each**; per-project build logs, `build-summary.json`. |
| Native host inspection | No running desktop process. Isolated rebuilt candidate path/hash recorded in `native-candidate.json`; **not launched**, because native computer APIs are disabled. Builds/static fixtures do not establish native interaction. |

The full test build retains the existing `LocalStoryboardTests` `xUnit2031` warning. Cached restore metadata from the preceding continuation was reused; no fresh NuGet vulnerability-feed result is claimed. The sandbox helper continued failing before command execution while applying deny-read ACLs; approved execution outside that helper allowed local verification. No automatic approval rejection occurred. An intermediate full run passed 1,508 tests and failed one fixture that incorrectly expected a terminal error after an automatically retried lost sync acknowledgement. The corrected test observes the lost response and actual replay; the earlier result is retained in `full-before.trx`/`full-before.log`. Legacy tests were updated to seed genuine owned grants and to assert that empty existing projects/local labels are not demo authorization.

Reproduce using the prompt-13 fixture setup with `artifacts/desktopai-p15/` as evidence/output, setting `WRITERAPP_P02_EVIDENCE` through `WRITERAPP_P12_EVIDENCE` to their fixture directories and `WRITERAPP_P13_EVIDENCE`, `WRITERAPP_P14_EVIDENCE`, `WRITERAPP_P15_EVIDENCE` to the absolute evidence root:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p15/ --logger 'trx;LogFileName=full.trx' --results-directory artifacts/desktopai-p15
# Build the same five projects sequentially with BaseOutputPath=artifacts/desktopai-p15/.
$env:PORT = '5184'
node WriterApp.Client/tests/serve-device-editor.mjs
# In another owned session with PLAYWRIGHT_MODULE set to the available runtime:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5184'
node WriterApp.Client/tests/desktopai-acceptance.mjs artifacts/desktopai-p15
```

The Windows candidate is `WriterApp.Desktop/artifacts/desktopai-p15/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. Use a separate Development build with isolated data/backend/native authentication for native acceptance, as described in the gap analysis. The editor sources/generated assets were unchanged in this prompt. The owned loopback browser fixture host is stopped after checks. Checkout hashes/status/diff evidence preserve unrelated work and all six preexisting deletions; no commit, deployment, external migration or user-document mutation occurred.

### Release prerequisites and next handoff

**P13-004 is closed for the supported implementation and available automated checks.** The actual account/provider demo gate remains open: use a deliberately opted-in, incomplete-onboarding account on the matching deployed backend, confirm the exact returned cloud/section identities, real free allowance and ordinary AI denial, generate once, review/Apply/Dismiss/Undo, refresh used status, reconcile completion on web/desktop, and verify restart/offline/second-account isolation. Provider quality, latency, real OIDC, authenticated full web shell and native desktop interaction were not exercised. Hosted SQL Server concurrency/transactions, iOS signing/device/package and physical export gates remain independently open.

The backend needs SQLite `20261004064041_OwnedOnboardingDemo` or SQL Server `20261004064042_OwnedOnboardingDemoSqlServer`, alongside preceding migrations and the additive demo routes. Reservations/receipts must remain while retry/no-reseed recovery is available; no pruning was introduced. Legacy metadata-only demos are not auto-adopted or retroactively given an allowance. A consumed provider attempt is not reset on network/provider ambiguity; refresh current status and retain ordinary plan/quota handling. The guide's local status is never authority for a free request.

Next: **prompt 16 — Materialize owned remote cover assets for desktop use (P13-005)**. Continue in this same checkout with `Implement prompt 16 in docs/desktopai-prompts.md, including the common requirements.` Preserve the server-owned demo identity/eligibility/reservation, explicit-choice sync, progress CAS/receipts, guest practice lifecycle, existing inline PNG/metadata guards and all prompt-14 translation/source/backend/recovery safeguards. Full parity/release acceptance remains open pending the remaining functional prompts and live gates.

## Prompt 16 — Owned remote cover materialization (2026-10-04)

Implemented the common/continuation requirements in this same dirty checkout. Traced the client cover request/concepts/save flow through the provider/controller and the device store, metadata sync and publishing path. The supported remote workflow resolves an owned project cover or server provider concept, validates static PNG bytes and retains their exact hash/provenance for private offline preview and publishing. Caching and preview leave the saved remote reference and writing/planning unchanged; the existing explicit Save remains the only cover-metadata mutation.

### Implemented boundaries

- The additive authenticated version-1 materialization endpoint accepts cloud project/document identity, current metadata/sync revisions and reference hash, with no client URL field. It rechecks ownership, active source, saved reference and known foreign reference registration before fetching and inside its persistence transaction. Anonymous/foreign/deleted/stale requests refuse without replacing the cover. Existing generation entitlements/quota and device checked-source guards remain; web requests now supply their owned project source for remote provider results.
- Remote fetch is disabled until `CoverAssets:TrustedStoragePrefixes` names real exact HTTPS provider account/container paths ending in `/`. Every redirect revalidates trust and DNS; private/reserved/mixed address sets and literal-IP/userinfo/encoded-path/foreign-host targets refuse. Production transport pins checked public addresses with normal hostname TLS, no automatic redirects/proxy/decompression/cookies and no forwarded user/provider credentials. Two redirects, 16 DNS addresses, 30-second fetch and 10-second connection limits are explicit.
- Remote format support is **static PNG only, at most 2 MiB**, exact media type and unencoded transport. Header/stream bounds, incomplete length, CRC/chunks, dimensions/raster expansion, APNG and unknown critical encodings are checked. JPEG/WebP/GIF/SVG return PNG guidance and preserve the prior cover; no transcoding/decoder was introduced. Provider JSON and device receipt/file parsing have independent bounds. Expired/deleted references retain prior cache and report actionable recovery/regeneration guidance.
- Immutable server owner/project/reference-hash records retain asset identity, original reference, source document/revisions, SHA-256, media type and exact bytes. Retried/lost acknowledgements reuse the same identity/bytes without redownloading an expired reference. Account/backend/local-project-scoped asset files are independently leased and atomically written; corrupt/unknown versions and changed immutable identities refuse without replacement. Version-1 drafts remain readable; new version-2 drafts retain aligned optional asset receipts. No cache pruning was added.
- Shared presentation exposes deliberate Cache, loading/cancel, expiry/malformed errors, cached/offline and signed-out states. Existing concept review/selection, metadata-CAS Save, prior-cover restoration and normal project sync use the exact PNG. Independent project cache is available to sibling manuscripts and survives draft replacement/restart. Late account/source changes cannot install a proposal. Invalid receipt data is now caught by the production Cover Studio handler with a readable error.
- Publishing uses the matching scoped cached asset offline without changing the remote URL or downloading. HTML/DOCX/EPUB retain the exact PNG bytes; the PDF preview source uses the same image. Actual Publishing handlers clear private preview/export state on account change and recheck captured account/document generation before saving, including after awaited PDF rendering. Explicitly saved inline covers retain the existing authored device-wide project lifecycle.

### Fresh verification

Evidence root: `artifacts/desktopai-p16/` (ignored local captures; historical evidence remains intact).

| Check | Result and exercised scope |
|---|---|
| Final unfiltered Release suite | **1,579 passed, 0 failed/skipped**, including **69 new prompt-16 cases**; `full.trx`, `full-tests.log`, `test-summary.json`. Prior translation/demo/source/history/sync/preservation suites remain included. |
| Asset policy/device/endpoint checks | `RemoteCoverAssetTests`, `OwnedCoverAssetEndpointTests`, `CoverAssetEncodingTests`, existing cover/concurrency/project-sync tests. Actual MVC authorization/controller/service/file-backed SQLite and device storage, with synthetic authentication/provider/DNS/HTTP transport seams. Covers owned remote project/provider concepts, PNG encodings, private/redirect targets, header/stream overflow, corruption/partial reads, stale metadata/source/account/backend, cancellation, lost acknowledgement, immutable replay/restart, foreign registration races, unknown cache versions, prior-cover recovery and scoped offline bytes. |
| Production page/publishing checks | `DeviceCoverStudioPanelTests` and `RemoteCoverPublishingPageTests` invoke actual Razor handlers. Cache/selection does not change serialized writing/project metadata; explicit Save/offline restore does. Sibling/project cache and sign-out refusal pass. HTML decoded image source, DOCX image-part bytes and EPUB image entries equal the source PNG. Native file saving is a synthetic adapter; physical file/PDF export is not established. |
| Database upgrades | `CoverAssetMigrationTests`: isolated SQLite upgrade from prompt 15 preserves remote URL, metadata revision, rich Unicode and sync version and does not materialize anything automatically. SQL Server idempotent script/model checks pass without database execution. Both migrations generated successfully; `sqlite-migration.log`, `sqlserver-migration.log`. |
| Integrated browser/editor profile | **62 shipped-editor checks, 116 actual Razor panel renders** at 1280×720 and 1920×1080, and **4 equivalent device/client Apply/reopen checks**, all passing. Includes fresh prior fixtures plus nine p16 states: empty/cached/expired/invalid-receipt/loading/offline/saved/signed-out Cover Studio and offline Publishing. Keyboard reachability, overflow and page-error checks pass. `browser-check.log`, `browser-results.json`, `screenshots/`. Cached/error full-cover and offline-publishing screenshots were visually inspected. Solid-color PNGs test bytes/presentation, not provider illustration quality. |
| Final sequential Release builds | Server, Client, Device.Shared, Windows and available iOS managed `iossimulator-x64`: **all passed, 0 warnings/errors each**; per-project build logs and `build-summary.json`. |
| Native inspection | No running `WriterApp.Desktop` process; isolated candidate path/SHA-256 in `native-candidate.json`, **not launched**. Native computer APIs are disabled. iOS compilation on Windows establishes neither signing/bundling nor simulator/device behavior. |

Intermediate evidence is retained rather than counted as acceptance: initial DOCX assertions used an assumed ZIP path, subsequently corrected to actual OpenXML image parts (`focused.trx`, then 49 passing `remote.trx`). Two later assertions incorrectly expected raw base64 HTML instead of its decoded attribute and a physical SQL Server `nvarchar(4096)` instead of the model's logical bound/`nvarchar(max)` (`cover-final.trx`). The subsequent full run passed 1,570 tests. Additional malformed-receipt tests exposed an incorrect exception assertion and the production page's missing `InvalidDataException` catch; the corrected assertions, page error handling and fresh final 1,579-test run are the current result (`full-before.trx` retains the intermediate failures). The full test build retains the existing `LocalStoryboardTests` `xUnit2031` warning. Cached restore metadata was reused; no fresh vulnerability-feed result is claimed. The sandbox helper continued failing before execution while applying deny-read ACLs; approved local execution allowed checks to proceed, with no automatic approval rejection.

Reproduce with fresh fixture roots and the existing locally available Playwright runtime:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p16'
foreach ($prompt in 2..12) {
    $fixtureRoot = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $fixtureRoot | Out-Null
    [Environment]::SetEnvironmentVariable('WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE', $fixtureRoot)
}
foreach ($prompt in 13..16) {
    [Environment]::SetEnvironmentVariable('WRITERAPP_P' + $prompt + '_EVIDENCE', $evidenceRoot)
}
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p16/ --logger 'trx;LogFileName=full.trx' --results-directory artifacts/desktopai-p16
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p16/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p16/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p16/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p16/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:RuntimeIdentifier=iossimulator-x64 -p:BaseOutputPath=artifacts/desktopai-p16/
$env:PORT = '5184'
node WriterApp.Client/tests/serve-device-editor.mjs
# In a second owned session, with PLAYWRIGHT_MODULE pointing to the available runtime:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5184'
node WriterApp.Client/tests/desktopai-acceptance.mjs artifacts/desktopai-p16
```

The Windows candidate is `WriterApp.Desktop/artifacts/desktopai-p16/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. Use a separately configured Development build with isolated data/authorized backend/native authentication for native acceptance. No editor algorithm/generated bundle changed. The owned loopback fixture host was stopped and port 5184 has no listener (`fixture-host-stopped.json`). Of 256 preexisting readable dirty files, 236 remain byte-identical, 20 were deliberately extended for this prompt and none is missing; all six preexisting deletions remain absent. New/previously clean changes and final status/diff audit are recorded separately. No commit, deployment, external migration, provider call or user-document mutation occurred.

### Release prerequisites and next handoff

**P13-005 is closed for the supported owned static-PNG implementation and available automated checks.** This does not claim remote JPEG/WebP/GIF/SVG parity. Deploy the matching asset routes and SQLite `20261004072914_OwnedCoverAssets` or SQL Server `20261004072915_OwnedCoverAssetsSqlServer`, after preceding migrations. Configure the exact real provider-owned storage path in `CoverAssets:TrustedStoragePrefixes`; empty defaults keep fetch disabled. Retain immutable asset records/local provenance while retry/recovery is offered; no automatic fetch, URL rewriting or pruning was introduced. See [device-development.md](device-development.md#desktop-ai-prompt-16--owned-remote-cover-assets-2026-10-04) for bounds/configuration/lifecycle.

Before live acceptance, use authorized owned project/provider PNG references on that configured backend, confirm real TLS/DNS/redirect/authentication, expired/deleted URLs and immutable replay, switch real accounts/backends, compare bytes and saved metadata after sync/relaunch, and physically save/reopen HTML/DOCX/EPUB/PDF. Hosted SQL Server isolation/retry/deadlock behavior, authenticated full web shell, real provider quality/latency, native Windows and iOS/package/device gates remain independently open (P13-G01–G04). Synthetic transport, browser fixtures and build success are not those gates.

Next: **prompt 17 — Align writing outline context across web and desktop (P13-006)**. Continue in this same checkout with `Implement prompt 17 in docs/desktopai-prompts.md, including the common requirements.` Preserve checked translation/source/backend/recovery, server-owned demo grants/explicit-choice sync/no-reseed/progress receipts and owned immutable cover/cache/metadata/explicit-Save boundaries. Match bounded source-pinned outline context in actual writing requests without implementing unrelated outline generation or changing authored content. Full parity/release acceptance remains open pending remaining prompts and live gates.

## Prompt 17 — saved writing outline context (2026-10-04)

Implemented P13-006 with shared bounded WritingOutline, owned source reads, provider JSON context, proposal receipts and generation/Apply guards across actual web/device selection, section, continuation, reusable presets and the device top menu. Limits: 1,000 sections plus 1,000 nodes; 200 characters per title, 20,000 total title characters, 262,144 canonical JSON characters. Existing continuation context remains at most 2,500 characters. No authored content or editor bundle change occurs during extraction.

Fresh evidence in `artifacts/desktopai-p17/`: **1,615 tests passed, zero failed/skipped**, including 36 new cases. All five sequential Release builds (server, client, Device.Shared, Windows, available iOS managed target) passed with zero warnings/errors. Browser evidence: **62 real editor checks, 116 actual Razor renders at 1280×720 and 1920×1080, four equivalent device/client Apply/reopen checks**, with no page errors. The shared outline summary appears in the actual writing review fixture. Tests exercise canonical equivalent requests and actual provider builders, owned multi-document reads, standalone/empty/oversize/escaping/reorder/delete cases, account/backend/cancellation, malformed receipt, stale generation/Apply and original-context retries. Production SQLite and Razor handler tests use synthetic authentication/provider/HTTP seams; these do not establish native or live semantic equivalence.

The initial focused run exposed missing cloud node IDs in the synced test fixture; the fixture now supplies real mappings. Two intermediate full runs exposed teardown races on isolated fixture sync/store locks; teardown now disposes the engine and waits for its offline gate before removing the owned temporary directory. Failed/intermediate logs and TRX remain alongside final evidence. Cached restore was used; no fresh vulnerability-feed claim. The existing LocalStoryboard analyzer warning remains in test compilation; explicit host builds are clean.

Reproduce with the preceding prompt-16 commands, changing the evidence root to `artifacts/desktopai-p17`, fixture variables 2–12 to its matching pNN subdirectories and 13–17 to the evidence root. Run the unfiltered Release suite, the checked-in `desktopai-acceptance.mjs` browser harness and all five builds sequentially. Preserve this dirty checkout and all six preexisting deletions. No commit, external deployment/migration, live provider call or user-document mutation was performed. The Windows candidate was built separately and was not launched; native computer APIs are unavailable. Real account/provider prose review, deployed backend and native save/relaunch remain open with the earlier setup prerequisites. User authorized finishing 17 before implementing 18; prompt 18 is the next implementation.

## Prompt 18 — real cover variations and adjustments (2026-10-04)

Implemented reachable variation, darker, brighter, cinematic and minimal image edits in both cover studios. The server's real multipart image-edit adapter receives the exact selected owned PNG and distinct operation instructions. Shared controls and labelled Original/Proposed images lead to explicit selection/Save/Dismiss. Unsupported model configurations advertise no edit operations before quota evaluation or transport. Typed source/asset/hash/revision checks, owner/deletion checks and server-issued proposal receipts prevent a different image/operation from borrowing an approval. No cover changes on generation or review. Web durable approval/commit/recovery receipts retain the prior cover, reject metadata races, survive acknowledgement loss/reload and remain discoverable after another preview. Device scoped atomic cache, offline local Save, prior-cover recovery and normal project sync remain separate states.

Fresh evidence root: `artifacts/desktopai-p18/`. **1,671 unfiltered Release tests passed; zero failed/skipped**, including **56 new cases** beyond prompt 17. `full-tests.log` and `full.trx` are the final suite; successful intermediate runs and the initial focused failures remain preserved. Tests exercise all five operations through actual device and web Razor handlers and normal owned SQLite HTTP endpoints, intercepted real-provider multipart routing/usage accounting, malformed/unchanged/oversized output, unsupported configuration, quota refusal before transport, anonymous/foreign owners, exact hashes, source deletion/version changes, late cancellation/account/backend changes, cache refusal, online/offline selection/Save, fresh component/store reopen, metadata conflict, original-cover recovery, forged proposal/operation rejection, interrupted pre-commit persistence and stable receipt replay without regeneration. SQLite upgrades preserve previous cover/manuscript/source metadata; SQL Server model/idempotent script checks pass without running SQL Server.

Final browser result: **62 real shipped-editor checks, 138 actual Razor renders at 1280×720 and 1920×1080, four equivalent device/client Apply/reopen checks**. Eleven new cover fixtures include both hosts' five adjustment reviews and web offline review. Browser fixtures use actual compiled web/device/shared scoped CSS. Original/Proposed screenshots were visually inspected after the comparison layout was improved. Synthetic solid-color images establish routing, lifecycle and exact bytes; they provide no evidence of darker/brighter or composition quality. These are headless browser fixtures, not native screenshots or a full signed-in web shell.

All five final sequential Release targets passed with **zero warnings/errors**: server, client, Device.Shared, Windows and the available iOS managed `iossimulator-x64` target. The complete test compilation retains the preexisting LocalStoryboard xUnit2031 warning. Cached restore was used; vulnerability-feed freshness was not established. Windows executable path/hash is recorded in `native-path-inspection.json` and `native-candidate.json`; the candidate was not launched. Native APIs remain unavailable. The owned allowlisted loopback editor fixture host is stopped after browser verification.

Initial focused checks caught reference-only collection assertions after reopening and the expected new inline asset identity replacing an old null identity. The first EF invocation read stale Debug output; its empty migrations were removed, the current Release assembly was rebuilt, and the final migrations were checked to add only the two new tables/indexes. The SQLite upgrade regression detected duplicated earlier table creation before this correction. Earlier migration files and user data were not replaced. Current model/upgrade/full checks pass. One source-only provider patch was initially rejected by automatic approval review as a private image upload; reading the explicit prompt authorization and proving verification uses intercepted synthetic transport resolved the review. No live request was made or credential acquired.

Preservation audit: all 284 previously readable dirty files remain present; 263 are byte-identical and 21 deliberately extended for this prompt. All six preexisting deletions remain absent. Fresh/new files and the final status are recorded separately. No commit, deployment, external migration/configuration, live provider request or user-document mutation was performed.

Reproduction:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p18'
New-Item -ItemType Directory -Force $evidenceRoot,(Join-Path $evidenceRoot 'p18') | Out-Null
foreach ($prompt in 2..12) {
    $dir = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $dir
}
foreach ($prompt in 13..18) { Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $evidenceRoot }
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=full.trx' --results-directory $evidenceRoot
# Run the five builds sequentially.
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p18/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p18/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p18/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p18/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p18/ -p:RuntimeIdentifier=iossimulator-x64
# In a separate owned shell, choose a free loopback port and start the allowlisted fixture host.
$env:PORT = '5185'
node WriterApp.Client/tests/serve-device-editor.mjs
# Then run the browser checks in another shell.
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5185'
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node WriterApp.Client/tests/desktopai-acceptance.mjs $evidenceRoot
# Stop only the host started above and verify its listener is gone.
```

Live/deployment prerequisites remain independent: apply the matching prompt-16 asset plus prompt-18 save/proposal migrations through the authorized configured-context process; configure a supported server-side GPT Image model/key, image-enabled account/quota and trusted storage when using remote sources. Review actual images for each of the five intended effects and record model/configuration and observed timing. Exercise real OIDC account changes, native keyboard/loading/offline/Save/relaunch at both viewports, deployed SQL Server concurrency and physically saved publishing bytes. The web requires connectivity for Save/Restore; a fresh offline browser needs an already resolved account identity to locate scoped review cache. A device offline Save confirms local metadata only, with normal cloud sync/conflict still pending. See the prompt-18 development section for exact formats, limits and migration IDs. No full parity, native iOS/package or live image-quality acceptance is claimed.

Next concrete implementation: prompt 19, checked-source web AI request/Apply contracts. Preserve prompts 14–18, saved-outline receipts, owned demo grants and cover source/asset/proposal/recovery guards. Prompt 20 still owns durable general web applied-event delivery; cover-save receipts do not implement that separate history workflow.

Prompt 17 preservation audit completion: 276 preexisting readable dirty files remained present (251 unchanged, 25 deliberately extended); no missing files, and all six preexisting deletions remained absent. The owned loopback fixture listener was stopped. `preservation-audit.json`, TRX counters and Windows candidate path/hash remain in the prompt-17 evidence root.

## Prompt 19 — checked production web AI (2026-10-04)

Implemented checked production web writing/custom presets, translation, quality/generated fixes, consistency/canon, scene coaching, all three synopsis modes and all four storyboard actions. Shared version-1 `WebAiSource` supplements sync versions with owned saved writing/planning/canon fingerprints; before/after provider checks reject authored changes even without a version change and allow unchanged-content metadata acknowledgements. Client leases check resolved account/backend, echoed identity, proposal date, local editor/card/focus and current source before retaining review or Apply. Missing capabilities, legacy unchecked results, malformed/expired output and ignored cancellation cannot retain usable approval. Server provider context/entity IDs are rebuilt from the owned source.

Scoped page/scene/card/synopsis saves and approved storyboard creation use serializable source CAS and `X-WriterApp-Checked-Save: 1`, required before claiming confirmed persistence. Scene ordinary saves also compare their loaded normalized card fingerprint; synopsis ordinary Save compares its loaded complete ten-field snapshot. Failed checks retain the local draft/review. Scoped scene Apply preserves authored tags/status/links and other fields; synopsis Apply changes only the reviewed field. Shared scene coaching/canon contracts are reused. Existing device strict revision/lifecycle contracts and ordinary save/editor Undo remain intact; no Device.Shared storage was imported into Client.

Complete web section expand/tighten/change-tone/show-don't-tell and reusable section prompts now map every saved page/run through the real editor and reuse prompt-14 durable approval/atomic commit/receipt/original-copy recovery. The additive `IsWriting` flag preserves language while existing translation approval hashes remain compatible. Unsupported content is preflighted before any page write. Raw unused context-drawer unchecked actions were replaced with actionable manuscript-editor guidance. See the [per-flow contract/evidence table](desktopai-gap-analysis.md#prompt-19-per-flow-checked-source-contracts-and-evidence-2026-10-04) for exact handler, server and limitation coverage; this closure is not inferred from a single endpoint.

Fresh evidence root: `artifacts/desktopai-p19/`. Final **unfiltered Release suite: 1,773 passed, zero failed/skipped**, in `full-final.trx` and `full-final.log` (1 minute 31 seconds). Compared with prompt 18, 103 cases were added and one redundant plain section outline adapter case was replaced by five real structured handler actions, net +102. Earlier focused/intermediate logs/TRX are retained. Actual Razor handlers and normal owned SQLite HTTP controllers exercise three-page writing commit/reopen/recovery, source/owner/target changes, missing capability, account change, malformed/expired output, ignored cancellation, concurrent writes, dedicated synopsis modes, three canon refresh kinds, quality handlers, consistency reports, all three scene handlers/card CAS and all four storyboard adapters/provider builders. Existing prompt-14–18 lifecycle, safe-upgrade, device, rich-content and architecture checks remain in the unfiltered suite. Provider/authentication transport seams are synthetic; no live provider call occurred.

All five sequential Release builds passed with **zero warnings/errors**: server, Client, Device.Shared, Windows and the available iOS managed `iossimulator-x64` target. Logs and exit codes are in `build-*.log` and `build-results.json`. The complete test compilation retains the preexisting LocalStoryboard xUnit2031 analyzer warning. Checks use cached restore (`--no-restore`); no fresh vulnerability-feed claim is made.

Fresh browser evidence: **64 real shipped-editor checks, 142 actual Razor fixture renders at 1280×720 and 1920×1080, four equivalent device/client explicit Apply/reopen comparisons**, no page errors or horizontal overflow. Two new editor cases exercise complete mapped heading/strong/emphasis/whitespace/blank/Unicode replacement and refusal of an unsupported later page before earlier content changes. Two new actual shared review fixtures display writing and scoped scene proposal markup inertly; their screenshots were visually inspected. These fixtures use compiled Client/Device.Shared/UI.Shared scoped CSS. Static Razor renders establish readable review/keyboard surfaces, while component tests establish event behavior; this is not full authenticated web-shell or native interaction. No editor source algorithm or generated bundle changed in prompt 19.

The owned allowlisted fixture host used port 5186, was stopped and has no listener (`fixture-host-stopped.json`). The isolated Windows candidate is `WriterApp.Desktop/artifacts/desktopai-p19/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`; absolute path and SHA256 are in `native-candidate.json`. No WriterApp.Desktop process was running at inspection, and this candidate was not launched. Native computer APIs remain unavailable; use a separately configured Development candidate, authorized backend/native authentication and isolated data for actual acceptance.

Intermediate checks exposed and corrected internal primary-document housekeeping in the content fingerprint, response-stream reuse after checked reads, unnecessary unchanged-notes flushes, lost boundary whitespace in synthetic writing fixtures, missing normal controller dependencies in the test host, and fixture card normalization/reflection setup. Scene Save normalization is now compared through normalized fingerprints; null optional tags/references remain compatible. Failures are preserved as intermediate evidence, and the final complete suite/build/browser checks pass. None of these checks modified user documents.

Preservation audit: all **311 preexisting readable dirty files** remain present; **284 byte-identical, 27 deliberately extended**, zero missing. All six preexisting deletions remain absent. `preservation-audit.json`, `final-status.txt` and final diff checks record hashes plus new/previously clean files separately. Existing phased work is uncommitted and must continue in this same checkout. No commit, deployment, external migration/configuration, live provider call or native/user-document mutation occurred. Prompt 19 adds no persisted entity or schema migration; preceding prompt-14–18 migrations remain prerequisites for their existing features.

Reproduction:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p19'
New-Item -ItemType Directory -Force $evidenceRoot,(Join-Path $evidenceRoot 'p18'),(Join-Path $evidenceRoot 'p19') | Out-Null
foreach ($prompt in 2..12) {
    $dir = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $dir
}
foreach ($prompt in 13..19) { Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $evidenceRoot }
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=full-final.trx' --results-directory $evidenceRoot
# Build sequentially to avoid shared intermediate races.
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p19/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p19/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p19/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p19/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p19/ -p:RuntimeIdentifier=iossimulator-x64
# In a separate owned shell, first verify the port is free.
$env:PORT = '5186'
node WriterApp.Client/tests/serve-device-editor.mjs
# In another shell:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5186'
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node WriterApp.Client/tests/desktopai-acceptance.mjs $evidenceRoot
# Stop only the fixture host you started; verify its listener is gone.
```

Explicit supported limits: section summaries without mapping, non-reusable section tools, aggregate scene writing/translation and unsupported rich nodes refuse Apply. Scene-route quality directs users to a linked manuscript page. Consistency anchors retain current-editor-page scope. Legacy cached quality results require a new checked run for Apply. Stale/oversized/unavailable typed canon requires refresh/backend update. Scene cloud AI-history Undo/Redo is unavailable; ordinary editor Undo or retained original-copy recovery remains. Interrupted multi-step storyboard creation requires inspecting saved state before retry. Ordinary/offline writing and cached readable results remain available; checked web generation/Save require the authenticated backend. Full inspector/native interaction and real provider prose/translation/canon/scene/synopsis semantics remain independent gates.

Before prompt-22 acceptance, deploy matching source/capability/save routes with preceding configured-database migrations through the authorized process. Use owned isolated non-demo projects and a second owner; verify real account/backend switching, missing capabilities, quota/entitlement refusal, cancellation and concurrent edits under deployed SQLite/SQL Server isolation/retry. Exercise full authenticated web/native keyboard/loading/offline/review/Apply/Undo/save-close-relaunch at both sizes and compare saved rich content/IDs/planning/canon, not labels. Native Windows, live provider/deployed database, physical export and iOS/package/signing/device acceptance remain open (P13-G01–G04).

Next concrete implementation: **prompt 20 — durable web applied-event reporting**. Continue in this same dirty checkout with `Implement prompt 20 in docs/desktopai-prompts.md, including the common requirements.` Preserve prompts 14–19, especially confirmed checked saves, exact target/source/date receipts, section writing/translation recovery, saved outline, owned demo and cover boundaries. General web applied reporting remains one-shot; checked-save acknowledgement and aggregate operation receipts do not implement its durable account/backend-scoped browser outbox or crash reconciliation.
## Prompt 20 — durable web applied history (2026-10-04)

Implemented the functional one-shot applied-reporting facet of **P13-G04**. Browser approval is now a version-1 immutable IndexedDB operation, scoped by resolved account/backend, retained before content Save. Shared typed identity/source/target/evidence and ordered transitions link to an owned server operation. Checked Page/SceneContent and scene/section-card content plus committed timestamp/typed response persist atomically through normal owned controllers. Reporting happens only after inspecting committed proof; reload, lost save/report acknowledgements and concurrent tabs reconcile the same operation rather than manufacture success or reapply a draft. Reporting failure preserves successfully saved writing. Cloud history merges only reported outcomes; browser receipts do not create native-device entries.

Actual production writing/custom, continuation, selection translation, generated quality and consistency revision handlers bind the owned proposal to PageEditor Save. Both scene-card Apply branches use durable planning receipts, retaining authored tags/status/links and unrelated fields. Complete section/document writing/translation links to the exact prompt-14 atomic aggregate receipt. Page/SceneContent Undo/Redo proposes a transition without changing history, persists approval before editor replacement, then checks exact expected current HTML/source inside the content transaction. Later edits or mismatched/replayed payloads refuse mutation. Card and aggregate snapshots cannot authorize manuscript HTML replay; planning inspection and explicit original-copy recovery remain available. Legacy external routes and device local recovery/receipts remain compatible.

Shared `WebAiHistoryDelivery` shows Apply intent separately from a saved outcome and pending/rejected/confirmed delivery, with inert original/proposed details, operation ID and Retry. Manual/history-load/reload/component-owned 30-second retries reconcile committed operations only. Unregistered/uncommitted intent remains inspectable without automatic Apply. Plan rejection, offline transport, deleted targets and account changes retain scoped unresolved intent; sign-out/backend/account changes cannot deliver foreign data. Returning to the original authorized scope can retry. Reporting consumes no generation quota; normal generation and Undo/Redo plan policies remain. Storage/size failures refuse the AI write before commit. No automatic pruning discards unresolved evidence.

Fresh evidence root: `artifacts/desktopai-p20/`. Final **unfiltered Release suite: 1,814 passed, zero failed/skipped**, `full-complete.trx` and `full-complete.log`; **41 new cases** relative to prompt 19. `WebAiHistoryOutboxTests`, `WebAiHistoryEditorTests`, `WebAiHistoryBoundaryTests` and `WebAiHistoryUiMigrationTests` cover pre-save/restart/crash boundaries, lost content/report ACKs, duplicate IDs/tabs, immutable payload mismatch, ordered Undo/Redo, wrong owner/proposal/target, concurrent SQLite writers, malformed cached confirmations, malformed/cancelled reporting after successful Save, storage/network failure, plan rejection/no provider quota consumption, account/backend/sign-out/cancellation, deletion, scene identity/later edits and aggregate reference reconciliation. Extended actual scene-handler tests confirm typed committed/reported planning and no manuscript Undo transition. Provider/authentication transport is synthetic; normal owned HTTP controllers and real SQLite transactions establish tested persistence, not deployed/provider acceptance.

Both schema prerequisites are additive: SQLite **`20261004112925_WebAiHistoryOperations`**, SQL Server **`20261004112927_WebAiHistoryOperationsSqlServer`**. Upgrade testing starts from the preceding cover schema and preserves rich writing, sync version and device receipt while creating empty web storage. SQL Server idempotent script/model checks validate the new owned operation table/indexes without executing an external database. Deploy through the authorized configured-context migration process alongside matching new prepare/inspect/report/move routes and the browser module. No external migration occurred here.

All five sequential isolated Release builds passed with **zero warnings/errors**: server, Client, Device.Shared, Windows and available iOS managed `iossimulator-x64`. `build-*.log` and `build-results.json` record results. The full test compilation retains the preexisting LocalStoryboard xUnit2031 analyzer warning. Cached restore was used; no fresh vulnerability-feed claim is made.

Browser checks passed **70 real shipped-editor checks, 148 actual Razor fixture renders at 1280×720 and 1920×1080, four equivalent device/client explicit Apply/reopen comparisons**, plus two real same-origin tabs concurrently retaining independent IndexedDB intents, confirmed delivery surviving reload and a late stale tab refusing downgrade. New actual shared Prepared/Rejected/Confirmed fixtures are keyboard reachable; Enter expands original/proposed inert evidence without executable provider markup or horizontal overflow. Screenshots of expanded states were visually checked. Compiled scoped styles and shipped editors are used, with no editor algorithm/generated bundle change. Browser storage/reload checks are real; static Razor fixtures do not establish a full signed-in web-shell or native interaction. `browser-complete.log`, `browser-results.json` and `screenshots/p20-history-*` retain evidence.

Intermediate logs are retained. Initial focused fixtures needed normal entitlement registration and correct JS module/debug seams; the first two-tab browser experiment timed out with a background rich-editor animation loop, corrected by using a minimal two-tab storage fixture. Final audit migrated the remaining scene-card one-shot reporter and corrected malformed/cancelled reporting handling after a known successful Save. The final suite/build/browser checks pass. No user document was modified by these checks.

The owned allowlisted loopback fixture host used port 5187 and was stopped; `fixture-host-stopped.json` records no listener. `native-candidate.json` records the rebuilt absolute Windows output path and SHA256 under `WriterApp.Desktop/artifacts/desktopai-p20/Release/net10.0-windows10.0.19041.0/win-x64/WriterApp.Desktop.exe`. No desktop process was running at inspection, and this candidate was not launched. Native computer APIs remain unavailable; native keyboard/loading/offline/save-close-relaunch requires a separately configured Development candidate with isolated data and authorized native identity/backend.

Preservation: all **345 preexisting readable dirty files** remain present (**321 byte-identical, 24 deliberately extended**), with unchanged/extended hashes in `preservation-audit.json`, zero missing; all six preexisting deletions remain absent. New/previously clean changes are recorded separately with `baseline.json` and `final-status.txt`. Work remains uncommitted in this same checkout. No commit, deployment, external authentication/registration change, provider call or native/user-document mutation occurred.

Reproduction:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p20'
foreach ($prompt in 2..12) {
    $dir = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $dir
}
New-Item -ItemType Directory -Force $evidenceRoot,(Join-Path $evidenceRoot 'p18'),(Join-Path $evidenceRoot 'p19'),(Join-Path $evidenceRoot 'p20') | Out-Null
foreach ($prompt in 13..20) { Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $evidenceRoot }
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=full-complete.trx' --results-directory $evidenceRoot
# Build sequentially.
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p20/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p20/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p20/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p20/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p20/ -p:RuntimeIdentifier=iossimulator-x64
# In a separate owned shell after verifying port availability:
$env:PORT = '5187'
node WriterApp.Client/tests/serve-device-editor.mjs
# In another shell:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5187'
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node WriterApp.Client/tests/desktopai-acceptance.mjs $evidenceRoot
# Stop only the host started above; verify the listener is gone.
```

P13-G04's **functional durable-reporting gap is closed** for the supported workflows. Deployed SQLite/SQL Server concurrency, full authenticated browser/native journeys, real account/backend switching/provider semantics and physical publishing bytes remain prompt-22 gates. Native iOS identity, Mac/signing/device and packaged acceptance remain P13-G03/prompt 21–23 work. No full parity/release acceptance is claimed. See development documentation for supported storage/request/inspection limits and explicit planning/aggregate replay restrictions.

Next concrete implementation: **prompt 21 — native iOS identity integration**. Continue in this same dirty checkout with `Implement prompt 21 in docs/desktopai-prompts.md, including the common requirements.` Preserve prompts 14–20, checked source/save/recovery, owned demo/cover/outline, durable browser intents/ordered receipts and existing device history. Add local platform configuration and native adapter/lifecycle tests without changing external authentication registrations unless separately authorized. Record actual Mac/signing/device prerequisites independently of Windows managed compilation.

## Prompt 21 — native iOS identity (2026-10-04)

Implemented `IosDeviceIdentityClient` as iOS's registered `IDeviceIdentityClient`, backed by pinned MSAL.NET 4.90.1 native iOS Keychain caching, main-thread system authentication presentation and silent renewal. Platform-specific SDK calls stay in iOS; Device.Shared retains normal authenticated backend, entitlements/quota, account generation, local writing/sync and all AI review/Apply guards. Windows's loopback callback validation is preserved while iOS validates its exact custom callback independently. OAuth state/nonce/PKCE and refresh remain MSAL responsibilities; access tokens never enter manuscript files, Razor/JS or diagnostic output.

Added explicit build environment/backend/public-auth settings, fixed `com.prosa.writer.ios` bundle/`msauth.com.prosa.writer.ios://auth` callback and signed app/MSAL Keychain groups. `AppDelegate.OpenUrl` forwards only active uncancelled matching callbacks, refuses duplicate/foreign/malformed continuations and leaves OAuth validation to MSAL. Native Created/Resumed restore silently with duplicate suppression; normal background handoff does not cancel system authentication. Stopped blocks new interactive presentation; Destroying cancels pending work. Connectivity uses the shared offline state. Version-1 secure selected-account state is scoped by environment/backend/tenant/authority/client/redirect/scopes; silent restoration cannot guess another cached account. No new database entity/schema migration is needed.

Shared account sign-out/revocation now clears UI/generation immediately and cancels queued/in-flight acquisition, with a final locked lifetime/generation check that rejects providers ignoring cancellation. Expired/revoked tokens invalidate old approvals and scoped canon/history/cover availability; same-account renewal preserves generation. Sign-out attempts both secure selection and native cached account removal, suppresses further silent use after failure and permits explicit retry. Guest sign-out does not construct an unconfigured MSAL application. Authentication does not delete or change local authored writing/planning/recovery.

**Default configuration remains deliberately unregistered:** `WriterApp.iOS/native-auth.json` has an empty ClientId. No authorized iOS public-client registration was supplied; the Windows registration is not assumed valid for iOS. Offline guest use remains available, including with malformed public auth settings. Supply the authorized iOS ClientId, tenant/authority, fixed redirect and API scopes through `ProsaIosAuthSettingsFile`; Release uses embedded settings and build-selected backend, not process overrides. The deployed bearer configuration additionally needs the approved iOS `azp` in `NativeAuth:AllowedClientIds` and matching issuer/tenant/audience/delegated scope. No external registration, consent or backend allowlist was changed. Exact setup is in the [prompt-21 development section](device-development.md#desktop-ai-prompt-21--native-ios-identity-2026-10-04), grounded in [Microsoft's MAUI/MSAL guidance](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/authentication?view=net-maui-10.0).

Fresh evidence: `artifacts/desktopai-p21/`. Final **unfiltered Release suite: 1,852 passed, zero failed/skipped**, `full-complete.trx`/`full-complete.log` (1 minute 57 seconds), **38 new cases** relative to prompt 20. `IosIdentityTests` compiles the actual platform-independent iOS adapter/options/lifecycle/configuration source into the test project, with synthetic native-session and secure-store boundaries. Cases cover configured/foreign/Windows callbacks, bundle/entitlement consistency, Development-only overrides, invalid settings/guest behavior, backend/environment selection scope, explicit switch, restart/silent renewal with extra cached accounts, expiry/revocation/foreign tenant/account, storage/network/removal failure, duplicate/malformed/late callbacks, normal background handoff, resume deduplication, cancellation/destruction/sign-out and ignored cancellation. Existing production `DeviceAiService.RequireCurrentAccount` rejects approval after iOS switch/revocation/destruction. Real local repository tests retain writing across sign-out/removal failure. Existing device auth, bearer validation, private caches/history/canon/cover and all preceding AI/editor/persistence regression tests remain in the complete suite.

All five sequential isolated Release builds passed with **zero warnings/errors**: server, Client, Device.Shared, Windows and available iOS `iossimulator-x64` managed target. `build-*.log`, `build-results.json`, `ios-initial.log`, `ios-restore.log` and `toolchain.log` retain evidence. The new iOS MSAL reference restored from the available package cache with `NuGetAudit=false`; subsequent checks used `--no-restore`. No fresh vulnerability-feed claim is made. The test compilation retains the preexisting LocalStoryboard xUnit2031 warning; the unused synthetic test-field warning from the initial run is corrected by native-removal failure coverage.

Browser evidence: **70 real shipped-editor checks, 156 actual Razor renders at 1280×720 and 1920×1080, four equivalent device/client explicit Apply/reopen comparisons**, plus the retained real two-tab IndexedDB outbox checks. Four new actual DeviceAccountMenu fixtures exercise guest, missing configuration, offline and rejected identity. Enter opens the account disclosure, enabled controls are keyboard reachable, guidance is readable without horizontal overflow or page errors, and synthetic tokens never appear. Screenshots were visually inspected. These are compiled shared/device/client fixture surfaces and shipped editors, not the native system authentication sheet or a complete authenticated shell. No rich-editor algorithm/generated assets changed.

Intermediate evidence is preserved: the first full run had five fixture-export failures because the new evidence root lacked the existing `p18` directory; creating the documented directories corrected those checks. One initial account fixture expected pre-restore wording rather than the actual unconfigured message; its assertion now verifies the real state. Callback exceptions and unconfigured guest sign-out were hardened during the final audit and are covered in the final full run. No user document was involved.

`native-candidate.json` records absolute rebuilt Windows EXE and iOS managed DLL/MSAL paths and SHA256. No WriterApp.Desktop process was running at inspection; no rebuilt candidate was launched. No Mac, Xcode signing host or authorized iOS device was available, and native computer APIs are disabled. The owned allowlisted loopback host used port 5188 and was stopped (`fixture-host-stopped.json`, no listener). Preserve this same checkout and preceding uncommitted work; `baseline.json`, `preservation-audit.json`, `final-status.txt` and `final-summary.json` record current checks and retained file hashes. All 371 preexisting readable dirty files remain present (365 byte-identical, six deliberately extended, zero missing); all six preexisting deletions remain absent.

Reproduction:

```powershell
$evidenceRoot = Join-Path (Get-Location) 'artifacts/desktopai-p21'
New-Item -ItemType Directory -Force $evidenceRoot,(Join-Path $evidenceRoot 'p18'),(Join-Path $evidenceRoot 'p19'),(Join-Path $evidenceRoot 'p20') | Out-Null
foreach ($prompt in 2..12) {
    $dir = Join-Path $evidenceRoot ('p' + $prompt.ToString('00'))
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $dir
}
foreach ($prompt in 13..21) { Set-Item -Path ('Env:WRITERAPP_P' + $prompt.ToString('00') + '_EVIDENCE') -Value $evidenceRoot }
dotnet restore WriterApp.iOS/WriterApp.iOS.csproj --ignore-failed-sources -p:RuntimeIdentifier=iossimulator-x64 -p:NuGetAudit=false
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=full-complete.trx' --results-directory $evidenceRoot
# Build sequentially to avoid shared intermediate races.
dotnet build BlazorApp.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p21/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p21/
dotnet build WriterApp.Device.Shared/WriterApp.Device.Shared.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p21/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p21/
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release --no-restore -p:BaseOutputPath=artifacts/desktopai-p21/ -p:RuntimeIdentifier=iossimulator-x64
# In a separate owned shell, after verifying port availability:
$env:PORT = '5188'
node WriterApp.Client/tests/serve-device-editor.mjs
# In another shell:
$env:DESKTOPAI_EDITOR_HOST = 'http://127.0.0.1:5188'
$env:PLAYWRIGHT_MODULE = 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
node WriterApp.Client/tests/desktopai-acceptance.mjs $evidenceRoot
# Stop only the fixture host started above and verify no listener remains.
```

**P21-001 implementation/available automated checks are closed; P13-G03 native acceptance remains open.** A separately authorized iOS public-client/API registration and allowlist, compatible Mac/Xcode/.NET iOS workload, Apple signing identity/provisioning profile with both Keychain groups, reachable backend and isolated non-demo account/data are required. On simulator/device verify actual callback/resume/cancel/expiry/secure cache, cold restart, sign-out and real account switch; then execute one checked AI request with explicit review/Apply/save/reopen, confirming stale old-account private data and approvals remain unusable and ordinary writing stays intact. The shared adapter tests do not execute the real Keychain, MSAL OAuth transport or UIKit system sheet. Windows managed iOS compilation does not produce native signed-in/device/package acceptance.

Next concrete action: **prompt 22 — available Windows/web/provider/database/physical export acceptance**. Continue in this same dirty checkout with `Implement prompt 22 in docs/desktopai-prompts.md, including the common requirements.` Preserve prompts 14–21, iOS callback/secure-selection/account lifetime safeguards and the durable web reporting/source/recovery contracts. Exercise authorized available gates with isolated data/owned hosts; retain unavailable prerequisites explicitly instead of reconstructing from HEAD or fabricating native/provider acceptance. Prompt 23 remains iOS/package acceptance work. No commit, deployment, live provider call, external registration/allowlist change or distribution occurred.

## Prompt 22 — available release gates and scoped repairs (2026-10-04)

The user requested prompt 23 during prompt-22 verification; the available prompt-22 work was finished first. Evidence roots are `artifacts/desktopai-p22/` and `artifacts/desktopai-p23/`, with fresh isolated normal non-demo fixtures. No desktop process was running at initial executable-path inspection. Native computer APIs are disabled; no actual Windows UI/screenshot/relaunch or native sign-in is claimed.

`acceptance-fixture.json` prepares two sections/three Unicode rich pages, scene/card, authored notes/tags, synopsis, three canon snapshots and original PNG cover. Web uses existing Development LocalDev `dev-oid`, an isolated SQLite database and Professional test entitlement, with no onboarding demo workspace. The equivalent local native project retains original writing/IDs/planning/cover on store reopen; its private canon is merely prepared under a synthetic account scope. This preparation is not real native enrollment/sign-in/cache usability evidence. The fixture's scene links the second section; its unlinked first section directly exercises the normal multi-page editor. `web-reviewed-fixture.json` preserves the exact browser/package fixture identity and paths.

Scoped production regressions repaired:

| ID | Observed behavior / repair | Regression evidence |
|---|---|---|
| P22-001 | Normal section loading concatenated all rich pages under the first page ID and discarded the cached later-page list. Keep actual pages; add save-before-switch keyboard page picker with retained-draft error; aggregate only Preview; version restoration updates its matching page. | Actual editor helper/refusal tests; full WASM shell navigates both pages, preserves other-page IDs/content through reviewed Apply/Undo/Redo, offline failure/reconnect and reload. |
| P22-002 | Capturing every input blur synchronously invoked .NET while Blazor was rendering (`heap is currently locked`). Defer/coalesce interop, listen only to actual window blur, cancel replaced registrations. Identical saved content now settles dirty save state before navigation/AI guards. | Three focused JS cases and actual shell without page errors. |
| P22-003 | Page switching destroyed a TipTap editor while queued pagination/resize work still accessed its view. Disable interop, clear timers and cancel queued/debounced observer work during destruction. | New real shipped-editor resize/scroll/destroy/idempotent-destroy case; actual repeated page switching/reload. TypeScript and tracked bundles regenerated with `npm run build`. |
| P22-004 | Real fresh SQL migration chain referenced missing project cover and scene-card columns. Add missing discovery attributes and guarded cover-column SQL; add conditional scene-column migration. | Real SQLEXPRESS fresh/existing-cover-column upgrades and all-mapped-column audit, unchanged writing/cover/sync-version, owned HTTP concurrency/replay/restart/report cases. |
| P22-005 | SQLite legacy linked-section routing ordered DateTimeOffset in SQL and threw. Filter owned matches in SQL, then compare UTC instants in memory. | Actual MVC mixed-offset/newest/401/foreign/missing-target case; linked section still redirects to its scene as intended. |
| P22-006 | Background document refresh outlived a device panel's renderer disposal and retained `.store.lock` during test teardown. Async disposal drains already-started document refresh reads. | Previously failing full-suite teardown now passes in the final unfiltered run. |

**Final current checks:** `full-final.trx`/`full-final.log`: **1,863 passed, zero failed/skipped**, 1 minute 46 seconds, including prompt-23 store integration. Eleven additional cases relative to prompt 21. The earlier `full-sixth.trx` had 1,859 passes before the four final routing/package-data cases. The preexisting LocalStoryboard xUnit2031 warning remains in test compilation; all explicit Release builds are warning-free. Intermediate failed harness/migration/browser attempts are retained as failure evidence and superseded by the named final checks.

`WRITERAPP_P22_SQLSERVER` explicitly enabled two real disposable local SQLEXPRESS executions, not just migration scripts. SQLite upgraded from the prompt-14 aggregate baseline; SQL checked both fresh schema and a previously added cover column. `20260316113000_AddProjectCoverImageUrlSqlServer` is now discoverable and preserves an existing column. `20261004143000_AlignAiSceneCardColumnsSqlServer` adds Summary/Status/SubplotTagsJson to both scene-card tables if missing; Down refuses destructive loss, so deployed rollback requires a verified backup. The live model-column audit reports no missing columns and no pending model changes. Concurrent approved page PUTs yield exactly one winner/one conflict, replay is idempotent, fresh-host report retries produce one confirmed outcome, wrong owners are rejected and other writing/synopsis stay intact. Both owned SQL catalogs were removed; `sqlserver-cleanup.txt` confirms zero remaining `WriterAppP22_` databases. No existing/deployed database was migrated.

Actual full-shell headless Edge run (`web-release-final.log`, `web-release-results.json`) passes **16 checks**, **zero page errors**, with existing Development LocalDev and **mock-text**, not OIDC/live provider output. It exercises keyboard page navigation/menu Escape focus at 1280×720 and 1920×1080, original/proposed review and Dismiss, explicit checked single-page Apply, report-only network interruption, real IndexedDB reload/retry without reapplying writing, scoped cloud Undo/Redo, failed offline page switch retaining its original rich draft, reconnect/save/reload, both physical downloads and unchanged cover metadata. Expected disconnected-request diagnostics and the preexisting duplicate Link-extension warning are preserved in the log; zero page errors is not zero console warnings. Screenshots in `web-screenshots/` were visually inspected alongside device/client browser fixtures. Other original journeys are covered by their actual production handlers/shared components and synthetic transport suites, not extrapolated into complete web/native/live runs.

| Acceptance flow | Current exercised host/evidence | Independent gate |
|---|---|---|
| Guidance → presets → writing/continuation/outline | All actual device/shared handlers and server suites; web writing/history full shell; practice/editor Apply/reopen. | Actual native guidance/full signed-in section/continuation and semantic provider results. |
| Selection/section/document translation | Actual client/device handlers, server atomic persistence/recovery, shipped real rich mapping and render fixtures. | Native UI and real translation quality/deployed aggregate operations. |
| Canon → consistency/style → targeted revision | Full production canon/coaching/quality suites and real anchored editor mapping. | Actual native/deployed bible extraction and human-reviewed findings/fixes. |
| Scene/storyboard → field/suggested-scene review | All four board actions and actual card/creation handlers, source/CAS/field preservation tests. | Actual signed-in/native inspector journeys and provider narrative quality. |
| Synopsis → evaluation/questions/field change | Actual three-mode handlers and durable local/server planning/recovery tests. | Native/deployed interaction and meaningful provider analysis. |
| Presets/history → offline/conflict/account switch | Actual stores/auth lifecycle, receipt/queue tests, real two-tab IndexedDB; full-shell save/reload/report retry/Undo/Redo. | Real native/OIDC account switching and cloud transfer/revocation. |
| Covers → original/adjusted concepts → Save/recovery/publishing | All five typed edit operations/provider adapter tests, actual studio handlers, immutable asset caches/recovery and publishing suites. Real disk inline-cover exports below. | Trusted remote storage/provider/image visual quality, actual native studio/export dialogs. |

Real browser downloads were saved to `exports/published-cover.docx` (4,281 bytes) and `.epub` (3,792 bytes). ZIP/XML inspection opens both archives, verifies all sections/Unicode and retained offline writing, and compares embedded PNG bytes to original SHA256 `21888E3D59F2A3620E97BA88BFC283CC4A9465501EAA6B2A831859CFE7F68EF1`. Web GET `/api/projects` confirms unchanged cover source metadata after publishing. `physical-export-verification.json` records entries/hashes. DOCX and EPUB file hashes are in `web-release-results.json`. Both server and served WASM export flags explicitly opt into local EPUB acceptance. The driver selects Include cover **after** each format change and asserts the actual request flag; initial exports without that sequencing were not cover acceptance. ZIP inspection is not independent Word/ebook-reader visual rendering or native Save dialog evidence.

Available browser/editor checks: **71 real shipped-editor checks, 156 actual Razor renders, four equivalent device/client explicit Apply/reopen comparisons**, plus real two-tab outbox checks; `browser-final.log`, `browser-results.json`, `screenshots/`. Three lifecycle JS cases pass in `editor-save-events.log`. All five sequential isolated Release targets pass with **zero warnings/errors** in `artifacts/desktopai-p23/build-*.log` and `build-results.json`: server, Client, Device.Shared, Windows and available iOS `iossimulator-x64` managed compilation. Both editor assets were freshly rebuilt. No new dependency or security-feed freshness claim is made.

Reproduce with the prompt-21 fixture-directory setup adapted to `artifacts/desktopai-p22`, setting P13–P22 to that root and preparing p18/p19/p20. Set `WRITERAPP_P22_SQLSERVER` only for a configured local integrated-auth SQL instance. Run the unfiltered Release tests with `--logger 'trx;LogFileName=full-final.trx'`; then the five builds sequentially with `BaseOutputPath=artifacts/desktopai-p23/`. Start only owned hosts after checking listeners. The exact safe SQLite/mock host environment and real shell command are in [development setup](device-development.md#desktop-ai-prompts-2223--isolated-release-verification-2026-10-04). The allowlisted fixture host uses port 5189; set `DESKTOPAI_EDITOR_HOST` and `PLAYWRIGHT_MODULE`, then run `desktopai-acceptance.mjs`. Both owned hosts are stopped and neither listener remains.

**P13-G04 local database/browser/disk-export facets pass.** Deployed migration/OIDC, remote storage/real covers, independent readers/native dialogs, P13-G01 native interaction and P13-G02 human-reviewed live provider remain open. No live provider/model/latency measurement, external registration, trust change, deployment, release distribution or user writing mutation occurred.

## Prompt 23 — package and versioned-data acceptance (2026-10-04)

Inspected current Windows MSIX identity/version/dependencies, build-selected backend/data configuration, manual same-host HTTPS update flow, native MSAL registration and shared stores; inspected iOS bundle/callback/Keychain entitlements/auth settings and toolchain. No Prosa package was installed, no native EXE was running and no authorized Mac/device/signing/iOS ClientId was available. Default iOS guest/offline behavior remains deliberately unregistered. SDK 10.0.400, MAUI Windows 10.0.20 and iOS 26.5.10301 workloads are recorded in `artifacts/desktopai-p23/toolchain.log`/`workloads.log`.

`Build-ProsaTestMsix.ps1` now supports explicit Environment/ApiBaseUrl/NoRestore and an absolute Development-only ValidationDataDirectory, isolating build output beneath its output directory. Production storage overrides are rejected before certificate creation. A new read-only `Inspect-ProsaMsix.ps1` verifies package file sizes/block hashes and CMS signature without trusting the review signer, matches publisher subject and reads actual packaged assembly metadata without executing it. Both PowerShell scripts parse; actual package execution succeeds. No install/trust/update-feed work is hidden in these scripts.

**Built local review MSIX:** `artifacts/desktopai-p23/msix/WriterApp.Desktop_0.1.0.1_x64_Test/WriterApp.Desktop_0.1.0.1_x64.msix`; SHA256 **D4058CC79528376152CF80090CD941D9B0A7AC2E9A232BC8954BFDBD0B9E1A14**. Actual identity is `Prosa.WriterApp.Desktop`, publisher `CN=Prosa Development`, x64 version `0.1.0.1`, executable `WriterApp.Desktop.exe`, dependency Microsoft.WindowsAppRuntime.1.7 (minimum 7000.617.2103.0); matching architecture dependencies are present. Package inspection verifies **247 block-map files and the CMS signature**. Packaged DLL SHA256 **BCB9376FA5AC189318423876BC477AA1D330EFCB310CB5E8663DFAA5266BD066**. Actual embedded configuration is **Development**, `http://127.0.0.1:5390/`, an absolute isolated fixture-native directory and no update feed. `msix-inspection.json` records exact paths/identity/metadata; `native-candidates.json` records separate Windows EXE/iOS managed DLL hashes and `Launched=false`.

Windows `signtool verify /pa /all /v` returns **1**, specifically an untrusted root certificate; no timestamp is present. Certificate thumbprint **E0B4936EA4F3BF762DD65E07B7BBF79CED1ECECD** matches the package signer. `native-after.json` verifies its temporary private store entry was removed, neither checked TrustedPeople store contains it, no Prosa package is installed and no desktop process was launched. This establishes review-artifact integrity/configuration independently of trusted installation/distribution; no machine trust or persistent approved signing identity was invented. Reproduction and safe installation/update prerequisites are in [Windows release setup](windows-desktop-release.md#desktop-ai-prompt-23-package-verification-2026-10-04).

`DesktopAiPackageDataTests` adds **three** production-store integration cases, one for each document envelope version 1–3. They preserve authored Unicode rich writing and page/section IDs, unknown metadata, exact source backups, guide v1 ID/practice/step, queued preset and applied-history event identities, before/after approved recovery, scoped canon, original cover data and unchanged sidecar hashes. An injected interrupted migration leaves source and exact backup intact; a fresh store retries safely, reopens current schema and does not reseed extra documents. Foreign account/backend cache/transfer scopes remain empty. These execute real file stores and existing synthetic history/account fixture boundaries; they do not execute Windows package installation, iOS container migration, real Keychain or MSAL transport. The new routing case plus these three cases pass in the final 1,863-case full suite; focused `package-data.trx` also passes all five requested fixture/routing/data cases.

| Prompt-23 facet | Current result | Required acceptance infrastructure |
|---|---|---|
| Windows package build/integrity/configuration | Passed; real manifest, packaged DLL and isolated Development settings inspected. | No extra claim of trust or latest native code having run. |
| Windows clean install/higher-version upgrade | **Open**; no installed package, temporary signer untrusted, version unchanged. | Approved stable trusted signing identity/test channel, matching dependencies, controlled isolated account/data; higher same-family version and actual installed/launched path. |
| Native Windows lifecycle/auth/UI | **Open**; no native control/launch. | Actual callbacks/secure token storage/sign-out/switch, background save/cancel, offline/review/Apply/history/reopen and both-size/native accessibility evidence. |
| Windows-hosted iOS managed compilation | Passed, zero warnings/errors. | Does not establish a runnable Mac bundle or simulator. |
| Mac simulator bundle/callback/Keychain/UI | **Open**; no Mac/Xcode or authorized iOS public-client configuration. | Compatible Mac/Xcode/workload, approved registration/API scope/allowlist, reachable backend and actual simulator app identity/container/lifecycle checks. |
| Physical iOS/signing/upgrade | **Open**. | Authorized Apple team/certificate/profile with both Keychain groups, device, actual app install/upgrade/callback/touch/accessibility/reopen. |
| Distribution and live update | **Open**; no feed/release published. | Stable trusted publisher/key/channel, increasing versions, release approval and relevant prompt-22 live gates. |

All available common regression/editor/browser/build/data/package checks are complete. Precise Windows/native/Mac/simulator/device and distribution steps are in the new [Desktop AI release checklist](desktopai-release-checklist.md); full parity/release acceptance stays open. Continue in this same uncommitted checkout when authorized native/provider infrastructure becomes available. Next concrete task is to execute the remaining **prompt-22 live gates and prompt-23 native gates**, not an invented prompt 24 or repeated feature implementation. Preserve preceding work, migration backups and recovery records. No commit, deployment, live provider call, external registration, trust mutation or distribution occurred.

Final preservation/cleanup: `artifacts/desktopai-p23/preservation-audit.json` retains all **386** preexisting readable dirty files (**373 byte-identical, 13 deliberately extended, zero missing**) and all six preexisting deletions. Sixteen new or previously clean paths are listed separately in `new-or-previously-clean-files.json`; prior feature work was not reconstructed or reverted. `final-status.txt`/`final-summary.json` record exact state/check counts. `git diff --check` reports no whitespace/conflict errors (existing CRLF normalization notices remain). Neither owned loopback listener remains. Package scripts parse, reject a Production validation-storage override before signing, and reject an intentionally altered packaged DLL by block hash; the owned tamper probe was removed (`script-checks.json`).

## AI parity prompt 1 — Formatting-preserving client targeted fixes (2026-10-05)

**G01 implementation and available verification pass.** The client now retains
the unchanged bold **clock** when the last plain **clock** in its reviewed
sentence becomes **chime**. Historical failing evidence remains in
`artifacts/ai-parity-2026-10-05/`; the maintained equivalent probe and current
evidence are `WriterApp.Client/tests/verify-targeted-revisions.mjs` and
`artifacts/ai-parity-p01/`.

### Production path and boundaries

- `PageEditor.ApplyQualityIssueFixAsync` still normalizes `rewrite` to `replace`,
  supplies the checked expected text/range, and follows the existing explicit
  review/Apply and `SaveCheckedAiAsync` history/save path. It no longer retries by
  replacing the expected text with a fresh read or falling back to an unbound
  JS mutation after an interop error. Failure remains visible to the writer.
- `src/targeted-revision.ts` builds an undispatched transaction against the actual
  host schema. It changes only a common-prefix/suffix-derived span expanded to
  whole words/graphemes, retains original source marks and validates the complete
  resulting text. The client patch and device `previewTargetedQualityRevision`
  share it. Unchanged bold/italic/link marks and rich siblings remain intact.
- Newly changed wording spanning incompatible marks, ambiguous insertion marks,
  partial words/graphemes, malformed Unicode, embedded leaves and unsupported
  block crossings refuse before dispatch. Deletion may remove mixed marks
  without assigning replacement marks. Provider markup becomes inert text.
  The existing device paragraph-split preflight remains stricter; the client
  deterministic split supports one uniform paragraph with retained attributes.
- Client Apply requires exact plain and captured-document ranges. Review range
  resolution may use an exact range or a unique matching anchor; missing or
  ambiguous recovery anchors refuse. Existing checked source/revision/ownership,
  request/save boundaries and durable history are retained. Client Apply also
  dry-runs appended editor transactions and refuses any that would change
  unrelated nodes; a trailing-table page needing editor normalization is a
  documented manual-edit case, rather than an untracked structural change.
- Consistency calls the same helper with its captured `ContinuityApplyRange`.
  A separate synthetic consistency fixture exercises that resolver, full payload,
  retained italic context and exact Undo. The original quality probe is not
  treated as proof of a separately reproduced consistency defect. Device
  consistency's existing stricter preflight is unchanged.

No content schema, shared presentation contract, backend authorization/usage,
storage migration or dependency changed. Both generated editor bundles were
rebuilt. Source/asset paths changed: `PageEditor.razor`,
`DocumentEditor.razor.cs` (actionable refusal), `src/{targeted-revision,
device-editor,tiptap-editor}.ts`, client `tiptap-editor-patch.js` and
`tiptap-editor.bundle.js`, device `editor/device-editor.js`. Regression additions
are `device-editor.html`, `verify-targeted-revisions.mjs` and
`Tests/WriterApp.Tests/PageEditorTargetedFixTests.cs`.

### Checks and actual results

| Command / evidence | Result |
|---|---|
| `npm run build` in `WriterApp.Client` | Both Vite builds passed; shipped assets regenerated. `editor-build.log` |
| `npx tsc --noEmit --target ES2022 --module ESNext --moduleResolution bundler --skipLibCheck src/targeted-revision.ts` | Passed. `targeted-typecheck.log` |
| `node WriterApp.Client/tests/verify-targeted-revisions.mjs` with `PLAYWRIGHT_MODULE` pointing to the existing runtime Playwright | **109 passed, 0 failed/page errors**, including 27 new targeted cases. `browser-results.json`, `browser.log`, `editor-parity-result.json`, `targeted-fix-{1280,480}.png` |
| `dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --filter 'FullyQualifiedName~PageEditorTargetedFixTests\|FullyQualifiedName~QualityFixClientHelpersTests\|FullyQualifiedName~ActualWebQualityHandler\|FullyQualifiedName~WebQualityServer\|FullyQualifiedName~WebConsistency\|FullyQualifiedName~LocalQualityTests\|FullyQualifiedName~LocalConsistencyRevisionTests\|FullyQualifiedName~LocalQualityPanelTests' --logger 'trx;LogFileName=targeted-parity.trx' --results-directory artifacts/ai-parity-p01/results -p:NuGetAudit=false -v minimal` | **77 passed, 0 failed/skipped**. `tests.log`, `results/targeted-parity.trx`. Existing `xUnit2031` warning in `LocalStoryboardTests.cs`; no new compilation warning. |
| `dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release --no-restore -p:NuGetAudit=false -v minimal` | Passed, **0 warnings/errors**, with final assets. `client-build.log` |
| `dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/ai-parity-p01/native-build/ -p:NuGetAudit=false -v minimal` | Passed, **0 warnings/errors**, isolated output; no app launch. `desktop-build.log` |
| `git diff --check` | Passed; existing CRLF normalization notices remain. `diff-check.log` |

Browser cases cover the bold-clock regression, unchanged/changed italic and link
marks, pure deletion and insertion, inert HTML-looking prose, complete combining
words and supplementary/joined emoji, mixed changed spans, invalid Unicode/partial
graphemes, stale source/captured ranges, duplicate anchors, embedded images/hard
breaks, block crossings, rich siblings, consistency and deterministic splits.
Actual client ProseMirror Undo/Redo restores exact supported HTML/JSON. Device
checks exercise inert preview, editor Apply/source restoration and reopening;
durable device Undo/Redo, save failure and interrupted recovery are independently
covered by the focused production workflow tests. Rejected client cases retain
identical document HTML/JSON. The two-size screenshots were visually inspected.

### Remaining acceptance and next task

Full signed-in client/browser save/reload/history, real provider generation,
quotas/latency and human prose quality, actual native Windows interaction and
relaunch, packaged distribution and iOS/device acceptance were not run. Native
computer controls are unavailable in this session; compilation and shipped-editor
fixtures do not close that gate. Use synthetic owned writing with the configured
backend and the freshly built application for those checks. No exercised
implementation defect remains; unsupported ranges keep the manual-edit refusal.

Continue with **prompt 2 in `docs/ai-parity-fix-prompts.md`** (client cancellation)
in this same uncommitted checkout. Prompt 5 may depend on this tested helper for
cross-page consistency. No user manuscript, deployment, external registration,
database migration or provider request was changed/run. The owned browser fixture
and ephemeral listener are closed by the driver. Preservation inventory and
hash comparisons are in `baseline-status.txt`, `baseline-hashes.json` and
`preservation-audit.json`; deliberate extensions are distinguished from unrelated
files. The inventory was captured after the initial shared TypeScript extraction
and before the remaining edits; it is not a claimed byte snapshot of those first
edited files.

Final prompt-1 preservation check: all **496** inventoried existing readable files
remain present (**485 byte-identical, 11 deliberately extended, zero unexpected
changes**). The isolated desktop's copied device editor asset matches the final
shipped source SHA256 exactly; `final-checks.json` records that comparison and a
zero `git diff --check` exit status. This establishes build contents, without
claiming that a native application loaded them.

## AI parity prompt 2 — Client request cancellation (2026-10-05)

**G08 implementation and available checks pass.** Ordinary client writing,
quality checks and targeted rewrites, and consistency checks and targeted revisions
now use one owned cancellation lifetime. Structured translation keeps its existing
owner, Cancel and confirmed persistence/recovery flow.

### Production behavior and files

- `DocumentEditor.RequestCancellation.cs` owns the captured document/section/page,
  backend, token and retained proposal identities. Cancel detaches it immediately,
  removes its checked proposals/previews/retry mappings and resets its pending
  state. An old operation's completion cannot clear a new operation. Route/page,
  section/scene navigation, context-tab changes, account invalidation and disposal
  cancel the relevant request. Target/backend drift also fails closed.
- `DocumentEditor.razor{,.cs}` routes ordinary writing, selection translation,
  quality and consistency through that owner. `PostAiActionAsync`, checked
  source/canon/outline preparation, HTTP response/error reads, history preparation
  and targeted generation/strict retries receive its token. Checked partials
  (`CheckedAi`, `CheckedCanon`, `CheckedWriting`, `Translation`, `WritingOutline`)
  guard retention after awaited work. Supported `PageEditor.razor` selection/plain
  text/content/range reads accept the token and refuse continuation after cancel.
- `WriterApp.UI.Shared/AiRequestProgress.razor{,.css}` provides an accessible pending
  status and **Cancel AI request**. It disappears on cancellation/completion;
  cancellation has its own unchanged-writing/quota message. Proposal Dismiss stays
  a separate completed-result action. Translation options close before dispatch,
  and targeted modals publish after preparation, so Cancel remains reachable.
  Pending work cannot be applied. Ordinary errors remain visible and retry works.
- Save-before-request and notes flushing retain their uncancelled durable save
  boundary. Already-started authored saves finish even when AI is cancelled. Apply
  validates and saves the already reviewed quality/consistency candidate, without
  starting another provider generation. Existing checked source, entitlement,
  quota, rich-content, history and ownership guards remain active. The owner's
  advisory usage refresh preserves a completed result only while its account-bound
  checked leases and access state remain valid.

`WebAiCancellationTests.cs` and `WebAiCancellationUiTests.cs` add **33 cases**.
The existing checked provider fixture gains bounded output/usage hooks; its normal
authorized SQLite routes and inert synthetic provider results remain. Tests delay
preparation, reference extraction, generation and history responses deliberately
past cancellation; run all three targeted rewrite helpers through strict retry;
exercise all three fresh-request races; test navigation/disposal/account/backend
changes, late JS reads, an authored save already in progress, ordinary errors and
account-bound usage refresh. Actual compiled Cancel-button event dispatch cancels
the production owner. All synthetic writing remains exact except the save-safety
fixture's explicitly authored pending draft, which persists completely.

The existing legacy tighten helper receives the owner token too. Current
checked-outline writing deliberately skips that retry to keep a proposal's ID
bound to its reviewed result; the helper boundary is tested separately. No retry
policy was changed by this prompt. Optional prompt 11 remains outstanding.

### Verification and reproduction

Evidence root: `artifacts/ai-parity-p02/`. Builds use isolated output because the
existing user-owned Blazor host locks `artifacts/bin/Debug/net10.0`; it was left
running. The initial ordinary-output test build encountered that lock, then the
isolated build/test run completed. Tests retain the preexisting `xUnit2031` warning
in `LocalStoryboardTests.cs`; the affected host builds have zero warnings/errors.

| Check | Actual result |
|---|---|
| Scoped checked writing/source/quality/consistency/translation and desktop reference tests | **194 passed, 0 failed/skipped**, including 33 cancellation cases. `focused-final.log`, `tests/focused-final.trx`. |
| Actual shared Razor Cancel controls and callback | Three pending/idle render cases and actual button dispatch pass within the .NET suite. Generated HTML in `ui/`. |
| `node Tests/ai-request-cancellation-layout.mjs` | **6 layouts pass**, 0 page errors; enabled/visible/keyboard focus, 40px button minimum and no horizontal overflow at 1280/480 pixels. `ui/browser-results.json`, screenshots visually inspected. |
| `node WriterApp.Client/tests/verify-targeted-revisions.mjs` | **109 shipped-editor checks pass**, 0 failures/page errors. `editor/browser-results.json`; formatting-safe Apply/Undo/reopen remains intact. |
| Client Release and isolated Windows Debug builds | Pass, **0 warnings/errors**. `client-release-build.log`, `desktop-build.log`; shared/device projects build transitively. No native launch. |

The exact scoped filter and repeatable commands are saved in
`artifacts/ai-parity-p02/verification.ps1`. The static browser fixtures use actual
compiled Razor HTML and production control CSS; the actual callback is dispatched
in the framework renderer. They are not a full signed-in client/native session.
No editor algorithm or TypeScript source changed, so no generated JS asset rebuild
was required; the real shipped-editor harness still ran. No contract/schema,
database migration or dependency was added.

### Transport limits, acceptance gates and next task

Cancellation discards local results and signals supported HTTP/JS operations.
Tests intentionally let remote work finish first: a provider can still complete,
record comparison history, or consume quota after dispatch. No refund or remote
termination is promised. Local Cancel does not cancel approved persistence.

Full signed-in browser save/reload/lifecycle, real provider/quota behavior, actual
native Windows interaction/relaunch, packaged release and iOS/device acceptance
remain open prerequisites. Native computer controls are unavailable here. No
user manuscript, live provider, external registration, deployment, release package
or running user process was changed. The browser drivers close their owned
ephemeral hosts/browsers.

Continue with **prompt 3 in `docs/ai-parity-fix-prompts.md`**, including arbitrary
scene-card field selection in both client editor and storyboard inspector and
validated partial persistence/history. Continue in this same dirty checkout.
Preservation hashes/status and deliberate extensions are recorded in
`baseline-status.txt`, `baseline-hashes.json` and `preservation-audit.json`.

Final prompt-2 preservation check: all **498** inventoried readable files remain
present (**485 byte-identical, 13 deliberately extended, zero unexpected changes**).
All **six** preexisting deletions remain absent. `git diff --check` passes using
the repository's configured CRLF handling; normalization notices are unchanged.
`check-preservation.ps1` records the exact source hashes and final status. Final
verification reran the recorded commands: **194/194 tests**, **109/109 editor
checks**, **six** control layouts and both affected host builds pass. These counts
remain scoped evidence, with the independent acceptance gates listed above.

## AI parity prompt 3 — Client scene-card partial approval (2026-10-05)

Implemented **G05** in this same dirty checkout, preserving prompts 1 and 2 and
unrelated phased work. Both the document editor and storyboard scene inspector
render `WriterApp.UI.Shared/SceneCoachingReview.razor`: readable current/proposed
values, individual checkboxes, **Approve all changed fields**, **Clear selection**,
and **Apply approved fields**. Whole-card reviews start with no selected fields;
separately requested single-field suggestions retain their scope and convenience
selection. Empty selection and Discard perform no save. Invalid/unchanged/omitted
fields cannot be approved. Legacy purpose maps to the editable role or intent.
Provider markup stays inert. Planning input and selection are frozen while the
approved save runs; a navigation change cannot adopt its response into a new target.

The additive `SceneCardUpdateRequest` / `SectionSceneCardUpdateRequest`
`ApprovedFields` contract uses the existing desktop `SceneCoachingField` enum.
`SceneCardApprovals` builds the review and a payload containing only selected
provider values. `WebAiMutationFilter` requires the checked source, current card
fingerprint and immutable durable history intent for partial saves.
`WebAiHistoryOperations.ValidateNew` verifies that selected values match the
owned generated proposal. Both controllers assign only selected stored
properties, including section-to-scene mirroring, within the existing checked
transaction. Unselected raw whitespace, JSON, links, status and other metadata
are preserved. An inherited legacy section first copies its exact linked scene
baseline, preventing omitted fields disappearing when the section acquires a card.

Client orchestration is in `DocumentEditor.SceneApproval.cs`, the existing save
and history delivery partials, and `StoryboardSceneInspector.razor`. Both routes
prepare durable outbox intent before persistence and reconcile committed receipts
afterward. History's `ApprovedFields` and filtered after-payload identify the exact
approved subset; failed saves do not claim Applied. Retry with a different subset
uses a new immutable operation, retaining the earlier unresolved intent. Existing
history delivery/recovery boundaries remain intact. **Planning scoped Undo/Redo
is still prompt 8**; this implementation does not advertise new replay capability.

Available verification is recorded under `artifacts/ai-parity-p03/`:

- **106/106 focused .NET cases pass**, including **39 new** approval/control
  cases. `WebSceneApprovalTests`, `WebSceneApprovalBoundaryTests` and
  `SceneApprovalReviewTests` cover both actual client handlers against authorized
  synthetic SQLite controllers, arbitrary disjoint subsets/all, empty/clear,
  Discard, single-field scope, stale local/server source, source drift just before
  save, account/target changes, save failure, pending-save input guards, precise
  durable history, restart/readback, exact raw metadata and legacy inheritance.
  Actual compiled Razor checkbox/all/clear events dispatch through the framework
  renderer. Existing scene/canon/desktop and durable history tests also pass.
- **24 headless Edge review layouts pass** at 1280/480 pixels across shared
  editor/inspector/desktop control fixtures and empty/partial/all/busy states.
  `Tests/scene-approval-layout.mjs` uses compiled Razor HTML and production CSS,
  checking selection/disabled states, keyboard focus, inert output and overflow.
  Fixtures are shared-control evidence, not a complete signed-in host journey.
- Affected client Release and Windows desktop Debug builds use isolated output.
  Exact reproducible commands and build logs are in `verification.ps1`;
  `focused.log`, `tests/focused.trx`, `ui/browser-results.json`, screenshots and
  both build logs contain the results.
- Two old desktop tests expected `Reference: character_1`. They fail identically
  with the inventoried, byte-exact original review (`desktop-reference-baseline.log`).
  `LocalScenePanelTests` now asserts the actual resolved `Label Character`, retaining
  its unresolved-link, inert/readable review, selected-save and history assertions.
  The preexisting `LocalStoryboardTests.cs:75` xUnit2031 warning remains.

No manuscript, editor bundle/schema, sync contract or database migration changed.
No live provider, deployment, external registration, package distribution or user
process was modified. Full signed-in browser journeys, live provider/quota behavior,
actual rebuilt Windows interaction/relaunch, packaged release and iOS/device
acceptance remain separate gates; native computer controls are unavailable here.

Continue with **prompt 4 in `docs/ai-parity-fix-prompts.md`**: supply saved glossary
to desktop quality checks, preserving checked source identity and the same checkout.
For planning recovery, execute prompt 8 after its dependencies. Preservation hashes,
status and deliberate extensions are recorded in `baseline-hashes.json`,
`baseline-status.txt`, `preservation-audit.json` and `check-preservation.ps1`.

Prompt-3 preservation audit: all **504** inventoried readable files remain present,
with **487 byte-identical**, **17 deliberate extensions**, **zero unexpected changes**
and all **six** preexisting deletions preserved. `git diff --check` passes with the
repository's configured CRLF handling. Both affected builds pass with **zero warnings
and zero errors**; the separate test analyzer warning is described above.

## AI parity prompt 4 — Saved glossary in desktop quality checks (2026-10-05)

**G06 implementation is complete in this dirty checkout.** Glossary rows remain
owned cloud document data, exposed by the existing authorized glossary controller;
document sync previously carried no glossary representation. The additive,
read-only `DeviceGlossarySnapshot` version-1 endpoint retains the same owned
`IDocumentRepository.GetAsync` gate as glossary list/create/delete. It includes
the server document ID, ordered terms and a content revision hash. It omits notes,
has no provider call, and rejects oversized context whole (1,000 terms, 256
characters per term, 1 MB serialized snapshot). No entitlement/quota or ownership
policy was changed, and no new dependency or database migration was added.

`DeviceGlossaryService`, registered by shared device DI, makes one bounded refresh
attempt on explicit Check style & quality. Requests capture account generation and
use the existing authenticated device transport. `LocalGlossaryStore` atomically
persists a version-1 snapshot in a separate backend/account/local/server mapping
cache. Refresh updates/deletions replace the full snapshot, including verified
empty. Missing-cache upgrades preserve existing local files without conversion;
corrupt/unsupported cache files are preserved and omitted. Glossary loading does
not upload or mutate local writing, and works for locally edited mapped documents.

`LocalQualityPanel` passes these saved terms to `LocalQualityChecks.Analyze` and
the shared `QualityCheckContext`. It displays loaded/verified-empty, cached-offline,
cached-refresh-failed and unavailable states with last successful check time/count.
Cached terms are explicitly potentially stale. Network, timeout and invalid
responses retain only the matching valid snapshot; 401/403/404 exclude cached
terms. Rejected authenticated sessions cancel the old account's check. Sign-out or
account changes clear findings, highlights and all targeted previews. Active
document/page/editor drift during refresh is rejected before results are adopted.
There are no per-keystroke glossary calls, and ordinary offline checks remain
available without glossary context. Casing fixes retain explicit preview/Apply and
existing durable History recovery; near matches are informational.

Two server reference defects surfaced in parity verification: finding cache hashes
ignored glossary edits, and reconstruction of an informational near-match suggestion
could manufacture a replacement fix. `QualityCheckService` now hashes the prose
and ordered saved terms before cache lookup and leaves glossary information-only
findings without fixes. Old finding caches recompute on the next explicit check;
glossary authoring, dismissal ownership and manuscript content remain unchanged.

Available evidence is under `artifacts/ai-parity-p04/`:

- **77/77 focused glossary/device/quality .NET tests pass**, including 37 new
  prompt-4 cases. `DeviceGlossaryTests` covers
  restart/offline cache, verified empty, update/delete, network/timeout/500/malformed/
  wrong-document/future-contract/oversized failures, revoked access, account/backend/
  local/server identity switches, late cancellation/deletion/mapping/account changes,
  mapped unsynchronized writing and lossless old-data startup. `GlossaryParityTests`
  uses ordinary-owner authenticated synthetic SQLite controllers and equivalent
  prose/glossary fixtures for casing, near matches, Unicode, selection offsets,
  cached finding semantics, updates/deletions, foreign/anonymous access and bounds.
- `LocalQualityGlossaryPanelTests` invokes actual compiled panel handlers through
  the framework renderer, checking status, no spontaneous calls, source/account
  drift, cached offline review/Apply and reopened History Undo/Redo. Existing local
  quality and server cache regressions also pass. JavaScript preview uses the
  existing test seam; no editor implementation changed.
- **12 headless Edge component layouts pass** at 1280/480 pixels. The maintained
  `Tests/glossary-quality-layout.mjs` renders actual compiled panel states with
  production CSS: fresh/offline/refresh-failed/empty/unavailable/targeted-review,
  glossary findings, informational near matches, keyboard focus, approval presence
  and no overflow. Screenshots and `ui/browser-results.json` record the scope.
- Client Release and Windows desktop Debug builds pass in isolated output with
  zero warnings/errors. Exact commands are in `verification.ps1`; `focused.log`,
  `tests/focused.trx`, `ui-browser.log` and both host build logs retain results.
  The preexisting test-only xUnit2031 warning in `LocalStoryboardTests.cs:75` remains.

No editor bundle/schema, manuscript sync payload, authored document format or
database migration changed. No user manuscript/process, live provider, deployment,
external registration or package distribution was modified. Actual interaction in
the rebuilt Windows application, full signed-in client/device/backend journeys,
deployed old-backend behavior, live identity and packaged/iOS acceptance remain
separate gates; native computer controls are unavailable here. A backend without
the device glossary endpoint reports glossary unavailable and leaves local checks
usable. The shipped-editor harness was not rerun because editor behavior/assets
are unchanged.

Continue with **prompt 5 in `docs/ai-parity-fix-prompts.md`**: resolve and navigate
client consistency primary findings across analyzed section pages, reusing prompt
1's checked formatting-preserving fix path and this same uncommitted checkout.
Preservation evidence is in `baseline-status.txt`, `baseline-hashes.json`,
`preservation-audit.json` and `check-preservation.ps1`.

Prompt-4 preservation audit: all **512** inventoried readable files remain present,
with **503 byte-identical**, **nine deliberate extensions**, **zero unexpected
changes**, and all **six** preexisting deletions preserved. `git diff --check` passes
with repository CRLF handling. No commit/reset/clean or checkout transfer was made.

## AI parity prompt 5 — Client consistency across section pages (2026-10-05)

G07 is implemented for manuscript section pages. The client reads the existing
checked structured source, includes all ordered page identities/content and binds
each finding's unique primary quote to an actual page and local range. Provider
offsets cannot redirect a quote. Jump and review save pending edits before opening
another page, preserve the request owner during controlled navigation, wait for
the keyed editor and revalidate source/account/backend. Review refresh and Apply
report drift rather than relocate a stale proposal. Approved Apply uses prompt
1's formatting-preserving helper and the actual target page's checked save header.
Concurrent edits before PUT are rejected; save failures retain the approved rich
draft for recovery. Page versions survive restart.

Existing conflicting-passage viewing and browser intentional decisions work after
controlled and manual page switches. Their storage policy is unchanged. Separate
scene-content routes keep their single-scene persistence target; cross-page
manuscript checks use the section route. Missing/duplicate evidence, changed
writing, moved/deleted/reordered pages and unsupported rich spans give actionable
status. Requests refuse context above 100,000 plain characters or existing source
graph bounds without truncation. `purpose=consistency` permits an owned annotated
source read; ordinary structured translation and its mutation paths still refuse
anchored annotations. Foreign and anonymous reads remain unauthorized.

Main changes: `ConsistencyPagePassages` in shared contracts;
`DocumentEditor.ConsistencyPages.cs`, checked canon/range/request orchestration;
the structured-source controller's read-only purpose; and compiled
`ConsistencyPrimaryPassageView` presentation. No dependencies, schema migration,
provider configuration or editor bundle changes were added.

Evidence root: `artifacts/ai-parity-p05/`. Exact reproducible commands and the scoped
filter are in `verification.ps1`.

| Check | Result and scope |
|---|---|
| Scoped .NET production-handler/contracts/structured-translation suite | **81 passed, 0 failed/skipped** in `tests/focused.trx`, `focused.log`. Includes page two while page one is active; rich checked Apply/restart; identical prose; local/server drift; reorder/move/delete; unsaved active-page save/failure; reviewed-page guard; failed save/recovery; source race before PUT; cancellation/late navigation/retry; intentional decisions/comparison; annotated read versus mutation and foreign/anonymous ownership. Existing LocalStoryboard xUnit2031 warning remains in test compilation. |
| Final compiled presentation correction | **3 passed** in `tests/ui-final.trx`; visual inspection caught literal Razor control text in the initial label. Corrected compiled label has a plain page title, inert evidence and actionable missing/ambiguous status. |
| Review refresh and persistence recheck | **7 passed** in `tests/pages-final.trx`; final refresh changes report source drift inside the review without an unhandled error. |
| `node Tests/consistency-page-layout.mjs` | **6 layouts passed**, no page errors or horizontal overflow, at 1280/480 pixels. Actual compiled shared passage component/CSS, synthetic data; screenshots visually inspected. `ui/browser-results.json`, `ui/passages-{1280,480}.png`. |
| `node WriterApp.Client/tests/verify-targeted-revisions.mjs` | **109 shipped-editor checks passed**, no failures/page errors. Both host bundles plus production client patch preserve supported marks and Apply/Undo; `editor/browser-results.json`, `editor/editor-parity-result.json`. |
| Client Release and Windows Debug isolated builds | Both succeed with **0 warnings/errors**; `client-release-build.log`, `desktop-build.log`. Server compiles through the scoped test run. |

The deterministic test provider makes one check request for the supported direct
suggestion and no extra generation during Jump, review or Apply. Server persistence
tests use temporary owned SQLite data; editor/browser checks use synthetic prose.
They do not establish provider prose quality, an authenticated full-shell journey,
rebuilt native loading/save/relaunch or deployed/package acceptance. Native computer
controls are unavailable; no user host was stopped and no manuscript was edited.
Run those gates with an isolated signed-in document and the rebuilt binaries.

Preservation: all **520** inventoried preexisting readable files remain present;
**508 byte-identical**, **12 deliberate extensions**, **zero unexpected changes**.
All **six** preexisting deletions remain absent and `git diff --check` passes.
`baseline-hashes.json`, `baseline-status.txt`, `preservation-audit.json` and
`check-preservation.ps1` record the same-checkout audit. No commit/reset/clean,
checkout transfer, deployment or live provider call occurred. Continue with
**prompt 6 in `docs/ai-parity-fix-prompts.md`**.

## AI parity prompt 6 — Explained client Style & quality review (2026-10-05)

G02 is implemented. The quality drawer now offers **Current page** or **Current
selection**, the shared style goals and **Suggest explained revision**, alongside
existing targeted findings. It uses the desktop's `StyleQualityReview` contract
and `StyleQualityRevisionReview` controls: correction versus preference, reasons,
voice/emphasis tradeoffs, individual checkboxes and a full preview containing
only selected changes. All suggestions start selected; writers can approve one,
any subset, all or none. Empty reports and Dismiss never save writing.

Generation flushes authored edits, captures fresh rich HTML and a page-local
range, then uses the existing checked `custom_transform` selection endpoint,
source revision, saved outline and entitlement/quota checks. The backend verifies
the exact saved range and supported style goal, replacing surrounding context
with owned saved writing. Current selection never reuses a collapsed old
selection. Missing `SupportsStyleQualityReview` gives actionable status rather
than sending an unsupported request.

Every returned edit is parsed and validated before a proposal is shown. Duplicate
or overlapping quotes, malformed reports and unsupported rich changes refuse the
whole batch. Before Apply, account/backend, page identity, exact HTML, checked
source and saved outline are revalidated. Approved edits use the prompt-1 safe
transaction core in descending order, with complete-result validation and one
editor dispatch. Untouched marks/structure survive; provider HTML is inserted as
text. A refused edit cannot leave a partial manuscript. The selected result uses
one checked rich save and pre-save durable AI history intent. Save failure retains
the approved rich recovery draft; confirmed page history Undo restores the exact
original after persistence/restart. No second generation occurs during Apply.

Cancellation covers preparation, generation and preview validation. Immediate
reuse and non-cooperating late responses cannot resurrect or replace a review.
Explicit Apply and authored saves retain their existing durable save boundary.
Page/account/backend changes and new AI requests invalidate the old review.

Evidence root: `artifacts/ai-parity-p06/`. Run `verification.ps1` there for exact
filters and commands; it rebuilds both editor assets before the browser probes.

| Check | Available result and scope |
|---|---|
| Focused .NET suite | **130 passed, 0 failed/skipped**, `focused.trx`, `focused-tests.log`. Actual client handlers, compiled shared control callbacks, temporary owned SQLite persistence, saved-range preflight, desktop style contracts/approval, targeted fixes, cancellation and consistency regressions. Includes page/selection; one/subset/all; empty/none/Dismiss; malformed/overlap/ambiguity/mixed formatting; source/page/account/backend drift; race before PUT; failed save and recovery; durable rich Undo; cancellation in preparation/generation/preview. The existing LocalStoryboard xUnit2031 warning remains in test compilation. |
| `node Tests/style-quality-editor.mjs` | **109 existing + 23 new shipped-editor checks passed**, no failures/page errors; `editor/browser-results.json`. Both shipped bundles, atomic partial/all approval and single native editor Undo, safe marks, desktop rich-preview reference, stale/duplicate/overlap/embedded/mixed/Unicode refusals and inert provider HTML. Empty paragraphs and inline breaks retain their HTML while offsets match saved plain text. |
| `node Tests/style-quality-review-layout.mjs` | **14 layouts passed** at 1280/480 pixels, no page errors or horizontal overflow; `ui/browser-results.json`. Seven actual Razor-rendered states with compiled production CSS, keyboard preview, selected-only prose and disabled controls. `ui/partial-{1280,480}.png` and full-state screenshots visually inspected. |
| Editor build and focused TypeScript check | `npm run build` and `tsc` on `src/web-style-quality.ts` pass; `editor-build.log`, `style-typecheck.log`. Only the client shipped bundle changes from the task baseline; the regenerated device assets are byte-identical. |
| Client Release and Windows Debug builds | Both succeed with **0 warnings/errors**, isolated outputs; `client-release-build.log`, `desktop-build.log`. Server compiles through the test run. |

Browser probes use synthetic prose and the real editor bundles/compiled shared
components. The client persistence fixture dispatches the production handlers and
shared callbacks through a synthetic provider/JS bridge; actual rich editor
mutation is verified separately by the shipped-editor probe. This does not
establish an authenticated full-shell journey, real model prose quality, rebuilt
native loading/save/relaunch or deployed/package acceptance. Native computer
controls are unavailable; no user host was stopped or manuscript edited.

For remaining acceptance, use an isolated owned signed-in document with supported
rich prose and a backend advertising checked source plus style review. In both
hosts generate for a selection and a page, uncheck edits, inspect the selected
preview, Apply, reopen and Undo; exercise Cancel, stale source and save failure.
Confirm the rebuilt binaries before collecting native evidence. No new migration,
dependency, provider configuration or external deployment was added.

Preservation audit: all **528** inventoried preexisting readable files remain
present; **515 byte-identical**, **13 deliberate extensions**, **zero unexpected
changes**, and all **six** preexisting deletions preserved. `git diff --check`
passes. Baseline hashes/status, final status and `preservation-audit.json` record
the same dirty checkout; no commit/reset/clean or checkout transfer occurred.
Continue with **prompt 7 in `docs/ai-parity-fix-prompts.md`**.

## AI parity prompt 7 — Matching recommended writing tools (2026-10-05)

G03 is implemented for all 15 advertised tools. `WritingRecommendations` remains
the shared catalog; `RecommendedWriting` supplies a version-1 tool identity,
exact parameter whitelist, declared output target and bounded result validation.
Both hosts submit the real catalog user **and** system templates, default catalog
settings and a complete ordered mapping of the saved section. This is distinct
from reusable presets: no fake preset, arbitrary trust flag or custom instruction
bypass was added. Matching backend availability/status capabilities prevent an
older backend from receiving unsupported recommendation generation.

| Tools | Supported output and approval |
|---|---|
| Deepen Character, Raise Stakes; Tighten Prose, Sharpen Ending, Heighten Theme; Clarify & Simplify, Strengthen Argument, Add Structure; Improve Readability; Improve Flow | Complete section run-text revision, all pages reviewed, explicit manuscript approval. Existing blocks, page order, marks and boundary whitespace stay in place. Add Structure uses signpost transitions within those boundaries. |
| Improve Hook | Opening paragraph on the first page; later paragraphs/pages must remain identical. Heading/embedded/empty openings require manual editing or another tool, explained before generation. |
| Continue Scene, Expand Idea | Exactly one new paragraph appended at the section end; retained writing stays in place. Client requires opening the last page before generation; desktop targets it automatically. Leading source echoes, recaps consisting only of existing text, invalid prose and multi-paragraph output are refused. |
| Generate Headlines | Exactly five distinct, bounded headline strings; shared radio selection, Copy selected text and Dismiss. No manuscript Apply or save. |
| Summarize Clearly | One concise summary string using the catalog's 2–3 sentence instruction; same copy-only review. Sentence quality/accuracy requires model and human assessment. |

The desktop ordinary writing panel now has Writing intent, Recommended tool,
explicit target guidance and Run recommended tool. Its separately identified
Recommended craft focus still guides the existing next-paragraph action. The
client existing intent recommendations now reach valid checked execution rather
than failing for missing reusable metadata. Shared `RecommendedTextReview`
encodes provider text and never renders it as HTML.

Preparation saves first, captures every saved page, checks source identity and
revision, saved outline, ownership, account/backend, entitlement and quota.
Backend mapping is verified against owned saved HTML for either host, including
requests without a client WebSource. Forged run text, templates or context cannot
be substituted. OpenAI receives the actual catalog system/user instructions plus
the existing strict complete-run schema or a strict exact-count items schema;
incomplete/malformed results are rejected by provider, executor and host layers.
Recommendation proposals expose no generic flat replacement operations.

Before approval/copy the hosts recheck reviewed source and account/outline.
Multi-page revisions reuse checked atomic persistence and original-copy recovery
on client, and immutable before/after snapshots with scoped Apply/Undo/Redo/resume
on desktop. The additive optional `LocalAiHistory.RecommendationId` is retained
and cannot be changed during history transitions; older null entries remain
valid. Copy-only results create no local recoverable manuscript entry. Client
paragraph approval uses its existing checked page save and durable page Undo.
Client aggregate section Undo/Redo remains **prompt 9 / G04**; original-copy
recovery is available now. No recovery state was fabricated for older entries.
Cancellation retains prompt-2 owned request semantics, immediate reuse and late
response protection; approved saves keep their durable persistence boundary.

Main changes: shared `RecommendedWriting`, availability/status contracts and
`RecommendedTextReview`; client recommended request/capture/review/copy paths;
desktop `LocalWriting`, `LocalWritingPanel`, local history metadata; controller
preflight, `CustomTransformAction`, `AiActionExecutor` and OpenAI schema/validation.
No editor schema, TypeScript algorithm or shipped editor asset changes, database
migration, new dependency, external configuration or deployment were needed.

Evidence root: `artifacts/ai-parity-p07/`; `verification.ps1` contains exact
filters/commands and fails on test, browser, build or preservation errors.

| Check | Available result and scope |
|---|---|
| Focused .NET suite | **328 passed, 0 failed/skipped**, `focused.trx`, `focused.log`. Catalog-wide shared contract, actual controller/action/executor, client/desktop handlers, actual Razor callbacks, provider HTTP request/schema seam, owned SQLite checked persistence and local recovery. Includes rich multi-page/empty final page, opening bounds, copy-only/no save, malformed/stale/forged source/templates/context, plan/ownership/capability, account/backend changes, cancellation/immediate reuse/late result, page Undo, section original-copy recovery, desktop Undo/Redo and idempotent interrupted-save resume. Prior reusable, style, cancellation and structured translation regressions pass. The existing LocalStoryboard xUnit2031 test warning remains. |
| `node Tests/recommended-writing-editor.mjs` | **109 existing + 8 recommendation checks pass**, no failures/page errors; `editor/browser-results.json`. Both shipped bundles, matching rich section-map previews, opening-only changes, empty paragraphs, actual web append command, inert provider-like markup, one editor Undo and refusal without mutation. |
| `node Tests/recommended-writing-layout.mjs` | **20 rendered layouts pass** at 1280/480 pixels, no page errors/horizontal overflow; `ui/browser-results.json`. Actual compiled shared copy controls in both host contexts and four actual desktop panel handler results, production scoped CSS, keyboard radio selection, disabled busy state, output-specific approval and copy button sizes. Representative narrow/wide screenshots visually inspected. Static select values are initialized as the Blazor runtime would initialize their DOM properties. |
| Client Release and Windows Debug builds | Both succeed with **0 warnings/errors**, isolated outputs; `client-release-build.log`, `desktop-build.log`. Server compiles through the tests. |

The browser probes use synthetic prose, real shipped editor helpers and rendered
production components. Host persistence tests dispatch actual handlers with a
synthetic provider/JS bridge; separate browser checks establish real editor
mutation. These do not establish an authenticated complete shell journey,
clipboard permission in that shell, live model prose/headline quality, native
WebView loading/save/relaunch, or deployed/package acceptance. Native computer
controls are unavailable. No user host was stopped, provider called or authored
manuscript used for a probe.

Remaining acceptance: in isolated owned signed-in documents, generate Deepen
Character/Raise Stakes on rich multi-page prose, review/Apply/reopen/recover;
generate five headlines, select another and copy without changing writing;
review a summary; append on the last page and Undo; revise a supported opening;
exercise Cancel, stale source and failed save. Confirm the actual rebuilt native
binary before collecting desktop evidence, and verify matching backend
capabilities/provider configuration separately.

Preservation: all **537** inventoried preexisting readable dirty files remain
present, **515 byte-identical**, **22 deliberate extensions**, **zero unexpected
changes**; all **six** preexisting deletions stay absent. Baseline/final status,
hashes and `preservation-audit.json` record the same dirty checkout;
`git diff --check` passes. No commit/reset/clean or checkout transfer occurred.
Next concrete task: **prompt 8 in `docs/ai-parity-fix-prompts.md`**, client
scene/synopsis scoped durable history recovery; keep this uncommitted checkout.

## AI parity prompts 8–9 — scoped client history recovery (2026-10-06)

Prompts 8 and 9 are implemented in this same uncommitted checkout. The initial
planning-only dependency checks passed before aggregate content changes began.
The dated prompt-7 aggregate limitation is superseded for new supported checked
replace actions; older records have no invented recovery state.

Prompt 8 captures exact raw persisted scene/section-card and synopsis field
values in the checked-save transaction. Recovery owns only values actually
changed by Apply, including affected section-card mirrors. Null/empty values,
raw tag JSON and whitespace are retained. Partial approval, all ten synopsis
fields, unrelated later planning, linked mirrors, repeated Undo/Redo, restart,
deleted/foreign targets and injected snapshot/outcome failure are exercised.
The new planning-only suite passes **19** cases (`artifacts/ai-parity-p08/planning.trx`).

Prompt 9 captures version-2 rich snapshots in `WebTranslationsController.Commit`,
inside the existing transaction with all pages, mirrors and terminal receipt.
It retains scoped page/section identities and ordering, exact changed HTML,
actual language changes and mirror creation. Replace does not add/remove or
reorder pages; those identities are validated, never fabricated as flat text.
All affected values and identities must match before the first restore write.
Later edits to unaffected fields/pages are preserved; changed affected content,
order, membership, language or mirrors refuse. The current checked source guards
the gap between fresh capture and replay. Server-owned receipts and snapshots
remain recoverable after lost acknowledgement; retrying an identical operation
returns its saved receipt without another mutation. Replay does not generate AI
or consume generation quota. Failed content/history storage rolls back together.

Translated section/document copies deliberately have **no destructive Undo**.
They and later edits remain intact; the existing **Recover original as separate
document** action stays available. Commit retries reuse original result IDs,
and original-copy retries preserve edited originals. Legacy aggregate receipts
and provider comparisons remain comparison-only. These are same-backend owned
graph snapshots, not portable device-local/cloud replay. Desktop local history
continues to use its own exact snapshots.

The shared `WebAiRecoveryPanel` is reachable from the document AI/history drawer,
Storyboard scene inspector and Synopsis page. It shows persisted Undo/Redo state,
legacy/copy explanations and inert scoped field/page comparisons. Current drafts
save first under disabled editing controls; failed saves refuse recovery. The
editor reloads saved pages after recovery. Reporting still uses the durable
browser outbox; the atomic server outcome does not depend on a successful later
report. Snapshot data never comes from provider prose. The additive history ReplayScope keeps scoped metadata from enabling legacy page Undo. Raw comparisons bypass the
scene-proposal JSON renderer, a presentation defect caught by the browser probe.

Main production files: `WebPlanningRecovery`, `WebAggregateRecovery`,
`WebAiRecoveryController`, `WebAiRecoveryContracts`, checked mutation filter,
aggregate/history controllers, `EfCoreAiActionHistoryStore`, Synopsis Apply,
`DocumentEditor.ScopedRecovery` and reachable history controls. The shared history
panel's opt-in plain comparison mode leaves normal desktop preview behavior
unchanged. No editor algorithms/bundles, dependencies, deployment, provider
configuration or user manuscripts were changed.

| Available check | Result and exact scope |
|---|---|
| Integrated focused .NET suite | **285 passed, 0 failed/skipped**, `artifacts/ai-parity-p09/focused.trx`. New 19 planning, 16 aggregate and 12 control/migration cases plus existing checked approvals, page/scene-content outbox replay, recommendation/structured translation handlers, device history and local scene/synopsis/writing recovery. Uses actual compiled handlers and ordinary owned SQLite persistence with synthetic provider/auth/JS transport. |
| Final control/history subset | **47 passed, 0 failed/skipped**, `controls-final.trx`. Actual compiled client Undo/Redo buttons, saved state refresh, report delivery, shared raw comparison states, legacy/copy/busy restrictions, migration preservation and desktop history controls. Overlaps the integrated suite; counts are not additive. |
| Rich editor browser probe | **109 existing + 8 recommendation checks pass**, `editor/browser-results.json`, no errors. Both shipped editor bundles; unchanged mapping/formatting/append/Undo behavior. No editor source regeneration needed. |
| Scoped history browser probe | **12 state/viewport checks pass**, `ui/browser-results.json`, no page errors or horizontal overflow at 1280/480. Six compiled Razor states, production scoped CSS and local icon font, keyboard expansion, inert comparisons and disabled unsupported/busy actions. Narrow/wide screenshots visually inspected. Actual C# callbacks are tested separately. |
| Builds | Server Debug through tests, client Release and Windows Debug succeed; client/Windows builds have **0 warnings/errors**, isolated `artifacts/ai-parity-p09/*-build/`. The existing LocalStoryboard xUnit2031 test warning remains. |
| Migration safety | SQLite upgrades preserve earlier history and leave `RecoveryJson` null; existing writing/sync/device migration tests pass. SQL Server idempotent script/model checks add only nullable snapshot storage, with no backfill/destructive SQL. No SQL Server execution or deployed migration occurred in these prompts. |

Reproduce with `artifacts/ai-parity-p09/verification.ps1`; it records exact scoped
filters and runs tests, `node Tests/scoped-recovery-layout.mjs`,
`node Tests/recommended-writing-editor.mjs`, isolated builds and preservation.
`artifacts/ai-parity-p08/verification.ps1` retains the planning-focused filter.
Matching migrations are SQLite `20261006100000_WebScopedPlanningRecovery` and
SQL Server `20261006100001_WebScopedPlanningRecoverySqlServer`; deployment must
include earlier WebAiHistoryOperations/translation/sync migrations too.

Available implementation and synthetic acceptance pass. **Full signed-in browser,
rebuilt native WebView save/relaunch, live-provider prose quality and deployed
schema/entitlement/sync acceptance remain open.** Native controls are unavailable;
no user process was stopped or provider called. On isolated owned migrated data,
apply selected scene fields and a synopsis alternative, Undo/reload/Redo; apply a
rich multi-page section revision and section/document translation, Undo/reopen/
Redo; edit an affected page to confirm refusal; edit an unrelated page to confirm
preservation; create and edit a translated copy and recover its separate original.
Verify the launched native binary before collecting desktop evidence.

Preservation: all **550** inventoried preexisting readable dirty files remain,
**527 byte-identical**, **23 deliberate extensions**, **zero unexpected changes**;
all **six** prior deletions remain absent. The baseline, final status and hashes
are audited in `artifacts/ai-parity-p09/preservation-audit.json`;
`git diff --check` passes. No commit/reset/clean or checkout transfer occurred.
Next concrete task: **prompt 14 in `docs/ai-parity-fix-prompts.md`** for fresh
integrated parity acceptance of prompts 1–9. Optional prompts 10–13 remain unrun;
do not describe their policies as implemented.

## AI parity prompt 10 — Durable desktop quality dismissal (2026-10-06)

G09 is implemented under the optional policy adopted by invoking prompt 10. Desktop
findings now retain dismissal through rerun/restart in an atomic, account/backend/
document/page journal, with occurrence and mapped cloud identity. Any exact page-text,
ordered glossary or catalog revision change makes the finding eligible again; this
deliberately errs toward re-showing findings rather than hiding a changed passage.
Selection decisions remain local. Signed-out decisions remain isolated from accounts.

The initial trace found `DismissIssueAsync` storing `(UserId, PageId, IssueKey)`
indefinitely, while the shared engine key covers rule, offsets, anchor and message
but omits checked source and glossary revision. Therefore legacy client decisions
cannot establish source equivalence. They retain existing client behavior and are
reported as separate by the new contract; they are not silently migrated/imported.
There was no persisted desktop current-check dismissal to backfill.

Production changes:

- `WriterApp.Shared/Quality/QualityDismissalContracts.cs` defines exact shared source
  hashing, the version-1 exchange/receipt and shared review rows. New server keys
  use `qd1:` plus a source/occurrence hash within the existing 128-character field.
- `Controllers/DeviceQualityDismissalsController.cs` uses existing ownership lookup,
  serializable sync-clock locking and shared deterministic rules. It validates the
  complete source/context and every pending key before one atomic desired-state
  exchange. Repeating a state is idempotent; failed storage rolls back. No provider,
  authored content, entity/schema change or migration is involved.
- `QualityCheckService` retains legacy filtering and additionally filters current
  source-bound page decisions in computed/cached results and issue listing. Its
  existing client Restore route clears matching cached scoped keys too. The shared
  source hash preserves the prior cache-version-2 formula and ordered context.
- `LocalQualityDismissalStore`, `DeviceQualityDismissals`, DI registration and the
  actual `LocalQualityPanel` save durable decisions before delivery, guard current
  account/source/mapping, retain pending restores and load verified backend states.
  Old mapped evidence survives remapping. Failed/unmapped/unsupported responses do
  not acknowledge the outbox or suppress unrelated occurrences. Local atomic write
  failure preserves the previous file and prevents delivery.
- Shared `QualityDismissalReview` is reachable in Writing → Style & quality after
  Check style & quality. It offers keyboard-accessible Review dismissed findings /
  Restore finding and discloses current/inactive source and pending/failure state.
  Earlier-source Restore clears local suppression and queues a tombstone until that
  exact full-page source can map again; it does not reopen a different current source.

Mapping limits are deliberate and visible: explicit cloud page/document IDs,
signed-in account, synchronized document, freshly verified glossary, compatible
rule version and exact full-page text/findings are required. There is no guessed
local/server ID equivalence. Retry/reconnect is **the next explicit full-page check**
after document synchronization; selections, stale source and legacy client decisions
do not claim automatic cross-host continuity. Old/new backend support is diagnosed
as failed/unsupported delivery while local decisions remain usable. Journal limits
are 1,000 decisions/2 MB per page, with 200 findings/100 KB receipt and a 10-second
exchange deadline. Unknown/corrupt files are preserved rather than overwritten.

Available verification in `artifacts/ai-parity-p10`:

| Check | Actual result |
|---|---|
| Focused .NET quality/device/glossary/contract/host-handler suite | **142 passed, 0 failed, 0 skipped**; `focused.trx`, `focused.log`. Actual panel callbacks, duplicate occurrences, restarted local journals/server, changed passage with identical finding key, glossary/rule/range drift, isolation/remapping, inactive-restore/source reversion, failed atomic local writes, offline dismissal/restore/reconnect, malformed/foreign receipts, client legacy preservation/Restore, backend ownership/stale/malformed limits and transactional storage rollback/idempotent retry. The device service talks to the actual authenticated test server in the convergence test; synthetic identity is confined to the test host. |
| `node Tests/quality-dismissal-layout.mjs` | **16 layouts passed**, zero page errors and overflow at 1280/480 pixels. Eight actual compiled Razor states with production scoped CSS: synced, pending, failed, unmapped, conflict, inactive, busy and restore-pending. Keyboard details/Restore focus, disabled controls and HTML-shaped inert excerpts checked. `ui/browser-results.json`; pending-480 and inactive-1280 screenshots visually inspected. Actual state-changing callbacks are covered by .NET tests, not static fixtures. |
| Client Release build | **Passed, 0 warnings/errors**; `client-build.log`. |
| Windows desktop Debug build after final changes | **Passed, 0 warnings/errors**; `desktop-build.log`. |
| Server/shared/device/UI compilation | Passed through the final focused suite; only the preexisting `LocalStoryboardTests` xUnit2031 warning remains. |
| Preservation | All **564** inventoried preexisting readable dirty files remain; **556 byte-identical**, **8 deliberate extensions**, zero unexpected changes; all six prior deletions remain absent. `baseline-status.txt`, `baseline-hashes.json`, `preservation.json`; `git diff --check` passes. |

Run `artifacts/ai-parity-p10/verification.ps1` to reproduce the focused synthetic
checks, browser evidence and isolated builds. The test authoring initially corrected
an incorrect rule-name assumption and a repeated-word fixture with an extra valid
occurrence. A browser fixture-name collision was separated from actual-panel
fixtures; final checks above pass against the final implementation.

Not run/remaining gates: a full signed-in deployed client/Windows journey and
relaunch of the actual rebuilt native binary with isolated owned data. No user
manuscript or host was modified, no process stopped, no provider invoked and no
deployment or packaging performed. In that isolated environment: dismiss one of
two identical occurrences offline, restart, reconnect/synchronize/full-page check,
verify web page checks reflect the scoped decision, Restore in either host and
check convergence; edit passage/glossary and confirm eligibility, then switch
accounts to confirm isolation. Verify native binary provenance before claiming
native acceptance. No live AI generation is needed for deterministic dismissals.

Next concrete task: **prompt 11 in `docs/ai-parity-fix-prompts.md`** if adopting
its optional targeted-result retry policy; otherwise prompt 14 for fresh integrated
acceptance of prompts 1–10. Optional prompts 11–13 remain unrun. Preserve this same
uncommitted checkout; historical prompt-9 handoff above remains dated evidence.

## AI parity prompt 11 — Shared opt-in targeted-result retry (2026-10-06)

Implemented **prompt 11** and adopted its optional policy for G10 in this same
uncommitted checkout. Both quality panels expose `TargetedQualityRetryOptions`:
**Allow one automatic strict retry for targeted fixes**, off when created, with
explicit possible additional quota consumption. Busy controls reject preference
changes. No saved preference silently enables retry. Deterministic local corrections
continue with zero provider generations.

The former client repetition/sentence/passive handlers now delegate to
`DocumentEditor.TargetedQualityRetry.cs` and shared `TargetedQualityRetry`.
`LocalQualityPanel` uses the same validator, eligibility, strict instruction and
bounded orchestration. Only usable prose which is unchanged or does not improve
the supported rule is retry-eligible. The second candidate receives the same full
validation. Empty, metadata/markup/instruction-shaped or severely shortened output
cannot retry; unsupported targets refuse before generation. Supported passive
patterns remain the existing English heuristic, not a factual or literary-quality
proof. Broader style review, consistency and other action policies are outside this
prompt.

Host-owned execution retains normal authentication, entitlements and quota checks.
Infrastructure exceptions never become semantic failure: client HTTP failures now
throw instead of returning empty prose. Source/account/revision and exact rich-range
checks run before/after each attempt and again after retry notification. Cancellation
propagates through both attempts; late/superseded results cannot schedule retry or
be offered for Apply. Invalid results show an explanation and manual retry guidance.
Only the final valid candidate is associated with approval/history; validation itself
never writes manuscript content.

Read-only client preflight extracts `validateTargetedRevisionRange` from the existing
shared editor Apply guards, without creating/dispatching a transaction. Both editor
bundles were rebuilt from source. Apply retains no-change refusal, minimal changed
spans, safe Unicode, embedded/block restrictions, mixed-mark refusal and exact
complete-result checks. Existing checked saves, revision guards and history recovery
remain authoritative. No credential/provider configuration, database schema,
external registration or deployment changed.

Available verification in `artifacts/ai-parity-p11`:

| Check | Actual result |
|---|---|
| Focused .NET targeted/shared/local/client-handler/cancellation/quality suite | **187 passed, 0 failed, 0 skipped**; `focused.trx`, `focused.log`. Actual host callbacks and adapter request counts for all three rules: valid first=1, invalid first with default-off=1, opted-in invalid first=2 maximum; valid/invalid second, strict instruction, manual error, unchanged writing/no invalid Apply. Authentication/quota/network/cancellation/stale source/anchor/embedded/metadata failures, between-attempt cancellation/source change, late responses and owner isolation covered. A valid second desktop candidate remains unapplied until approval, then saves the correct history snapshot and passes scoped Undo/Redo checks. Client final-candidate history linkage is asserted. |
| `npm run build` in `WriterApp.Client` | **Passed**, both shipped editor bundles regenerated; `editor-build.log`. |
| `node Tests/targeted-retry-editor.mjs` | **109 existing + 12 new checks passed**, zero page errors. Both shipped schemas: plain/marked passage, stale anchor, block image, cross-block and hard-break preflight; every preflight leaves editor HTML unchanged. `editor/browser-results.json`. |
| `node Tests/targeted-retry-layout.mjs` | **8 layouts passed**, zero page errors/overflow at 480/1280 pixels. Four compiled Razor states with production scoped CSS, default-off/opted-in/busy controls, keyboard focus/space and accessible quota description; `ui/browser-results.json`. Both screenshots visually inspected. Static layout fixtures do not claim interactive host callbacks, which are separately tested above. |
| Client Release build | **Passed, 0 warnings/errors**; `client-build.log`. |
| Windows desktop Debug build | **Passed, 0 warnings/errors**; `desktop-build.log`. |
| Server/shared/device/UI compilation | Passed through final focused suite; only the preexisting LocalStoryboardTests xUnit2031 analyzer warning remains. |
| Preservation | All **575** inventoried preexisting readable dirty files remain: **559 byte-identical**, **16 deliberate extensions**, zero unexpected changes. All six prior deletions remain absent; `baseline-status.txt`, `baseline-hashes.json`, `preservation.json`. `git diff --check` passes. |

Run `artifacts/ai-parity-p11/verification.ps1` to reproduce the scoped tests,
browser checks and isolated builds. Initial compilation corrected a nonexistent
busy-property assumption. The first real-browser preflight exposed Apply's intended
no-op refusal; the implementation now calls extracted range guards directly. The
browser harness was corrected to use the existing completion signal, the device
snapshot API and the actual block-image schema. Final checks above use the corrected
production code and regenerated assets.

Not run: full signed-in deployed client and actual rebuilt/relaunched native host
with isolated owned data, live-provider quality/quota consumption or packaged
release acceptance. No user manuscript modified, process stopped, provider invoked,
deployment performed or package distributed. Remaining environment journey: in each
host run a synthetic finding, confirm default-off refusal/manual retry, explicitly
opt in, use a controlled invalid-first response and observe only one strict extra
request; cancel/edit the source between attempts and confirm no Apply; approve a
valid final result and reopen/Undo/Redo its supported scoped history. Record actual
provider request/quota accounting and native binary provenance separately.

Next concrete task: **prompt 12 in `docs/ai-parity-fix-prompts.md`**, explaining
explicit reusable-prompt continuity if adopting that optional policy; prompt 13
aligns tone choices. Prompt 14 performs fresh integrated acceptance of all adopted
prompts. Preserve this same checkout and its uncommitted work; prompts 12–14 remain
unrun. Earlier next-task notes above are dated historical handoffs.

## AI parity prompt 12 — Explained manual reusable-prompt continuity (2026-10-06)

Implemented **prompt 12** and adopted its optional policy: retain explicit
local/cloud transfers and existing conflict review. G11 is now an **intentional,
explained manual workflow**. This does not introduce automatic library synchronization
or claim that every host's existing copies always have identical contents.

Both reachable prompt libraries use `PromptLibraryContinuity` in UI.Shared. Its
host-specific introduction labels local device versus cloud storage; keyboard-accessible
help explains desktop local Save → Copy / update cloud → Send/retry, client direct
cloud Save, destination Refresh and desktop Import. First transfer creates a cloud
copy; subsequent transfers use its confirmed link. An imported local copy is not
an automatic upload link. Reimporting one cloud revision reuses its existing copy,
preserves later local edits and does not revive explicit local deletion. Account,
backend and project context must match. Offline local management and existing AI
connection/sign-in/quota requirements remain unchanged.

`PromptLibraryItem` adds optional presentation-only origin/transfer status, separate
from unsupported-preset notices so supported Run/Edit/Pin are not disabled by status.
Desktop labels local presets without recorded cloud provenance, imported cloud copies,
pending/conflict snapshots and confirmed/canceled receipts. It flags local changes
made since a retained snapshot. Pending includes failure or a lost acknowledgment:
the backend may already have committed, and retry must reuse the retained operation.
A completed receipt confirms that operation, not current cloud state. Canceling an
unacknowledged intent cannot undo a remote commit. The client offers explicit
**Refresh cloud presets** through its existing loader and labels entries as cloud
presets; Save/Delete messages explain the effect on desktop copies.

The existing `PromptTransferReview` still displays both full templates, parameters
and metadata before Keep both, Keep cloud/import or Cancel. Keep both queues a
separate cloud copy requiring Send; Keep cloud retains local edits and imports
separately. Local deletion leaves cloud copies; cloud deletion leaves local copies.
No queue/request/storage/retry/conflict/account filtering, preset execution,
editor Apply/history or database/migration behavior changed. No new behavioral
regression suite was added for this presentation task: existing suites were reused,
with snapshot evidence added to existing panel tests and two focused compiled-help
UI cases. A one-off acceptance program reuses existing synthetic test seams.

Available verification in `artifacts/ai-parity-p12`:

| Check | Actual result |
|---|---|
| Focused existing reusable-prompt transfer/contracts/endpoints, actual desktop library/writing panel and compiled-help UI checks | **137 passed, 0 failed, 0 skipped**; `focused.trx`, `focused.log`. Offline local create/edit/pin/delete, lossless metadata, source deletion, import idempotence/local edits, lost-response/restart retry with one backend commit, concurrent queue creation, cloud edit/delete conflict choices, owner/backend isolation, bad/old backend receipts, typed execution, review/approval and scoped writing behavior remain passing. |
| Synthetic edit → explicit transfer → other-host refresh/import journey | **Passed**; `acceptance.log`, `ui/synthetic-continuity-journey.json`. Actual DocumentEditor `LoadPromptPresetsAsync`, two DevicePromptLibrary/store instances and authenticated in-process backend. Local save stayed local; first explicit transfer became visible only after client Refresh; later local edit remained absent until explicit update, using the same cloud identity. The loaded client retained its old value until Refresh. A separate desktop Refresh loaded only cache; explicit Import created one local copy, and repeated same-revision Import reused it. Complete manuscript page DTOs remained identical. |
| `node Tests/prompt-continuity-layout.mjs` | **14 layouts passed**, zero page errors/overflow at 1280/480 pixels. Compiled shared cloud/device help and actual LocalPromptPanel offline, pending-failed, conflict, confirmed and imported snapshots with production scoped CSS. Keyboard help expansion/focus, disabled signed-out transfers, no-confirmation wording, retained retry button, full version comparison, confirmed receipts without pending retry, imported origin and narrow layout checked. `ui/browser-results.json`; conflict-480 and cloud-help-1280 screenshots visually inspected. Static snapshots do not execute host callbacks; those are covered by the existing .NET suites. |
| Client Release build | **Passed, 0 warnings/errors**; `client-build.log`. |
| Windows desktop Debug build | **Passed, 0 warnings/errors**; `desktop-build.log`. |
| Server/shared/device/UI compilation | Passed through focused checks/acceptance; only the preexisting LocalStoryboardTests xUnit2031 test analyzer warning remains. |
| Preservation | All **585** inventoried preexisting readable dirty files remain: **574 byte-identical**, **11 deliberate extensions**, zero unexpected changes. All six prior deletions remain absent. Both editor bundles and transfer/storage/controller implementations remain byte-identical. `baseline-status.txt`, `baseline-hashes.json`, `preservation.json`; `git diff --check` passes. |

Reproduce with `artifacts/ai-parity-p12/verification.ps1`; the standalone acceptance
program is under `artifacts/ai-parity-p12/acceptance`. Its initial read assumed the
shared preset DTO, whereas the existing client uses its own nested DTO; reflecting
its loaded names corrected the probe without changing production contracts. The
final journey runs production adapters against the existing authenticated test host,
with synthetic identity/data only. No new dependency or provider call was needed.
Editor algorithms/assets were unchanged, so unrelated editor rebuilds were not rerun.

Not run: full signed-in deployed browser UI, actual rebuilt/relaunched native host,
installed package or deployed backend/migration acceptance. No user manuscript,
existing library or running process was changed; no deployment or distribution.
Remaining full-host reproduction: use the same owned account/backend with synthetic
presets, save desktop edits, interrupt a send, restart and retry its retained snapshot;
refresh the client, verify one cloud copy, make a conflicting cloud edit and compare
both versions before choosing a resolution. Refresh/import on another desktop,
edit the imported local copy and verify Refresh does not replace it. Sign out/switch
accounts and confirm imported/queued account data is isolated. Verify actual native
binary provenance before claiming native acceptance; provider generation is not
needed to establish preset-library continuity.

Next concrete task: **prompt 13 in `docs/ai-parity-fix-prompts.md`** to adopt its
optional shared tone policy. Prompt 14 then performs fresh integrated acceptance of
all adopted changes. Prompts 13–14 remain unrun. Continue in this same uncommitted
checkout; earlier next-task notes remain dated historical handoffs.

## AI parity prompt 13 — Shared supported tone choices (2026-10-06)

Adopted the optional G12 policy in `docs/ai-parity-fix-prompts.md`: preserve existing
tones and add Executive to the shared advertised descriptors with its existing
client meaning. `WritingActions.ToneDescriptors` now supplies the six stable values
Neutral, Formal, Casual, Friendly, Technical, Executive, selector labels and legacy
client preset labels. The client `_aiActionPresets` and shared `WritingOptions`
derive their tone entries from it. Existing Friendly/Technical client labels,
Shorten, Fix grammar, action identifiers and parameter values are preserved.

The actual client Executive preset sends `rewrite.selection`, `tone=Executive`,
`length=Same`, `preserve_terms=true`, `instruction=Rewrite (Executive)`. Desktop
`LocalWriting.Prepare` now supplies that same instruction for its new Executive
selection rewrite. It does not replace saved preset parameters. Shared command
validation accepts Executive; backend RewriteSelectionAction forwards the options
and instruction unchanged, and ChangeToneSection retains its existing generated
instruction. OpenAiProvider's existing prompt construction uses literal
`Tone: Executive.` plus `Instruction: Rewrite (Executive)`. No new business-style
interpretation, model/provider configuration or live generation was introduced.
The backend's existing support for arbitrary saved custom tone text is retained;
the shared advertised choices are not a new restriction on stored presets.

Available evidence is under `artifacts/ai-parity-p13`:

| Check | Actual result |
|---|---|
| Focused tone contracts, actual client handler, desktop panel, writing endpoints, local writing/recovery, reusable-prompt compatibility and revise-action suites | **125 passed, 0 failed, 0 skipped**; `focused.trx`, `focused.log`. Every advertised tone is accepted by shared validation and reaches selection-rewrite and mapped-section tone endpoints/provider request construction. All six actual client preset handlers create checked proposals with one synthetic request and unchanged manuscript pages; Executive's four style parameters match the actual desktop prepared request. The desktop setting callback previews Executive with unchanged saved content and existing review. |
| Saved preset compatibility | Existing contracts remain passing. New focused cases save/reload Executive, Formal, dramatic and cinematic without canonical definition loss, then validate their exact typed execution parameters. Existing local writing tests verify scope/parameters, stale/cancelled Apply refusal, interrupted save/recovery and Undo/Redo across restart. |
| `node Tests/writing-tone-layout.mjs` | **10 layouts passed**, zero page errors/overflow at 1280/480 pixels; `ui/browser-results.json`. Actual compiled desktop panel and shared rewrite/tone-only/busy WritingOptions snapshots use production scoped CSS. Checks cover all six option values, Executive selected, keyboard focus/Home/End, disabled busy fields and enabled Preview rewrite. Client catalog evidence is extracted from actual preset objects with preserved labels and focusable Executive. It is a catalog snapshot, not the complete authenticated client shell. Desktop-480 and catalog-1280 screenshots visually inspected. Static snapshots do not execute host callbacks; focused .NET checks cover those separately. |
| Client Release build | **Passed, 0 warnings/errors**; `client-build.log`. |
| Windows desktop Debug build | **Passed, 0 warnings/errors**; `desktop-build.log`. |
| Server/shared/device/UI compilation | Passed through focused checks; the preexisting LocalStoryboardTests xUnit2031 test analyzer warning remains. |
| Preservation | All **589** inventoried preexisting readable dirty files remain: **579 byte-identical**, **10 deliberate extensions**, zero unexpected changes. All six prior deletions remain absent. Both editor bundles, provider implementation/configuration and preset storage/transfer implementations remain byte-identical. `baseline-status.txt`, `baseline-hashes.json`, `preservation.json`; `git diff --check` passes. |

Reproduce with `artifacts/ai-parity-p13/verification.ps1`. It records the exact test
filter and isolated output paths, UI checks and build commands. Initial test-only
repairs made the overloaded shared validation target explicit and compared decoded
wire values instead of boxed JsonElements; the final evidence reflects passing
checks. No dependency or editor asset rebuild was necessary.

Not run: complete signed-in browser shell, actual rebuilt/relaunched native host,
installed package/deployed backend acceptance or live-provider prose quality. Native
verification requires launching the changed binary with isolated synthetic data;
the static catalog and successful Windows build do not establish native loading.
Full-host reproduction: select each tone in the client menu and desktop Rewrite/
Change tone selectors, confirm Executive preview/settings, review before Apply,
save/reopen and verify existing saved presets including custom tone strings. Keep
live generation quality separate from the deterministic menu/request contract.
No user manuscript/library or running process was changed; no deployment/distribution.

Next concrete task: **prompt 14 in `docs/ai-parity-fix-prompts.md`**, the fresh
integrated acceptance of prompts 1–13. Perform a new G01–G12 audit and report actual
full-flow/editor/build evidence and remaining gates; do not infer blanket parity
from this bounded contract task. Continue in the same uncommitted checkout. Earlier
next-task notes remain dated historical handoffs; prompt 14 remains unrun.

## AI parity prompt 14 — fresh integrated acceptance, 2026-10-06

Prompt 14 is now executed in the same uncommitted checkout, including adopted
optional policies 10–13. This dated section supersedes the older next-task/unrun
notes above. HEAD is `6dafbbfa65414f83b38414cdda0d26009fc3536b`; the initial status
and hashes identify the additional phased implementation. The complete current
G01–G12 table, new defects and prerequisites are in
[desktopai-gap-analysis.md](desktopai-gap-analysis.md).

Evidence: `artifacts/ai-parity-p14`, including `setup-evidence.ps1`,
`verification.ps1`, `profile.md`, `ledger.md`, `source-trace.txt`, baseline/final
status and preservation reports. Durable browser drivers are
`Tests/ai-parity-integrated-browser.mjs` and `Tests/ai-parity-planning-browser.mjs`.
Only isolated synthetic databases/local stores were mutated. Existing Development
LocalDev and mock-text were used for actual WASM; no authentication bypass or
provider configuration was added to production. Provider credentials/configuration
and both shipped editor assets were preserved.

| Fresh check | Result and scope |
|---|---|
| Focused integrated .NET suites | **1,150 passed, zero failed/skipped**, `focused.trx/log`. Covers G01–G12 checked handlers, endpoint request construction, explicit/subset approvals, persistence, cancellation/late results, actual retry counts, account/backend ownership, glossary/cache/dismissal lifetimes, transfer conflicts and planning/aggregate/local recovery. The preexisting LocalStoryboardTests xUnit2031 analyzer warning remains. |
| Reproduced teardown race | Initial integrated run: 1 failed / 1,143 passed, retained in `focused-initial.trx/log`. Sign-out reload retained the local store lease during disposal. Library/Writing now implement async disposal and await started account refreshes. Two real-store gated regressions plus 99 panel checks passed before the final integrated run. |
| Reproduced scene recovery failure | Actual client intent-only Apply → reopen → Writing tools Undo returned HTTP 200 but the intent reappeared via legacy purpose. `intent-initial.trx` reproduces the raw storage mutation. Unchanged cards now skip editor saves; both scene endpoints preserve raw purpose if resolved role/intent did not change. Three added regressions include pending/unrelated manual edits and both endpoints; 72 focused scene/recovery checks passed, then the integrated rerun. |
| Compiled-control browser checks | **142 layouts passed**, zero overflow/page errors, fresh 1280/480 snapshots. Cancellation 6, scene approval 24, glossary 12, consistency 6, style review 14, recommendations 20, scoped recovery 12, dismissal 16, targeted retry 8, prompt continuity 14, tones 10. Reports under `ui/p02`–`ui/p13`. These execute static compiled controls/production CSS; callbacks and transport are tested separately. |
| Real shipped-editor harnesses | **109 common + 44 focused checks passed**. The 109-case baseline repeats in four harnesses and is counted once. Additional targeted formatting proof 1, style 23, recommendations 8, retry 12. Zero errors. Complete-word/mark preservation, partial style changes, list insertion, stale/read-only/embedded refusal and undo invariants are covered. |
| Actual Development WASM writing journey | **10 checks passed**, `integrated-browser.json`, screenshots. Rich multi-page graph, navigation at 1280/1920, all six tone menu labels, exact Executive request, Discard, Cancel of held HTTP dispatch, new request, marked selection Apply, interrupted report delivery/IndexedDB retry, Undo, Redo and reopen. Exact unrelated saved pages retained. HTTP dispatch cancellation is separate from live-provider cancellation. |
| Actual Development WASM scene/recovery journey | **Four checks passed**, `planning-browser.json`: all/clear/subset review, only Narrative Intent approved, unchanged other editable fields/manuscript, reload then reachable scoped Undo/Redo. Legacy purpose in the public DTO is a projection; raw omitted-field preservation is checked by endpoint regressions. Mock reference IDs were added only to this isolated fixture; the initial unresolved-entity rejection was correct. |
| Explicit preset continuity journey | Actual client loader plus two local libraries: local edit → explicit send → client refresh → later local edit stays local → explicit versioned update → client refresh → other desktop library refresh/cache → explicit import, with same-revision import idempotence and exact manuscript retention. `continuity-journey.log`, `ui/p12/synthetic-continuity-journey.json`. Deterministic authenticated in-process backend; not a native/external account run. |
| Builds | Server Release, client Release and Windows desktop Debug passed with zero warnings/errors. Logs: `server-build.log`, `client-build.log`, `desktop-build.log`; isolated outputs. No editor assets needed rebuilding. |

Preservation audit: **592 preexisting readable dirty files retained**, 579 byte-identical,
13 deliberate extensions for the two repairs and four handoff documents, zero
unexpected changes/missing files. All six preexisting deletions remain absent.
Both editor bundles, provider implementation/configuration and prompt storage/transfer
implementations are byte-identical to the initial inventory. `git diff --check` passes.

Representative desktop Executive-480, style partial-480, complete-word editor-1280
and actual scene-review screenshots were visually inspected; layout checks cover
all retained sizes/states. Source traces include real request, source/fingerprint,
approved-field, durable intent, save and replay code; endpoint registration alone
was not treated as acceptance.

Commands (PowerShell, repository root):

```powershell
. artifacts/ai-parity-p14/setup-evidence.ps1
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -p:BaseOutputPath=artifacts/ai-parity-p14/build/ --filter $taskFilter --logger 'trx;LogFileName=focused.trx' --results-directory artifacts/ai-parity-p14
node WriterApp.Client/tests/verify-targeted-revisions.mjs
node Tests/style-quality-editor.mjs
node Tests/recommended-writing-editor.mjs
node Tests/targeted-retry-editor.mjs
# verification.ps1 enumerates all 11 layout harnesses with fresh evidence paths.
dotnet build BlazorApp.csproj -c Release -p:BaseOutputPath=artifacts/ai-parity-p14/release-build/
dotnet build WriterApp.Client/WriterApp.Client.csproj -c Release -p:BaseOutputPath=artifacts/ai-parity-p14/release-build/
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug -p:BaseOutputPath=artifacts/ai-parity-p14/native-build/ -p:ProsaValidationDataDirectory=C:/Users/Johan/source/repos/WriterApp/artifacts/ai-parity-p14/native-validation-data
dotnet run --project artifacts/ai-parity-p14/acceptance/Acceptance.csproj -p:BaseOutputPath=artifacts/ai-parity-p14/acceptance-build/
dotnet run --project artifacts/ai-parity-p14/scene-fixture/Fixture.csproj -p:BaseOutputPath=artifacts/ai-parity-p14/scene-fixture-build/
& artifacts/ai-parity-p14/start-host.ps1
node Tests/ai-parity-integrated-browser.mjs artifacts/ai-parity-p14
node Tests/ai-parity-planning-browser.mjs artifacts/ai-parity-p14
& artifacts/ai-parity-p14/stop-host.ps1
& artifacts/ai-parity-p14/check-preservation.ps1
git -c core.safecrlf=false diff --check
```

The host helper rejects an occupied port and databases outside the evidence root;
cleanup checks owned PID/start time and records listener removal. Earlier failed
probes/logs remain under `initial-host` and root failure reports. No user-owned
process was stopped; no commit/reset/clean/deploy/distribution occurred.

Remaining **P14-N02 / P3**: list-ended page navigation can persist one empty trailing
paragraph before any AI Apply. Initial failure and fresh before/after navigation
reports retain this fact. The browser driver accepts only that precisely observed
normalization and then compares exact saved pre-request graphs for AI actions.
Fix initialization-versus-edit tracking and verify list/table-ended autosave/reopen
on both hosts before claiming byte-identical navigation preservation.

Not run: actual changed Windows binary/native loading, external signed-in backend,
live-provider prose quality/cancellation, installed package or deployed acceptance.
Native automation is unavailable; the build and prepared normal local fixture do
not prove loading. Follow-up requires the isolated native candidate and supported
identity/provider configuration; execute style/recommendation/scene/glossary/
consistency/cancel/retry/recovery journeys across reopen. Manual preset transfer,
local versus server-scoped recovery and retained translated copies are intentional
documented differences. Prompt 14 delivers a reviewable current acceptance report;
it does not declare unconditional AI parity or release approval.
