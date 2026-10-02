# Desktop–client parity implementation prompts

These prompts implement the five stages in [the gap analysis](desktop-client-gap-analysis.md). Run them in order in this repository. Large stages are split into bounded tasks so storage, sync and UI can be reviewed separately. Each prompt is an implementation task, not a request for another plan.

## Sequence

| Requested stage | Prompts |
|---|---|
| 1. Compatibility and save/reopen acceptance | 1–2 |
| 2. Shared tabbed editor panels and aligned controls | 3 |
| 3. Local section/page management, search and preview | 4–5 |
| 4. Local projects/scenes before storyboard, notes and synopsis | 6–8 |
| 5. Publishing, advanced AI and account interfaces | 9–11 |
| Final acceptance across all stages | 12 |

Start a task with: **“Implement prompt N in docs/desktop-client-parity-prompts.md, including the common requirements.”** Reinspect the repository each time; earlier validation counts and environment assumptions are historical evidence, not guarantees about the current tree.

## Common requirements for every prompt

- Read applicable repository instructions, `docs/device-development.md`, `docs/desktop-client-gap-analysis.md`, `docs/shared-ui-uat.md` and the relevant previous implementation evidence. Preserve unrelated work. Inspect current code before choosing contracts or adding abstractions.
- The web client is the GUI and interaction reference. Extract shared presentation into `WriterApp.UI.Shared` and use it from both hosts. Keep device repositories, lifecycle and orchestration in `WriterApp.Device.Shared`, Windows adapters in `WriterApp.Desktop`, and shared backend contracts in the appropriate existing contracts project. Do not reference the web client or a native host from the presentation library. This supersedes the older Release 1 instruction to place all reusable UI in Device.Shared.
- Ordinary writing, local organization, notes and preview must work without network, sign-in or paid entitlement. AI and cloud sync use the authenticated backend and existing entitlements. Never place provider secrets in a device app or weaken backend authorization.
- Preserve durable local saves, save-before-navigation, failed-close protection, recovery journals, edits arriving during saves, account/backend isolation, conflict copies and explicit resolution. Ordinary autosave must not toggle contenteditable, reset selection or blur the editor.
- Shared components receive typed state/callbacks or narrow host adapters. Do not copy the web's API-save orchestration into desktop, create a universal service, or expose placeholder tabs/buttons as functioning features.
- New persisted entities require stable local identities, explicit server identity mapping, versioned storage, compatibility with older data, and safe migrations with recovery. Do not reset existing writing. Unknown/unsupported content must be preserved and explained, never silently discarded.
- Add focused behavior tests for changed persistence, sync, security and content boundaries. Reuse existing suites. Extend the real-editor harness where editor behavior changes. Rebuild tracked editor assets after source changes.
- Build the server/web solution in Release, Windows host and available iOS managed target with warnings treated as errors. Run the full server/shared test suite after integrated changes. Keep CI blocking and cover new source/assets/configuration paths. Record unavailable platform checks accurately; managed iOS compilation is not an iOS device test.
- For UI changes, exercise both hosts with synthetic data and capture comparable screenshots, including 1280×720 and 1920×1080 where available. Verify keyboard navigation, empty/loading/error states and offline behavior. Do not claim visual parity from compilation or source inspection alone.
- Do not deploy Azure, publish/push remotely, install packages into Windows, trust certificates, alter authentication registrations or force-push history as part of these prompts. Building an existing package is allowed; installing or distributing it is a separate task. Do not change the landing-only Azure deployment filter.
- Use isolated local/test data. Credential-dependent or unavailable-device checks remain explicit acceptance gates. Continue independent implementation; never substitute mocks or invented success for live verification.
- Update `docs/device-development.md` and append evidence to `docs/desktop-client-parity-uat.md` (create it in prompt 1). Keep historical reports intact. End each task with implemented behavior, checks actually run, unresolved defects, blockers and the next dependency. Do not proceed past a data-loss blocker into dependent feature work.

## Prompt 1 — Close save/reopen and current GUI acceptance gaps

```text
Implement prompt 1 with the common requirements. Establish a trustworthy runtime baseline for the shared-UI refactor and repair defects found within its scope.

Inspect the existing UAT reports, shared UI wiring, native save/lifecycle services and web onboarding/save flow. Create docs/desktop-client-parity-uat.md with separate implementation, automated-test and live-acceptance evidence.

Use an isolated normal web document outside the onboarding demo and a native synthetic local document containing equivalent Unicode text and formatting. Verify edit → autosave → navigate away → reopen → close app/browser → relaunch → reopen. Compare content and formatting, not merely a Saved label. Check Ctrl+S, Ctrl+B/Ctrl+I, undo/redo, caret retention after ordinary autosave and edits made while a save is pending. Exercise save failure and failed-close protection through existing test seams without risking user documents.

Investigate the previous web result where onboarding restored starter text after reload. Identify whether this is demo seeding, a fixture/authentication issue or persistence failure. Fix unintended repeated seeding or save failure if reproduced; do not bypass authentication or disable legitimate onboarding globally. If a normal authenticated environment is unavailable, record the precise missing prerequisite and leave web live-persistence acceptance open.

Repeat native TXT/HTML Unicode import/export and reopen, page navigation, focus/context toggles, library actions and Settings reachability. Capture library/editor states at the requested sizes where possible. Record unavailable sizes or interrupted UI access. Do not mark prior-run evidence as newly verified.

Acceptance: locally verifiable save/reopen paths pass with content evidence; any reproduced regression has a focused test; remaining live gates have exact reproduction steps and prerequisites. No false “all accepted” conclusion while a normal web save/reload remains unverified.
```

## Prompt 2 — Define and enforce cross-host document compatibility

```text
Implement prompt 2 with the common requirements, using prompt 1 evidence.

Inspect both TipTap entry points/extensions, device HTML/legacy validators, sanitizers, import/export converters and sync payloads. Write docs/editor-content-compatibility.md listing nodes, marks and attributes as: editable in both hosts, preserved but not editable on device, intentionally sanitized on import, or invalid/unsafe. Distinguish missing toolbar controls from unsupported schema.

Implement one explicit compatibility contract used at the relevant editor/content boundaries. Start with existing common text constructs, heading levels, lists, links and compatible marks; consolidate duplicated schema/command definitions where safe. Add desktop controls for compatible formatting only when their shared command, state and round-trip behavior are verified. Do not falsely claim tables/images are supported by accepting their tags alone.

For rich content outside the supported set, preserve the complete original payload, present an actionable read-only explanation, and prevent accidental partial saves from replacing the cloud source. Provide a safe way to export/preserve that original or open the cloud version through the existing trusted backend URL. Do not auto-convert saved rich content into lossy text. Import sanitization must be separately disclosed and tested.

Add fixtures for Unicode, nested lists, headings 1–6, supported marks, safe/unsafe links, tables/images, attributes and legacy formats. Test web → sync → desktop open/save → sync → web round trips for supported content, and byte/content preservation without mutation for unsupported content. Verify no-op opening does not create a destructive upload. Keep web advanced editing working.

Acceptance: the supported intersection round-trips without semantic loss; unsupported documents remain intact and clearly noneditable; capability reporting agrees with visible controls. Record remaining richer-schema gaps explicitly rather than expanding this task into an unbounded media editor.
```

## Prompt 3 — Share tabbed context panels and align editor controls

```text
Implement prompt 3 with the common requirements. Extract the web editor's category/subview navigation and reusable supported panel presentation into WriterApp.UI.Shared; make both hosts consume it.

Use the current web hierarchy as reference: Writing, Story, Navigator, Notes & Tasks, History and conditional Advanced. Introduce typed category/subview descriptors and explicit availability rather than platform checks. Desktop initially exposes working Writing and Navigator content; add other categories only when a functioning adapter exists. Do not create empty Story/History tabs to simulate parity. Preserve the web's complete category set, plan gates and existing handlers.

Compose desktop's five AI actions and shared proposal comparison into Writing. Put local section/page navigation in Navigator. Align action labels, spacing, focus/context controls and document action placement with web. Move import/export into a familiar document-actions entry, preserving native dialogs. Keep sync status discoverable and diagnostics/update maintenance in Settings.

Bring supported status content into a shared presentation: Unicode-aware word count consistent with the client's algorithm, save state and clear local-versus-cloud status. Display page estimates only where their meaning is defined; do not invent pagination metrics. Keep host-specific entitlement/account fields conditional.

Implement accessible tab roles, selected state, keyboard traversal and predictable focus when panels switch or collapse. Preserve text selection across toolbar/AI actions and save-before-navigation. Test tab-state changes, capability filtering and document-switch reset; use real native/browser flows to check focus and layout.

Acceptance: both hosts use the same tab-navigation components and supported panel presentation; web-only panels still work; desktop shows no dead controls. Capture normal/focus/collapsed states and document intentional capability differences.
```

## Prompt 4 — Add durable local section/page management

```text
Implement prompt 4 with the common requirements. Extend the existing local model and Navigator to create, rename, reorder, move and delete sections/pages without an account or network.

Inspect the current server structure rules and local repository rather than inventing incompatible hierarchy semantics. Define deterministic order, valid parentage and last-page/last-section behavior. Preserve identities when renaming/reordering/moving; allocate new identities only for genuinely new entities. Provide keyboard-accessible move controls alongside any drag/drop interaction.

Flush pending writing before structural operations. Apply each mutation atomically to the appropriate local aggregate and journal sync work only after durable persistence. Deletion must be recoverable through a persisted trash/tombstone or equivalent durable recovery mechanism; do not depend solely on in-memory Undo. Choose the next active page predictably and keep failed mutations from removing the current editor content.

Carry supported structural changes through existing document sync contracts. Handle concurrent rename/move/delete versus edit without silently dropping either version. Preserve unknown metadata during changes and do not rewrite unrelated sections just to normalize display order.

Acceptance: create/edit/reorder/move/delete/recover/restart works offline; failed-save and concurrent-edit tests preserve writing; synchronization retains stable local/server mapping. Both web and desktop can reopen the resulting document correctly. Do not implement the project Part/Chapter/Scene tree yet; that follows in prompt 6.
```

## Prompt 5 — Add offline search and full-document preview

```text
Implement prompt 5 with the common requirements after prompt 4.

Extract reusable search-results and preview presentation from the client where practical. Add desktop search across active local document titles and writing, with results identifying document, section and page. Exclude trash by default; make any trash scope explicit. Keep results current after edit, rename, move, restore and delete. Use a bounded cancellable local query/index strategy; prevent stale results from navigating to obsolete content. Match Unicode/case behavior to a documented rule.

Selecting a result saves pending work, opens the correct local page and highlights or navigates to the match without changing the stored text. Do not upload local writing for search. Provide keyboard operation and meaningful empty/error states. Leave metadata entities not yet implemented out of the scope rather than displaying misleading filters.

Add an offline read-only full-document preview with correct section/page order, supported formatting, safe links, outline navigation and find-in-preview. Preview must use current locally saved content after a flush and must not mutate the editor or create sync revisions. Explain unsupported rendering while preserving original data. Reuse the client visual language and share pure rendering components where compatible.

Acceptance: search and preview work signed out with network unavailable, survive restart, and reflect structural edits. Test stale query cancellation, Unicode matches, deleted content, safe rendering and save-before-result-navigation. Add local notes/scene metadata to search only when later prompts provide their repositories.
```

## Prompt 6 — Establish local project and scene foundations

```text
Implement prompt 6 with the common requirements before building storyboard, notes or synopsis.

Inspect the backend project/tree/scene contracts and current manuscript bindings. Add versioned durable local project and Part/Chapter/Scene entities compatible with those semantics. Define the relationship between scene identity, manuscript section/page content and project membership explicitly. Avoid duplicate canonical copies of the same writing. Preserve local identity separately from server identity; a ServerProjectId alone is not the local model.

Implement offline project creation/rename, project selection, manuscript opening and tree create/rename/reorder/move/recoverable-delete. Enforce acyclic parentage, allowed node types, deterministic ordering and safe scene/document deletion. Existing standalone local documents must continue to open and edit unchanged. Offer an explicit nondestructive attach/create-project operation rather than silently converting all documents.

Extract reusable project hub/navigation presentation so web and desktop share it. Keep capability-specific actions hidden until supported. Add the new screens and context breadcrumbs to desktop navigation. Store the last writing context per project safely and restore it after restart.

Acceptance: a signed-out user can create a project, organize a multi-scene manuscript, write, restart and resume offline without identity or content loss. Include migration, invalid-tree, crash/partial-write and document-association tests. Project sync is the next task; do not imply cloud availability from a local success badge.
```

## Prompt 7 — Synchronize project structure and scene metadata safely

```text
Implement prompt 7 with the common requirements after prompt 6.

Extend the existing authenticated sync design to cover project hierarchy, scene metadata and manuscript associations. Inspect existing API authorization/version/change-feed patterns and use additive compatible contracts. Document aggregate boundaries, version checks, identity mapping, tombstones and the order in which parent/child/document dependencies upload and download.

Keep local persistence independent of cloud entitlement. Queue durable operations offline, support idempotent retry and interrupted batches, and retain account/backend isolation. Design explicit handling for move-versus-delete, rename-versus-rename, edit-versus-delete, missing parent and divergent tree changes. Preserve both versions or recoverable conflict evidence; do not apply silent last-write-wins to writing or structure.

Update conflict presentation to explain affected projects/scenes and the result of each resolution. Do not automatically merge structures where intent is ambiguous. Ensure older document-only sync clients continue to operate safely or receive a clear capability/version response without damaging new entities.

Acceptance: integration tests cover offline queue/restart/retry, two-client edits, project/document identity mapping, tombstones, authorization, entitlement loss and account switching. Demonstrate web/device round trips in an approved local/test environment. Deployed-backend compatibility remains a release dependency until separately deployed and verified; do not deploy as part of this prompt.
```

## Prompt 8 — Add local planning surfaces on the project foundation

```text
Implement prompt 8 with the common requirements after prompts 6–7. Work in buildable slices: scene metadata and storyboard first, then notes/tasks/annotations, then synopsis.

Extract the client's reusable scene cards, inspector and storyboard presentation into the shared UI. Back desktop planning with the local project model and sync contracts; all ordinary planning edits must work offline. Preserve the client's established field semantics, ordering, navigation and accessible alternatives to drag/drop. Keep AI insights conditional until prompt 10 supplies their adapters.

Add durable local notes, task/annotation state and synopsis fields following the web semantics. Define annotation anchors that can survive edits where possible; show detached/orphaned anchors for review rather than attaching them silently to unrelated text. Extend versioned storage and entity sync with explicit conflict behavior, including concurrent manuscript edits. Enable the corresponding Story, Notes & Tasks and Synopsis surfaces only when their persistence paths are complete.

Extend local search to available scene metadata/notes with explicit scope filters. Ensure trash/deletion and project reassignment do not leak orphaned results. Keep synopsis and planning content distinct from manuscript text; applying a planning edit must not rewrite prose.

Acceptance: create/edit/reopen planning content offline, synchronize through the approved test backend, and compare web/desktop representations. Cover anchor invalidation, concurrent metadata edits and scene deletion/recovery. Report each completed slice and any live test gates; do not present unsaved mock panels as implemented features.
```

## Prompt 9 — Expand publishing and native import/export

```text
Implement prompt 9 with the common requirements. Bring desktop publishing closer to the web using shared export-dialog/preview presentation and explicit host capabilities.

Retain native HTML/TXT export. Add Markdown export and DOCX import with documented formatting conversion. Add the web's applicable document/section scope, template/preset and cover-selection concepts. Persist locally owned presets and preserve them across restart. Cover assets need durable local storage, validation and project association; do not refer to temporary file paths or remote URLs as if available offline.

For DOCX and EPUB export, inspect and reuse existing server conversion services through authenticated backend adapters where appropriate. State clearly which formats require connectivity or entitlement. A local-only document must not be silently uploaded for export: show the content scope and destination and require explicit user action before transmission. Prefer local conversion when an existing safe compatible implementation is available. Implement PDF/print through a real supported host adapter; do not display browser-only print controls as working native features.

Prevent stale exports by flushing and snapshotting the selected content. Apply converters to the snapshot without mutating source writing. Explain lossy conversions, supported content and unavailable capabilities. Handle canceled file dialogs, failed network exports and disk errors without misleading success messages.

Acceptance: Unicode, headings, lists, links, scene order, selected scope and cover behavior have format-specific tests; generated files reopen in independent readers. Native dialogs and preview are exercised. Do not label a format complete solely because an API returned bytes. Live entitlement checks remain explicit if credentials are unavailable.
```

## Prompt 10 — Expand AI tools, reusable prompts and history

```text
Implement prompt 10 with the common requirements after the project/planning foundations and shared panel work.

Preserve the existing five desktop actions. Add dedicated translation, reusable prompt library, consistency analysis, style/quality analysis and scene-card coaching using existing backend contracts and shared client panel presentation. Include storyboard/synopsis AI entry points supported by those contracts. Implement these as explicit capabilities; do not fabricate endpoints or replace missing backend features with client-side provider calls.

Persist locally authored prompt definitions and synchronize them only through supported authorized contracts. Define which manuscript/planning revision each request analyzes. For stale or offline cloud state, explain the required synchronization and avoid analyzing a different revision silently. Cancellation, expiry, quota, entitlement loss and reconnect must leave writing intact.

Keep proposals inert until explicit Apply. Translation, manuscript edits and scene/synopsis-field changes need distinct typed application targets. Reject stale proposals or provide a safe re-run/compare path. Record durable AI operation history, original/proposed revisions and safe restore information; add the shared History surface without confusing editor undo with persisted versions. Do not store tokens or unnecessary sensitive request data in diagnostic logs.

Acceptance: each added action has contract/error tests, explicit target validation and stale-apply protection. Exercise real authorized AI actions when a configured test account is available; automated fake responses test safety but do not establish live AI acceptance. Keep unsupported actions absent or explicitly unavailable without claiming parity.
```

## Prompt 11 — Add account, plan, billing and help interfaces

```text
Implement prompt 11 with the common requirements. Share the web's reusable account/plan/usage presentation through host adapters while keeping native browser sign-in and secure token handling.

Add a desktop Account screen with identity, backend connection, current plan/entitlements, usage where provided, refresh, sign-in/out and clear expired-session/duplicate/deleted-account guidance. Distinguish cached information from a fresh backend response. Never treat cached paid status as authorization for backend operations.

Provide billing/upgrade navigation through an explicit system-browser handoff to a trusted configured backend URL. Preserve return context where the existing authentication design supports it. Do not collect payment data, embed a checkout impersonation, or manufacture unsupported account-linking operations. Do not alter registrations or complete purchases during validation.

Add shared documentation/feedback entry points. Keep native update/diagnostic controls accessible through Settings and explain what diagnostic export contains. Feedback must be previewable; do not automatically submit writing, identifiers or logs. Make signed-out/offline account screens useful without blocking access to local documents.

Acceptance: authenticated/unauthenticated/offline/expired/forbidden/quota states have accurate UI and tests; signing out preserves local writing and isolates subsequent account sync. Verify safe browser destinations and native callback behavior in an approved environment when available. No change to backend authorization or token storage protection.
```

## Prompt 12 — Run integrated parity acceptance and close regressions

```text
Implement prompt 12 with the common requirements after prompts 1–11. Audit the implemented result against docs/desktop-client-gap-analysis.md and the compatibility contract; do not assume every prior prompt completed every live gate.

Run the full automated suites, real-editor harness and warning-as-error builds. Verify the actual Windows app and web client with equivalent synthetic projects and writing: create, structure edits, search, preview, notes/synopsis, close/restart/reopen, import/export, shared tabs, formatting, AI preview/application, account and sync conflict resolution. Test offline-to-online transitions, expired sessions, entitlement loss and conflicting edits without losing local work.

Capture paired library/editor/project/planning/account screenshots at 1280×720 and 1920×1080 where available. Check tab hierarchy, density, typography, icons, focus order, keyboard operation, scrolling, long titles, empty/error states and small-window behavior. Fix reproduced in-scope regressions and rerun affected checks. Preserve intentional native differences such as local/cloud status, recovery, file dialogs and updates.

Update the gap matrix to Present/Partial/Missing with evidence links. Separate implementation completion from live acceptance. Record unavailable credentials, signing/device prerequisites and untested viewport sizes precisely. Do not equate managed iOS compilation with mobile GUI acceptance. Document any backend rollout/migration/version dependencies without deploying them.

Acceptance: no known unresolved data-loss or broken-save regression; completed capabilities are supported by evidence; every remaining gap has a concrete next action. Finish with a release recommendation grounded in results, not an automatic claim of full client parity.
```
