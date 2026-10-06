# Desktop AI parity implementation prompts

Date: 2026-10-03. Reference: the current `WriterApp.Client` AI workflows compared with `WriterApp.Device.Shared`, which supplies the Windows desktop UI.

These are executable implementation prompts. Run them in order, in the existing checkout, and include the common requirements each time. They close the AI gaps identified in the benchmark without rebuilding capabilities already present.

Start a chat with:

> Implement prompt N in docs/desktopai-prompts.md, including the common requirements. Complete the implementation and verification, update the evidence and handoff, and preserve unrelated work.

## Sequence and dependencies

| Prompt | Deliverable | Dependency |
|---|---|---|
| 1 | Establish current implementation and acceptance baseline | None |
| 2 | Character, place and timeline bibles | 1 |
| 3 | Bible-aware consistency coach and issue interaction | 2 |
| 4 | Individual style and quality issues with targeted fixes | 1 |
| 5 | Section and document translation | 1 |
| 6 | Writing presets, section actions and next paragraph | 1 |
| 7 | Complete scene-card coaching scope | 2, 6 |
| 8 | Synopsis evaluation and guiding questions | 1, 7 |
| 9 | Full reusable prompt workflows and safe synchronization | 6 |
| 10 | Cloud AI history alongside durable local history | 3–9 |
| 11 | AI cover concept generation and persistence | 1, 10 |
| 12 | Guided desktop AI onboarding | 6, 8, 9, 11 |
| 13 | Integrated client/desktop acceptance and final gap table | 2–12 |
| 14 | Safe structured web section/document translation (P13-003) | 13 |
| 15 | Server-authorized desktop onboarding demo (P13-004) | 13 |
| 16 | Owned remote cover materialization (P13-005) | 13 |
| 17 | Equivalent bounded writing outline context (P13-006) | 13 |
| 18 | Real cover variation and adjustment workflows in both clients | 16 |
| 19 | Checked-source web AI request and Apply contracts | 14, 17 |
| 20 | Durable web AI applied-event reporting | 14, 19 |
| 21 | Native iOS identity integration | 13 |
| 22 | Windows/web, provider, database and physical export acceptance | 14–20 |
| 23 | iOS and packaged application acceptance | 21; relevant checks from 22 |

Use the default sequence unless explicitly instructed otherwise. A prompt's dependency is complete when its required implementation exists and its available checks pass. An unavailable live environment does not prevent independent feature implementation; record that gate separately.

## Common requirements for every prompt

- Read applicable repository instructions, `docs/device-development.md`, `docs/editor-content-compatibility.md`, this file and the preceding entries in `docs/desktopai-uat.md`. Inspect the actual working tree before editing. Existing uncommitted changes belong to the user and must remain intact.
- Treat the client as the interaction reference, then trace each visible feature through its handler, backend request, result, Apply path and persistence. A button, provider action registration or custom-prompt workaround does not establish feature parity. Flag incomplete client behavior instead of copying it as a working feature.
- Reuse presentation through `WriterApp.UI.Shared`; keep local persistence, sync and lifecycle orchestration in `WriterApp.Device.Shared`, native Windows adapters in `WriterApp.Desktop`, and contracts in their existing shared layer. Do not reference the web client from the device or presentation libraries or copy its large editor wholesale.
- Keep ordinary writing, local planning and available cached results usable offline. AI generation uses the existing authenticated backend, entitlements and quotas. Provider credentials stay on the server. Never weaken backend ownership or authorization to make a desktop action work.
- Preserve explicit preview/Apply/Dismiss, source and target identity, revision checks, save-before-request, sync conflicts, account/backend isolation, durable pre-Apply recovery and existing local AI undo/redo. Any unavailable version-check contract needs an additive implementation or an actionable capability response, not a bypass.
- Use typed request, result and application contracts. Bound input/output sizes; malformed JSON, invalid targets, stale proposals and cancelled requests must not mutate writing or planning. AI output is inert data until the user approves it. Do not render provider HTML as trusted markup.
- Preserve original rich content and supported formatting. Unsupported content must remain intact with an actionable explanation. Do not flatten an entire manuscript or overwrite unrelated fields to apply a smaller change. Multi-page changes require durable, recoverable application and explicit failure semantics.
- New persisted entities require versioned storage, safe migration, stable identities and account isolation where appropriate. Synchronization requires explicit identity mapping, concurrency, retry, deletion and conflict behavior. Do not silently use last-write-wins for authored writing or planning.
- Add focused behavior tests for changed data, authorization, revision and recovery boundaries. Extend the real editor harness for changed selection, anchor, formatting and Apply behavior, and regenerate tracked editor assets from their source. Build the affected projects; perform the complete integrated checks in prompt 13. Report unavailable checks accurately.
- Verify meaningful UI changes in the actual desktop host and client when available, including keyboard, loading, empty, error and disconnect states. Confirm the running executable's output path, rebuild that output and relaunch with isolated test data before claiming the desktop loaded the change. Use isolated build output if active assemblies are locked.
- These prompts authorize implementation and local verification. Deployment, distribution, sending feedback and changes to external authentication registrations are outside their scope unless the user separately requests them. Continue independent work when credentials or a native device are unavailable.
- Append dated evidence to `docs/desktopai-uat.md`: implemented behavior, exact checks and results, exercised hosts, unresolved defects, live gates and the next concrete prompt. Update relevant development documentation when contracts or setup change. Never report source inspection, mocks, prior test counts or a successful build as current live provider acceptance.

## Current baseline to preserve

This snapshot describes source implementation, including uncommitted changes, rather than the installed application or a live provider run. Reverify it in prompt 1.

| Capability | Current desktop position |
|---|---|
| Basic writing | Rewrite, expand, shorten, summarize and custom instructions have explicit proposal application. Rewrite uses fixed settings; expand/shorten target selections. |
| Translation | Selection translation with a language picker, preview and Apply. |
| Style | Selected-text or current-page AI revision, not the client's individual issue workflow. |
| Consistency | Section report, readable findings and individual review/Apply; bibles are excluded. |
| Scene cards | Suggestion and storyboard refinement; application updates seven narrative fields while retaining authored links, tags and status. |
| Synopsis | Ten-field planning and field improvement; evaluation and guiding questions are absent. |
| Storyboard | All four shared AI actions and explicit suggested-scene creation are implemented. |
| Prompts | Durable local custom prompts and explicit account-wide plain-template cloud copy/import. |
| History | Durable local comparisons, guarded undo/redo and recovery copies; cloud history is not integrated. |
| Covers/onboarding | No desktop AI cover studio or equivalent guided AI onboarding. |

## Prompt 1 — Establish the current AI baseline

```text
Implement prompt 1 with the common requirements. Create docs/desktopai-uat.md and a current AI feature matrix before implementing new features.

Trace the client editor and synopsis, cover and onboarding workflows, plus DeviceAiActions, AdvancedAiRequests, LocalAiPanel, LocalConsistencyRevisions, LocalAiHistoryActions and LocalStoryboardData. Verify the baseline table in desktopai-prompts.md against the current source. Identify shared UI, existing test seams, server capabilities and local/cloud identity contracts for later prompts.

Preserve the already implemented consistency suggestion review/Apply, current-page style revision, scene-card parsing and preservation, translation picker, durable history, and all four storyboard AI actions. Do not replace these with older analysis-only adapters. Distinguish implemented client cover generation/save from variation and adjustment buttons that currently only update UI state. Do not count backend-only outline generators as working client features without a reachable workflow.

Run focused existing AI, scene, history, consistency and editor checks. Build the affected shared, client and Windows projects. Inspect the actual desktop launch path before any native claims. Record build or environment limitations without altering user documents or issuing live provider calls solely to populate a baseline.

Acceptance: every benchmark feature has current source evidence and separate implementation, automated and live-acceptance statuses. Capture baseline regressions and fix blockers within this scope; provide a concrete handoff to prompt 2.
```

## Prompt 2 — Add durable character, place and timeline bibles

```text
Implement prompt 2 with the common requirements after prompt 1. Deliver the desktop counterpart of the client's story canon workflow.

Inspect DocumentBiblesController, BibleRefreshService, BibleModels, the continuity AI actions, and the client's Update all bibles orchestration. Reuse the existing extraction/update endpoints and policies, including how successful extraction becomes stored canon. Preserve review requirements where they exist; do not silently promote raw provider JSON into authored planning.

Implement typed, versioned, account/backend-isolated local bible storage with cloud document identity, source version, refresh state and timestamps. Support viewing cached character/place/timeline canon offline, loading owned cloud snapshots, initial extraction, update of stale canon and retry after interruption. Surface stale, unavailable and invalid results honestly. Validate any entity references and preserve existing authored canon on malformed or partial responses.

Document whether bible edits are supported by the client and match that actual scope. Use server-authoritative version checks and safe local cache refresh; if synchronization or concurrency support is absent, implement an additive contract or explicitly limited transfer workflow. Do not invent unrestricted automatic merging.

Acceptance: extraction/update/view works for all three bible types; restart retains valid cached data; account changes cannot reveal or apply another account's canon; wrong-document, stale, invalid JSON, disconnect and interrupted-refresh tests preserve prior data. Record the live provider gate and hand off the typed context to prompt 3.
```

## Prompt 3 — Complete consistency coaching context and interaction

```text
Implement prompt 3 with the common requirements after prompt 2. Extend the existing desktop consistency coach rather than replacing its current individual suggestion Apply path.

Match the client's request construction: include version-matched character, place and timeline bible context alongside the saved section and relevant story context. Show exactly which canon is current, stale or missing. Require refresh or offer a clearly disclosed reduced-context run according to the established contract; never claim bible-aware analysis when snapshots were omitted.

Reuse or extract issue presentation with severity filtering, evidence quotes, affected section/page, Jump to passage, suggestion review, Apply and applied-state feedback. Resolve anchors using current editor text and the client's fix/proposal contract. Preserve LocalConsistencyRevisions' unique-match and changed-source protections. If an issue requires a generated fix rather than a direct replacement, route through the existing backend action and preview the returned prose before application.

Preserve unrelated markup, writing and planning; record every applied change in durable local history and recovery. Handle multiple findings without treating an earlier approved change as permission to overwrite later user edits.

Acceptance: bible-derived findings use the intended source versions; filters and Jump locate the correct passage; ambiguous/missing anchors, stale writing, account switches and invalid fixes cause no mutation. Approved targeted fixes persist, reopen and undo/redo correctly. Verify the real editor interaction and preserve the existing suggestion tests.
```

## Prompt 4 — Add individual style and quality issues

```text
Implement prompt 4 with the common requirements after prompt 1. Bring the client's issue-based Style & quality Coach to desktop while keeping the existing broad style revision available.

Inspect PageQualityChecksController, SceneQualityChecksController, QualityCheckEngine, quality DTOs, QualityIssueCapabilities, QualityFixClientHelpers, and the client's issue highlights and proposal workflow. Distinguish deterministic quality analysis from provider-generated rewrites. Share suitable analysis code or use the existing backend with an exact-source contract; do not replace real issue analysis with a generic AI prompt.

Implement equivalent issue list, severity/rule controls where supported, selected issue, passage navigation/highlighting and actionable versus informational states. Support the client's targeted fixes, including repeated words, sentence length and passive voice where those capabilities are actually implemented. Generate any needed rewrite through the existing authorized AI endpoint, validate revised prose and preview before Apply.

Keep anchors correct after typing and autosave. Recompute or invalidate affected issues after changes, without resetting caret/focus. Preserve rich formatting and durable AI history for provider-assisted fixes. If deterministic analysis can safely run locally, retain its offline benefit without presenting offline AI generation.

Acceptance: issue detection agrees with shared reference fixtures; Jump/highlights point to the intended text; review/Apply changes only the affected range; stale anchors and prompt/meta leakage cannot enter writing. Verify supported fixes, Unicode, rich-text ranges, keyboard use and restart/undo behavior in the real editor harness.
```

## Prompt 5 — Add section and document translation

```text
Implement prompt 5 with the common requirements after prompt 1. Extend selection translation to the client's section and document scopes and actual translation application choices.

Inspect translate.selection/section/document, TranslationProposalPanel and the client's translation request and Apply code. Extract reusable scope/language/result presentation and retain the shared language catalogue. Reuse existing backend actions rather than simulating a full-document feature with an unbounded custom prompt.

Capture an ordered, complete source across pages/sections with stable identities and exact revision checks. Flush local changes and synchronize as required. Reject unsupported source content before generation instead of silently omitting it. Preserve the client's structural mapping and supported application choices; provide explicit targets and before/after review. Never apply one returned translation blob over unrelated pages.

Implement recoverable multi-page application or creation of a separate translated copy as appropriate to the selected client-equivalent option. Preserve originals, rich content where supported, ordering and planning identities. Validate missing/duplicate section markers, partial results, wrong language/target metadata and source changes before Apply.

Acceptance: selection, section and document scopes translate exactly their intended source; explicit application survives restart and undo/recovery; interruption cannot leave silent partial overwrites. Tests cover multi-page Unicode, structure mapping, unsupported content, stale versions, cancellation and account changes. Verify preview/application options in both hosts and record live language-quality checks separately.
```

## Prompt 6 — Complete writing actions and continuation

```text
Implement prompt 6 with the common requirements after prompt 1. Add the client's missing writing presets, section actions and dedicated continuation workflow.

Inspect the current client action catalogue, rewrite presets, RecommendedAiActions and backend action definitions. Add supported tone/length/preserve-term settings and dedicated change-tone and Show, don't tell workflows. Add section expand/shorten where available, and match actual client semantics for grammar-labelled presets without claiming a stronger guarantee than their implementation supplies. Preserve the desktop's existing Summarize section action and its non-destructive append behavior.

Implement propose.next-paragraph through its existing endpoint with the client's saved section/scene-beat context and applicable genre/task recommendations. Preview one new paragraph, suppress repeated source echoes according to the established validator, and insert at the intended continuation point only after approval. Keep continuation distinct from replacement.

Use typed scopes and application targets for selection replacement, section revision and continuation. Preserve all pages and supported formatting; recover multi-page section edits durably. Filter availability using server capabilities and entitlements, with actionable disabled states and existing quota/failure handling.

Acceptance: presets send the selected parameters; each command targets the correct range/scope; section revisions include every page; continuation appends a new paragraph without replacing or recapping existing writing. Verify stale selection, missing action, invalid output, cancellation, Apply/Dismiss, undo/restart and source preservation.
```

## Prompt 7 — Complete scene-card coaching scope

```text
Implement prompt 7 with the common requirements after prompts 2 and 6. Extend the current typed scene-card suggestion/refinement workflow to the client's individual-field coaching and full supported metadata.

Inspect the client's field selector, scene.suggest, scene.refine, scene.find-open-questions, scene-card proposal parsing, entity reference catalogues and application rules. Preserve the desktop's seven-field narrative application and authored-value protections. Reuse the current storyboard coaching entry points rather than adding duplicate coaches.

Add field-scoped instructions, explicit before/after field review and application restricted to the selected field. Provide the dedicated open-questions workflow. Extend review/application to supported status, POV, place, timeline, subplot tags, tags and references using typed IDs and validated current-project bible/planning entities. Reject unresolved or wrong-project links; never guess IDs from labels or overwrite omitted fields with empty values.

For whole-card proposals, expose which fields will change and let the user approve the intended updates with client-equivalent behavior. Save through local planning, sync and durable target-scoped history; preserve unrelated manuscript content and existing metadata. Invalidate proposals after meaningful source or planning changes while retaining metadata-only acknowledgement tolerance.

Acceptance: all actual client coaching fields have working desktop counterparts; scoped Apply changes only its approved target; omitted and malformed values preserve authored data; invalid entity links are explained. Verify save/reopen, sync mapping, stale proposals, field undo/redo and both editor/storyboard entry points.
```

## Prompt 8 — Add synopsis evaluation and guiding questions

```text
Implement prompt 8 with the common requirements after prompts 1 and 7. Add the missing evaluation and guiding-question workflows alongside existing synopsis field improvement.

Inspect the client Synopsis page, DocumentSynopsisController, SynopsisAiContextBuilder and field catalogue. Reuse its evaluate/questions/suggest contracts and feature gates. Share coaching presentation where suitable and retain all ten local synopsis fields, user notes and explicit field application.

Ensure requests analyze the intended locally saved and cloud-confirmed synopsis, not an older server copy. Add source-version protection to existing contracts if required. Present evaluation and questions as readable feedback with appropriate headings and no manuscript mutation. Keep suggested field text separate from commentary and apply only to the chosen field after approval. Handle empty synopsis, standalone/no-project context and missing scene prerequisites explicitly.

Acceptance: each of the three modes reaches the correct backend workflow and renders its actual result; evaluate/questions cannot mutate writing or planning; stale suggestions, account changes and malformed responses cannot apply. Approved field suggestions persist, reopen and undo through history. Check plan/quota/error states and document the remaining provider-quality acceptance.
```

## Prompt 9 — Complete reusable prompts and safe cloud synchronization

```text
Implement prompt 9 with the common requirements after prompt 6. Extend the local custom-template library to the client's usable preset workflows.

Inspect AiPresetsController, PromptPresetDto, the client's preset editor, pinned/recommended actions, DevicePromptLibrary and LocalAiStore. Add local create/edit/delete, categories, supported built-in action presets and typed parameters, custom templates, selection/section scope and pinning where the client supports them. Preserve existing prompts during migration and validate unsupported action/parameter combinations explicitly.

Run presets through the same revision-checked manuscript proposal pipeline as built-in actions. A reusable custom prompt must be able to apply a reviewed result to its declared target; the existing analysis-only panel must not masquerade as that capability.

Preserve explicit cloud copy/import and add broader lossless transfer. Implement automatic synchronization only after establishing an additive server concurrency/idempotency contract with stable IDs, version tokens, deletes and conflict resolution. Persist the local queue across restart, isolate accounts/backends and preserve divergent authored versions. Do not simply retry non-idempotent creates or silently merge templates. Do not claim automatic preset sync as existing client behavior unless verified.

Acceptance: old plain prompts survive migration; local editing and preset management work offline; execution preserves scope and parameters; cloud retry, interrupted uploads, concurrent edits, deletes and account switching cannot duplicate, overwrite or leak prompts. Verify actual selection/section Apply and visible conflict resolution.
```

## Prompt 10 — Integrate cloud AI history with local safety

```text
Implement prompt 10 with the common requirements after prompts 3–9. Connect desktop to the client's owned backend AI history and applied-event workflows while retaining durable local history as the recovery source for local changes.

Inspect AiActionsController history/applied routes, transport snapshots, client AI undo/redo behavior, LocalAiStore, LocalAiHistoryActions and DeviceAiUndoStore. Distinguish generated/reviewed/applied/undone events and local versus server IDs. Record proposal identity and source/target metadata consistently for writing, consistency, style, scene and synopsis actions. Assess storyboard history coverage and preserve suggested-scene creation without inventing reversible cloud operations.

Provide shared browsable comparisons with origin and freshness. Reconcile corresponding local/cloud entries without duplicate rows. Load authorized cloud history, support offline cached viewing and isolate accounts/backends. Add durable applied-event delivery and retry only with explicit idempotency/concurrency contracts; a reporting failure must not roll back a completed local save or falsely mark an unsaved change applied.

Do not enable undo of a cloud-only entry unless its exact target snapshots, local identity mapping and current-content guards establish a safe reversible change. Offer inspection/recovery alternatives with clear capability states. Preserve later user edits, retention limits and interrupted-Apply recovery.

Acceptance: remote and local entries are correctly identified and combined; Apply records once after local durability; retry/restart handles interrupted reporting; wrong-owner and switched-account history cannot appear. Verify stale undo refusal, supported target undo/redo, offline history and separate recovery copies without overwriting later edits.
```

## Prompt 11 — Add AI cover concepts and durable selection

```text
Implement prompt 11 with the common requirements after prompts 1 and 10. Add a working desktop AI cover studio matching the client's implemented concept-generation, selection and save workflow.

Inspect ProjectCoverStudio, CoverApiClient, CoversController, CoverPrompt and current local cover/publishing storage. Extract shared prompt/style/genre/mood controls, generation states and concept selection. Call the existing authenticated cover endpoint with the correct project/document association, plan/quota controls and server-owned credentials.

Cache selected cover assets durably through existing validated asset storage or a bounded additive adapter. Preserve MIME/size checks, safe URL/auth handling and project/account isolation. Make selection an explicit persisted project-cover change with prior-cover recovery, supported sync and publishing integration. Handle interrupted downloads and saves without losing the prior cover. Confirm whether existing cover endpoints can check ownership/revisions; add necessary guards rather than assuming they do.

Do not present client variation/darker/brighter/cinematic/minimal buttons as functioning generation: currently these update UI state only. Hide or clearly disable those operations until implemented by a separate authorized scope. Preserve existing local cover selection.

Acceptance: generate concepts → preview → select → save → restart → publish uses the chosen valid asset; offline viewing works for cached covers; invalid media, generation failure, cancellation, stale project and account switches preserve the prior cover. Record live image-generation acceptance separately.
```

## Prompt 12 — Add guided desktop AI onboarding

```text
Implement prompt 12 with the common requirements after prompts 6, 8, 9 and 11. Adapt the client's guided first-use AI experience to the desktop's local-first document lifecycle.

Inspect OnboardingService, OnboardingStateStore, onboarding overlays, demo eligibility and existing starter-document preservation tests. Share useful guidance and connect every suggested action to an implemented desktop workflow. Support skip/resume and durable completion state. Keep local create/open/write usable while signed out or offline, with online AI prerequisites introduced at the relevant action.

Use isolated, clearly identified starter content. Preserve user writing on revisit, restart and onboarding migration; never repeatedly seed or overwrite a real manuscript. Reuse the server's authorized demo eligibility and quota contract for any first AI demonstration. Do not grant a fake entitlement or make provider calls automatically upon opening a tutorial.

Guide the user through selection/preset choice, request, comparison, explicit Apply/Dismiss and undo, then show relevant synopsis/prompt/cover entry points without requiring all of them to complete onboarding. Handle expired identity, unavailable provider, quota and cancellation in the same production paths as normal use.

Acceptance: new and returning users can skip/resume/restart without source changes; first AI requests require deliberate action and server authorization; successful review/Apply/undo works on test content; offline and existing-document flows remain usable. Verify keyboard/focus, small-window layout, account switching and no repeated demo seeding.
```

## Prompt 13 — Verify integrated AI parity and publish the final gap table locally

```text
Implement prompt 13 with the common requirements after prompts 2–12. Exercise the complete client/desktop AI journeys, repair regressions within this scope and create docs/desktopai-gap-analysis.md with the final evidence-backed comparison table.

Use isolated equivalent documents with Unicode, multiple sections/pages, supported formatting, scene cards, synopsis and bibles. Exercise writing presets, section revisions, continuation, all translation scopes, individual style issues, bible-aware consistency and fixes, scoped scene coaching, synopsis modes, reusable prompts, history and cover selection. Verify all four existing storyboard actions and suggested-scene creation remain connected and safe.

Test Apply/Dismiss, undo/redo, save/reopen/relaunch, offline transitions, stale source/planning, conflict state, quota/plan rejection, cancelled/late requests, malformed results and account/backend switching. Compare persisted content, structural IDs and metadata rather than Saved labels. Confirm normal web persistence using non-demo data. Inspect the actual desktop binary path before rebuild/relaunch and native verification.

Run the full relevant server/shared tests, real editor/browser harnesses and Release client/server/Windows builds according to current repository commands. Check the device-shared managed target and available iOS compilation for regressions, while distinguishing compilation from device acceptance. Capture comparable client/native desktop screenshots at 1280x720 and 1920x1080 where supported; verify keyboard and readable results, not just matching tabs.

Use a live authenticated provider only when the necessary authorized environment is available. Otherwise leave exact prerequisites and reproduction steps for the live gate. Do not invent quality, latency or parity percentages from source coverage.

Acceptance: the final table lists each original gap as implemented, partial or missing, with source evidence, test results and independent live-acceptance status. Preserve known incomplete client cover operations as client limitations. Report unresolved defects and a concrete release handoff; do not claim full parity while required workflows or acceptance gates remain open. Update docs/desktopai-uat.md and keep historical evidence intact.
```

## Remaining gaps after prompt 13

Prompts 14–23 continue the same sequence and inherit every common requirement above. Their source of truth is [desktopai-gap-analysis.md](desktopai-gap-analysis.md), including P13-003–006, incomplete client cover operations and P13-G01–G04. Prompts 1–13 and their evidence are historical; do not rerun their implementations or infer current acceptance from their old counts.

For each continuation prompt, reread the gap table and current production paths, preserve this checkout's uncommitted work, append dated evidence to `desktopai-uat.md`, and update only the relevant gap/ledger statuses with source and fresh checks. Keep implementation, automated, native, deployed/provider and physical acceptance separate. P13-001/002 are regression checks to retain, not features to rebuild. Do not remove a safety restriction merely because a backend action exists.

Prompts 14–21 implement functionality and can proceed with isolated local verification. Prompts 22–23 exercise release gates and repair defects they expose. Unavailable native tools, credentials, supported database hosts or Mac/device infrastructure leave those specific gates open with exact prerequisites; they do not prevent independent implementation. These prompts retain the common authorization boundary for external deployment, distribution and authentication registrations.

## Prompt 14 — Restore safe web section/document translation

```text
Implement prompt 14 with the common requirements and continuation requirements. Close P13-003's functional web translation gap while preserving the safety restriction until the complete supported workflow passes verification.

Trace DocumentEditor translation request/review/Apply, the existing TranslationStructures contract, desktop LocalTranslation, translation endpoints and normal web persistence/history. Reuse shared capture/preview/schema logic. Capture every ordered section/page/run from saved source with owned document/project identity, language metadata and checked source revisions. Request and validate complete typed results; reject missing, duplicated, reordered, foreign, malformed or oversized targets and unsupported content before mutation.

Implement readable per-page Original/Proposed review, explicit Apply/Dismiss and replace/duplicate section/document choices. Replace must atomically preserve all page/section IDs, formatting and unrelated planning; duplication must create the complete ordered graph with stable retry identities and correct translation/language metadata. Use an owned backend aggregate transaction with compare-and-swap and idempotent operation receipts, not a series of first-page PUTs. Persist approved recovery evidence before committing; distinguish failure before commit, lost acknowledgement and successful persistence. Reload must reconcile an already approved operation without regenerating or duplicating it. Do not report Applied until persistence is confirmed. Provide guarded aggregate undo/redo or an explicit recoverable original-copy path; do not feed aggregate snapshots into legacy page-HTML history commands.

Retain preview/copy and selection translation. Enable broader Apply only for the verified typed contract; old plain-text proposals and unsupported backends retain actionable refusal. Test rich Unicode, multiple sections/pages, blank pages, structural IDs, copies, stale source/account, conflicts, cancellation/late results, malformed output, partial failure, receipt replay, reload and history with normal non-demo data. Exercise real web component handlers, production SQLite HTTP persistence and both shipped editor bundles; build affected Release projects.

Acceptance: supported web replace and duplication preserve complete structure and durable recovery, failed/stale requests never change authored content, and no false Applied state occurs. Update P13-003's implementation status with fresh evidence; native/live language acceptance stays separate.
```

## Prompt 15 — Connect desktop guidance to the authorized onboarding demo

```text
Implement prompt 15 with the common requirements and continuation requirements. Close P13-004 by connecting desktop guidance to the actual server-owned onboarding/demo policy, retaining the existing local guide and guest practice lifecycle.

Trace OnboardingController, bootstrap/eligibility/quota services, web onboarding state, DeviceOnboarding, LocalOnboardingStore and the deliberate production writing request. Add typed owned bootstrap/status/completion contracts and durable account/backend-scoped mappings between guide, local practice and returned cloud demo identities. Only an explicit signed-in demo choice may create/link the server demo or synchronize its writing. Never silently adopt, overwrite or upload an existing manuscript or edited guest sample. Retain stable retries, no-reseed behavior, deleted/missing sample recovery and explicit guest continuation.

Derive demo availability and allowance from the authenticated server's eligibility policy, including the permitted action/scope and usage state. A local provenance marker or request flag cannot grant a free entitlement. Reconcile desktop availability with a genuine server-authorized demo response instead of manufacturing quota or disabling ordinary plan gates. Use the normal save/source/sync/preview/Apply/Dismiss/history path. Mirror only explicitly defined onboarding progress/completion; guide completion, request success and approval remain distinct states. Reconcile web/device progress without losing completed steps or reseeding either sample, using versioned compare-and-swap/idempotent storage.

Test first bootstrap, lost acknowledgement/retry, second window/device, guest sign-in, account/backend switch, altered/empty/trashed practice, ineligible/expired/already-used demo, quota denial, offline resume, stale Apply and server completion reconciliation. Verify existing manuscripts remain unchanged and ordinary non-demo AI still requires its actual plan/quota. Build affected server/shared/device/client/Windows targets.

Acceptance: desktop can discover and deliberately execute the same server-authorized demo policy as web, with durable correct identities and completion reconciliation. Local guidance remains usable offline. Update P13-004 with automated evidence and retain the real account/provider demo gate until exercised.
```

## Prompt 16 — Materialize owned remote cover assets for desktop use

```text
Implement prompt 16 with the common requirements and continuation requirements. Close P13-005's remote cover/offline format gap without weakening the current inline PNG, metadata concurrency or asset validation boundaries.

Trace CoverApiClient, CoversController, CoverImageService, DeviceCoverStudio, LocalCoverStudioStore, project sync and publishing. Define an additive authenticated owned-asset contract that resolves valid provider/project cover assets on the server and returns bounded validated image bytes, media type, immutable asset identity/hash and project/source metadata. Do not introduce a client or server arbitrary-URL downloader: authorize the asset before fetch, constrain provider storage/redirect destinations and reject private/internal targets, oversized bodies, unsupported encodings and malformed images. Provider secrets remain server-only. Define supported formats explicitly; normalize supported remote formats through a bounded safe decoder if necessary and disclose unsupported ones without replacing the current cover.

Cache materialized assets atomically under account/backend/project identity with versioned metadata. Retain the remote reference/provenance and content hash; offline previews and publishing must use the exact validated cached bytes. Fetching or previewing cannot change the project cover. Explicit Save uses the existing expected metadata revision, prior-cover recovery and normal sync/conflict flow. Handle expired URLs, deletion, cancellation, offline interruption, acknowledgement loss and account changes without losing previous cover/cache or exposing another owner's assets. Never overwrite authored project metadata to refresh a cache.

Test valid supported remote formats, redirects, hostile/foreign URLs, ownership, expiry, MIME/signature mismatch, decompression/size bounds, corrupt/partial downloads, stale metadata, restart/offline access, account isolation and publishing byte identity. Exercise shared concept selection, actual desktop page orchestration and production asset endpoints. Build affected targets and document physical export as a separate gate.

Acceptance: supported owned remote cover results can be reviewed, explicitly saved, reopened offline and used by publishing while retaining exact asset identity and safe recovery. Update P13-005; real provider/storage and physical export acceptance remain open until exercised.
```

## Prompt 17 — Align writing outline context across web and desktop

```text
Implement prompt 17 with the common requirements and continuation requirements. Close P13-006 by matching the relevant web writing context without adding unrelated outline-generation features.

Trace GetOutlineTextForAi and every affected web writing action, LocalWriting.Prepare, reusable preset execution, continuation context and backend context builders. Define a bounded typed shared outline snapshot from the same owned saved document/planning structure, with stable entity IDs, ordering, document/project identity and source/planning revision or fingerprint. Supply equivalent context from both hosts where the actual action consumes it. Keep saved outline context separate from authored prose, coaching notes and provider output. Do not silently substitute another document's project outline or truncate beyond declared limits.

Capture after local save and required sync, bind the snapshot to the proposal and recheck relevant source/planning changes before generation completion and Apply. Preserve metadata-only acknowledgement handling, supported preset scopes and existing explicit approval/history. Keep deterministic cached/local context usable offline; generation remains authenticated and quota-gated. Missing capability/context receives accurate guidance, with an explicit supported reduced-context mode only if defined by the contract.

Add equivalent web/device request tests for selection, section, continuation and reusable prompts; cover multi-document projects, standalone documents, empty/oversized outline, reordered/deleted nodes, stale planning, account/backend switch and context escaping. Verify context extraction never changes writing or planning. Preserve the established action-specific provider context limits and disclose them rather than claiming identical output quality.

Acceptance: the applicable actions send equivalent bounded source-matched outline context from both hosts, and changed planning cannot reuse an earlier approval. Update P13-006 with source/request evidence; provider semantic equivalence requires separate live review.
```

## Prompt 18 — Implement real cover variations and adjustments in both clients

```text
Implement prompt 18 with the common requirements and continuation requirements after prompt 16. Implement the known missing client operations: variation, darker, brighter, cinematic adjustment and minimal adjustment, in both web and desktop. These are new workflows; do not count the former UI-state stubs as an implementation reference.

Trace both cover studios and the current provider abstraction to establish actual image-edit/variation capabilities. Add typed authenticated owned requests bound to the selected source asset/hash, project identity and cover/source revision. Use a real supported provider operation; a changed label, CSS filter, random seed alone or unchanged image does not establish the requested behavior. Bound prompt/image inputs and outputs, keep credentials on the server and enforce image entitlements/quotas. If the configured provider lacks an operation, advertise it as unavailable before charging/requesting generation; implement the supported provider adapter rather than fabricate results.

Reuse shared brief/concept/review UI, asset materialization and local cache. Show original and proposed concepts with readable operation/source labels, loading/cancel/error states and explicit selection/Save/Dismiss. An adjustment must not silently replace the saved project cover. Preserve prior-cover recovery, metadata compare-and-swap, offline cached previews and normal project sync. Prevent wrong-owner, stale asset, late cancelled and previous-account results from changing selection or cover.

Test actual request/provider routing, result validation, all five operations, unsupported capability, quota denial, source deletion/change, cancellation/late results, save conflicts, offline/restart and original-cover recovery in both host adapters. Record real-provider visual review separately: synthetic images establish lifecycle correctness, not darker/brighter/composition quality.

Acceptance: each supported operation has a reachable generation-to-review-to-explicit-save journey in both clients; unsupported configurations remain honestly unavailable. Add a separate ledger entry for this former client limitation and retain independent live image-quality acceptance.
```

## Prompt 19 — Upgrade production web AI flows to checked-source contracts

```text
Implement prompt 19 with the common requirements and continuation requirements. Close the remaining web checked-source gap identified after prompt 13, preserving legacy wire compatibility without keeping unchecked production UI paths.

Audit production web writing/custom presets, translation, quality/generated fixes, consistency/canon, scene coaching and synopsis against the device's existing revision/capability contracts. Identify exactly which reachable web requests omit expected document/project/planning/canon revisions or skip source/target rechecks. Reuse existing shared contracts and backend validation before creating additive fields/routes. Do not import Device.Shared storage or lifecycle classes into the web client.

Migrate each affected production web flow to save-before-request, owned stable target identity, bounded source/context and advertised checked capabilities. The server must verify applicable revisions/fingerprints before and after provider execution; the client must verify echoed identity, account/backend, expiry and relevant current source before review/Apply. Source comparison supplements sync metadata; metadata-only acknowledgements must not invalidate unchanged content unnecessarily. Apply remains explicitly approved and scoped to supported rich content/planning. Protect aggregate writing through prompt 14's persistence/recovery boundary. Unsupported backends or legacy unchecked proposals receive an actionable refusal rather than a bypass.

Keep omitted version fields compatible for older external callers where already supported, but do not claim that those callers gain checked semantics. Add real web handler/server tests for changed writing/planning/canon, foreign targets/owners, account switch, malformed/expired result, missing capability, ignored cancellation and concurrent writes. Preserve readable review, ordinary save/undo and all four storyboard actions. Exercise real editor mapping where application changes and build affected targets.

Acceptance: every audited reachable production web AI flow has documented checked-source behavior or an explicit supported capability limitation. Update the gap analysis with a per-flow contract/evidence table; do not mark all web guards implemented solely from one endpoint test.
```

## Prompt 20 — Make web applied-event history delivery durable

```text
Implement prompt 20 with the common requirements and continuation requirements. Close P13-G04's functional one-shot web applied-reporting gap while keeping actual persisted changes authoritative.

Trace RecordAppliedEventAsync, browser history and Apply/Undo/Redo, existing server applied-event routes, prompt-14 aggregate operations and device receipt semantics. Implement a versioned account/backend-scoped durable browser outbox with stable operation identity, proposal/source/target identity, approved before/after evidence or safe references and ordered outcome transitions. Derive outcomes from confirmed persistence; a generated proposal, preview or failed save cannot report Applied. After reload or lost acknowledgement, reconcile committed writes before retrying reports. Preserve enough pending intent to recover the crash window between content commit and report enqueue.

Use an owned idempotent server receipt contract with immutable payload checks and supported ordering. Reuse shared receipt concepts without pretending browser operations are native-device entries or interpreting aggregate snapshots as page HTML. Handle multiple tabs, retry, offline state, sign-out/back-in, deletion, entitlement rejection and account switching. Reporting failure must not roll back a successfully saved manuscript or manufacture successful cloud confirmation. Show pending/rejected/confirmed delivery honestly while retaining local/source guards and safe recovery.

Test crash/reload boundaries, network loss after commit and after receipt insertion, duplicate requests, payload mismatch, out-of-order transitions, wrong owner/proposal, quota/plan rules, foreign account and simultaneous tabs. Verify eventual confirmed history matches persisted content and cannot authorize unsafe Undo after later edits. Add schema migrations only if required, with safe upgrade tests and deployment prerequisites. Preserve existing device history receipts.

Acceptance: confirmed web saves/Undo/Redo survive reload and eventually report exactly one valid ordered outcome when connectivity/authorization permit, with inspectable unresolved delivery. Update the functional history gap separately from deployed/browser acceptance.
```

## Prompt 21 — Integrate native iOS sign-in with device AI

```text
Implement prompt 21 with the common requirements and continuation requirements. Replace the default unavailable iOS identity scaffold with the native authentication integration required by the shared AI/account lifecycle.

Trace Windows native identity, DeviceAccountService/IDeviceIdentityClient, configured authentication options and iOS MauiProgram/host lifecycle. Implement an iOS-specific supported native sign-in/sign-out/token adapter with secure platform storage, system authentication presentation, callback/resume/cancellation and token refresh. Keep app IDs/scopes/authority/redirect configuration explicit and environment-specific. Do not embed provider credentials, client secrets or copy Windows-only APIs into shared code. Add required local platform configuration; external authentication registration changes require their existing authorization separately.

Register the adapter in iOS and preserve shared account-generation/backend isolation. Returning from authentication, switching/signing out and expired/revoked tokens must refresh UI availability and invalidate stale proposals/canon/history/cover data without damaging local documents. Ordinary offline writing and guest guidance remain usable. Handle interrupted background/foreground auth and prevent duplicate callbacks or late responses from restoring an old account.

Add focused adapter/configuration/lifecycle tests and compile Device.Shared, Windows and the available iOS managed target. On an available authorized Mac/device, test native callbacks, secure cache, sign-out, real account switch, one checked AI request and explicit Apply/reopen; otherwise record exact registration, Mac/signing/device prerequisites with that acceptance gate open. Do not equate a Windows managed iOS compilation with a runnable app.

Acceptance: iOS uses a real native identity adapter with tested shared lifecycle boundaries. Separate code/compilation status from native signed-in/device acceptance in P13-G03.
```

## Prompt 22 — Close available Windows, web and live release gates

```text
Execute prompt 22 with the common requirements and continuation requirements after prompts 14–20. Exercise P13-G01/G02/G04 and repair scoped regressions; do not repeat completed feature implementations or turn missing infrastructure into fabricated acceptance.

Read the final gap table, current contracts/migrations and seven-flow acceptance profile. Inspect the running Windows executable Path before rebuilding/relaunching. Prepare isolated Development data and equivalent normal non-demo web/native projects with Unicode, multiple rich pages/sections, scene cards, synopsis and bibles. Preserve user processes/documents and start/stop only owned validation hosts. Exercise all original AI journeys plus new translation, onboarding, remote covers/adjustments, outline context and durable web reporting. Check keyboard/focus/selection/drawers/readable reviews at 1280x720 and 1920x1080; capture comparable actual web/native screenshots.

Verify Apply/Dismiss, scoped Undo/Redo/recovery, persisted content/IDs/metadata after save/reload/native close-relaunch, offline transitions, stale source/canon/planning, conflicts, quota/plan denial, cancellation/late results and real account/backend switching. Use an authorized authenticated provider/test account only when available; record model/configuration and human-reviewed prose, translation, canon and image results. Measure latency only if actually observed. A labelled local sample must never grant server demo eligibility by itself.

Run the current full .NET suite, real browser/editor harnesses and sequential Release server/client/device/Windows builds. Verify migrations and transaction/receipt/concurrency behavior on isolated configured SQLite and SQL Server hosts where available. Deployment/live migration remains separately authorized; use existing deployed capabilities or leave precise prerequisites. Check non-demo authenticated web persistence, history retry after reconnect and physical saving/opening of published cover exports, comparing actual bytes and retained source metadata.

Acceptance: append dated evidence per workflow and per host, fix reproducible scoped defects with regression checks, and update each functional/native/provider/database/export status independently. Unavailable native control, authorized credentials, SQL Server or physical export leaves the corresponding gate open with exact reproduction steps. Do not claim full parity while any required functional workflow or acceptance gate remains unresolved.
```

## Prompt 23 — Verify iOS and packaged application acceptance

```text
Execute prompt 23 with the common requirements and continuation requirements after prompt 21 and the relevant implementation/Windows checks from prompt 22. Exercise P13-G03 for iOS and packaged Windows without treating build success as distribution acceptance.

Inspect current packaging, update, native authentication and storage configuration. Build and locally verify a Windows package and an iOS simulator/device app using available supported tooling, isolated accounts/documents and authorized signing configuration. Inspect actual installed/launched binary/app identity, environment/backend and storage paths before claiming the latest code ran. Test clean installation and upgrade of representative prior versioned local data, stable document/guide identities, pending preset/history transfers, cached canon/covers, approved recovery records and no unexpected reseeding. Preserve source backups and authored content through failures/retries.

Verify native authentication callbacks/secure token storage, sign-out and account/backend isolation, background/foreground save and request cancellation, keyboard/touch/accessibility, readable results, offline writing and selected AI review/Apply/history flows. Reopen/relaunch and compare persisted rich content, structural IDs, metadata and cover bytes. Run shared regression tests and available Release targets; distinguish Windows managed iOS compilation, Mac bundle/signing, simulator acceptance and physical device acceptance.

Use local package installation/test channels within existing authorization; do not publish a release, alter external registrations or obtain signing credentials by workaround. If required infrastructure is unavailable, finish independent configuration/tests/builds and record the precise Mac/device/signing/install/update prerequisites and remaining gate.

Acceptance: package/device evidence identifies the actual version/host/data and demonstrated lifecycle behavior; migration or identity defects are repaired with regression coverage. Update the final release checklist and gap table with separate Windows package, iOS simulator, physical device and distribution statuses. Full release acceptance requires the relevant prompt-22 live gates as well as these native checks.
```

## Primary implementation references

- Client writing/coaching/history: `WriterApp.Client/Pages/DocumentEditor.razor` and `.razor.cs`.
- Desktop writing orchestration: `WriterApp.Device.Shared/Pages/DocumentWorkspace.razor` and `Services/DeviceAiActions.cs`.
- Desktop planning/coaching: `WriterApp.Device.Shared/Services/AdvancedAiRequests.cs` and `Components/LocalAiPanel.razor`.
- Consistency: `Controllers/DocumentBiblesController.cs`, `Application/Continuity`, `AI/Actions/ContinuityActions.cs`, `WriterApp.Device.Shared/Services/LocalConsistencyRevisions.cs` and `WriterApp.UI.Shared/ConsistencyReport.razor`.
- Quality: `Controllers/PageQualityChecksController.cs`, `Controllers/SceneQualityChecksController.cs`, `Application/Documents/Quality` and existing style/quality tests.
- Synopsis: `WriterApp.Client/Pages/Synopsis.razor`, `Controllers/DocumentSynopsisController.cs` and `Application/Synopsis`.
- Prompts: `Controllers/AiPresetsController.cs`, `WriterApp.Device.Shared/Services/DevicePromptLibrary.cs` and `Storage/LocalAiStore.cs`.
- History: `Controllers/AiActionsController.cs`, `WriterApp.Device.Shared/Services/LocalAiHistoryActions.cs`, `Storage/DeviceAiUndoStore.cs` and `WriterApp.UI.Shared/AiHistoryPanel.razor`.
- Storyboard: `WriterApp.UI.Shared/Projects/StoryboardInsights.razor`, `StoryboardBoard.razor` and `WriterApp.Device.Shared/Services/LocalStoryboardData.cs`.
- Covers: `WriterApp.Client/Components/Covers/ProjectCoverStudio.razor`, `WriterApp.Client/Services/CoverApiClient.cs`, `Controllers/CoversController.cs` and `Application/Covers`.
- Onboarding: `WriterApp.Client/Services/OnboardingService.cs`, `WriterApp.Client/State/OnboardingStateStore.cs`, `Controllers/OnboardingController.cs` and the existing preservation/eligibility tests.
- Prior parity guidance: `docs/desktop-client-parity-prompts.md`, `docs/desktop-client-parity-uat.md` and `docs/desktop-client-parity-release-checklist.md`. Their dated statuses are historical; current source and newly recorded evidence take precedence.
