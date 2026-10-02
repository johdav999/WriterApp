# Desktop–client parity acceptance evidence

## Prompt 1 — 2026-09-28

Status: implementation and automated checks passed; live acceptance is **partial**. The normal local-development web document survives navigation, reload and reopening in a new tab. Native runtime acceptance and external authenticated web acceptance remain open. Do not treat this as release approval or complete cross-host parity.

### Implementation

- Fixed an onboarding overwrite in `DocumentEditor.EnsureOnboardingStarterTextAsync`. Route loading called this before `PageEditor` mounted. The plain-text helper consequently returned empty even when `_activePage.Content` contained saved writing, allowing the client to PUT the demo starter over it. Initialization now preserves persisted HTML, including image-only content and explicitly empty paragraph markup, and does not seed through an unmounted editor. Legitimate server bootstrap and onboarding remain enabled; authorization was not changed.
- Added four regression cases against the real component helper: formatted Unicode, image-only HTML, empty paragraph HTML and an empty string before mount. They assert no HTTP mutation and no replacement of the loaded page. Existing server bootstrap tests continue to cover initial seeding and preservation of existing content. The original failing browser fixture was not rerun as an onboarding walkthrough, so that live gate remains explicit below.
- Added repository-wide generated-output exclusions in `Directory.Build.props`. Validation builds with redirected output had rediscovered older nested `artifacts/.../bin/...` content, causing an MSB3030 copy failure in the Windows build. Rebuilding with the exclusions passed. Nested artifacts are now ignored by Git, and changes to the props file trigger Windows CI. No generated output was deleted.
- Inspected existing save orchestration: ordinary autosave leaves editing enabled; navigation/closing flushes the editor; failures return false and restore editing; native close is cancelled until a successful flush. Existing tests exercise real isolated file stores with injected write failures and delayed writes. Native close-event behavior itself remains a live gate, not a claimed test pass.

### Automated checks actually run

| Check | Result |
|---|---|
| Server/web Release solution, warnings as errors | Passed, 0 warnings/errors |
| Windows host Debug and Release, warnings as errors | Passed, 0 warnings/errors |
| iOS Debug `iossimulator-x64`, `Compile` target, warnings as errors | Passed, 0 warnings/errors; managed compilation only |
| Full server/shared suite | **550 passed, 0 failed, 0 skipped** |
| New onboarding preservation theory | All 4 cases passed; included in 550 |
| Shipped device-editor browser harness | **13 passed** in the in-app browser |

Relevant existing coverage rerun in the full suite includes `LocalAutosaveTests`: failed primary save/retry and recovery, edits during a pending save followed by a final flush, newest-writing recovery after failed autosave, interrupted sessions and conflicting sessions. Transfer/repository tests cover Unicode conversion and durable file round trips. These are service-level checks, not proof of native dialogs or window lifecycle.

Commands used (from repository root):

```powershell
dotnet build BlazorApp.sln -c Release --no-restore -warnaserror -p:BaseOutputPath=artifacts/parity-p1/ -v minimal
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj -c Release --no-build --no-restore -p:BaseOutputPath=artifacts/parity-p1/ -v minimal
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Release --no-restore -warnaserror -v minimal
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-x64 -t:Compile -warnaserror -v minimal
node WriterApp.Client/tests/serve-device-editor.mjs
```

The harness server was already listening on 127.0.0.1:5179; the new launch reported the occupied port, and the existing harness was opened and its fresh results inspected. iOS restore initially failed because sandbox networking blocked NuGet; rerunning with approved network access succeeded. No dependency versions or warning policy were changed to obtain these results.

### Live web evidence

Used an isolated SQLite database, `artifacts/parity-p1.db`, with the repository's existing Development identity (`dev-oid`), a completed-onboarding test profile and AI disabled. Schema creation used `EnsureCreated`; this does not test migrations. The UI reports “Not signed in” because this is the local development identity, not external browser sign-in. No production identity, Azure database or AI provider was used.

Through the UI: created a new project, created its first scene, opened that normal manuscript, and entered Unicode text. Ctrl+B and Ctrl+I created actual `strong` and `em` elements. After autosave, typing without refocusing appended at the expected caret; undo removed that append and redo restored it. Ctrl+S was invoked and the eventual saved state observed, though autosave was also active so its timing is not isolated here.

Expected saved content:

```html
<p>Parity P1 — Räksmörgås 日本語 café.<strong> Bold</strong><em> Italic</em> caret</p>
```

Navigated to Projects and used Resume: HTML matched. Reloaded the scene: DOM still showed the same Unicode text, `strong` and `em`. Closed the test tab and opened the observed scene URL in a fresh tab: content and formatting remained. This is a new-page lifetime check, **not a full browser-process restart**. Focus/exit-focus and hide/show-panel controls changed the visible panels correctly. The empty project state and loading states were observed. Live offline/failure cases were not exercised in this web fixture.

Local ignored evidence in `artifacts/uat/parity-p1/`:

- `web-editor-1280.jpg`, `web-library-1280.jpg`: actual 1280×720 captures.
- `web-editor-1920.jpg`, `web-library-1920.jpg`, `web-reopened-1920.jpg`: requested 1920×1080, but the returned images remained **1280×720**. The suffix records the request only; these are not wide-layout acceptance evidence. Viewport override was reset.
- `editor-harness.txt`: fresh 13 passing browser results.

Visual review at 1280×720 shows toolbar wrapping and internal writing/context scrollbars. The short fixture is readable; long-line/narrow-canvas usability and paired native comparison remain open. Historical captures in `docs/shared-ui-uat.md` and `docs/release-1-uat.md` have not been reclassified as new passes.

### Issue ledger and remaining acceptance steps

| ID | Priority/status | Finding and next verification |
|---|---|---|
| P1-001 | Data loss — fixed, live walkthrough retest open | Client initialization could replace saved onboarding content before mount. Four regression cases pass. In a disposable incomplete-onboarding account, edit the demo with the HTML above, save, leave/reopen and reload twice; confirm no starter replacement and that first-time seeding still works. |
| P1-002 | Build — fixed | Nested validation outputs were included as content. Generated-output exclusions and subsequent clean-warning builds verify the repair. |
| P1-003 | Acceptance blocker — native capture | Launch and activation found Prosa, but two captures showed the Codex/ChatGPT window instead. No text or keyboard input was sent to that mismatched target. Restore a reliable Prosa capture or perform the native checklist below manually. |
| P1-004 | Acceptance gate — external web identity | Local Development persistence passes. A configured normal test account/backend outside onboarding is still required. Repeat edit/autosave/navigation/reload and full browser restart with that account; compare actual content and formatting. Do not disable authentication to close this gate. |
| P1-005 | Acceptance gate — viewport/process coverage | 1920×1080 override was not reflected in captured dimensions. Use a browser/native window that supports those sizes, verify dimensions and capture paired library/editor states. Full browser restart remains untested. |

Native checklist once P1-003 is resolved (use synthetic documents in an isolated test profile; never change permissions on the user's real store):

1. Create a local document signed out/offline with the equivalent Unicode text, bold and italic marks. Verify Ctrl+S/B/I, undo/redo, and append without refocusing after autosave. Navigate between existing pages and the library, reopen, close Prosa normally, relaunch and compare the content and formatting.
2. Import UTF-8 TXT and supported HTML with `Räksmörgås 日本語 café`; export both, reopen/import the exported files and compare text plus supported HTML formatting. Verify the original document is unchanged by export.
3. Exercise create/open/rename/duplicate and search where available, focus/context toggles, keyboard traversal and Settings reachability. Only delete disposable fixtures through an authorized recovery/confirmation flow. Capture empty/loading/error and offline states.
4. In a test host using the existing injected `AtomicDocumentWriter` failure seam, fail saving after an edit. Attempt navigation and close: the window must remain open, writing and editing must remain available, and retry after clearing the failure must persist it. Existing service tests pass; the integrated native event path still needs this check.
5. Capture library/editor at actual 1280×720 and 1920×1080 with equivalent content in both hosts. Check long lines and panel scrolling. Record actual capture dimensions.

No deployment, remote push, package installation, certificate trust or authentication registration changes were performed. Prompt 2 is the next implementation dependency; do not treat the unresolved persistence/lifecycle acceptance gates as closed or advance a release on these results alone.

## Prompt 2 — 2026-09-28

Status: compatibility and preservation implementation complete; automated checks pass. Native supported-content creation/backup/restart was exercised. Authenticated end-to-end sync and native display/export of a downloaded protected rich page remain acceptance gates.

### Implementation

- Added the versioned [content compatibility contract](editor-content-compatibility.md) and a shared JSON allowlist used by the device browser editor, native validation/import/export and backend sync validation. Web tables/images/styles retain their richer schema.
- Guarded original content at both editor initialization and `LocalEditorSession.Edit`; append/replace imports cannot replace a protected original. Unsupported content remains escaped/read-only and offers a direct original-source backup action. The full document backup preserves page formats and exact content strings in an inert JSON envelope. Ordinary HTML/text exports refuse unsupported pages.
- Aligned headings 1–6, underline, list start, code-language and link attributes across the permitted edit/import/export paths. The device's shared toolbar now exposes headings 4–6. Other schema support is distinguished from available controls in the matrix.
- Fixed acknowledgement ordering: an editor callback rejected by the host does not advance the accepted version, so a subsequent close/navigation flush can retry or report failure. Compatibility errors are surfaced through the existing save/transfer error UI.
- Aligned the backend's older upload allowlist with the shared vocabulary. Exact unchanged rich/legacy pages can pass through an upload only after existing ownership/version checks and comparison with that document's original page IDs, content and formats. New/changed source still undergoes supported-HTML validation. No authorization or entitlement policy changed.
- Existing Windows CI filters already cover `WriterApp.Shared/**`, device source, editor source/tests and shared UI. Main CI remains blocking. No landing deployment filter changed.

### Automated evidence

| Check | Result |
|---|---|
| Full server/shared suite on final source | **597 passed, 0 failed, 0 skipped** (47 added since Prompt 1) |
| Both rebuilt shipped editor bundles in browser harness | **16 passed** |
| Server/web Release solution | 0 warnings/errors, `-warnaserror` |
| Windows Release host | 0 warnings/errors, `-warnaserror` |
| Windows Debug runtime build | Passed before final backend/validation consolidation; used for the native observations below |
| iOS Debug `iossimulator-x64` managed `Compile` | 0 warnings/errors, `-warnaserror`; not a device/archive run |

New tests cover Unicode; all heading levels; nested lists; supported marks; safe links and unsafe URLs; tables/images/styles/unknown attributes; malformed legacy JSON; source-backup decode equality; explicit import sanitization; rejected replacements; no-op save/revision/upload behavior; and failed callback retry. Real sync-engine tests use temporary file stores and a fake transport. Separate `DocumentSyncTests` exercise the real backend and isolated SQLite database, including exact preservation of existing rich/legacy content and rejection of the same unsupported payload introduced as a new document.

The browser suite loads **both real shipped bundles** and tests web → device → web common-format round trips. It also verifies that the web still retains block images, tables and alignment while the device refuses that rich source without emitting replacement writing. The initial rich-image fixture nested a block image inside a paragraph and was corrected to the web schema's block structure; no web production schema was changed to make the fixture pass.

Commands use the Prompt 1 build/test forms with `BaseOutputPath=artifacts/parity-p2/`. Bundles were rebuilt with `npm run build`. Browser harness: `$env:PORT='5181'; node WriterApp.Client/tests/serve-device-editor.mjs` and the printed localhost URL. The isolated port avoids interfering with the older harness process. Final logs: `artifacts/parity-p2-build.log`, `parity-p2-tests.log`, `parity-p2-windows.log`, `parity-p2-ios.log`.

### Native/live evidence

Native capture successfully targeted the new Debug build this time. Created only a new synthetic document named **Parity P2 — Unicode headings**, signed out/offline, and typed `Räksmörgås 日本語 café — Heading six`. The shared dropdown showed headings 1–6, and selecting Heading 6 changed the writing and current-format state.

Used **Import and export → Lossless source backup**, chose a workspace path through the native picker and observed successful export. Reading the exported JSON confirmed the exact page content `<h6>Räksmörgås 日本語 café — Heading six</h6>` and format `Html`. A coordinate click on the separate picker process was rejected by automation; confirming the focused filename with Enter succeeded. No existing file was overwritten.

Closed that app window normally, verified it disappeared from the app inventory, relaunched the same Debug build and opened the synthetic document from the library. The Unicode text and Heading 6 state survived. This supplies new native restart evidence for this supported fixture; it does not retroactively complete the full Prompt 1 checklist.

Ignored evidence under `artifacts/uat/parity-p2/`:

- `native-source.json`: actual native source export.
- `native-heading-backup.jpg`: successful export and Heading 6, 2566×1016.
- `native-reopened.jpg`: same content after full app restart, 2566×1016.
- `browser-results.txt`: final browser harness results.

### Remaining gates

1. With a configured paid staging account, create a web document containing a supported page and a table/image/style page, sync to desktop, open both and verify the protected page's source and read-only explanation. Export its backup through the native picker, edit the supported sibling, sync back and compare the rich page's original content exactly. Confirm no-op opening sends no writing upload. Automated transport/backend tests pass; this live authentication/network path is not claimed.
2. Inspect the native protected-content/error UI on the final build and repeat failed-close behavior. Supported-content native rendering was exercised; a downloaded rich-page fixture was not placed into the live local store. Do not replace user data to manufacture that fixture.
3. Paired 1280×720 and 1920×1080 captures remain open. This run captured native 2566×1016; browser harness output proves editor behavior, not complete app-panel parity. Real iOS testing still requires the Apple toolchain/device.
4. Prior release gates (onboarding live walkthrough retest, external web identity, package installation/update, paid AI/conflict scenarios) remain as documented. Source-backup restoration through a product UI is future work; the backup is deliberately not a prose import format.

Prompt 3 may build on the explicit capability contract, but these open gates must not be reported as release acceptance. No Azure deployment, remote push, package installation or credential/registration changes occurred.

## Prompt 3 — shared context panels and controls (2026-09-28)

Status: implementation and automated checks pass. Shared tab navigation, supported panel presentation, document actions and status are in both hosts. Live browser checks and native normal/focus/collapsed captures passed within the limits below. This is not complete feature parity or release approval.

### Implementation

- Added typed category/subview descriptors with explicit availability, shared `EditorContextNavigation`/`EditorTabList<TKey>`, and accessible tab/panel relationships. Left/Right wrap and Home/End select and focus the corresponding tab. A small shared module suppresses browser scrolling for those navigation keys while leaving Tab/Shift+Tab and native Enter/Space activation intact. Explicit lowercase ARIA values fix the boolean attribute rendering caught by the new renderer test and browser snapshot.
- Web category hierarchy, feature gates, persisted per-document/section selection, onboarding tab IDs, data-loading callbacks and save handlers remain in the client. Desktop has working Writing and Navigator only; no empty Story, Notes, History or Advanced categories. Device panel state resets on document change and library exit.
- Shared Writing presentation composes existing host AI actions and escaped proposal comparison. Desktop retains five authenticated backend actions with preview/apply/undo safety. Pointer activation of AI actions preserves editor selection; custom instruction fields remain focusable. Local sections/pages and existing sync/conflict controls belong to Navigator; Settings retains maintenance.
- Shared document-actions presentation keeps the web's existing width/layout/save/import/export callbacks. Desktop places Save now and Import/Export beside its heading, with the existing native dialogs, compatibility guards, source backup and sanitization disclosure. Fixed a too-narrow file-transfer popover and removed accidental dependence of these document actions on the Writing category. The last correction was compiled and covered by the full suite; its Navigator-specific native rerun was interrupted as noted below.
- Shared status accepts word count, save state and storage labels, with optional host-specific content. The extracted client token rule counts Unicode letters/numbers and apostrophes after the same HTML text mapping; NFC normalization makes decomposed/composed accents agree without modifying saved content or selection mapping. Device scope is the current page (including pending edits/legacy content); web scope remains the section. Desktop displays no invented page count. Protected/recovery/trash/empty writing contexts explain why tools are unavailable.
- Existing toolbar commands, save-before-page-navigation, recovery, failed-close handling and editor schema are unchanged. No TypeScript or tracked editor bundle changed in Prompt 3. Existing selection/save/content/sync safety tests remain in the full suite. Linux CI is blocking and unfiltered; Windows CI already covers `WriterApp.UI.Shared/**`, device source and configuration. Azure's landing-only path filter is unchanged.

### Automated checks

| Check on final source | Result |
|---|---|
| Full server/shared suite | **614 passed, 0 failed, 0 skipped**; 17 added since Prompt 2 |
| Server/web Release solution | Passed with `-warnaserror`, 0 warnings/errors |
| Windows Release host | Passed with `-warnaserror`, 0 warnings/errors |
| Windows Debug runtime build | Passed with `-warnaserror`; used for live captures before the final category-independent file-actions correction |
| iOS Debug `iossimulator-x64` managed `Compile` | Passed with `-warnaserror`, 0 warnings/errors; not a simulator/device/archive run |

New tests cover capability filtering through the actual shared Razor renderer, selected/tab/panel semantics, keyboard wrap/Home/End and nontrapped Tab, device state notifications/reset/invalid categories, HTML block boundaries, Unicode/combining accents, apostrophes, emoji/punctuation, legacy text/JSON and malformed JSON. The first run found two issues: implicit boolean ARIA rendering and decomposed-accent word counting. Both were corrected and the integrated suite passed again after the final file-actions correction.

Final logs: `artifacts/parity-p3-final-build.log`, `parity-p3-final-tests.log`, `parity-p3-final-windows.log`, `parity-p3-final-debug.log`, `parity-p3-final-ios.log`. Builds use `BaseOutputPath=artifacts/parity-p3-final/`, `parity-p3-final-windows/` and `parity-p3-final-ios/` respectively, preventing conflicts with running development builds.

### Live browser and native evidence

The web server ran only on localhost:5393 against a copied isolated SQLite database (`artifacts/parity-p3.db`), with the existing Development identity and AI disabled. The old scene reference was absent in this database, so a new normal document was created through the product UI. Route: `/app/documents/3c08f809-dd24-4159-a065-be70e7ae5fcc/sections/b5dbe300-573a-48e9-824d-d0c87d0db60c`. Synthetic text `Räksmörgås 日本語 café — Heading six` produced **5 words**, saved through the existing backend flow and survived reload. No onboarding starter appeared.

Verified all web categories render their existing panels: Writing, Story, Navigator, Notes & Tasks (including Annotations), History and Advanced. History/Prompt Library and AI retained Free-plan upgrade/disabled explanations. Category ArrowRight selected Story and focused `context-category-Story`; subview End/Home selected and focused `onboarding-tab-quality`/`onboarding-tab-ai`. Document actions retained all five existing commands; Escape closed them and restored trigger focus. Collapse/focus hid the panel and left focus on Show panel/Exit focus. No browser error log entries were observed.

Both requested browser dimensions were confirmed from the DOM and captured: **1280×720 and 1920×1080**, each normal/focus/collapsed. At 1280 the web's existing print layout uses horizontal scrolling inside the writing canvas; tabs/status remain reachable. Temporary viewport overrides were reset, the browser tab was closed and the owned test server stopped.

Native Windows reopened the existing synthetic **Parity P2 — Unicode headings** fixture with its Heading 6/Unicode content intact, signed out/local. It showed only Writing/Navigator, the expected five AI actions with the cloud prerequisite, local navigation/sync controls, **5 words**, local save/storage labels and document actions. Clicking Navigator removed Writing tools from that panel. Left returned selection and visible focus to Writing. Native normal/focus/collapsed states and the widened file-transfer popover were inspected and captured at **2566×1016**; its disclosure, mode/format selectors and save-location control fit the popover.

Native capture was temporarily occluded by another window. No input was sent to that other application; the exact returned Prosa window was recovered before continuing. A later attempt to verify the final category-independent file-actions correction in the Release build was occluded again, so that particular live rerun remains open. No user writing was edited, overwritten or deleted in native testing. Native pickers were retained but no actual file transfer was repeated in this task.

Ignored evidence: `artifacts/uat/parity-p3/` contains `native-writing-final.jpg`, `native-focus-final.jpg`, `native-collapsed-final.jpg`, `native-document-actions-final.jpg`, `native-navigator.jpg`, and the six `web-{normal,focus,collapsed}-{1280,1920-request}.jpg` captures. The 1920-request captures were confirmed to be 1920×1080; the filename preserves how the request was made. Earlier native Writing/Navigator captures precede the final ARIA/NFC corrections; the `*-final` captures include those fixes and the wider popover.

### Intentional differences and remaining gates

- Desktop's Navigator is section/page based; web has projects/parts/chapters/scenes. Desktop's five AI actions are available through existing backend adapters; web coaches, advanced presets, scene cards, notes/tasks and history remain web-only. Desktop has no placeholder tabs for them. Web retains table/image/zoom/account/plan controls and print-layout page estimates; device retains its compatible toolbar and local-save actions.
- Repeat native **Navigator → Document actions → Import/Export** on the final Release build to verify the last category-independent visibility correction. Existing picker callbacks were not changed. Repeat native file transfer, protected-content/errors, failed-close and a multi-page keyboard/save-before-navigation flow in an uninterrupted session as inherited from earlier gates.
- Matched native 1280×720 and 1920×1080 comparisons remain open; native captures in this run are 2566×1016. Browser sizing is now verified at both requested sizes. Screen-reader behavior still needs platform acceptance, beyond ARIA/render/keyboard checks.
- Paid staging credentials are required for live AI proposal/apply with preserved selection, paid sync/conflicts and rich-content download/reupload. Free/offline explanations and existing automated safety checks pass; no paid operation is claimed. External web identity, onboarding walkthrough, package installation/update and real iOS checks remain as previously documented.

No reproduced data-loss blocker was found. Prompt 4 (durable local section/page management) is the next implementation dependency. No Azure deployment, remote push, package installation, certificate trust or authentication-registration change occurred.
## Prompt 4 — durable local section/page management (2026-09-28)

### Implementation and automated evidence

Implemented offline create/rename/reorder/move/delete/restore in the local Navigator, keyboard move buttons and destination selectors, last-section/last-page guards, predictable active-page fallback, deletion confirmation, and durable local trash. Local mutations flush pending writing/recovery before an atomic aggregate save; revision conflicts and write failures preserve the previous aggregate and pending editor text. Stable IDs survive rename, move, deletion and restoration. Independent document/conflict copies detach active and archived identities.

Storage schema 2 migrates schema 1 with an exact `.v1.bak` before replacing it, keeps legacy migration backups separate, and preserves unknown entity metadata. The optional sync structure request explicitly authorizes moves/removals while retaining old-client compatibility and existing journal hashes. Real backend tests verify persisted moves, removal, restore, operation replay, version conflicts, and safe rejection of protected source/web-only metadata. Sync-engine tests verify durable conflicts for remote edit versus local rename/move/delete, and structure upload/recovery across restart. Downloads preserve local identities when server page parentage changes and archive remotely removed writing.

Final checks:

| Check | Result |
| --- | --- |
| Release `BlazorApp.sln`, warnings as errors | Pass, 0 warnings/errors |
| Full Release test suite | **637 passed**, 0 failed/skipped (23 additional tests) |
| Windows Release build, warnings as errors | Pass, 0 warnings/errors |
| Isolated Windows Debug build, warnings as errors | Pass, 0 warnings/errors |
| iOS managed `Compile` for `net10.0-ios`/`iossimulator-x64`, warnings as errors | Pass, 0 warnings/errors; this is not an Apple runtime/package test |

Ignored logs: `artifacts/parity-p4-build.log`, `parity-p4-tests.log`, `parity-p4-sync-tests.log`, `parity-p4-windows.log`, `parity-p4-native.log`, `parity-p4-ios.log`. The builds use separate `BaseOutputPath` directories (`parity-p4`, `parity-p4-windows`, `parity-p4-native`, `parity-p4-ios`). Test stores/databases are isolated temporary directories/in-memory SQLite. Existing CI remains blocking; no editor TypeScript changed, so bundle/harness regeneration was not required.

### Live acceptance and remaining gates

An isolated Development desktop build was launched with `ProsaValidationDataDirectory=C:/Users/Johan/source/repos/WriterApp/artifacts/parity-p4-native-data`. No existing desktop documents were opened or changed. Capture repeatedly showed a different active application instead of Prosa. Automatic approval review rejected activating the stale target because its capture was not verified. The exact current Prosa window was re-identified; subsequently Computer Use reported physical Escape and stopped. No desktop clicks/typing/deletions were performed, and these unrelated captures were not archived as evidence.

The Computer Use [skill](C:/Users/Johan/.codex/plugins/cache/openai-bundled/computer-use/26.924.22138/skills/computer-use/SKILL.md) requires its [guidance](C:/Users/Johan/.codex/plugins/cache/openai-bundled/computer-use/26.924.22138/docs/guidance.md), which says: “If Computer Use reports that the turn ended or that the user stopped Computer Use, stop issuing app input.” Live UI work stopped accordingly. A temporary ignored browser-host experiment was prepared at `artifacts/parity-p4-ui`, but initial restore failed due to restricted network access. Cached offline restore succeeded; the host was not run or inspected after Computer Use was stopped. No live browser/native UAT pass or screenshots are claimed for this prompt.

Remaining acceptance:

- With the isolated desktop build in the foreground, create multiple sections/pages, edit Unicode writing, rename/reorder/move, delete/restore, restart, and verify active-page selection, retained writing and trash. Include keyboard operation, failed-save/close and concurrent-edit flows. Automated storage/service/render tests pass, but the actual MAUI/WebView flows still need acceptance.
- Capture both native and web screens at 1280×720 and 1920×1080; inspect normal/focus/collapsed Navigator states, empty/loading/error/offline behavior and screen-reader navigation. No new screenshots were verified in this run.
- Deploy the updated backend through the normal separately authorized process, then use a paid staging account to verify desktop/web reopen, sync of stable identities, conflict resolution, and rejected-upload recovery. Older deployed servers safely reject page moves/removals; local functionality remains available. SQL Server acceptance and real iOS runtime checks remain open.

No reproduced data-loss blocker remains in the automated checks. Prompt 4 implementation is present; its live acceptance is incomplete. No next parity prompt was implemented. No Azure deployment, remote push, authentication/permission/certificate change, or package installation occurred. The isolated desktop window remains available for manual inspection.
## Prompt 5 — Offline search and full-document preview (2026-09-28)

Implementation is complete for active local document titles/writing search and saved full-document preview. Native Windows acceptance remains open; browser-host evidence below is explicitly separate from a MAUI/WebView2 run. No Azure deployment, remote push, package installation or account/authentication change was made.

### Implementation and regressions resolved

- `LocalDocumentSearch` reads bounded active local files, extracts writing, applies the documented NFC/ordinal case rule and identifies document/section/page results. Debounce/cancellation/generations suppress late queries. Repository changes refresh open results. Selecting a result flushes pending writing and revalidates current identity/content before navigation; moved pages retain their identity, deleted results cannot open obsolete content. Repeated selection of the same result triggers a new selection request, while autosave does not reselect writing.
- `LocalDocumentPreview` renders all active sections/pages in saved order, safe supported formatting/links, encoded unsupported originals and explanatory text. Find highlights and outline navigation operate on a saved snapshot. The editor stays mounted behind preview; no writing or sync revision is produced by reading, finding or selecting an outline entry.
- Shared `SearchResultButton`, `SearchHighlightText`, `DocumentPreviewShell` and `DocumentPreviewSection` provide common presentation. The web retains its server query/filters and preview content selection. This extraction does not change the web's existing section-content scope or add local planning repositories.
- Live checks found and fixed a view-reset/layout parameter feedback loop that prevented writing workspaces from opening, root-resolving preview fragment links that returned to the library, and the web editing canvas remaining visible underneath preview. Reset is idempotent; outline URLs retain the current document; the web hides its mounted editor in preview. Regression tests cover reset feedback and route-preserving links. A misleading Writing-tools empty-page message during device preview was also removed.

### Automated checks on final source

| Check | Result |
|---|---|
| Full server/shared test suite | **657 passed, 0 failed, 0 skipped**; 20 added since Prompt 4 |
| Server/web Release solution, `-warnaserror` | Passed, 0 warnings/errors |
| Windows Release host, `-warnaserror` | Passed, 0 warnings/errors |
| iOS Debug `iossimulator-x64`, managed `Compile`, `-warnaserror` | Passed, 0 warnings/errors; no mobile GUI/archive acceptance |
| Editor asset rebuild (`npm run build`) | Passed; tracked device and client bundles rebuilt |
| Real device-editor browser harness | **17 checks passed**, including cross-mark Unicode search selection with no content/version callback and missing-match selection preservation |

Search/preview tests cover canonical Unicode offsets/case/accent rules, editing/rename/move/delete/restore/restart, exclusion of document trash, stale-query completion even when a store ignores cancellation, canceled debounce before flushing, failed saves retaining pending writing, result/file/text limits and unreadable originals. Rendering tests cover supported HTML/text/JSON, inert unsupported HTML/JSON, safe links, ordered all-page preview, cross-mark highlights, unchanged revisions/file bytes and encoded shared search text.

Logs: `artifacts/parity-p5-{build,tests,windows,ios,editor-build}.log`. Output roots: `artifacts/parity-p5/`, `parity-p5-windows/`, `parity-p5-ios/` beneath their projects. Browser harness evidence: `artifacts/uat/parity-p5/editor-harness.txt`.

### Live browser-host and web evidence

The genuine Device.Shared components ran in a temporary ignored ASP.NET interactive browser host on **127.0.0.1:5395**, with the real file repository isolated under `artifacts/parity-p4-browser-data/documents`. Account identity was unconfigured, documents LocalOnly, and the initial checks ran with no backend listening. Search/preview use no backend adapter. This verifies device components and actual TipTap interaction in a browser, not the native Windows host or a physical disconnected-device run.

Created **Parity P5 — Offline manuscript**, document `8ec8e86d-9f41-4fec-97ad-f593e65feb52`, through the UI. Wrote decomposed-accent `Räksmörgås café 日本語 — first page` on Page 1, added Second page! through Navigator and wrote `Second scene café with pending writing`. Searching `CAFÉ` flushed pending writing and returned both pages with their section/page identities. Enter opened Page 1, selected the exact source span `café`, and left the document at revision **4**. Repeating the same result after moving the caret selected the match again; distinct request URLs were observed. Empty-query results and Escape dismissal worked. The host was restarted and the browser reopened the durable writing; search still returned both pages.

Preview showed both pages in order, Find reported **2 matches on 2 pages**, and the outline's Second page! entry retained the document URL and scrolled its content into view using Enter. Back to editing restored the original writing. The file SHA-256 remained `EACC547F852469D9350E354C1E4841180A89E081748C55EA3842C31D797F3627` across preview/find/outline/back/restart/repeated search selection, with revision 4 unchanged. Unsupported original, trash, error and structural freshness behavior were exercised in automated tests rather than destructive UI actions.

The normal web client ran on **127.0.0.1:5394** with isolated SQLite `artifacts/parity-p5.db`, the existing Development identity and AI disabled. The copied older database had no live document rows, so a new normal document was created through the UI: `/app/documents/424b9b9e-928a-41d2-85c5-6ce6c24a1b21/sections/010c149f-2ff9-4be8-914d-0e2302d426d1`. Equivalent first-page Unicode writing saved and survived server/browser reload. Shared preview rendered it; the editing canvas was hidden while its TipTap instance stayed mounted. Web server search for `日本語` returned the shared page-result button and retained its Enter callback. Web search continues to use its existing server normalization; the new NFC matching rule applies to local search/preview.

Both browser hosts were captured with DOM-confirmed **1280×720 and 1920×1080** preview dimensions. The smaller device viewport uses vertical preview scrolling; outline/find and status controls remain available. Ignored captures in `artifacts/uat/parity-p5/`: `device-preview-1280.jpg`, `device-preview-1920.jpg`, `device-outline-1280.jpg`, `device-search-1920.jpg`, `device-search-selected-1920.jpg`, `web-preview-1280.jpg`, `web-preview-1920.jpg`. Older captures were replaced after the relevant visual fixes. Temporary validation servers/tabs were stopped/closed and viewport overrides reset.

### Remaining live gates and next dependency

- Repeat the verified search/preview/reopen/keyboard flows in the **actual Windows MAUI/WebView2 app**, using an isolated Development validation directory at both requested window sizes. Native computer controls were unavailable in the current browser surface; historical native captures are not reused as Prompt 5 acceptance. Include a physical offline run and native close/relaunch.
- iOS has only managed compilation evidence; signing, simulator/device launch, native lifecycle and mobile layouts still require their platform environment.
- Browser tests establish saved local behavior with unconfigured identity/no backend use. Native picker, paid sync and live AI checks remain the previously recorded gates; this prompt adds no cloud search or rollout requirement.
- No known unresolved save/data-loss defect was reproduced in this task. Prompt **6** is the next implementation dependency: local projects and Part/Chapter/Scene foundations before planning metadata search.

## Prompt 6 — Local projects and scene foundations (2026-09-28)

### Implementation

Implemented a versioned optional project within the canonical manuscript aggregate, separate local/server identities, backend-compatible Part/Chapter/Scene placement, unique scene-to-section associations, deterministic ordering/moves, recoverable tree deletion/restoration, manuscript Trash safeguards, explicit standalone attachment, project selection/opening and durable last-page resume. Project and writing mutations use a single atomic document file and revision check. Shared `ProjectHubHeader`/`ProjectSwitcher` are used by web and device; existing shared `NavigatorRow` presents both trees. New device navigation and project/writing breadcrumbs link the hub and manuscript. Existing standalone documents stay standalone; migration preserves exact backups and unknown fields.

This is intentionally a local project foundation, not project cloud synchronization. Document-only sync paths cannot bind/upload/replace local projects. Project sync and AI controls explain their unavailability. Tree trash retains all writing as accessible manuscript sections. Permanent project-manuscript deletion is blocked; document Trash/restore is supported. See `device-development.md` for the single-manuscript boundary, duplication behavior and canonical association semantics.

### Automated validation on final source

| Check | Result |
|---|---|
| Full server/shared test suite | **675 passed, 0 failed, 0 skipped** (18 new cases since Prompt 5) |
| Server/web Release solution, warnings as errors | Passed, 0 warnings/errors |
| Windows Release host, warnings as errors | Passed, 0 warnings/errors |
| Isolated Windows Debug validation build | Passed, 0 warnings/errors |
| iOS Debug `iossimulator-x64` managed `Compile`, warnings as errors | Passed, 0 warnings/errors; no simulator/device runtime claim |
| Real device-editor browser harness | **17 checks passed** |
| Scoped `git diff --check` | Passed; normal LF/CRLF checkout notices only |

New tests cover offline organization/write/restart/context identity, nondestructive attachment, subtree delete/restore isolation, invalid types/parents/cycles/cross-project references, duplicate associations/order, document Trash/restore/duplicate, migrations from both previous envelope versions, exact backups, unknown fields, future-version preservation, interrupted atomic create/update, ignored partial staging files, stale-save protection, and rejection of document-only sync for local projects. Existing recovery, edits-during-save, editor compatibility and sync tests remain passing. No TypeScript/editor schema changed in this prompt, so no new bundle regeneration was needed.

Logs: `artifacts/parity-p6-{build,tests,windows,ios,native}.log`. The initial focused run exposed a test expecting a JSON exception where the stronger identity guard correctly throws InvalidOperationException; the assertion was corrected. All final checks above passed after integration.

### Live evidence

**Device browser host**, unconfigured identity and isolated real file storage `artifacts/parity-p6-browser-data/documents`, at `127.0.0.1:5395`: verified empty hub, creation, project rename, scene creation, keyboard selection/opening, Unicode writing/Ctrl+S, project breadcrumbs, and resume after stopping/restarting the host and opening a fresh browser context. The existing backend was not running during initial creation/writing; local project operations do not call it. Project `b9097e4d-a2b4-4bde-acc0-c2c70e4d4404` owns manuscript `46f7c86d-a1a0-4252-a4b9-f12d717fcb24`; scene two references section `02ba7d61-7e17-467c-9303-48634d432ca0` and page `a4045f80-d6a2-4af3-9ba9-7fd752f94c5e`. The reopened editor showed exact `Räksmörgås café 日本語 — scene two`, matching saved HTML and the persisted last page. Added Part I and moved Chapter 1 beneath it through the UI. Tree delete/restore, failed writes and invalid associations were tested in isolated automated cases rather than destructive native UI checks.

**Actual Windows MAUI/WebView2 app**: built/launched with Development validation storage `artifacts/parity-p6-native-data`, without touching normal user data. Verified Projects navigation/empty hub, created an offline project, opened its manuscript and typed `Native offline project — Räksmörgås 日本語`. The real file contains exactly `<p>Native offline project — Räksmörgås 日本語</p>`, project `e568b6d8-88b4-451b-b724-23c2efea9854`, document `34c233a7-0cd4-447d-aad1-fe0527ce8810`, page `fedec263-9e90-4062-9380-308c6ef0e644`. Closed normally using the window close control, confirmed the window exited, rebuilt the final UI, relaunched and reopened the manuscript. The writing and project breadcrumbs survived; the final native hub shows the corrected tree titles. Captures: `artifacts/uat/parity-p6/native-reopened.png` and `native-project.png`, at the available native window size (2566×1016). Temporary occlusion by another window was handled without typing into it; no unrelated capture was saved as acceptance evidence.

The native layout check found scene titles wrapping into a narrow column because the shared editor's grid row styling also matched the hub. A scoped flex layout fixed it; the final browser and native hub captures verify normal title layout. Local project AI controls now show only their unavailable explanation. Project inputs update on input, and invalid parent choices cannot invoke Add/Move.

**Web client**: normal Development web host at `127.0.0.1:5394`, isolated SQLite `artifacts/parity-p6-web.db`, existing Development identity and AI disabled. Shared project header/selector loaded and selected existing synthetic project `5da6c576-61c1-4dbf-a4fa-9c2ee694afc9`; Navigator, Inspector, name and manuscript controls remained available with existing web plan gates. An initial empty SQLite fixture lacked application tables and failed the sign-in check; replaced it with a SQLite backup of the prior synthetic fixture, without altering authentication or application code. External authenticated acceptance is not claimed.

Web and device-browser project hubs were captured at DOM-confirmed **1280×720** and **1920×1080**. Smaller screens scroll vertically to the full tree/management controls. Files under `artifacts/uat/parity-p6/`: `device-project-1280.jpg`, `device-project-1920.jpg`, `web-project-1280.jpg`, `web-project-1920.jpg`, plus `editor-harness.txt`. Native window dimensions are recorded separately; browser-host screenshots do not substitute for native viewport coverage.

### Remaining acceptance gates

- Repeat the full multi-scene create/rename/reorder/move/delete/restore and attach-existing flows in Windows at both requested sizes, including screen-reader navigation, physical network disconnection, and failed-close/crash recovery. Native creation/write/normal-close/relaunch passed; the full fault/structure matrix has automated coverage but is not a complete native end-to-end pass.
- Run a real iOS simulator/device on Apple infrastructure, with its native adapters, lifecycle and layouts. Managed compilation alone is insufficient.
- Project-aware cloud sync, conflict handling and server identity reconciliation are **Prompt 7**, not enabled by local save success. Paid/authenticated sync/AI acceptance remains dependent on that work and a configured staging account.

No reproduced data-loss blocker remains. No Azure deployment, push, installation, auth-registration change or next-prompt implementation was performed. Existing repository work and historical reports were preserved.

## Prompt 7 — Project-aware sync and conflict handling (2026-09-28)

Implemented v2 project/manuscript aggregate contracts, explicit identity mapping, notes/card metadata transport, node tombstones, transactional parent-before-child persistence, web-change version triggers, provider migrations, old-client guards, offline durable replay, project-preserving conflict copies and project/scene conflict presentation. Server deletion now includes hidden tombstones when cleaning project/document references. Local saves remain independent of sign-in/paid access. Architecture, limitations and operation ordering are appended to `device-development.md`.

### Automated evidence

- **691 passed, 0 failed, 0 skipped** in the full server/shared test suite (16 additional cases since Prompt 6).
- **47 passed, 0 failed, 0 skipped** running the document/project sync integration suite against SQL Server LocalDB using isolated disposable `WriterApp_SyncTests_*` databases. Sandbox account initially could not start LocalDB; rerunning outside the sandbox resolved the environment issue. Tests then caught a fixture that assumed cascading scene deletion and prompted explicit dependent cleanup plus permanent soft-node cleanup coverage. Final results pass.
- Release server/web solution: **0 warnings/errors**, warnings treated as errors. Windows Release, isolated Windows Debug, and iOS `net10.0-ios` `iossimulator-x64` managed Compile also pass with **0 warnings/errors**. iOS result is compilation only.
- Tests cover scene notes and associations, tombstone delete/restore, stable local/server identities, idempotent replay after lost acknowledgment/restart, offline rename/reconnect, two devices, web metadata invalidation, rename/rename and move/delete conflicts, hard-deletion evidence even for clean local copies, both resolution choices/backups, account/backend isolation, cross-owner and entitlement rejection, missing parent/omitted structure rollback, v1 capability rejection, missing v2 route, and permanent cleanup of soft-deleted nodes.
- Fresh isolated SQLite startup applied all migrations including `20260928191657_AddProjectSynchronization`. The migration backfill regression seeds the historical schema explicitly and still verifies existing writing/version initialization. SQL Server tests exercise current schema and installed triggers; a full production migration rehearsal remains a release gate.
- Logs: `artifacts/parity-p7-{build,tests,sqlserver-tests,windows,ios,native-build}.log`. Final server/test output root is `artifacts/parity-p7-final2/` under each project. No editor source changed in Prompt 7, so the real-editor harness was not rerun; Prompt 6 evidence is historical, not a new run.

### Live local HTTP and UI evidence

The actual server ran on **127.0.0.1:5394**, with AI disabled and fresh isolated SQLite `artifacts/parity-p7-migrated.db`. Only the existing Development identity `dev-oid` was used, with a paid entitlement fixture in that test database. Production authentication/registrations were unchanged. Windows Data Protection failed under the sandbox account; the same server ran outside the sandbox with normal Data Protection. Its generated static-asset manifest cache path was corrected for that account in ignored build output only.

An ignored console validation host used the **real DeviceSyncApi, DeviceSyncEngine, journal and FileLocalDocumentStore over HTTP**, without a substitute sync API. It uploaded **Parity P7 HTTP project**, local document `1cdb9c3f-999b-41a8-951c-ff9915383521`, server document `716100a2-8702-4d7c-877d-5d4e96e5c44e`, project `577b43b5-2e8b-4cd1-a21d-7843cc260478`, and its chapter/scene/manuscript associations. The normal web Projects screen displayed the uploaded tree. Renaming it through the web UI to **Parity P7 renamed on web** and restarting the device console sync returned that name with unchanged server/local mappings and two nodes. Both completed with `Synchronization complete.` Logs: `artifacts/parity-p7-http-download.log` and `parity-p7-http-web-rename.log`; isolated device files under `artifacts/parity-p7-http-data`. This demonstrates actual local HTTP round trips; it is not an external OAuth or native paid-account test.

Device.Shared UI ran in the existing ignored browser host on **127.0.0.1:5395** with copied synthetic Prompt 6 data isolated under `artifacts/parity-p7-browser-data`. The project-aware backend explanation, enable control, sign-in status and retained Part/Chapter/Scene structure rendered at DOM-confirmed **1280×720 and 1920×1080**. The normal web project reference was captured at both sizes. Both smaller layouts scroll vertically without losing the project selector. Captures: `artifacts/uat/parity-p7/{device,web}-project-{1280,1920}.jpg`. The device test has a richer existing local tree than the HTTP web fixture; these are comparable layout checks, not claims of identical planning interfaces.

The actual **Windows MAUI/WebView2** Prompt 7 Debug app launched with copied synthetic data isolated under `artifacts/parity-p7-native-data`. Documents and Projects rendered with the new sync controls; keyboard selection opened the saved project. Clicking Enable while signed out displayed **Sign in again to synchronize. Local writing is preserved.** and retained the project/tree. This was a smoke check at the existing native window size, not a full native two-device conflict test. Temporary browser servers/tabs were stopped/closed and viewport overrides reset.

### Remaining acceptance and rollout gates

- Separately deploy v2 backend/migrations, then test signed-in paid accounts and entitlement expiration against staging/Azure. No deployment, push, account registration or provider-secret change was performed.
- Repeat native project edit/offline/restart/reconnect and conflict-resolution UI flows on two signed-in devices, including keyboard/screen-reader inspection of both project copies and failed-close/recovery. Automated engines cover these data paths; the native smoke check does not replace the complete live matrix.
- Run iOS on Apple simulator/device infrastructure. Managed Compile does not verify its adapters, signing, lifecycle or layout.
- Supported sync is one manuscript with linked Part/Chapter/Scene hierarchy. Front matter, multiple manuscripts and unlinked scenes fail closed with an explicit explanation. Notes/card data transport is ready; their offline editing/planning surfaces are Prompt 8. Other task/annotation/history entities are not claimed as synchronized here.

No reproduced data-loss defect remains in the covered flows. Historical reports and unrelated working-tree changes were preserved.


## Prompt 8 — Local planning implementation and acceptance (2026-09-29)

### Completed slices

1. Shared scene cards and inspector fields, offline scene metadata/notes and ordered storyboard with accessible move controls. Working links from Projects and the editor's planning panels.
2. Durable scene comments/tasks, resolve/reopen, quoted evidence and explicit detached-anchor review. Draft retention, revision checks, scene deletion/recovery and independent conflict copies.
3. All ten shared synopsis fields, local persistence, versioned planning sync and explicit planning search scopes with current-target validation.

### Automated evidence

- Release solution, Windows Release and iOS managed Compile builds pass with warnings treated as errors (zero warnings/errors). iOS Compile is not Apple simulator execution.
- Full test suite: **703 passed, 0 failed, 0 skipped**. SQL Server LocalDB document-sync suite: **53 passed**, including the planning tests and trigger invalidation.
- Tests cover metadata/synopsis round trips without prose changes, original-file backup, stale local save protection, unique/missing/ambiguous quotes, sticky detachment, search scope and deletion/restoration, conflict identity detachment, web row changes, older protocol rejection, request replay, omitted-annotation rollback and concurrent planning/manuscript conflicts.
- Logs: `artifacts/parity-p8-tests.log`, `parity-p8-sqlserver-tests.log`, `parity-p8-build-final.log`, `parity-p8-windows-final.log`, `parity-p8-ios-final.log`. Editor JavaScript was not changed for Prompt 8, so the editor harness was not rerun.

### Live local HTTP and UI evidence

The isolated SQLite backend `artifacts/parity-p8-backend.db` applied the new planning migration on startup. It uses the existing Development identity and synthetic paid entitlement from the earlier acceptance database; AI is disabled. Production registrations, credentials and data were unchanged. This does not verify Entra sign-in or Azure deployment.

The real DeviceSyncApi + DeviceSyncEngine + file store/journal uploaded **Parity P8 HTTP project**, server document `10981a98-3337-4360-a746-62705c8d357f`, with scene summary/notes, a task and synopsis. The current web client at `/app/synopsis/...` displayed `HTTP island logline`. Editing its Notes to `Web edited synopsis notes for device recovery.` through the web UI and restarting real device sync returned that exact value to the local file. `artifacts/parity-p8-http-roundtrip-final.log` records synchronization completion. An initial harness assertion incorrectly checked an unrelated pre-existing P7 document; it was restricted to its P8 fixture and passed on rerun.

The browser host rendered the actual Device.Shared components with an isolated file store and unconfigured sign-in. UI checks saved scene summary/notes, added a task with a missing quote, showed its detached warning, resolved it, saved a synopsis and reopened persisted fields after host restart. Scoped search found metadata/synopsis and opened the matching scene inspector from the existing planning route. A literal-string scene-card binding defect found in this check was fixed and rechecked. Shared web styling also exposed stale .NET 9 fallback assets; current-build priority and .NET 10 paths fixed the rendering. Synopsis layout no longer forces horizontal overflow at 1280 pixels.

Screenshots under `artifacts/uat/parity-p8/`: `story-1280.png`, `story-1920.png`, `web-synopsis-1280.png`, `web-synopsis-1920.png`. These compare the real device components in a browser host and the current web client at 1280x720 and 1920x1080; they are not native WebView screenshots.

### Remaining release gates and scope limits

- Run the planning flows in the actual Windows WebView, including native close/relaunch, keyboard focus and accessibility; run the iOS simulator/device equivalent on a Mac. Managed builds and browser-host flows do not establish these checks.
- Rehearse production migrations/backups and signed-in paid account synchronization against staging, including two native devices and entitlement changes. No Azure deployment was performed.
- Full live web storyboard comparison remains gated by the configured test account's Storyboard entitlement; shared scene cards/fields are compiled in both hosts, but this is not a claim of complete visual parity with every web board mode.
- Device board drag/drop, inline annotation range highlighting/reanchoring and advanced planning AI are not implemented by this slice. Ordinary saved planning works locally; AI remains conditional for Prompt 10. Task/comment creation and resolve/reopen are supported; arbitrary existing annotation-body editing is not exposed yet.

## Prompt 9 — Publishing/import/export implementation and acceptance (2026-09-29)

### Implemented

Local Markdown/DOCX/EPUB export, DOCX import, shared web/device scope and preview presentation, local named presets, durable validated PNG covers, whole-manuscript/one-section scope, and a real Windows PDF adapter. Existing HTML/TXT/native dialogs remain. Converters reuse the backend's portable source through WriterApp.Publishing; local exports do not need connectivity, credentials or entitlement and never upload manuscripts. Export captures the latest saved revision after the editor navigation save guard. Native output uses transacted writes. Converter and reader limits are displayed before import/export.

### Automated checks

- Full suite: **714 passed, 0 failed, 0 skipped** (`artifacts/parity-p9-tests-final.log`), including 11 publishing cases.
- Release solution and Windows Release: zero warnings/errors with warnings treated as errors (`parity-p9-build-final.log`, `parity-p9-windows-final.log`).
- iOS managed Compile: zero warnings/errors (`parity-p9-ios-final.log`); this is not simulator/device execution.
- Native isolated Debug build also passes (`parity-p9-native-final.log`).
- Tests cover Unicode, headings/emphasis/lists/links, scope, reordered scenes and trash, source immutability, DOCX reopen/import, cover embedding in HTML/DOCX/EPUB, cover ownership, malformed ZIP/expanded-input bounds, PNG checksums, preset restart/CAS and future-version preservation.
- No editor JavaScript changed in this slice. Focused changed-path whitespace checks passed; earlier unrelated working-tree changes were preserved.

### Live evidence

Synthetic local manuscript **Parity P9 Island manuscript** contains a heading, Swedish/Japanese text, bold, link and ordered list. It uses isolated storage in `artifacts/parity-p9-native-data`. The actual Windows app opened it, navigated to Publishing, generated PDF, displayed the Windows Save As dialog, and saved successfully. Native close/relaunch retained the manuscript and saved Paperback options. The rebuilt app's PDF preview displayed the whole title after correcting a clipped fixed-width WebView. Canceling this preview reported **PDF preview canceled. No file saved.** The final native export `artifacts/parity-p9-exports/native-paperback.pdf` independently opens with pypdf: two pages, approximately 6×9 inches, with Swedish/Japanese text, heading and both list entries intact. `native-initial.pdf` records the earlier successful manuscript export. The final screenshot is `artifacts/uat/parity-p9/native-pdf-saved.jpg` (2566×1016 native window capture).

Generated DOCX independently opened with python-docx and preserved the synthetic paragraph text; EPUB ZIP/package/XHTML independently parsed with Python/lxml. These are structural/content reader checks, not a claim of verified Word or ebook-reader visual pagination. HTML preview rendered the real converter output in both hosts. Test exports and fixture generator are under ignored `artifacts/parity-p9-exports` and `artifacts/parity-p9-fixture`.

The actual Device.Shared components in the browser host exercised preview, empty-preset validation, successful named preset save, and reload/restart persistence. A missing exception catch for invalid preset names was found and fixed. Native file/PDF actions are correctly unavailable in this browser test host. The current web client exercised the extracted export scope and preview iframe using an isolated Development backend and synthetic Standard entitlement (AI disabled). The web preview rendered saved Swedish/Japanese text. This is not production sign-in or live paid-account validation.

Screenshots under `artifacts/uat/parity-p9`: `device-publishing-1280.png`, `device-publishing-overview-1280.png`, `web-preview-1280.png`, `web-preview-1920.png`, and `native-pdf-saved.jpg`. Web captures are 1280×720 and 1920×1080. Device browser capture remained 1280×720 despite the viewport override attempt; do not treat its earlier wide filename as evidence of a 1920 capture. Native controls and keyboard selection/Save As shortcut were exercised at the actual native size.

### Remaining acceptance and scope limits

- Complete native DOCX file-picker import/append/replace/reopen, PNG picker selection, Save As cancellation/overwrite and disk-failure acceptance, using disposable copies. Converter/storage tests and successful PDF Save As do not establish every native picker branch.
- Visually reopen exports in Microsoft Word and an EPUB reader; check long books, nested formatting, covers and reader pagination. Test large PDF manuscripts/covers against WebView2's NavigateToString size limit. PDF creation and independent text/page-size checks passed; physical printer output was not tested and has no dedicated control.
- Capture device components at a verified 1920×1080 viewport and native smaller-window/accessibility checks. Run iOS on a Mac/device with native adapters before claiming mobile publishing.
- Desktop does not yet expose multi-section/page/text-selection export, cloud preset synchronization, editable custom templates, cover studio, synopsis export, or the web's complete TOC/header/footer controls. Built-in layouts are explicitly format-limited. Local publishing requires no live entitlement gate; backend paid-template behavior still needs an authorized staging account.
- No Azure deployment, remote push, authentication-registration change or software installation was performed. Prompt 10 can build advanced AI adapters next; these publishing acceptance gates remain part of Prompt 12 release acceptance.


## Prompt 10 — Advanced AI, reusable prompts and history (2026-09-29)

### Implemented scope

The five existing writing actions remain, with dedicated selection translation added. An AI workspace supplies section consistency, style/quality and custom analysis plus scene-card, synopsis-field and storyboard-subplot coaching. All run through existing authenticated backend action contracts. Shared comparison/history components serve web and device; desktop now has a History tab. Local reusable prompts support explicit authenticated copy-to/from-cloud through the existing preset contract. Automatic preset merging, overwrite and background sync are deliberately not exposed because that API has no conflict/idempotency contract.

Apply is explicit and target-specific. Translation uses the selected manuscript range; synopsis updates one named field; scene cards update the seven supported narrative fields disclosed in the UI; analysis has no Apply operation. Saved source revision checks, current-account guards, proposal expiry, atomic pre-apply history and separate-copy recovery preserve local writing/planning. Invalid/future evidence is retained. No credentials or sensitive provider diagnostics are written to this history.

### Automated acceptance

- Full Release suite: **737 passed, 0 failed, 0 skipped** (`artifacts/parity-p10-tests-final.log`).
- Release solution, Windows Release and iOS managed Compile: **zero warnings/errors**, warnings treated as errors (`parity-p10-build-final.log`, `parity-p10-windows-final.log`, `parity-p10-ios-final.log`). Managed iOS compilation does not establish mobile execution.
- Tests cover every added route/target, required translation selection/language, analysis-only protection, synopsis/card prose preservation, changed/deleted local revisions, remote version changes before/during execution, old-backend preflight rejection without a billable call, missing version echoes, cancellation with a late response, account switching, expiry, durable prompts/history, future-file preservation, full planning recovery with detached identities, preset copy contracts/no retry, and inert rendering of AI text. Existing error/entitlement/quota tests remain passing.
- NuGet initially reported an unavailable audit endpoint. An authorized metadata restore succeeded, followed by clean warning-as-error builds; auditing was not disabled.

### UI evidence

Actual Device.Shared components ran in the isolated browser host against synthetic P9 manuscript storage. Empty prompt validation, successful local prompt save, reload/load of that prompt, signed-out cloud transfer messaging, disabled AI on a local-only document, translation controls and the separate History tab were exercised. The actual Windows app was rebuilt into `WriterApp.Desktop/artifacts/parity-p10-native` using isolated validation storage, opened the synthetic manuscript, showed the new editor controls/History, and loaded the browser-saved prompt after native process startup. No real manuscript or cloud account was used.

Evidence: `artifacts/uat/parity-p10/device-ai-library.png` (1280×720) and `native-ai-library.jpg` (2566×1016). Screenshots precede final small helper-text/capability-preflight refinements; feature UI behavior is unchanged. No new editor JavaScript was needed. The web client compiles against the shared history presentation and its renderer safety test passes; a new paired live web History capture was not performed in this slice.

### Remaining live acceptance and explicit limits

- An authorized AI-enabled staging account/provider is still required for real translation, consistency, style, scene-card, synopsis and storyboard responses; preview/Apply/reopen; quota/expired session/reconnect; and cross-account prompt transfers. Synthetic/fake-response tests prove guards, not provider quality or paid-account acceptance. The previously used local backend test entitlement had AI disabled.
- Deploy the backend capability/version checks before using this desktop build's cloud AI. An older backend now stops at preflight without sending a billable action. Provider execution can still consume quota if the source changes during that execution; its stale proposal is discarded safely. No Azure deployment was performed.
- Consistency is section-only without bibles. Scene-card application excludes status/tags/entity links. Storyboard supports subplot continuity analysis, not the web's full collection of board generators. Workspace reusable prompts are analysis-only; manuscript transformations use the retained editor Custom flow. Advanced output remains a shared text comparison rather than the client's complete specialized issue-card interaction set.
- History is local operation/recovery history, not synchronized cloud version history. Interrupted Applying entries require reviewing current writing or recovering a separate copy. Test large-history retention/storage behavior before broad release. Full native proposal application with a real provider, mobile GUI, accessibility/small-window and exact paired viewport acceptance remain release gates.
- Previous prompt 9 publishing gates remain open. No commit, push, deployment, authentication-registration change or software installation was performed by prompt 10.

## Prompt 11 — Account, plan, billing and help (2026-09-29)

### Implementation and automated acceptance

Desktop now has Account & Help with native identity/backend/connectivity, refreshed plan/usage, dated cache guidance, sign-in/out, account failure guidance, reviewed billing destinations and shared help/feedback presentation. Web Account uses the same plan/usage card and feedback composer. Cached plan data is display-only and account-generation isolated. Feedback requires a current preview, attaches no manuscript/identifiers/logs and discloses backend-added sender identity. Native update/diagnostic controls remain in Settings.

- Full Release suite: **753 passed, 0 failed, 0 skipped** (`artifacts/parity-p11-tests-final.log`).
- Release solution, Windows Release and iOS managed Compile: **zero warnings/errors**, warnings treated as errors (`parity-p11-build-final.log`, `parity-p11-windows-final.log`, `parity-p11-ios-final.log`). iOS compilation does not establish device execution.
- Sixteen new tests cover authenticated/signed-out/offline behavior, transient cache retention, expiry/forbidden/payment-required/duplicate/deleted responses, late account-switch responses, unsafe URLs, fixed billing/docs destinations, feedback payload/no retry, signed-out submission rejection and inert shared rendering including exhausted allowance.
- Existing authentication, sync isolation and local-save tests remain passing. Native isolated Debug compilation also passed. No editor JavaScript changed in this slice.

### UI evidence

The actual Windows app opened Account & Help using isolated synthetic P9 storage. It showed signed-out/unconfigured-native-auth guidance and previewed `https://localhost:7384/app/account/billing` before a separate Open website action. That action was not invoked against the unavailable HTTPS test backend. Native evidence: `artifacts/uat/parity-p11/native-account.jpg` (2566×1016).

The real web client ran against an isolated SQLite test copy with AI and Stripe disabled. Its shared card displayed synthetic Standard/Active status and unavailable token budget accurately. A synthetic feedback draft was previewed; Send was never clicked. Development's existing header said Not signed in while the profile endpoint supplied synthetic authenticated status, so this is presentation evidence, not production authentication acceptance. Screenshots: `web-account-1280.png` and `web-feedback-1280.png` under the same evidence directory (1280×720).

The actual Device.Shared components in the browser harness showed signed-out guidance, unavailable host handoff, blank-feedback validation and a reviewed synthetic draft with Send disabled. `device-feedback-1280.png` captures this at 1280×720. A help-anchor routing issue found during these checks was fixed; the rebuilt device host verified Prepare feedback stays at `/account#feedback-draft`. The web link now preserves `/app/account` as well. Earlier screenshots precede this small routing fix and the account menu's delegation to the shared refresh service; final builds/tests include both changes.

### Remaining live acceptance

- Configure an authorized native test sign-in environment to exercise real authentication callback, sign-out/restart with local writing preserved, paid/free accounts, expiry, reconnect and entitlement changes. Fake-response tests cover guards but do not replace live acceptance.
- Verify system-browser billing sign-in and manual return/Refresh against an approved reachable backend. No callback was invented, no payment was completed, and no authentication registration was changed.
- If desired, explicitly authorize a test feedback delivery to the support destination; this implementation was validated without sending feedback. Backend delivery configuration remains external to this slice.
- Complete paired 1920×1080, small-window, keyboard/accessibility and iOS device acceptance in Prompt 12. Prior publishing/AI/sync acceptance gates remain open.
- No commit, push, Azure deployment, software installation or change to backend authorization/token storage was performed.

## Prompt 12 — Integrated acceptance (2026-09-29)

The [integrated acceptance report and release checklist](desktop-client-parity-release-checklist.md) is the current decision record and supersedes earlier test totals and open viewport statements where fresh evidence exists. Final validation: **756/756 tests**, **17/17 shipped-editor checks**, solution/Windows/iOS managed warning-as-error builds with zero warnings/errors. Native writing/normal close/relaunch, Unicode/bold, notes, HTML Save As/import and stale-export rejection passed; web writing/synopsis persisted. Device component search/preview, keyboard categories, section creation and planning persistence passed.

Fixed misleading AI-limit messaging while AI is disabled, category ordering/helper text, section-title Save requiring blur, and planning input bindings that lost rapid keystrokes in the server-hosted component test. The final binding was retested with full text and saved/reopened synopsis/summary. Earlier native captures precede that last shared binding change; final platform builds include it.

Recommendation: **internal Windows local-first preview candidate only**. Live paid/auth/sync/AI, all publishing failure branches, complete viewport/accessibility pairs, signing/install/update and iOS execution remain explicit gates. The current gap matrix distinguishes implemented scope from those gates; historical prompt results are not automatic release approval. No deployment or remote Git operation occurred.
