# Desktop versus web client gap analysis

## Current acceptance matrix — 2026-09-29 (prompt 12)

This matrix supersedes the historical inventory below. **Present** means the stated capability is implemented; **Partial** means some web scope is absent; **Missing** means no equivalent desktop workflow. None of these labels alone establishes live cloud acceptance. Evidence, fixes, test results and precise release gates are in the [prompt 12 acceptance report](desktop-client-parity-release-checklist.md).

| Area | Status | Evidence and remaining action |
|---|---|---|
| Local library, save/reopen and recovery | Present | Native create/close/relaunch/Unicode-bold reopen and HTML round trip passed; finish injected native write-failure/close and packaged upgrade checks (report gates 4/6). |
| Shared shell, editor frame and supported formatting | Present | Shared UI, native/web editor captures and 17 shipped-editor checks; finish complete native viewport/accessibility matrix (gate 5). |
| Tabbed panels | Partial | Shared categories, corrected web-aligned order and keyboard arrow navigation; desktop Synopsis is additional. Specialized web panel contents remain broader. Add missing contents only with working adapters. |
| Rich document compatibility | Partial | Preservation/rejection contract and tests pass; desktop intentionally supports a smaller schema. Extend tables/images/advanced marks with round-trip fixtures before enabling editing; see [compatibility contract](editor-content-compatibility.md). |
| Section/page management | Present | Create/rename/reorder/recoverable deletion implemented; single-click title saving verified. Project scene creation is a distinct operation; reconcile unbound sections explicitly before unified organization parity. |
| Local search and preview | Present | Unicode query/result navigation and highlighted safe preview verified. Planning search implemented and tested; full long-book/keyboard acceptance remains (gate 5). |
| Local project/tree foundation | Present | Durable Part/Chapter/Scene model, project hub and resume; native creation/reopen verified. Finish all move/delete/recovery native branches and explicit section association UX. |
| Project/planning cloud synchronization | Partial | Versioned aggregate/identity/tombstone/conflict implementation and automated integration tests; no live paid two-client acceptance. Execute gates 1/2. |
| Storyboard and scene inspector | Partial | Dedicated desktop storyboard now shares the client board and insights, with offline chapter/scene editing, filters/colors, multi-selection/bulk updates, atomic chapter moves, inspector fields, and all four board AI adapters. Desktop/client builds and 78 related tests passed; native interaction and live provider acceptance remain. See [storyboard evidence](desktop-storyboard-uat.md). |
| Notes, tasks and annotations | Partial | Durable scene notes/tasks/quote anchors and detached-anchor protection implemented/tested; live notes save/reopen verified. Full web inline annotation interaction is absent; add explicit anchor-aware editor UX. |
| Synopsis | Present | Local separate synopsis fields, shared presentation and save/restart verified. AI coaching implementation has a separate provider gate (3). |
| Publishing | Partial | Local HTML/TXT/Markdown/DOCX/EPUB conversion, presets/covers and Windows PDF adapter implemented. Native HTML round trip verified now; prior PDF evidence retained. Finish gate 4; multi-selection scope/custom templates/full TOC/header/footer/synopsis export remain feature work. |
| AI actions and coaching | Partial | Existing five actions plus translation and analysis/planning adapters, typed Apply/version guards and tests. No authorized live provider acceptance; gate 3, then specialized issue-card interactions/bibles/full generators. |
| Reusable prompts | Partial | Durable local prompts and explicit authorized cloud copy; no automatic conflict-safe preset synchronization. Define server concurrency contract before automatic sync. |
| History | Partial | Durable local AI operation/recovery history and shared presentation; not complete synchronized cloud version history. Add history/version API adapter and retention acceptance. |
| Account, plan, billing/help | Partial | Full Account & Help, shared plan/feedback presentation, safe system-browser destinations and tests. Signed-out native and synthetic web viewed; real sign-in/callback/paid/billing return need gates 1/3. Feedback delivery requires explicit authorization. |
| Identity exceptions | Present | Expired/forbidden/duplicate/deleted guidance and tests; run real native exception/callback scenarios in gate 1. |
| Conflict review, updates and diagnostics | Present | Device-specific supported surfaces; automated guards pass. Live conflict resolution and signed update/distribution gates 1/6 remain. |
| Cover studio / guided onboarding | Missing | Local cover selection is not a cover-design studio. Design offline ownership and shared onboarding flows before adding these screens. |
| iOS host acceptance | Missing | Managed compile passes; native adapters, signing, Mac/device execution and GUI acceptance required (gate 6). |

Recommendation: internal local-first Windows preview candidate, **not full web parity or public/cloud release approval**. No unresolved data-loss/broken-save regression was observed in exercised flows after fixes. Unexecuted acceptance remains explicit in the report.

## Historical baseline — before parity prompts 1–12

The remainder records the original 2026-09-28 gap inventory. Statements such as “no screen,” “three routes,” or “five AI actions” describe that earlier baseline, not the current implementation.

Date: 2026-09-28. Baseline: current working tree after the shared UI refactor, including uncommitted changes.

## Conclusion

Desktop is currently a local-first document editor with optional cloud synchronization and five AI actions. The web client is a broader project and manuscript workspace. Sharing visual components has reduced duplicate presentation code, but has not transferred the web client's screens, panel contents, project model or feature workflows.

The largest remaining gaps are project/scene organization, the editor's tabbed context panels, search, rich document compatibility, publishing/export, and account/plan management. These require different amounts of work: some are presentation reuse; others require local data models, persistence and synchronization before their UI can work offline.

This is a source-based inventory, supplemented by the previous [UI verification pass](shared-ui-uat.md). “Present” means implemented in source, not certified in a live paid-account environment. Web features can be conditional on plan, configuration or document context. No new runtime tests or pixel comparisons were performed for this analysis. Route aliases and redirects are not counted as separate product screens.

## Screens and application navigation

| Screen / area | Web client | Desktop | Gap and consequence |
|---|---|---|---|
| Application shell | Logo, project/document breadcrumb, global search, user badge; Editor, Synopsis, Storyboard, Projects and Account navigation | Same shared shell/branding; Documents navigation, Account and Settings disclosures | Shared frame, substantially different navigation and information density. Desktop has no global search or project breadcrumb. |
| Home/library | Project/document views, continue-writing context, project cards, covers and writing activity | Local document cards, create, import, rename, duplicate, trash/restore, per-document sync | Partial overlap. Desktop is document-oriented and lacks the project hub and its context. |
| Document editor | Section/scene routes with full editor and context categories | One local-document route selecting existing local pages | Shared editor frame; different unit of navigation and fewer tools. |
| Project workspace | Project creation/rename, manuscript entry, Part/Chapter/Scene structure, overview and cover studio | No screen | Major missing workflow. A stored `ServerProjectId` is not a local project workspace. |
| Storyboard | Scene board, scene inspector and insights components | No screen | Missing visual planning and scene metadata workflows. |
| Synopsis | Story-intent fields and AI coaching with explicit field application | No screen | Missing planning surface and local storage/sync for those fields. |
| Account | Account/plan details and billing entry | Browser-based sign-in/out and backend connection check in a disclosure | Authentication exists; account management parity does not. |
| Upgrade/billing | Upgrade, checkout/billing routes | No equivalent screen | Paid-feature rejection can be handled, but there is no equivalent plan-selection/billing journey. |
| Onboarding | Start/onboarding and guided walkthrough | No equivalent screen | Desktop starts with local library controls; no comparable project introduction. |
| Identity exception screens | Duplicate-account and deleted-account routes | Error/status messages | Functional rejection exists, but recovery guidance is less developed. |
| Sync conflict review | No matching device-sync route in `WriterApp.Client` | Dedicated `/sync/conflicts` screen, links to preserved copies and explicit resolution | Desktop-specific capability; should be integrated visually, not removed for parity. |
| Maintenance/recovery | Web-specific save/recovery behavior | Settings for updates/diagnostics; document recovery notice | Necessary device-specific UI. Not a missing web feature. |

Desktop currently has **three route declarations**: `/`, `/documents/{DocumentId:guid}`, and `/sync/conflicts`. Its Account and Settings are disclosures inside the shell, not full screens. Web routes are listed in [Client/Pages](../WriterApp.Client/Pages); desktop routes in [Device.Shared/Pages](../WriterApp.Device.Shared/Pages).

## Editor panels and controls

The web organizes its right side into **Writing, Story, Navigator, Notes & Tasks, History**, and conditionally **Advanced**. Desktop has one **Document** panel containing page navigation, sync, AI controls and import/export in a vertical stack. `RightPanelShell` is shared, but the tab hierarchy and its contents are not.

| Panel / control | Web client | Desktop | Assessment |
|---|---|---|---|
| Writing → Writing tools | Contextual AI actions, proposal handling and associated state | Rewrite, Expand, Shorten, Summarize and Custom; preview/Apply/Dismiss | Partial feature overlap; different action layout and no common tabbed writing-tools panel. |
| Writing → Consistency Coach | Dedicated coach UI | Absent | Missing analysis/results workflow. Requires backend adapter and context availability. |
| Writing → Style & quality Coach | Quality checks, issue presentation and controls | Absent | Missing quality-analysis workflow. |
| Story → Scene card Coach | Scene metadata and coaching | Absent | Local pages are not yet full project scenes. Data model and sync work precede UI parity. |
| Navigator | Project/manuscript navigation; project workspace supports structural editing | Section headings and selectable existing pages | Shared row appearance only. No matching Part/Chapter/Scene management or local create/rename/reorder/delete page/section controls in the desktop workspace. |
| Notes & Tasks → Notes | Dedicated notes panel | Absent | Requires local note persistence and synchronization, not only a tab. |
| Notes & Tasks → Annotations | Annotation/task controls and status filters | Absent | Missing anchored content, lifecycle and panel workflow. |
| History | History filters, changes/diff presentation and version/AI operations | Editor undo/redo; last AI-apply restore and interrupted-apply recovery | Safety mechanisms exist, but there is no equivalent browsable history screen. |
| Advanced → Prompt Library | Reusable prompt UI, conditional availability | One custom-instruction textarea | One-off custom prompts are not a reusable library. |
| Translation proposal | Language selection and dedicated translation proposal panel | No dedicated translation controls | A custom AI instruction is not translation feature parity. |
| Focus / context visibility | Focus and hide/show panel controls | Same shared controls and device presentation state | Implemented overlap; native visual/interaction acceptance is still incomplete. |
| Basic toolbar | Bold, italic, paragraph/headings, lists, quote, links | Same shared essential toolbar plus undo/redo | Strongest area of component reuse. |
| Advanced toolbar | Tables, images, zoom, strike/code, headings 4–6, alignment, indentation and table editing | No equivalent visible controls | Significant UI gap. Some underlying marks/nodes already exist in desktop StarterKit; see compatibility section. |
| Document actions | Import/export menu, document preview, documentation/feedback controls | Import/export disclosure; separate diagnostics in Settings | Different discoverability and fewer actions. |
| Status bar | Word count, estimated pages, save status, plan/AI usage and contextual operation messages | Local/cloud status, explicit local save and shortcut hint | Shared wrapper, different content. Word count and useful document metrics are straightforward parity candidates. |
| Full-document / export preview | Reading preview, export preview and find-in-preview | Absent | Desktop is currently an edit-first canvas without the corresponding preview workflow. |

Primary sources: [web editor markup](../WriterApp.Client/Pages/DocumentEditor.razor), [web panel definitions](../WriterApp.Client/Pages/DocumentEditor.razor.cs), [desktop editor](../WriterApp.Device.Shared/Pages/DocumentWorkspace.razor), [shared components](../WriterApp.UI.Shared).

## Feature and data gaps

| Capability | Current desktop position | Work needed for comparable functionality |
|---|---|---|
| Ordinary offline writing | Implemented with local repository, autosave, journals and close/navigation protections | Preserve this advantage throughout parity work; web HTTP-save orchestration cannot simply be copied. |
| Document library operations | Create/open/rename/duplicate/trash/restore/delete implemented | Match the client's interaction layout where useful, while keeping local recovery and explicit destructive confirmations. |
| Project and scene structure | Local document → sections → pages; optional server IDs | Add local project/structure entities, stable identity mapping, structural operations and conflict semantics. |
| Basic formatting | Essential shared toolbar works with local TipTap | Finish keyboard/selection and multi-size acceptance. |
| Rich content compatibility | Conservative HTML/legacy validation; unsupported saved content is preserved rather than silently converted | Define a common versioned content capability contract; add schema, sanitizer, import/export and round-trip tests before exposing richer commands. |
| Search | No matching global-search surface | Local index/query and result navigation for offline use; optional backend search when online. Define whether notes/scene cards join the index. |
| Cloud save/sync | Paid-account document change feed, upload/download, durable queue and preserved conflict copies | Do not rebuild this. Extend contracts for additional entities if projects, notes, annotations or presets must sync. Verify real paid-account flows. |
| AI editing | Five actions implemented through the backend using the synced cloud copy | Reuse richer writing-tools presentation through device adapters; separately add coaches, translation and prompt library as required. AI remains online. |
| AI/history safety | Explicit application, stale-content safeguards, undo/recovery | Add a persistent history browser and comparable restore/diff experience if required. Undo is not version history. |
| Import | Native UTF-8 TXT/HTML/HTM into a document/page | Web section importer accepts TXT/DOCX. DOCX desktop import needs a conversion path and compatibility checks; it is not an HTML-picker change. |
| Export/publishing | Native whole-document HTML/TXT | Web offers HTML/Markdown, conditional DOCX/EPUB, PDF through print, scope selection, templates/presets and cover options. Decide which must work offline and which can use authenticated backend export. |
| Account/entitlements | Native browser sign-in, token handling, backend verification; service-side entitlement enforcement | Account/usage presentation, billing navigation and actionable expired/duplicate/deleted-account UX. Keep existing authorization boundaries. |
| Cover and planning assets | No equivalent studio or asset-management surface | Local asset storage/cache, project attachment model and synchronization plus reusable cover UI. |
| Help and support | Local diagnostics/update controls | Add equivalent documentation/feedback entry points with intentional data handling. |

**Important content distinction:** the desktop validator and StarterKit already understand more than the visible toolbar exposes, including headings 4–6 and several marks such as strike, underline and code. That does not mean all web formatting is supported. Tables, images and richer attributes are outside the current safe desktop editing path. A cloud document can therefore be synchronized without being fully editable on desktop. This is the highest-impact compatibility gap for users alternating between hosts.

**Important sync distinction:** the current mapping transfers document, section and page content/metadata. A project reference does not transfer the full project tree, storyboard, notes, annotations, synopsis, covers or prompt library. Adding those screens without extending local persistence would undermine the offline-first requirement.

Sources: [local document model](../WriterApp.Device.Shared/Storage/LocalDocument.cs), [sync mapping](../WriterApp.Device.Shared/Services/DeviceSyncMapping.cs), [sync API](../WriterApp.Device.Shared/Services/DeviceSyncApi.cs), [desktop editor schema](../WriterApp.Client/src/device-editor.ts), [native account menu](../WriterApp.Device.Shared/Components/DeviceAccountMenu.razor).

## Why the GUI still differs

The refactor shares the frame and selected components, not the entire web page. `AppShell` and `EditorWorkspace` accept host content; the desktop supplies fewer navigation items and a different right-panel body. `EditorToolbar` supplies essentials while the web adds advanced control groups. `EditorStatus` shares its wrapper but each host supplies different status information. `NavigatorRow` shares a row, not the complete project navigator.

Consequently, matching CSS alone cannot produce matching screens. The next extraction should target the **tabbed context-panel navigation and supported panel bodies**, together with small host adapters. Web and desktop route pages should continue to own their respective persistence/lifecycle. Avoid referencing `WriterApp.Client` from the device library or copying its large editor page wholesale.

## Recommended sequence

Executable implementation tasks for this sequence are in [desktop–client parity prompts](desktop-client-parity-prompts.md).

Priorities below assume the goal is a familiar desktop writing experience that stays local-first, followed by broader client parity.

| Priority | Deliverable | Relative scope | Completion evidence |
|---|---|---|---|
| P0 | Finish current refactor acceptance and define content compatibility | Medium | Native save/restart/import/export; normal authenticated web save/reload; rich-content round trips; paired 1280×720 and 1920×1080 captures. |
| P1 | Shared tabbed context panel, matching action placement and richer status | Medium | Same navigation/control hierarchy for supported panels; local/cloud status remains explicit; no dead feature tabs. |
| P1 | Local section/page management and search | Medium–large | Create/rename/reorder/delete structural items and find writing offline; preserve IDs, undo/recovery and sync conflicts. |
| P1 | Full-document preview and practical publishing formats | Medium–large | Preview and agreed formats work with Unicode, sections and supported formatting; offline/online boundaries are explicit. |
| P2 | Local projects, Part/Chapter/Scene structure and scene metadata | Large | Durable offline project workflow plus safe synchronization and web round trips. Enables meaningful project navigator/storyboard parity. |
| P2 | Notes, annotations, synopsis and reusable prompts | Large collectively | Shared panels backed by local repositories and entity-level sync/conflict rules. |
| P2 | Account/plan/usage and help surfaces | Medium | Same understandable account state and approved browser handoff for billing, without interrupting local writing. |
| P3 | Storyboard, cover studio, advanced coaches and richer history | Large collectively | Equivalent user workflows and preserved local data; online features handle disconnect/entitlement loss cleanly. |

Before implementing P2/P3, decide which web capabilities are required offline. Project planning and notes naturally fit the local-first model; AI calls and billing remain backend/browser operations. Supporting advanced formatting is a content-safety decision as well as a toolbar decision.

## Acceptance gaps versus implementation gaps

The previous pass recorded 546 passing server/shared tests, 13 editor-harness checks and zero-warning web/Windows/iOS-managed builds. Those results do not prove all shared screens match visually, or that paid native authentication/sync/AI succeeds against the deployed backend. Exact paired viewports, final native restart/import/export, live paid-account workflows and normal authenticated web persistence remain acceptance gaps. Missing project screens, tab bodies, search and rich export are implementation gaps. Track these separately so passing a test does not accidentally imply a feature exists.

No application code was changed as part of this analysis.


## Prompt 10 implementation update (2026-09-29)

This dated update supersedes the original AI/history observations above. Translation, section consistency/style analysis, reusable local prompts, scene-card/synopsis coaching and storyboard subplot analysis now have desktop adapters. Shared AI comparison/history presentation and a desktop History tab are present; durable recovery creates separate local copies. Prompt cloud transfer is explicit copy-based, not automatic merging. Advanced AI parity remains **Partial**: bibles, full issue-card UI, all storyboard generators, cloud history and live paid-account/provider acceptance remain outstanding. The backend version-check capability must be rolled out first. See `desktop-client-parity-uat.md`, prompt 10, for evidence and exact limits.

## Prompt 11 update — Account and help (2026-09-29)

Desktop Account & Help now exposes identity, backend/connectivity, plan/subscription/usage, refresh/sign-in/out and expired/forbidden/duplicate/deleted guidance. Web and desktop share the account summary and feedback preview. Billing uses reviewed trusted system-browser destinations; updates/diagnostics remain in Settings. Cached account data is memory-only, dated and account-isolated, and never grants backend access. Feedback attaches no manuscript or diagnostics and requires explicit preview/Send; the existing backend adds sender identity, disclosed in the UI.

Status: implemented with automated and synthetic web/native UI evidence; live authenticated account, billing return/refresh, feedback delivery and mobile acceptance remain open. See the Prompt 11 entry in `desktop-client-parity-uat.md`. This does not claim complete account/billing parity or close earlier release gates.
