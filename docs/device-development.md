# Device application development

## AI parity prompt 5 — Checked client consistency pages (2026-10-05)

Client section consistency now reads the existing version-1 structured source with
`GET api/documents/{id}/structured-translations/source?scope=section&sectionId={id}&purpose=consistency`.
This read-only purpose permits annotated writing; ownership and graph limits still
apply. Translation generation/approval/commit retain their annotation restriction.
Rebuild the server, client and shared UI together; no migration or content-format
change is required. An older backend may reject an annotated source; the client
reports the unavailable check instead of using unchecked page data.

The client captures every ordered manuscript page in the selected section. Unique
evidence identifies the actual page; provider offsets only refine that verified
passage. Jump/review saves pending writing, validates the captured account/backend
and source, opens the owned page, waits for its keyed editor and resolves its local
range. A changed page/order/section or duplicate quote requires a fresh check.
Page changes within the same checked section retain intentional decisions; account,
document and section invalidation remain strict. Existing conflicting-passage
viewing remains available. Scene-content routes keep their separate single-scene
persistence target; use the manuscript section route for findings across pages.

Reproduction is in `artifacts/ai-parity-p05/verification.ps1`, with evidence and
remaining full-shell/native/provider gates in the dated prompt-5 UAT entry.
Continue in this same dirty checkout with **prompt 6**.

## AI parity prompt 4 — Saved glossary context (2026-10-05)

**Check style & quality** reads the document's saved glossary through the owned,
read-only `GET api/documents/{id}/glossary/device` version-1 contract. It makes one
bounded refresh attempt per explicit check (10-second timeout), without an AI
request or manuscript upload. The local editor still saves before the check.
Mapped documents can refresh terms while their local writing is unsynchronized;
new local documents must synchronize once to acquire their server identity.

`LocalGlossaryStore` stores atomic version-1 files under `glossary-cache`, separated
by the hashed backend/account scope, local document ID and mapped server ID.
This additive cache needs no authored-document or sync-format migration; existing
files remain untouched. An absent cache means unavailable, never verified empty.
Malformed/future cache files are preserved and excluded. The contract supports up
to 1,000 saved terms of at most 256 characters and a 1 MB JSON snapshot; oversized
context is rejected whole, never silently truncated. Authored glossary rows and
notes are not changed by reads. Terms retain server order for shared near-match
resolution.

The quality panel displays whether terms were loaded for the current check,
verified empty, cached offline, cached after refresh failure, or unavailable. It
shows the last successful check time and cached term count. Cached values may be
stale; this is a read-only snapshot, not automatic glossary editing/sync. Network,
timeout and invalid-response failures retain a valid matching cache and leave
ordinary checks available. Explicit 401/403/404 responses exclude cached terms;
the authenticated transport's rejected-session event cancels the old account's
review. Account changes clear findings/previews/highlights and require a new check.
Late account, document mapping, active page or editor-text changes cannot adopt
results. No refresh happens during typing. Offline casing fixes retain explicit
review/Apply and existing durable scoped History Undo/Redo. See
`docs/desktopai-uat.md` for evidence and independent native/deployed gates.

## AI parity prompt 3 — Shared scene approval (2026-10-05)

`SceneCoachingReview` now offers approve-all and clear-selection controls alongside
its existing typed field checkboxes. Desktop continues to save the explicitly
approved `SceneCoachingField` subset through `LocalSceneCoaching`; both client
editor and storyboard inspector use the same review. Client partial saves carry
`ApprovedFields` plus the checked source, saved-card fingerprint and durable
history operation. The server assigns only selected properties, preserving raw
unselected strings/JSON and lifecycle metadata. A legacy section inheriting a
scene card materializes that exact baseline before its first partial save.
Legacy narrative purpose maps to the client's editable role or intent rather
than becoming a second independent alias checkbox. No editor bundle/schema,
device sync format or database migration changed. Evidence and independent
native/provider gates are recorded in `docs/desktopai-uat.md`.

## Desktop AI prompt 11 — Cover studio and offline publishing (2026-10-03)

Open **Cover studio** from a project document or its Publishing page (`/documents/{id}/cover`). `CoverBrief` and `CoverConcepts` in `WriterApp.UI.Shared` supply the same genre, mood, visual style, palette, preview and explicit selection to both clients. The client's variation/adjustment buttons were UI-only stubs; they are removed from the reachable workflow. “Choose another brief” changes choices without claiming to generate an image. Generation is deliberate and uses the authenticated `api/covers/generate` endpoint; provider credentials and image entitlement/quota policy remain server-owned.

The additive `CoverPrompt.ContractVersion=1` request carries owned cloud project/document IDs, acknowledged project metadata revision and document sync version. The server checks active ownership, association and both revisions before and after generation, then echoes the source contract. A desktop rejects legacy, wrong-target or mismatched responses. Generation requires synchronized saved writing and clean shared project metadata. Status/plan displays are advisory; the server's existing `CoverImageService`/`IAiUsagePolicy` remains authoritative. HTTP requests capture the account generation before token acquisition, disable credential forwarding/redirects through the existing handler, use bounded streaming reads and time out after three minutes. Concept JSON is bounded to 16 MiB, 1–4 concepts, each a validated inline PNG of at most 2 MiB. Remote URLs and other MIME types are rejected with actionable guidance, rather than fetched with authentication. The configured GPT image flow already requests PNG base64. Remote-only provider output requires a separately implemented trusted materialization path; no direct external image fetch is introduced here.

`LocalCoverStudioStore` saves version-1 concept assets and selected index under `<local-document-root>/cover-studio/<backend-account-hash>/<document-id>.json`. IDs, project/document association and source versions are retained. Atomic replacement preserves the last valid cache on failed/cancelled reads or writes. Account changes cancel in-flight work and clear private concepts immediately; same-account token renewal does not cancel. Cached concepts are available offline to their original account; stale concepts are viewable with an explanation but cannot replace a saved cover. Moving a document hides previews for its old project and preserves that cache. Corrupt/future caches are preserved and reported; users can generate a replacement.

Explicit Save calls `SetProjectCoverAsync`, which checks the current document and shared project metadata revisions under the document store's cross-process lock. One authoritative project metadata replacement contains the validated new PNG data URI, prior cover (including “no cover”), stable change ID, incremented metadata revision, and dirty flag. Project metadata version 1 remains readable; cover changes write version 2. Restore exchanges current and prior covers, with the same guards. This is separate from manuscript AI undo/redo; no writing is flattened or changed. Hydration exposes dirty metadata as pending upload even after a crash before any document snapshot is written. Replaying a completed concept save is recognized by its stable change ID and exact selected asset. Recovery is local and survives acknowledgement/download of project metadata; it is deliberately excluded from cloud sync. Deleted/conflicted documents, stale sources, invalid assets, interrupted saves and account changes cannot promote a new cover before the atomic commit. Cancellation after a committed save requires reopening to confirm its durable result; the prior cover remains recoverable.

The saved `CoverImageUrl` travels through existing project-aware v4 sync with its existing ownership, immutable operation receipts and metadata conflict checks. To transport inline assets, the HTTP envelope now permits up to 5 MiB, while writing/planning with its inline cover removed remains limited to 2 MiB; the additional cover is PNG-validated and limited to 2 MiB decoded. Oversize envelopes fail without changing server data. Older backends retain queued local changes until upgraded. Web cover saves now send `ExpectedMetadataRevision` from `ProjectDto`; the owned server route uses SQL compare-and-swap and increments the revision so concurrent cover saves conflict instead of overwriting. No new database schema or migration is needed for these existing revision columns.

Publishing always embeds a valid saved project PNG when Include cover is selected. It adapts its association to the document being exported, so sibling documents use the same project asset. Standalone and existing document-local PNG covers remain supported. Choosing a local PNG in a project also explicitly changes its project cover and retains prior-project-cover recovery; writing remains untouched. HTML, DOCX, EPUB and Windows PDF consume inline bytes without networking; plain text and Markdown still reject Include cover. Existing remote covers are retained and disclosed rather than downloaded; choose/save a PNG for offline use. The native file dialog/PDF exporter and physical export acceptance are unchanged.

Evidence and the prompt-12 handoff are in `docs/desktopai-uat.md`. Native WebView interaction, signed-in deployed sync and live image-provider acceptance remain distinct from automated/component/browser fixtures.

## Desktop AI prompt 10 — Cloud AI history and durable outcome reporting (2026-10-03)

**History** combines durable local records with explicitly loaded owned cloud comparisons. `DeviceAiHistoryService` reads the additive `GET api/ai/actions/history/device?documentId=...` route, caches version-1 snapshots under `<local-document-root>/ai-library/cloud-history/<backend-account-scope>/<local-document-id>.json`, and labels origin, cloud check time and source freshness. Source freshness refers to the last acknowledged cloud revision; pending local changes are disclosed. Matching requires proposal, account/backend scope, document, section, page, action and source revision; matching cloud rows enrich local rows rather than create duplicates. Old records without a retained proposal identity remain local and are not guessed into a cloud mapping. Lists are bounded to 200 entries; excess cloud entries remain retained and are disclosed as available through web history. The device endpoint selects a stable bounded set by proposal ID and sorts that set by creation time; it does not claim to return the latest 200 from a larger history.

Cloud comparisons are inert data. A cloud-only entry has no local Undo/Redo or Recover a copy action because its exact local target snapshots and identity mapping are absent. Local changes still use the existing snapshot/content/format/structure guards and separate-copy recovery. Linked entries require the original account/backend for undo or recovery, including when offline identity is restored. Authored writing remains available locally. An account change clears linked rows and cloud comparisons immediately before asynchronous document refresh. Same-account token refresh preserves ongoing reviews and reporting.

`DeviceAiProposal.HistoryOrigin` captures the stable proposal identity, original cloud document/section/page, generated action and acknowledged source version with the backend/account hash. Optional `LocalAiHistory.CloudOrigin` and `Deliveries` are additive to existing history versions 1–5; reading old files does not rewrite them. Origins and original source evidence cannot be replaced. Writing/preset/translation, provider style fixes, consistency revisions, scene fields, synopsis and storyboard analyses preserve that origin. Deterministic local style fixes remain local; no provider proposal is invented. The report retains separate local source/target document IDs, section/page/node identities and source revision. Creating a suggested scene records an approved local intent before creation and a completed outcome after saving; creation is inspectable/recoverable as a copy, without inventing a reversible cloud operation. An interrupted creation must be inspected before another creation, rather than blindly replayed.

Saving a terminal history state atomically includes its reporting intent in the same recovery file. `Applying`, `Undoing` and `Redoing` are not completed outcomes. Reviewed/Applied/Undone events have stable IDs, an ordered predecessor chain and hashes of the retained original/approved snapshots; repeated completion does not create another event. The scoped reporter attempts delivery after durable local completion without awaiting HTTP in the save path. Failed delivery leaves that intent intact and never rolls back a completed save. **Report saved events** retries retained intents after restart or interruption, at most 200 per invocation; repeat until the pending count is zero. It replays the exact event and never calls the legacy non-idempotent applied route. Receipt files under `ai-library/history-receipts/<scope>/<local-document-id>/<operation-id>.json` confirm the full request hash and sequence independently of local durability. No credentials are stored.

`POST api/ai/actions/history/device/events` accepts a version-1 `DeviceAiHistoryReport`. It checks owned document/proposal and exact generated source/target metadata, gates history entitlement, and serializes predecessor validation and immutable receipt insertion in a transaction. Owner/operation IDs and owner/local-entry/sequence are unique. Reusing an event with different content, a changed source or out-of-order transition is rejected. Original receipts replay after later events. SQLite and SQL Server migrations add only `DeviceAiHistoryEvents`; apply `20261003145238_DeviceAiHistoryReporting` or `20261003145251_DeviceAiHistoryReportingSqlServer` alongside the new routes. Preserve ledger rows while clients can retry. Local outcome records remain separate from the legacy applied-event snapshot table so cloud Undo cannot interpret device or structured snapshots as page HTML. The legacy web history listing includes confirmed device applied/undone outcomes, but web applied delivery itself remains its existing one-shot route and a later integrated acceptance gate.

Bounds: history comparisons have at most 200 unique proposals and 100,000 characters per original/proposed value; streaming responses/cache snapshots are bounded to 16 MiB, reporting responses to 64 KiB and reporting requests to 16 KiB. Server inspection streams a bounded projection without private request context, caps result materialization at 12 MiB, and refuses more than 2,000 outcome/legacy events in the selected set. Existing 32 MiB local recovery-file limits remain. Reporting chains are capped at 10,000 transitions; no retention cleanup removes local recovery or pending receipts. Invalid data and old/missing routes retain the preceding cache and pending intents with actionable errors. Scope changes cannot acknowledge or display another account's results. Deployment, live provider, live SQL Server concurrency and native Windows/iOS acceptance remain separate from local fixtures and builds.

Legacy history DTOs now add nullable `CanCloudUndo`/`CanCloudRedo` capabilities. The EF store computes these from actual legacy before/after snapshots, independently of confirmed device outcomes. The web client uses explicit capabilities when present and retains its older-backend fallback and own newly applied session behavior. A device-only Applied/Undone report cannot enable a web cloud Undo/Redo button. This does not repair the legacy routes' separate current-content/concurrency acceptance gaps.

See the dated evidence and prompt-11 handoff in `docs/desktopai-uat.md`. Preserve prompt-9 preset queues/receipts separately from AI history outcome identities.

## Desktop AI prompt 9 — Reusable presets and versioned transfers (2026-10-03)

**Advanced → Prompt library** now uses `LocalPromptPanel`, shared `PromptPresetEditor`, `PromptLibraryList` and `PromptTransferReview`. Local create/edit/delete, categories, selection/section targets, custom variables, typed builtin parameters and durable pins work offline. Quick access contains at most three presets. If account membership introduces additional previously saved pins, the UI discloses this and retains their metadata for explicit unpinning. Recommended craft templates come from the shared genre resolver. Supported builtin revision presets are Rewrite (selection), Expand, Shorten, Change tone and Show, don't tell (selection/section). Other action IDs, ignored parameters, invalid types, reserved execution variables and lenient/missing custom variables are retained on import but explicitly unavailable for execution. Reset parameters changes the draft only. This does not claim every registered AI action is a supported writing preset.

Version-2 preset files remain under the existing `<local-document-root>/ai-library/prompts/<id>.json` directory. Version-1 plain prompts are read without rewriting their bytes or identity; explicit edits/pins/deletes upgrade only that record. Records retain created time, revision, category, kind, action/template, parameters, scope, pins and optional project/cloud origin. Expected local revisions, atomic writes and a shared root file lock prevent competing edits/queues from overwriting each other. Deletion retains a tombstone. Authored local presets remain available offline; imported presets are isolated by the backend/account hash. An imported cloud revision has a stable local identity, never overwrites a later local edit, and cannot be revived by a retry after local deletion. A full pin slot causes an actionable import refusal instead of discarding a cloud pin. Inputs are bounded to 100-character name/category/action, 2,000-character templates/variable values, 100 parameters/20,000 serialized parameter characters and 64,000 serialized definition characters. Parameter JSON is retained losslessly, including unsupported values.

`PromptDefinition` and `ReusablePrompts` pin the declared scope and normalized execution parameters. Custom tokens share the backend's `{name}`, `{{name}}` and `${name}` rules through `PromptTokens`; `{context}` is supplied from the exact writing target. `LocalWritingPanel.RunPresetAsync` reuses save/sync, live selection capture or complete page/run mapping, inert comparisons, explicit Apply/Dismiss and the existing guarded writing application. Custom section requests use version-1 `writing_structure` and the additive `StructuredPresets` capability. The backend checks owned project/document/section/pages, exact selection/context/parameters and document versions before and after generation. Custom mapped output has no flat replacement operation. Version-3 writing history adds optional immutable preset metadata while preserving existing history versions 1–5; durable Applying intent, restart completion and scoped Undo/Redo remain authoritative.

Cloud copy/update/import/delete are **explicit transfers**, not background synchronization. `GET api/ai/presets/transfer-library` returns owned complete definitions with revision tokens. `POST api/ai/presets/transfer` takes a version-1 `PromptTransferRequest` with stable operation/preset IDs, action, expected token and complete definition. A serializable transaction commits both the mutation and an owner/operation-scoped immutable receipt. Replaying the same operation returns its original receipt, even after later deletion; reusing it with different content is rejected. Updates/deletes require the current token, including changes made through legacy CRUD. Conflict responses contain the current owned version or a deleted state. Legacy client CRUD remains compatible and does not gain automatic synchronization or concurrency guards of its own.

Queue intents and cloud caches persist under `<local-document-root>/ai-library/prompt-transfers/<scope>/` and `<local-document-root>/ai-library/prompt-cache/<scope>.json`. Queue creation deduplicates across store instances; pending authored payloads cannot be replaced by subsequent edits. Lost responses remain Pending and retry the identical operation. A confirmed copied preset links future explicit updates to its cloud ID/token. Conflicts require keeping both through a separate stable cloud fork, keeping/importing the cloud version as a separate local record, or canceling while retaining originals. Conflict resolution itself survives interruption. Account/backend changes clear displayed cloud/imported content immediately and cannot store results into a different scope. Local deletion does not silently delete cloud copies; queued sends refuse a deleted local source. Transfer responses are bounded to 128 KiB; libraries to 16 MiB/500 presets. Unsupported/missing backend routes never fall back to a non-idempotent create.

**Backend prerequisite:** deploy matching routes and apply `20261003125036_ReusablePresetTransfers` for SQLite or `20261003125046_ReusablePresetTransfersSqlServer` for SQL Server before using versioned transfers. These additive migrations add nullable scope, default-false pin metadata and the durable `PromptPresetTransfers` receipt table. Preserve receipts for as long as clients may retry; pruning them independently would invalidate replay guarantees. No database deployment or live provider/native acceptance was performed. Legacy `DevicePromptLibrary` plain-copy methods remain compatibility adapters; the production prompt-library route uses only the new scoped queue. Prompt 10 owns cloud/local AI history reporting and reconciliation.

Transfers request response headers before reading bounded streams. Malformed/null/oversized stored cloud parameters are rejected without replacing a valid device cache. `DeviceAuthenticatedHandler` captures account generation before acquiring a token; transfers attach their original generation, and both explicit and ordinary requests refuse a changed principal before sending private content. Same-account token refresh notifications preserve active library operations and writing reviews. A newly acknowledged project ID updates preset availability without reopening the library.

## Desktop AI prompt 2 — Durable story canon (2026-10-03)

Open **Writing → Consistency** to view character, place and timeline bibles. The canon cards support **Load saved canon**, **Build/Update canon**, **Rebuild canon from manuscript**, and **Update all bibles**. Generation is explicit and uses the authenticated backend's existing extraction, incremental refresh, entitlement and quota policies. The parent workspace saves writing/planning and locks editing during the request, synchronizes, then pins the acknowledged cloud revision. Canon refresh stores validated server canon; it does not replace manuscript text or apply fields to authored scene cards. The client has no manual bible edit/save workflow; desktop matches that read/refresh scope.

Version-1 cache files live under `documents/canon-cache/<scope>/<local-document-id>/<kind>.json`. Scope hashes the configured backend URI and MSAL home-account identifier, independently of display name and access tokens. Files bind local/cloud document identities and contain snapshot/source/checked versions, source hash, refresh/check timestamps, validated JSON and Ready/Refreshing/Interrupted state. Writes use the existing atomic writer. Unknown storage versions, oversized or malformed data and mismatched identities are rejected without replacing the file. The Windows identity adapter now supplies the stable home-account identifier; hosts without it disable this cache capability instead of falling back to a display name. Signed-in users can read cached canon offline; after restart, native identity restoration is required to select the account's cache. Expired identity that cannot be restored offline does not expose cached account data.

The additive routes are `GET api/documents/{id}/bibles/{kind}/device?expectedDocumentVersion=...` and `POST .../{kind}/device/refresh`. GET carries the expected manuscript version; POST additionally carries an expected snapshot fingerprint, full-rebuild flag and optional owned cloud section ID. Contract version 1 returns `DeviceBibleSnapshot` in `WriterApp.Shared.Canon`. A short serializable transaction rechecks the owned, active document's `DocumentSyncRecords` version and the snapshot fingerprint before committing. Provider execution happens before that transaction, through the existing bible service. SQL Server's configured execution strategy wraps the transaction. Legacy client refresh routes keep their request/response DTOs and now recheck manuscript source/canon before committing, protecting device updates from competing web refreshes. Deleted scenes force re-extraction from the remaining manuscript because they have no text delta. No database schema change is required.

`SourceDocumentVersion` is confirmed only when the stored extraction source hash equals the checked manuscript's section/plain-text hashes. Stale historical snapshots have a null source version and remain readable. The cards distinguish a match to the last synchronized local manuscript from a fresh cloud check; later cloud changes require another load. Evidence IDs and timeline entity references must have valid shapes; unresolved character/place references are reported without binding them into planning. Input is bounded to two million manuscript content characters, canon JSON to one million characters/depth 32/5,000 entries, and response/cache files to eight million bytes. Provider markup is rendered as encoded data.

Every attempt reads remote state before refreshing. Failure, disconnect, cancellation, account switch or a changed local source retains the previous cache with Interrupted state. A process exit during Refreshing also retains previous entries; retry loads remote state rather than blindly replaying a create/update. A timeout/cancel may occur after the server committed, so loading reconciles that result. Update all runs sequentially and stops on failure; earlier successful bibles stay saved and the error explains this partial completion. It does not claim an atomic three-bible update.

**Backend prerequisite:** publish these additive revision-checked routes alongside the existing sync v4/document version infrastructure before enabling this desktop workflow. An older backend produces actionable unavailability guidance. No deployment, external authentication registration or live provider call was performed.

`DeviceBibleService.ContextAsync` exposes version-matched Ready snapshots as `DeviceCanonContext` for prompt 3. Interrupted, stale or locally unsynchronized snapshots are excluded. Prompt 2 deliberately retains the section-only consistency request/explanation and existing suggestion review/Apply/history behavior. See [prompt 2 evidence and handoff](desktopai-uat.md#prompt-2--durable-character-place-and-timeline-bibles-2026-10-03).


Prosa's Windows and iOS applications use .NET MAUI Blazor Hybrid. Each application is a native host around a shared Razor UI, so ordinary editing and local file persistence can run on the device while authentication, paid-account synchronization, and AI operations use the existing backend.

## Projects

- `WriterApp.UI.Shared` is the plain `net10.0` presentation library used by both `WriterApp.Client` and `WriterApp.Device.Shared`. It has no reference to either host, storage, HTTP services, MAUI, or Windows APIs.
- `WriterApp.Device.Shared` contains reusable Razor UI, the configured backend `HttpClient`, and the device-local document store. It targets plain `net10.0`, so the main Linux CI workflow can compile and test this layer.
- `WriterApp.Desktop` is the Windows MAUI host. Its files stay in the app data directory and its unpackaged development build targets Windows 10 version 1809 or later.
- `WriterApp.iOS` is the iPhone and iPad MAUI host. It targets iOS 15 or later.
- `WriterApp.Shared` remains the home for contracts shared with the existing web client and server.

`BlazorApp.sln` includes the cross-platform shared device project. `WriterApp.Device.sln` groups the shared library and both platform hosts for device development. The platform hosts are kept out of the server solution because GitHub's Linux runner cannot build the Windows and iOS workloads.

### Shared web and device presentation

Both solutions include `WriterApp.UI.Shared`. `AppShell`, `LibraryHeader`, `DocumentCard`, `EditorWorkspace`, `EditorHeading`, `EditorToolbar`, `EditorPanelControls`, `EditorStatus`, `NavigatorRow`, `RightPanelShell`, `AiTextComparison`, and `Icon` are consumed by the existing web pages and device pages. Client shell, library, editor, navigator styles and design tokens live in that library. Host pages compose slots and callbacks; they do not copy these components. Web-only toolbar groups remain in the web host's additional-controls slot.

The shared `ui.css`, logo, Bootstrap stylesheet and Material Symbols font are bundled locally. Web assets use the root `/_content/WriterApp.UI.Shared/` path because the client is mounted under `/app`; MAUI uses relative `_content/WriterApp.UI.Shared/`. The old client design-token URL forwards to the shared stylesheet. The icon font is distributed with its Apache license; Bootstrap retains its MIT notice. Keep future asset changes in the shared library.

Web routes retain API saves, authorization, project/scene orchestration and feature gating. Device routes retain `LocalDocumentRepository`, autosave/recovery, `DeviceSaveLifetime`, sync, native authentication, AI and file-transfer services. `DeviceWorkspaceView` coordinates presentation-only focus/panel state with the device shell; leaving an editor resets it. Settings contains maintenance and synchronization details. A local document/page ID is used for editing; a server ID is consulted only by existing sync/AI services. Status distinguishes local durability from cloud synchronization.

Both editor entry points use the existing shared TypeScript commands. They remain separate because the desktop's deliberately limited content schema and preservation checks must not be replaced by the richer web schema. The desktop entry point reports formatting state to the shared Razor toolbar and accepts explicit commands without replacing content or toggling editability during autosave. Both Vite builds deduplicate ProseMirror packages to avoid duplicate keyed plugins. Run `npm run build` in `WriterApp.Client` after editor-source changes and check in both generated bundles.

AI parity prompt 1 (2026-10-05) extracts `src/targeted-revision.ts` as a schema-owned
minimal-change transaction shared by the device targeted preview and client
quality/consistency Apply. It retains untouched marks and rejects incompatible
changed marks, embedded content and unsafe source/Unicode boundaries before
mutation. Host source/revision, approval, persistence and recovery stay in their
existing layers. The maintained real-editor driver is
`WriterApp.Client/tests/verify-targeted-revisions.mjs`; set `PLAYWRIGHT_MODULE` to
the existing Playwright module path and run it from the repository root after
building bundles. It serves only test assets on an owned ephemeral loopback port,
closes its browser/listener in `finally`, and writes evidence beneath
`artifacts/ai-parity-p01/` (overridable by `EDITOR_EVIDENCE_DIR`).

Desktop now includes local projects/scenes, section/page management, search/preview, storyboard/planning, notes/tasks/quote annotations, synopsis, translation/analysis adapters, local AI history and Account & Help. It still has a smaller rich-text schema, fewer specialized web panels and publishing controls, and no full cloud-version history. See the [current parity acceptance report](desktop-client-parity-release-checklist.md) and [gap matrix](desktop-client-gap-analysis.md) for implementation versus live-release status. Prompt 21 supplies iOS native MSAL identity and authentication lifecycle hooks; registration/signing/Mac/device acceptance and other native adapters remain separate gates. See [shared UI verification](shared-ui-uat.md) for evidence and remaining acceptance gates.

## Build and run

### Required Visual Studio workload

The .NET 10 device hosts require Visual Studio 2026 with the **.NET Multi-platform App UI development** workload. The repository's `WriterApp.vsconfig` records the installer component ID `Microsoft.VisualStudio.Workload.NetCrossPlat`.

From an elevated PowerShell session, add the required workload to Visual Studio 2026 Professional with:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" modify `
  --installPath "C:\Program Files\Microsoft Visual Studio\18\Professional" `
  --config "$PWD\WriterApp.vsconfig"
```

The toolchain was verified on 2026-09-27 with .NET SDK 10.0.400 and latest-patch roll-forward, .NET runtime 10.0.11, Visual Studio 2026 18.9.2, MAUI 10.0.20, and the iOS workload manifest 26.5.10301. Visual Studio 2026 supplies the `android`, `ios`, `maccatalyst`, and `maui-windows` workloads on this machine. `Microsoft.Maui.Sdk/10.0.20` is installed under the system .NET packs directory.

The verified Windows flow restores and builds the desktop host with zero warnings, includes the shared Razor static assets beneath `_content/WriterApp.Device.Shared`, and launches a responsive `WriterApp.Desktop` process. The main solution also builds in Release with zero warnings; the current server/shared suite has 756 passing tests. See the [parity Prompt 12 acceptance report](desktop-client-parity-release-checklist.md) for the current release decision and the limits of native validation; older prompt results below are historical.

Restore and verify the cross-platform layer on any supported development system:

```powershell
dotnet restore BlazorApp.sln
dotnet build BlazorApp.sln --configuration Release --no-restore
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-build
```

Build and run the Windows host on Windows:

```powershell
dotnet restore WriterApp.Desktop/WriterApp.Desktop.csproj
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Debug --no-restore
dotnet run --project WriterApp.Desktop/WriterApp.Desktop.csproj --framework net10.0-windows10.0.19041.0
```

Restore the iOS project on Windows or macOS. With the iOS workload installed, Windows can restore the project and compile its `iossimulator-x64` managed assembly with zero warnings. Running the iOS Simulator, building for a physical iPhone or iPad, producing an app bundle/archive, signing, and publishing cross into Apple's toolchain and require a paired Mac with a compatible Xcode installation. Device signing and distribution additionally require an Apple developer certificate and provisioning profile.

```powershell
dotnet restore WriterApp.iOS/WriterApp.iOS.csproj
dotnet build WriterApp.iOS/WriterApp.iOS.csproj --configuration Debug
```

## Backend and local data

The Windows Debug host uses `Development` and `http://localhost:5387/` by default; `WRITERAPP_API_BASE_URL` is a Debug-only override. Windows Release builds use `Production` and `https://app.prosa-app.com/` unless explicitly built as `Staging` or with another approved backend URL. iOS now uses explicit build-selected `ProsaEnvironment`/`ProsaApiBaseUrl` with the same defaults; Staging requires its backend URL. An iOS device's localhost is that device, so configure an authorized reachable HTTPS Development backend for physical testing. Keep tokens and secrets out of repository configuration. See [Windows desktop beta packaging](windows-desktop-release.md) for environment selection, signing, updates, data backup, and diagnostics.

### Local document schema and repository (Release 1, Prompt 2)

The local store writes one UTF-8 JSON file per document beneath `Path.Combine(FileSystem.AppDataDirectory, "documents")` on both hosts. The filename is the local document ID in `N` format, for example `0123456789abcdef0123456789abcdef.json`. MAUI resolves the parent directory for the installed host; code must use this API rather than a hard-coded Windows username or iOS container path.

The current envelope is **schema version 4**: `{ "schemaVersion": 4, "document": { ... } }`. `LocalDocumentCodec` is the version dispatch and validation boundary; it also reads recognized versions 1–3 and marks them as migrated before a subsequent save writes the current envelope. Future schema changes must add explicit migration handling there. Unsupported versions, invalid identities, malformed payloads, and invalid ordering are preserved and surfaced as read issues; extension data is retained at supported boundaries, while unknown fields outside those boundaries are rejected. The app never silently rewrites a future schema into an older version.

`LocalDocument` contains a stable local ID, optional server document/project IDs, title, kind, language, creation/modification timestamps, `DeletedAtUtc`, a monotonically increasing `LocalRevision`, `SyncState`, an opaque `ServerVersion`, and `LastSyncedAtUtc`. Ordered sections include narrative purpose and their own local/server IDs and timestamps. Each section contains ordered pages with local/server IDs, timestamps, and explicit `ContentFormat` and `Content` fields. This mirrors the existing `SectionDto` / `PageDto` contracts instead of losing page boundaries. Collections are read in ascending `OrderIndex`; duplicate order indexes or IDs within the aggregate are rejected.

New page content is **HTML**, matching `tiptap-editor.ts`'s `editor.getHTML()` callback and the server's `PageDto.Content`. Serialization as a JSON file does not make the writing itself JSON. Storage treats content as data and does not render or sanitize it; the editor/import boundary must validate or sanitize content before rendering in Prompts 4 and 10.

UI code uses the registered `LocalDocumentRepository` for create, load, list, save, rename, duplicate, move to trash, restore, and permanent delete. It has no file paths or serialization responsibilities. For example:

```csharp
LocalDocument document = await repository.CreateAsync("My manuscript");
document = await repository.RenameAsync(document, "My revised title");
LocalDocumentList library = await repository.ListAsync();
document = await repository.MoveToTrashAsync(document);
document = await repository.RestoreAsync(document);
```

Keep the returned snapshot after every mutation: saves and lifecycle changes check its local revision and reject stale writes with `LocalDocumentConflictException`. `SaveAsync` edits existing documents only and cannot silently recreate deleted files. Listing defaults to active documents; `LocalDocumentScope.Trash` and `All` are explicit alternatives. Trashed documents cannot be edited or renamed until restored. Duplication creates an independent active document with new local IDs throughout and clears all server identity, version, and sync fields.

### Durability, migration, and recovery

Writes are serialized, staged in a unique same-directory `*.tmp` file, flushed to disk, and committed with filesystem replacement. Cancellation or failure before replacement leaves the previous file intact. Staging files left by a terminated process are never treated as saved documents. A `.store.lock` file lease also prevents two app instances from performing competing operations; contention raises a retryable I/O error. The lease is released by the OS when the process ends; the empty lock file may remain. Durability ultimately depends on the device filesystem and storage hardware. These transaction safeguards do not replace the editor autosave/recovery journal planned in Prompt 5.

Unversioned scaffold drafts (`documentId`, `title`, `contentJson`, `updatedAtUtc`, `serverVersion`) migrate on first load/list. Before replacing the source, the store saves its exact original bytes to `<id>.json.legacy.bak`. It retains the document ID, title, timestamp, version token, and exact content; it creates one section with one page. A completed migration preserves the generated section/page IDs on subsequent reads. A failed migration can retry from the original file and backup. An existing backup that differs from the source causes a recoverable error rather than overwriting either copy.

Legacy HTML becomes `ContentFormat.Html`; valid JSON and other text remain `LegacyJson` and `LegacyText`. They are preserved without pretending they are HTML. The device editor converts supported TipTap JSON and plain text to its document representation, then persists HTML on the first actual edit. Opening a legacy page alone does not rewrite it. Unsupported JSON remains read-only. The old scaffold did not distinguish local/server identity, so migration preserves its ID locally and does not invent a server association.

`LocalDocumentList.Issues` identifies unreadable files while healthy documents continue loading. Direct loads throw `LocalDocumentReadException` with the same structured issue. The document library displays these issues. To recover a file, close Prosa, back up the entire documents directory (including `.legacy.bak` files), then inspect/repair a copy or use an app version supporting its schema. Preserve an existing versioned file before restoring its legacy backup because that backup predates later edits. There is no automatic corrupt-file deletion or backup rollback that could hide newer writing.

Permanent deletion requires a current revision and a trashed document. It removes the document, its migration backup, and associated staging files. Cloud-linked documents retain their trash record until the sync protocol can acknowledge deletion; permanent removal is deliberately unavailable for them at this stage. Local changes mark linked documents `PendingUpload`, preserve unresolved `Conflict` state, and retain the last server version. These are stored prerequisites; no synchronization or network calls are implemented by the local repository.

Prompt 2 verification: main solution Release build, Windows host Debug build, and iOS managed Debug build passed with zero warnings; **409 tests passed**, including 27 local-storage cases covering lifecycle/restart, ordered multi-section HTML, independent copies, stale writes, filesystem locking, interrupted replacement/cancellation, corrupt/unavailable files, legacy migration/retry, and retained sync metadata.

### Document library and navigation (Release 1, Prompt 3)

The shared start route `/` now shows the local document library. Select **Create new document** to open a new Untitled draft at `/documents/{localDocumentId}`; rename it from the library. Recent documents are sorted by local modification time. Open, rename, duplicate, and move to trash are available without sign-in or a network connection. Trash has restore and permanent delete actions. Moving to trash requires a second confirmation; permanent delete additionally requires typing `DELETE`. Cloud-linked trash records cannot be permanently removed until the server sync protocol supports deletion acknowledgments, as described above. The library reloads disk state after each operation and after app restart. Unreadable document files appear as notices alongside healthy documents.

The `/documents/{localDocumentId}` route reads by device ID. Prompt 3 established the workspace and source inspection; Prompt 4 now supplies the editable TipTap surface described below. Trashed documents remain read-only.

Status labels distinguish local save state (`Unsaved`, `Saving`, `Saved`, `Save failed`) and synchronization state (`Waiting to sync`, `Syncing`, `Sync error`, `Conflict`, `Synced`). For new local documents the library shows `Offline · Saved locally`. Save and sync transitions beyond the stored states will be supplied by the editor autosave and sync engine in Prompts 5 and 8; the current library never calls the backend or claims that pending work is synced. Keyboard focus moves to the rename input or confirmation control when those panels open and returns to the page heading after completion. The layout wraps document actions and form controls for narrow windows.

Prompt 3 verification: main solution Release build, Windows host Debug build, and iOS managed Debug build pass with zero warnings; **413 tests pass**. The four library tests exercise offline lifecycle across restart, stale action handling, healthy documents alongside corrupt files, and status precedence. The Windows output contains the shared library and workspace styles, and the Windows executable initially opened a `Prosa` window. Native UI automation did not expose that window for visual or keyboard walkthrough, so those interactions still need a manual check on an interactive desktop. Next dependency: **Prompt 4, port the TipTap editor and bundle its assets inside the MAUI WebView.**

### Offline TipTap editor (Release 1, Prompt 4)

Open a local document, select a page grouped under its section, and write directly in the workspace. The toolbar supports paragraph style, headings 1–3, bold, italic, bulleted/numbered lists, block quotes, links, undo, and redo. Standard TipTap shortcuts work, including Ctrl+B, Ctrl+I, Ctrl+Z and Ctrl+Y. Ctrl+K opens the link controls; Ctrl+S and **Save on this device** commit to the local repository. Link controls accept HTTP, HTTPS, and mailto addresses.

Switching pages or navigating inside the app saves first. The editor is briefly locked for navigation, close, or content replacement, and a failed save blocks the transition while keeping text in memory. Ordinary autosave and explicit Save keep the editor editable and preserve typing focus. Save status changes from Unsaved through Saving to Saved, or to Save failed. The local session preserves newer edits if they arrive during a pending save, including reverting text to its previous value. A stale document revision never overwrites newer disk data; the user can copy their unsaved text before reloading to resolve it. Each page has its own editor instance and undo history; ordinary Razor rerenders do not reset selection or content. Programmatic content updates suppress change callbacks. Undo history resets when switching pages.

**Save boundary:** Prompt 5 now adds autosave, disk recovery records, and Windows lifecycle flushing as described below. Explicit Save and save-before-navigation remain available.

The device entry point `WriterApp.Client/src/device-editor.ts` reuses the web client's `tiptap-commands.ts` and the same pinned TipTap 3.31.3 packages. It omits web authentication, backend persistence, pagination, and browser localStorage. All required code and CSS are bundled into `WriterApp.Device.Shared/wwwroot/editor`, and both MAUI hosts package them as RCL assets. No CDN, development server, or backend connection is needed in the installed app. These two generated distribution assets are intentionally tracked, like the existing web editor bundle; normal `bin`/`obj` output remains ignored.

From `WriterApp.Client`, run `npm ci` then `npm run build` to rebuild both editor bundles. `npm run dev:device` watches the device entry point. Both builds stage their output under the ignored `dist` directory before publishing changed assets to their existing `wwwroot` paths. Unchanged assets retain their timestamps. On Windows, memory-mapped old assets are renamed into `dist/*-retired` before replacement; subsequent builds remove them once the host releases them. This avoids Windows error 1224 when a running host maps an editor bundle. An exclusive lock that also prevents renaming still requires closing the host and rebuilding. Run `node --test tests/atomic-editor-output.test.mjs` to check publication, including a real Windows memory-mapping regression. The separate device Vite configuration deduplicates the ProseMirror runtime packages because the existing dependency tree contains nested copies that otherwise create conflicting plugin registries.

The editor checks stored HTML/TipTap JSON before loading. Unsupported elements or attributes (including tables, images, embedded scripts, and advanced styling) keep that page read-only with an escaped source preview; they are not silently dropped from saved content. Unsupported pasted HTML falls back to plain text with a notice. Legacy plain text is converted to text nodes, never interpreted as HTML. File drop is disabled. Advanced content support and import/export sanitization remain later work.

Validation for this step: **418 .NET tests pass**, including local multi-section/page save and reopen, disk failure/retry, concurrent edits/reversion during save, and stale revision preservation. The main Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. The device TypeScript passes type checking. `npm audit` reports **zero vulnerabilities** with no dependency/lockfile changes. Nine browser checks run against the actual shipped bundle and pass: initialization without feedback, formatting callbacks, safe links, undo/redo/save shortcut, HTML reopen/cleanup, Windows shortcuts/selection/save locking, plain-text paste fallback, legacy conversion, and unsupported content rejection.

To repeat the browser checks, run `npm run test:device` from `WriterApp.Client`, then open `http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html`. It serves only the test page and the two built editor assets, using in-memory test content. Stop with Ctrl+C. These browser checks and filesystem tests verify the editor and persistence separately; a complete create/edit/save/reopen walkthrough inside the native Windows WebView remains a manual check because the available UI automation cannot inspect that native window. Next implementation dependency: **Prompt 5, autosave, recovery, and offline resilience.**

### Autosave and recovery (Release 1, Prompt 5)

The editor saves locally after **two seconds without an edit**, with a **15-second maximum interval** during continuous typing. Each received edit writes an atomic recovery snapshot independently of the debounced primary save. Save, page/section changes, internal navigation, and Windows window deactivation flush the latest WebView snapshot. The Windows close handler cancels the initial close, waits for the save, and closes only on success; failed saves leave the window and writing open for retry. This covers normal window closing, including Alt+F4, rather than forced process termination or an OS shutdown deadline.

`LocalAutosaveCoordinator` serializes recovery and main writes for a session; `LocalEditorSession` retains any edits arriving during a save. The existing document store continues to serialize main-file access and reject stale revisions across app instances. Status shows Unsaved, Saving, Saved, or Save failed. Recovery-write failure is visible and retryable, but does not prevent a working primary destination from saving. Main-save failure retains the journal and in-memory writing; Ctrl+S or **Save on this device** retries. A redundant journal cleanup failure does not misreport a durable primary save as failed. No account, network, server, or AI operation participates in this path.

Recovery records live in `Path.Combine(FileSystem.AppDataDirectory, "documents", "recovery")`. Each editing session has a unique recovery ID and a version-1 envelope containing that ID, timestamp, and the full document snapshot with its base local revision. Separate app instances cannot overwrite each other's journals. Discovery is read-only to avoid removing a record another process just replaced; a redundant record left by interrupted cleanup can remain on disk without prompting for recovery. Writes use the same flushed staging-and-replacement mechanism as main documents. Orphan staging files are ignored. Back up the recovery subdirectory with the rest of the documents directory.

The startup library checks recovery records and offers **Restore as a copy** or **Discard recovery…**, with a second confirmation before discard. Opening an affected document asks the user to resolve its recovery first. A record whose page content already matches the main document is redundant and ignored; differing content is offered even if the main revision has advanced, since that can represent a conflict rather than obsolete writing. Restore creates an independent local document with fresh identities and no server links, preserving the original. Corrupt or unsupported journals remain on disk and produce a visible notice. Recovery records remain independently recoverable if the original was corrupted or deleted; discard the recovery separately if that writing is no longer wanted.

An abrupt termination recovers the latest **completed recovery write**. Text still only in the WebView event queue, or a write interrupted before atomic replacement, cannot be guaranteed. If both primary and recovery writes fail (for example, a full disk), keep the app open and fix storage or copy the writing before forcing it closed. Ordinary close is blocked on save failure. iOS uses the shared autosave/recovery implementation, but native iOS suspension/background lifecycle hooks remain part of future iOS work.

Prompt 5 validation: **427 tests pass**, including nine new deterministic tests for idle debounce, continuous typing, pending-save edits and final flush, failed primary save/retry, recovery after interruption, stale-base recovery copies, post-commit cleanup/reversion, journal failure/corruption, independent session journals, and interrupted journal replacement. Tests use a manually advanced `TimeProvider` and explicit fault/continuation injection, not timing-sensitive sleeps. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. The web editor assets and npm dependency tree are unchanged.

Native Windows lifecycle UAT remains manual because the available UI automation cannot inspect the app window: type then immediately switch pages, navigate home, deactivate, and close/reopen; confirm the final text in each case. For recovery, type, allow the recovery write to complete, terminate the process before autosave, then relaunch and restore a copy. Repeat with storage access denied to verify visible failure, a blocked normal close, and successful Save after access is restored. These native checks have not been claimed as passed by the service tests. Next implementation dependency: **Prompt 6, native sign-in and backend authentication**; local editing remains available without it.

### Native sign-in and backend authentication (Release 1, Prompt 6)

Windows now has an account menu with system-browser sign-in, silent session restoration, local sign-out, and an explicit backend connection check. It uses MSAL authorization code + PKCE with a localhost redirect and a cache persisted only through MAUI `SecureStorage`. The shared HTTP handler attaches access tokens only to the configured backend API, refuses redirects, and handles rejected sessions without automatically replaying requests. Local editing, saving, and recovery do not depend on authentication.

The backend adds an optional JWT bearer scheme alongside the existing Easy Auth web flow. Validation covers signature, exact issuer, audience, expiry, tenant, delegated scope, and allowed native client ID. Explicit Authorization headers cannot fall back to web/development identities. Existing admin policy remains in place; native token role claims do not grant legacy web Admin privileges. `/api/native/session` is a protected native probe, followed by existing `/api/auth/me` provisioning/account checks in the desktop connection flow.

**Setup is still required:** native auth is disabled by default, and the desktop shows a configuration notice until its registration settings are supplied. Follow [Native authentication setup](native-authentication.md) for exact Azure registration steps, server and desktop environment variables, the public placeholder script, Easy Auth audience constraints, and existing-account identity checks. No Azure registration changes or deployment were performed. Real browser login, SecureStorage restart/sign-out, web compatibility on the Azure edge, and existing-customer identity continuity remain live verification gates. iOS builds with the unconfigured identity adapter; native iOS auth setup remains future work.

Prompt 6 validation: **456 tests pass**, including 29 new backend/device authentication cases; Release solution, Windows Debug, and iOS managed Debug builds have zero warnings. NuGet vulnerability checks report no known advisories for the backend and desktop dependency trees. Tests use synthetic signed tokens and fixed in-memory discovery, not real tenant credentials. Next implementation dependency: **Prompt 7, paid document synchronization APIs**. End-to-end authenticated sync and AI validation require the Azure setup and live sign-in checks first.

### Paid document synchronization API (Release 1, Prompt 7)

The backend now exposes paid initial/incremental discovery, snapshot download, upload, rename, trash, restore, and permanent deletion at `/api/sync/v1/documents`. Each aggregate has an opaque version; stale writes return structured conflicts, and durable operation receipts make retries idempotent. Database triggers track web and sync edits, while permanent tombstones prevent stale devices from recreating deleted IDs. Current subscription and ownership checks apply to every request.

See [Document synchronization API](document-synchronization.md) for contracts, client queue rules, limits, error handling, retention, and migration instructions. Both SQLite and SQL Server migrations are included; no Azure database or deployment was changed. Version 1 protects advanced web metadata by rejecting omitted existing sections/pages and unsupported HTML instead of silently removing them.

Prompt 7 validation: **472 tests pass**, including 16 synchronization cases; provider-selectable tests also pass against local SQL Server Express temporary databases. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. Desktop queue/reconciliation and visible conflict copies remain **Prompt 8**. Native registration and live sign-in verification from Prompt 6 remain prerequisites for end-to-end device sync.

### Desktop synchronization and conflict copies (Release 1, Prompt 8)

The shared device engine now persists account/backend-bound sync journals and immutable operation IDs, uploads committed local edits, downloads incremental changes, and retains independent copies for explicit conflict resolution. Windows triggers sync after sign-in, debounced local saves, reconnection, and Sync now. Library/editor controls show cloud state and last-sync time separately from local saving. Local-only documents require explicit enrollment; permanent cloud deletion retains local writing and never reuses the deleted server ID.

See [Desktop synchronization](desktop-synchronization.md) for queue guarantees, retry/error handling, deletion behavior, current limits, and live staging UAT. Prompt 7 migrations and Prompt 6 native registration/identity verification remain rollout prerequisites. No Azure deployment or database changes were made by Prompt 8. Next desktop work: **Prompt 9, AI actions**.

Prompt 8 validation: **500 tests pass**, including 28 new deterministic engine/HTTP cases. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. Native Windows live synchronization and staging account setup remain manual verification gates.

### Current desktop AI parity baseline (2026-10-03)

The [desktop AI prompt sequence](desktopai-prompts.md) covers the remaining client AI workflow gaps. Its [Prompt 1 evidence and feature matrix](desktopai-uat.md) verifies the current source, focused tests and shipped-editor browser boundaries, and gives the concrete bible-contract/cache handoff for Prompt 2. Existing desktop consistency suggestion Apply, page-level style revision, typed scene proposals, translation and all four storyboard AI actions remain implemented. Native authenticated/provider acceptance is separate and remains open; the older dated validation counts below are historical.

### Focused desktop AI actions (Release 1, Prompt 9)

The editor now sends Rewrite, Expand, Shorten, Summarize, and Custom requests through the authenticated backend. It checks current AI availability/quota, previews results before Apply, saves the original page for guarded undo/recovery, and applies accepted text as a normal local edit. See [Desktop AI actions](desktop-ai-actions.md) for scope, error behavior, backup, and live staging checks. Next desktop work: **Prompt 10, import and export**.

Prompt 9 validation: **520 tests pass**, including 20 new AI request, transport, failure, cancellation, and backup cases. All 12 browser editor checks pass. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. Live AI and sync verification still requires native registration and a configured staging backend.

### Offline desktop import and export (Release 1, Prompt 10)

Windows now uses native file pickers for UTF-8 text and restricted HTML import, with new-document import as the default and an explicit Append/Replace choice for the selected page of an existing section. Local HTML and plain-text export works without a sign-in or server document. See [Desktop import and export](desktop-import-export.md) for the formats, safety limits, overwrite behavior, and manual Windows checks. DOCX and PDF remain server-dependent and are not exposed in this offline flow. Next desktop work: **Prompt 11, packaging, updates, and diagnostics**.

Prompt 10 validation: **535 tests pass**, including 15 new import/export cases. The Release solution, Windows Debug host, and iOS managed Debug host build with zero warnings. Native Windows picker and overwrite behavior still need hands-on verification before release.

### Windows beta package, updates, and diagnostics (Release 1, Prompt 11)

The Windows host now has MSIX identity and version metadata, a test-signing build script, a Windows build/package workflow, build-time Development/Staging/Production backend selection, a manual HTTPS update check, and rotating structured diagnostics with a native export action. See [Windows desktop beta packaging](windows-desktop-release.md) for installation, signing, upgrade, uninstall, backup, update feed, and release validation steps. A stable trusted signing identity and a hosted update feed are required before customer distribution. Native install/upgrade/uninstall validation remains part of Prompt 12 UAT.

Prompt 11 validation: **543 tests passed**, including eight new configuration, update, and diagnostics cases. Windows Debug and iOS managed Debug hosts built with zero warnings. MSIX publishing was initially blocked by missing runtime packs; Prompt 12 restored them and fixed package-type, signing and symbol-tool issues. The corrected script now produces a test-signed Release MSIX with warnings treated as errors. The Windows CI job and real install/upgrade/uninstall remain to be verified.

### Release validation and troubleshooting (Release 1, Prompt 12)

The [Release 1 UAT report and checklist](release-1-uat.md) records native observations, automated coverage, regression steps, local evidence, and unresolved release gates. **Distribution is NO-GO** until those gates are closed. This pass verified signed-out creation/editing, autosave focus, orderly restart, Unicode append import/HTML export, and native diagnostic export. The editor now appears before collapsed AI and transfer panels. All **544 .NET tests** and **12 shipped-editor browser checks** pass; the server Release solution, Windows Debug host, Release MSIX and iOS managed Debug assembly build with zero warnings.

For installation and upgrade, follow [Windows beta packaging](windows-desktop-release.md). This machine rejected the test signing chain; per-user trust did not suffice and was removed. Machine-wide trust was not approved or changed. A package build alone does not establish that installation or upgrade works. Preserve one approved signing identity and use a higher package version for upgrade testing.

For backend configuration, use the build-time environment settings above and [native authentication setup](native-authentication.md). Sign-in additionally needs a registered native client and the documented `WRITERAPP_AUTH_*` settings; configuring an API URL alone is insufficient. Missing authentication configuration must leave local writing available. Rehearse [sync migrations and API configuration](document-synchronization.md) in staging before testing [desktop synchronization](desktop-synchronization.md) and [AI actions](desktop-ai-actions.md). No staging account or production access was assumed in this pass.

Local writing, recovery, sync metadata and logs live below the host's `FileSystem.AppDataDirectory`, not the repository. Packaged Windows data is normally under `%LocalAppData%\Packages\<package-family-name>\LocalState`; unpackaged Debug data uses a separate host-specific location. Do not infer the current store from a different build's folder. Close Prosa and copy the entire app data directory before an upgrade rehearsal, app reset or uninstall; also export important documents to HTML or text. Keep the original backup intact and restore to a separate test copy when diagnosing failures. A document export does not contain recovery/sync state.

If saving fails, keep the window open, copy unsaved writing, check writable disk space/access, then retry **Save on this device**. If recovery is offered, restore a copy and compare it with the original before removing anything. For stale revisions or cloud conflicts, preserve both versions and resolve through the app. Export diagnostic logs via **Updates and diagnostics** and inspect before sharing. For MSIX `0x800B0109`, verify the exact signer and approved trust scope; do not bypass Windows verification. A missing `mspdbcmf.exe` requires the VS C++ tooling used for package symbols; the build script discovers an installed x64 tool through `vswhere`.

Known validation limits: forced-process recovery, the complete native library lifecycle, long manuscripts, full keyboard/high-DPI/small-window coverage, live authentication/sync/AI, installed package upgrades, and remote CI are still open. iOS remains a managed-code scaffold with the platform adapters described above. See the report for the exact remaining acceptance cases rather than treating service tests as end-to-end evidence.

## Remaining release work

The app now has native startup, shared document library and navigation, backend configuration, and a versioned local document repository. Product functionality still requires:

1. Manually verifying Windows deactivation, orderly close, and forced-process recovery; add iOS lifecycle integration when implementing that host.
2. Deciding which additional web formatting features to support and adding native iOS file dialogs when that host is implemented.
3. Configuring Azure/native registrations and verifying Windows sign-in end to end; implement the iOS authentication adapter when building that host.
4. Rehearsing/applying the synchronization migrations in staging, then verifying authenticated Windows/web sync and AI actions against a configured staging provider.
5. Establishing the final Windows publisher/signing identity, hosting the update feed, and validating a real MSIX upgrade; adding Apple bundle identifiers, provisioning profiles, capabilities, privacy declarations, and App Store metadata.
6. Running the Windows package workflow on GitHub and adding an iOS CI runner once Apple build infrastructure is available.

Changes limited to these device projects do not match the Azure landing-site workflow's `Prosa.Landing/**` path filter and therefore do not trigger that deployment.

## Release 1 implementation

### Desktop–client parity baseline (Prompt 1)

See [desktop–client parity UAT](desktop-client-parity-uat.md) for the 2026-09-28 baseline and remaining reproduction steps. Fixed web onboarding initialization that could mistake an unmounted editor for empty saved writing and replace it with starter text. Four new preservation cases bring the suite to **550 passing tests**; the shipped-editor browser harness passes **13 checks**. Server/web Release, Windows Debug/Release and iOS managed Debug compilation pass with warnings treated as errors.

A normal isolated Development web document retained Unicode text and bold/italic formatting through autosave, navigation, reload and a new tab. External authenticated acceptance, full browser restart, native restart/import/export/failed-close UAT and paired viewport checks remain open. Native capture returned the wrong application, so historical desktop results are not new passes. Requested 1920×1080 browser captures remained 1280×720. Follow the linked checklist before claiming full acceptance.

`Directory.Build.props` excludes nested artifacts/bin/obj from default item discovery, preventing redirected validation output from being copied recursively into later builds. Keep disposable output under ignored `artifacts` folders.

### Cross-host content preservation (parity Prompt 2)

Use the [editor content compatibility contract](editor-content-compatibility.md) when changing the schema, sync content, import/export or formatting controls. The shared JSON allowlist feeds native validation and the bundled device editor. Unsupported saved pages are read-only, and edit/import replacement is blocked at service boundaries. The UI offers a lossless source backup containing the complete document envelope; ordinary HTML/text exports refuse unsupported pages instead of stripping them. File import remains an explicit, disclosed sanitizing conversion.

Headings 1–6 are now selectable in the shared toolbar's device configuration. Import/export retain headings 4–6, underline, ordered-list start, code-language and supported link attributes. Rich web tables/images/styles remain preserved but noneditable on device. Web advanced editing has not been reduced to the device subset. See the appended Prompt 2 [acceptance evidence](desktop-client-parity-uat.md) before treating authenticated sync or native rich-content UI as accepted.

Use the ordered [Release 1 desktop implementation prompts](release-1-desktop-prompts.md) to build the offline editor, native authentication, paid synchronization, AI actions, export, packaging, and final release validation.

### Prompt 3: shared context navigation and editor status (2026-09-28)

`EditorContextNavigation` and `EditorTabList<TKey>` now render both web and device category/subview tabs from typed `EditorTabDescriptor<TKey>` values with explicit availability. The client still owns its categories, plan gates, onboarding IDs, persisted selection, loaders and callbacks. Desktop supplies only Writing and Navigator; its presentation state resets to Writing when changing/leaving documents. Shared navigation implements roving tab focus, Left/Right wrap, Home/End, selected state and panel relationships; normal Tab traversal remains available.

`WritingPanel`, `AiTextComparison`, `NavigatorRow`, `EditorDocumentActions` and `EditorStatus` provide supported presentation in both hosts. Desktop's five existing AI actions/proposals belong to Writing; local pages and cloud sync controls belong to Navigator. Document actions beside the heading contain Save now and native Import/Export, available independently of the selected category. The wider file-transfer popover keeps its sanitization disclosure, compatibility protections and native picker callbacks. Maintenance remains in Settings. Protected/recovery/empty writing contexts have explanations instead of empty tools.

`EditorWordMetrics` shares the client's HTML text mapping and Unicode letter/number/apostrophe token rule. Canonically equivalent accents are normalized for counting only (NFC); persisted writing and selection mapping are unchanged. The device counts its current page, including pending edits and supported legacy text/JSON; the web retains its section scope. Each scope is stated in the status tooltip. No desktop page estimate is invented. Shared save presentation distinguishes the device's local copy from the web's backend document; account/plan/token fields stay host-specific.

Validation and remaining live gates are recorded in [Prompt 3 parity evidence](desktop-client-parity-uat.md#prompt-3--shared-context-panels-and-controls-2026-09-28). No editor TypeScript/schema was changed, so the tracked editor bundles did not require regeneration in this task. Prompt 4 adds durable local structure management; the current Navigator only opens existing pages.
## Prompt 4: durable local sections and pages

The desktop Navigator supports offline section/page creation, renaming, ordering, page moves between sections, deletion to local trash, and restoration. Manage disclosures expose keyboard-accessible buttons and section selectors. A document must retain a section, and each section must retain a page. Create appends; move appends to its destination; reorder swaps the two affected order values; restore appends without renumbering unrelated siblings. Renames, moves, and restores retain local/server identities. A duplicate gets independent identities, including its trash.

`LocalDocumentStructure` transforms one document aggregate; `LocalDocumentRepository.ChangeStructureAsync` persists it through the existing atomic writer and revision check. The workspace freezes the current editor only during the operation, flushes writing and recovery first, and adopts a structure change only after durable persistence. The active page remains selected across rename/reorder/move. If removed, selection advances to the next page at the previous flattened position, falling back to the preceding page. A failed save or stale revision retains the editor writing and prevents navigation/closing.

Local storage schema **2** includes full section/page trash payloads inside the same document file. Schema 1 is migrated only after its exact bytes are saved to `<document>.json.v1.bak`; legacy draft migration keeps its existing `.legacy.bak`. Failed migrations preserve the source and can retry. Future schemas still fail closed. Unknown document/section/page JSON fields are retained as extension data. No permanent section/page purge is exposed in this release.

The sync upload contract has an optional `structure` object with `allowPageMoves`, `removedSectionIds`, and `removedPageIds`. Every omitted existing server identity must be listed explicitly. Existing clients without this object retain the previous no-removal/no-move behavior; older servers reject these changes safely. Deploy the updated backend before accepting desktop structure sync in staging/production. Existing journal fingerprints exclude this additive capability, avoiding a migration-driven upload of every existing document. Ownership, plan authorization, aggregate version checks, durable operation receipts, account bindings, and conflict copies remain in effect.

Cloud removal rejects items with web-only notes, annotations, versions, quality records, translation grouping, or outline/project links, and originals that cannot be restored through the device HTML upload contract. The entire cloud document is retained on rejection; the local deleted writing remains recoverable. Restore the local items and use **Retry rejected upload**, or manage those items in the web application. Unsupported original content remains movable/renameable without rewriting its content. Concurrent remote edits and local rename/move/delete produce durable conflict copies rather than silently overwriting either side.

For isolated Windows acceptance testing, a Development build may set an absolute `ProsaValidationDataDirectory` MSBuild property. This redirects document/recovery/sync/AI-undo and diagnostic storage without changing the normal app data directory. Production/Staging builds reject this property. Example:

```powershell
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj -c Debug --no-restore -warnaserror -p:BaseOutputPath=artifacts/parity-p4-native/ -p:ProsaValidationDataDirectory=C:/Users/Johan/source/repos/WriterApp/artifacts/parity-p4-native-data
```

See `docs/desktop-client-parity-uat.md` for automated results and the remaining live gates. This work does not add a project Part/Chapter/Scene tree.

## Prompt 5: offline search and saved document preview

The device header searches active local document titles and writing across every active section/page. Results identify the document, section and page; document/section/page trash is excluded. No writing is sent to the backend. Search scans current files rather than maintaining a second persisted index, so edits, renames, moves, deletes and restores are reflected after durable saving and after restart. Changed events refresh an open query. A 300 ms debounce, cancellation and query generations suppress obsolete results. Each query reads at most 256 files/32 MiB, searches at most 2 million title/source-writing characters, accepts a trimmed query up to 200 UTF-16 characters and returns at most 50 results. Limits and unavailable files are explained in the results panel.

Matching uses NFC canonical equivalence and `OrdinalIgnoreCase`, with offsets mapped back to untouched source text. Accents are significant; `café` matches `café` and `CAFÉ`, while `cafe` does not match `café`, and `STRASSE` does not match `Straße`. HTML search extracts visible writing, ignoring active/script content; legacy JSON extracts text nodes rather than unknown metadata. Extremely deep/complex text extraction is bounded (128 levels/100,000 HTML nodes). Unsupported originals remain intact and may have incomplete text extraction; use a source backup for their full original.

Before a query or result navigation, the mounted editor flushes through the existing save lifetime. Save failure retains pending writing and cancels navigation. Result selection reloads and revalidates the current page by stable identity, follows moves and refuses deleted/nonmatching targets. A fresh navigation request selects repeated identical results without making ordinary autosave reselect text. Editable matches use TipTap selection and scroll only, without a content transaction or new revision. Protected originals direct the user to Preview/Find. Native buttons support Tab/Enter; Escape dismisses results; empty and storage-error states explain the outcome. Metadata filters wait for their local repositories.

Preview flushes writing, checks the saved fingerprint and renders an immutable local snapshot with all active sections/pages in order. The editor remains mounted and hidden, preserving its undo state; structure and AI mutation controls are unavailable while previewing. Supported HTML/text/legacy JSON retain compatible formatting; links are sanitized and use `noopener noreferrer`. Unsupported formatting is shown as encoded text with an explicit explanation; source content is never converted or saved by preview. Outline links retain the document route, and Find highlights up to 200 matches per page without changing files or sync revisions. Search result buttons, highlighted text, preview shell and section presentation live in `WriterApp.UI.Shared`; web orchestration and server search remain in the client. Web preview hides its mounted editing canvas while reading.

The acceptance run also fixed an idempotent view-reset feedback loop that could prevent a document from opening. Validation, browser-host evidence and outstanding native/mobile gates are recorded in [Prompt 5 parity evidence](desktop-client-parity-uat.md#prompt-5--offline-search-and-full-document-preview-2026-09-28). Prompt 6 establishes local projects/scenes before extending search to planning metadata.

## Prompt 6: durable local projects and scenes

The device now has a Projects hub at `/projects`, project selection at `/projects/{localProjectId}`, explicit creation/attachment, and writing-context breadcrumbs. `ProjectHubHeader`, `ProjectSwitcher` and the existing `NavigatorRow` provide shared presentation in web and device hosts. Web API orchestration and plan gates remain in the client. Local project creation, rename, hierarchy editing and manuscript opening require neither authentication nor connectivity.

### Persistence and canonical writing

Document envelope schema **3** adds an optional version-1 `LocalProject` aggregate. One local project owns exactly one manuscript file. `ProjectId` and each `NodeId` are stable device identities, separate from optional server mappings. `ManuscriptId` must equal the containing document ID. A Scene references exactly one unique active section in that manuscript; the section's pages remain the only canonical writing. Part and Chapter nodes do not carry content. Additional sections created through the ordinary document Navigator can remain outside the project tree, and are still accessible in the manuscript. No server project identity is inferred from local creation.

Project creation writes the project, default chapter/scene and manuscript in **one atomic commit**. Attachment explicitly adds organization around an existing standalone document without replacing document/section/page IDs or content. Existing documents are not automatically attached. Version 1/2 envelopes migrate to version 3 with exact `.v1.bak`/`.v2.bak` backups before replacement; legacy backups remain supported. Unknown document/project/node fields round-trip. Future project versions and invalid associations fail closed, preserving the source.

Tree placement uses backend `ProjectNodeHierarchyRules`: Part at root; Chapter at root or under Part; Scene under Chapter. This depth/type restriction also forbids cycles. Active sibling order is unique and deterministic; moves/restores append, up/down swaps siblings. Scene traversal determines manuscript section order; unassigned or deleted-tree writing follows active scene sections. Scene/section renames keep the linked titles aligned. This first local model does not implement front matter or multiple project documents.

Deleting a tree item is **organization-only, recoverable deletion**. A deletion token groups the active subtree; previously deleted children keep their separate tokens. Writing remains in the manuscript, search and preview. Restoring a subtree does not resurrect older independent deletions. Deleting linked sections through the ordinary Navigator is blocked; page management remains available with existing page trash and last-page protections. Moving a manuscript to document Trash also hides its project, and restoring it restores project access. Permanent removal of a project manuscript is blocked for now. Duplicating a manuscript produces independent standalone writing with new content IDs, without copying project ownership; it can be explicitly attached to a new project.

The project and manuscript share the existing store lease, atomic writer and optimistic `LocalRevision`. Concurrent/stale operations cannot overwrite committed content, and partial staging files never become projects. Last writing page is saved in the project aggregate through the editor session; Resume revalidates it and falls back to an existing page if removed. Opening a scene first saves existing writing and uses stable page identity. Context changes do not toggle editor editability during ordinary autosave.

### Cloud boundary and next dependency

Local projects display an explicit local-only explanation. Document-only sync enable/upload/download and store-level sync replacement reject local projects; AI controls are hidden for them. Cloud-linked standalone documents must be duplicated before local attachment. Prompt **7** must implement project-aware identity mapping, sync and conflicts before enabling cloud/AI for these projects. No backend migration or deployment is required for this local task. See the appended [Prompt 6 UAT evidence](desktop-client-parity-uat.md) for verified flows and remaining runtime gates.

## Prompt 7 — Project-aware synchronization (2026-09-28)

This section supersedes the Prompt 6 cloud boundary. Devices now use `api/sync/v2/documents`; project sync is optional and requires the existing authenticated paid entitlement. Local editing and saving remain independent of both. The Projects hub and document sync controls expose enable/status/retry/conflict feedback. AI uses its existing authenticated manuscript adapter after a server document identity exists; advanced planning interfaces remain Prompt 8.

### Aggregate, identities and compatibility

One project, its Part/Chapter/Scene tree, scene notes/cards, and one canonical manuscript form a single sync aggregate. The payload includes project title/subtitle/author/language/genre/export settings/cover URL; node metadata JSON; all existing scene-card fields; scene notes; canonical section/page writing; explicit node deletion tokens. Scene content is mirrored from ordered canonical pages for existing web consumers. Additional manuscript-level notes, annotations, tasks, versions, assets and multi-document/front-matter planning are not introduced by this contract.

Local document/project/node identities remain stable and have explicit server counterparts. Newly uploaded projects/nodes reserve their local UUID as the server UUID; newly downloaded projects/nodes receive separate local UUIDs. Parent and section associations map through those identities. Sections/pages retain the established sync identity mapping. Resume-page state stays local and does not dirty cloud content. Conflict copies receive independent project/node/section/page IDs and no server bindings; ordinary manuscript duplication retains the earlier standalone-copy behavior.

`SyncProject.Version = 1` is additive inside the v2 API. The local envelope remains schema 3/project version 1 with additive optional metadata and preserved extension fields. Both providers have additive migrations for `Projects.SyncEnabled` and `ProjectNodes.SyncDeletionId`. Existing structured projects are backfilled; the marker remains after a web hard deletion of the final node. Database triggers on projects/nodes/scene notes/cards invalidate the existing document version and change-feed sequence, including web writes outside sync.

V1 document-only operations remain supported for standalone documents. V1 mutations against structured projects return HTTP 426 `project_sync_required`; v2 uploads cannot omit a structured project's payload. A backend without v2 returns an upgrade explanation on the device; no silent fallback discards project metadata. Unknown project versions, invalid/missing parents, duplicate identities/section links and unsupported placements are rejected without committing writing. Web projects with multiple manuscripts, front matter, unlinked scenes or unsupported ordering return 422 `project_shape_unsupported` and retain their originals. Such a feed item stops that sync pass for review; it is not silently skipped. Attach cloud standalone writing by first making the existing explicit independent duplicate.

### Commit order, durability and conflicts

The server uses the existing serializable transaction and sync-clock lock. It checks owner, current paid access, immutable operation receipt and aggregate expected version before committing. It materializes project/manuscript/sections/pages, then parent-before-child nodes and scene metadata, then the final version/receipt. Intermediate saves remain inside one transaction. A retry of an acknowledged or ambiguously acknowledged operation returns its original receipt. There is no partially uploaded tree and no independent child batch to reorder. Downloads contain the complete snapshot and replace the local file atomically under its revision check.

Durable immutable requests and a rescan of saved local revisions retain offline edits across restart. Journals and bindings remain scoped to backend plus owner. Account changes cannot rebind an existing aggregate. Local save races preserve newer writing. The entire aggregate is the conflict boundary: concurrent rename/rename, move/delete, edit/delete or divergent trees require explicit resolution, even when different fields changed. No automatic structural merge is attempted. A missing remote node from web hard deletion also creates a conflict when local writing was otherwise clean, retaining the lost hierarchy as evidence.

Device tree deletions use retained node tokens, hiding them from ordinary web queries while retaining writing and scene metadata. Restore clears the appropriate token. Missing nodes in uploads are rejected; they are not inferred as deletions. Permanent manuscript/project cleanup explicitly includes hidden nodes so SQL Server foreign keys and retained links remain correct. Cloud manuscript deletion produces the existing document tombstone; local content is retained and never automatically recreated with the old server ID.

Conflict UI identifies project/scenes, marks deleted tree items, links both independent trees, and explains whole-aggregate resolution. Keeping local uploads against the reviewed server version; keeping cloud adopts that snapshot. Both first retain a fresh independent local backup; cloud conflict copies remain available. A newer remote edit causes another conflict. Missing/invalid parents are validation errors, preserving the saved local file and queued request for correction rather than guessing a new tree.

### Release prerequisites

Deploy the backend migrations and v2 routes separately before enabling this build against the deployed service. No deployment was performed here. Validate production authentication/paid access, migration rollout/backup and two signed-in native devices against staging. SQL Server service integration and SQLite migration/HTTP round trips were verified locally; this is not Azure verification. Apple simulator/device execution and complete native conflict/accessibility/offline-lifecycle acceptance remain gates. See the Prompt 7 report in `desktop-client-parity-uat.md`.


## Desktop/client parity Prompt 8 — Local planning (2026-09-29)

Projects now open a durable local planning workspace with Story, Notes & Tasks and Synopsis views. The editor also exposes links to these views for project manuscripts. Scene cards, quick title/status presentation, inspector field controls and synopsis fields are shared with the web client in `WriterApp.UI.Shared/Projects`. Device orchestration lives in `WriterApp.Device.Shared`; the web retains its existing save/AI adapters. Board grouping and navigation remain host orchestration. Device ordering uses explicit Move up/down controls and the existing Project structure screen; it does not pretend to implement web drag/drop or unsupported menu actions.

Scene metadata/notes, comment/task creation, resolve/reopen state and all ten synopsis fields save into the same atomic local document aggregate. Planning never writes manuscript page content. Saves flush before scene/view/navigation changes and through the native save lifetime. A failed revision check retains the draft. Unadded annotation drafts require Add or Discard before leaving. AI planning adapters remain Prompt 10 work.

Local file schema remains 3 with project payload version 2. The first project-v1 upgrade retains the original bytes beside the document as `.json.project-v1.bak`; existing backups are never overwritten. Unsupported project versions fail closed. Existing project/node extension data and untouched scene-card fields are retained. New nested fields unknown to this build are rejected rather than silently discarded.

Annotations retain stable local/server identities, quoted evidence, status and timestamps. Device-created quotes do not invent ProseMirror positions. A unique exact quote remains identifiable after surrounding edits; missing or repeated quotes become detached and stay available for review. Prose changes clear old web positional ranges. Detachment is sticky rather than automatically reattaching to a later matching passage. This is conservative quote tracking, not native inline range highlighting or automatic reanchoring. Existing web range/author data is preserved when applicable; the server supplies authors for new annotations.

Planning sync uses `/api/sync/v3/documents` and project payload version 2. Deploy both provider migrations `AddPlanningSynchronization`/`AddPlanningSynchronizationSqlServer` before connecting this device build to a deployed backend. Projects retain a planning-capability flag even after web deletion of their last planning row. Older v1/v2 clients receive an upgrade-required response before they can omit planning. SQLite and SQL Server triggers advance the owning document version/change feed when ordinary web synopsis or annotation rows change. Requests, receipts, ownership, entitlement checks and whole-aggregate conflict copies retain the existing sync protections. Conflicts include concurrent writing/planning edits and missing remote annotation evidence; annotations cannot be silently removed by omission. Copied conflicts receive independent local identities.

Search has Writing (default), Planning & notes and combined scopes. Planning results open the appropriate Story, Notes & Tasks or Synopsis view and revalidate project/node identity and current text. Trashed documents and deleted scenes are excluded; restoration returns retained metadata to search. Task search opens all statuses so resolved evidence stays visible.

The local web host now prefers current static-asset manifests and uses .NET 10 fallback paths, preventing stale .NET 9 styles from masking shared UI changes. No Azure deployment or authentication registration change was made. See the Prompt 8 UAT report for verified evidence and remaining live gates.

## Prompt 9 — Local publishing and native conversion (2026-09-29)

Open a manuscript, then **Document actions → Import / Export → Publishing, export layouts and PDF**. Existing quick HTML/TXT export and recovery source backup remain available. DOCX import is available from the library and from the editor's explicit append/replace import flow; keep the original DOCX because conversion is lossy.

`WriterApp.Publishing` compiles the existing portable converters and document models, referenced by both the backend and Device.Shared. Source files remain in their existing locations through linked compile items and are excluded from the backend's direct compilation. This reuses the actual DOCX/EPUB/Markdown/HTML implementations without a backend dependency or a second converter copy. Server authorization and export endpoint behavior remain in the backend. Desktop conversion is entirely local: HTML, TXT, Markdown, DOCX and EPUB require neither network nor paid entitlement. Windows PDF uses a dedicated WebView2 host adapter. No export uploads local writing or fetches remote cover/images.

`ExportScopeFields` and `ExportPreviewFrame` are shared presentation used by the web export dialogs and device publishing screen. Desktop supports the whole active manuscript or one section/scene, respecting canonical section/page order and omitting trashed scenes. Navigation from writing uses the existing flush/save guard; each preview/export reloads saved writing and captures a revision-labelled snapshot. Rendering never writes back to the manuscript. Unsupported source content fails with an explanation; use the source backup to preserve it.

Publishing settings live in version 1 JSON at `documents/publishing/{documentId:N}.json`. Named presets have stable local IDs; options, presets and embedded PNG cover bytes survive restart. Covers are bound to local document/project identity, validated for size, dimensions, chunk bounds and checksums, and never reference temporary files. Settings use atomic replacement and revision checks; unsupported/newer files are preserved. These are deliberately local settings, not new cloud entities: no server identity mapping or sync contract is asserted. Changing manuscript project association requires resolving the old settings association rather than silently carrying its cover across projects.

Limits: PNG covers only, at most 2 MB and 32 megapixels (8000 pixels per dimension); at most 50 presets. Built-in Manuscript/Paperback/A4 layout templates apply to HTML/PDF. DOCX/EPUB use the existing converter layouts; TXT/Markdown reject embedded covers. DOCX import has a 5 MB compressed limit, 25 MB expanded total, 10 MB per part and 2000 ZIP entries. Supported paragraphs, headings, basic emphasis, lists and safe HTTP/HTTPS/mailto links convert; tables, images, comments, tracked changes, headers/footers and advanced layout may simplify or disappear. The UI explains these limits before import and distinguishes HTML preview from Word/ebook pagination.

PDF uses WebView2 PrintToPdfStreamAsync with the chosen page dimensions, dedicated immutable HTML, blocked remote requests and disabled scripts. Windows save uses the native picker and a transacted write; cancellation does not report success. PDF availability is host-specific; no browser print button is presented as native support. iOS still needs its own native file/PDF adapters and device acceptance. Very large HTML/embedded covers remain subject to WebView2 NavigateToString's size limit and fail without changing writing; large-book PDF acceptance remains open.

See the Prompt 9 entry in desktop-client-parity-uat.md for tested behavior and release gates.


## Desktop parity prompt 10 — AI workspace and durable history

The editor preserves Rewrite, Expand, Shorten, Summarize and Custom and adds **Translate selection** with a target language. Writing links to **AI analysis, prompt library and history**; the separate History tab shows saved AI operations. Project planning links to the same AI workspace for scene-card coaching, synopsis field coaching and storyboard subplot analysis. Shared `AiHistoryPanel` and `AiTextComparison` render in both client and device hosts.

The workspace provides section consistency and style/quality analysis, reusable custom analysis, scene-card proposals, synopsis field proposals and storyboard subplot continuity through the existing authenticated action API. Analysis never applies prose changes. Translation has a dedicated selection target; scene and synopsis Apply change planning only. Scene-card Apply supports summary, narrative purpose/role/intent, emotional beat, key events and open questions. Status, entity references and tags remain authored. Consistency currently excludes character/place/timeline bibles; the UI explains that limitation. Style analysis uses the existing custom-transform contract with a fixed analysis instruction. Reusable workspace prompts are analysis-only; the existing editor Custom action remains available for manuscript edits.

Prompts are atomic, versioned local files under `documents/ai-library/prompts`. Saving creates a new definition. Explicit cloud copies use `GET/POST api/ai/presets` through the authenticated backend client; only account-wide custom templates with no additional parameters map to the local schema. Import re-fetches the current account's definitions. Transfers never update/delete an existing definition or retry a POST automatically: the current preset API has no idempotent upsert/conflict token. After an interrupted upload, refresh before retrying because a copy may already exist.

AI history lives under `documents/ai-library/history/<document-id>` with original local writing/planning snapshots, proposal text, typed target, source local/cloud revisions and Reviewed/Applying/Applied state. New planning records also retain the node identity and saved result snapshot. The shared History panel presents readable action cards, status badges, structured scene previews, and Undo/Redo controls. Applied writing changes restore the affected page; scene changes restore only the fields changed by AI; synopsis changes restore only the selected field. Each operation checks the current affected values against its saved evidence, preserving unrelated edits and blocking automatic replacement of later edits to the same writing or fields. Reviewed analyses have no change to undo. Older records support undo when the target and result can be established unambiguously; otherwise the panel explains that a recovery copy is available.

Applying is recorded before a mutation, Applied after local save; an interrupted Applying record is recovery evidence, not proof of a completed save. Undo/redo use durable Undoing/Undone/Redoing states and can finish an interrupted save on retry. Recovery forks the original snapshot into a new local-only document/project with remapped identities. It never replaces current writing or uploads the recovered copy. Proposal application is not resumed after restart. Editor undo remains separate from persisted AI history. Files contain user content needed for recovery, not credentials or provider diagnostics; normal device storage/backup protection applies. See [AI history verification](ai-history-uat.md).

Requests explicitly synchronize first and bind to the acknowledged document version. Local revision/content/planning changes, deletion, account changes and 30-minute proposal expiry reject Apply; repository revision checks also protect concurrent saves. Cancellation, unavailable entitlement/quota and transport errors do not apply proposals. Remote edits discovered on later sync continue through the existing conflict flow.

**Backend rollout prerequisite:** `/api/ai/status` now advertises `supportsDocumentVersionChecks`; requests carry optional `expectedDocumentVersion` and successful responses echo `sourceDocumentVersion`. The AI action endpoint checks the sync version before and after provider execution and returns `409 ai.stale_source` when it changes. Desktop checks capability before any billable call and rejects missing/mismatched echoes. Older clients remain compatible because the request field is optional. Release this backend change, including the existing sync/planning migrations from prompts 7–8, before enabling the updated desktop AI flow. No deployment or migration execution against Azure was performed here.

See `docs/desktop-client-parity-uat.md` for prompt 10 evidence and remaining live acceptance gates.

## Desktop parity prompt 11 — Account, billing and help (2026-09-29)

**Account & Help** opens the desktop account screen. It shows native identity, configured backend origin, connection status, plan/subscription and token usage when the backend provides them. Refresh verifies `api/native/session` and fetches `api/auth/me?force=1` through the existing authenticated client. Sign-in/out retain the existing system-browser and secure-token adapters. Expired, forbidden, duplicate and deleted accounts receive explicit guidance; signed-out/offline users retain access to local writing.

`AccountSummary`, `HelpLinks` and `FeedbackComposer` are shared with the web Account page. Device account information is a memory-only display cache bound to the native account generation. The screen labels its last check and stale/offline state; signing out or switching accounts invalidates the displayed profile. This cache never authorizes backend operations. It is not persisted across application restarts.

Billing buttons preview a fixed destination before an explicit system-browser handoff. The configured backend must use HTTPS without credentials, query or fragment. Supported paths are `/app/account/billing`, `/start?plan=standard` and `/start?plan=pro`; documentation uses the existing `https://docs.prosa-app.com/` destination. Links contain no native tokens, identity or manuscript data. Billing has no supported native return callback in the existing contract: return to the app and refresh manually. Hosts without a system-browser adapter explicitly disable handoff. Payment collection and account linking remain website/backend responsibilities.

Feedback starts as an in-page draft, validates its type/title/description and requires preview before Send. Editing or changing the native account invalidates the reviewed snapshot. Only entered text is submitted to the existing `api/feedback` contract, with diagnostics disabled; no manuscript, document identifiers or logs are attached. The UI discloses that the existing backend adds signed-in sender identity for support replies. Sending is explicit, requires connectivity and authentication, and is never automatically retried. Drafts are not saved across page navigation. Settings still provides native updates and diagnostic export; the Account screen explains that export contains app/runtime information and sync counts, not writing or tokens.

See the Prompt 11 UAT entry for verified checks and remaining live-account acceptance. No backend authorization, token protection or authentication registration was changed.

## Desktop AI prompt 4 — Local quality findings (2026-10-03)

Writing → Style & quality now combines the existing broad AI revision with offline page/selection quality checks. `QualityFindings` is shared presentation; `LocalQualityPanel`, `LocalQualityChecks` and `LocalQualityActions` own device lifecycle, source checks and durable application. Server/device detection uses the pure `WriterApp.Shared/Quality` rule catalog. Catalog cache version 2 invalidates earlier server results whose whitespace offsets could be incorrect.

The quality editor adapter uses `CaptureAiAsync(quality: true)`: blocks contribute `\n\n`, hard breaks `\n`. `device-quality.ts` and quality preview use those same UTF-16 offsets. Ordinary AI and consistency capture retain their existing mapping. Do not mix offsets from the two captures. Highlights are editor decorations, never stored markup; typing clears them and source changes require a new check.

Repeated adjacent words, rule-provided name casing and paragraph splits can be reviewed locally. Other supported repeated-word/sentence/passive fixes call existing revision-checked `rewrite.selection` after save/sync, preserving the input language. Glossary and stored scene hints are disclosed omissions; passive detection is an English heuristic. A finding dismissal affects the current check only and does not synchronize the client's persisted dismissal state.

Before generation, `validateQualityRange` proves the exact source and one complete text-block range with uniform inline marks, whole words and valid Unicode boundaries. Mixed formatting, embedded content or cross-block ranges require manual revision. Preview keeps provider text inert; local paragraph splitting retains block attributes/marks. Apply checks current page content/format, target/account/cloud bindings and expiry, then records an immutable Applying recovery entry before save and Applied after commit. Existing History restores only that page and retains separate-copy recovery for interrupted/changed-source cases. If the writing was committed but history completion fails, report that saved state explicitly.

Limits: 500,000 characters per checked page, 200 findings and 100,000 characters per proposal. Detection/finding display is ephemeral; approved writing and history are durable. See [prompt 4 evidence and live gates](desktopai-uat.md). Regenerate the tracked editor bundles with `npm run build` after adapter changes. Native Windows/provider acceptance is still separate from the successful builds, component tests and real-editor browser checks.

## Desktop AI prompt 3 — Canon-aware consistency (2026-10-03)

The Consistency Coach consumes `DeviceBibleService.ContextAsync` after save/sync and includes only current account/backend/document/source-matched character, place and timeline snapshots. It shows Current/Stale/Missing/Interrupted state and unresolved references. Update all bibles refreshes the existing canon workflow; reduced-context analysis requires an explicit choice and reports each omitted bible in the result. Local cached canon and history remain readable offline.

`LocalConsistencyContext.Prepare` constructs the client's named `character_bible_json`, `place_bible_json` and `timeline_bible_json` options, saved story planning and the whole saved section. `consistencyPlainText` maps every page through the actual device schema, including legacy JSON, then joins pages with two newlines. Within each page, consistency uses single newlines between blocks/hard breaks. Quality retains its distinct double-newline block mapping. Never interchange those offsets or use regex HTML stripping to resolve a consistency passage.

`AiActionExecuteRequestDto.ExpectedCanonVersions` and response `SourceCanonVersions` are optional typed dictionaries; `/api/ai/status` advertises `supportsCanonVersionChecks`. Device analysis requires that capability before sending a billable request, including reduced-context runs, and verifies the echoed dictionary. Legacy clients may omit it. The backend checks owned stored snapshot tokens, exact supplied canon content and manuscript source hash before and after provider execution. Omitted bibles must be `{}`. A changed source/canon returns `409 ai.stale_source`. The continuity action now honors the editor text override; saved planning reaches the provider's continuity prompt. Deploy this additive backend contract before enabling the updated device feature; no deployment occurred here.

Shared `ConsistencyReport` supplies severity filtering, evidence, scene/page labels, Jump, per-finding review and Applied feedback. Device orchestration validates exact original anchors; after an approved change, subsequent findings must still match uniquely in the current writing. Jump binds document/page identity, full current page text and exact range; opening another page does not mutate the analyzed planning resume field. Missing/ambiguous passages fail with guidance. Findings without direct prose can request a generated fix through the existing `rewrite.selection` action. Generation uses saved/synchronized revisions, bounded finding guidance and the original language/voice; returned `revisedText` JSON or `<<REVISED>>` wrappers are read as inert prose. Instructions, HTML, invalid joins, duplicated surrounding text and malformed/unchanged output cannot become approved replacements.

`validateQualityRange(..., quality: false)` preflights consistency ranges before generation. Automatic fixes support complete words in one text block with uniform marks; mixed formatting, embedded content, cross-block or multi-paragraph replacements receive a manual-revision explanation. `previewSafeConsistencyRevision` preserves the target's marks and unrelated supported structure. Explicit approval rechecks source, canon, account, target and expiry, records Applying with the original snapshot and intended HTML, then saves and records Applied. Partial history completion reports that writing was saved and the original remains recoverable. Existing durable History Undo/Redo and separate-copy recovery are retained; later user edits never inherit an earlier approval.

Limits: at most 200 saved pages, 500,000 source characters per page, 100,000 section characters, 250,000 canon characters, 100,000 planning characters, 200 findings and 100,000 response characters. Context exceeding a limit is rejected rather than silently truncated; planning includes all active nodes within its character limit. See `desktopai-uat.md` for exact evidence, native/provider gates and prompt 5 handoff.

## Desktop AI prompt 5 — Complete section and document translation (2026-10-03)

Open **Writing tools → More writing tools → Translation**. Selection, current section and entire document share the language catalogue, searchable target picker, auto/explicit source language and literal/natural/formal/informal styles. Selection keeps its existing targeted review/Apply. Section and document previews list every intended page and offer Replace or Duplicate as a new section/document. `TranslationOptions` and `TranslationProposalPanel` (including CSS) live in `WriterApp.UI.Shared` and are consumed by the client and device hosts; orchestration stays in `LocalTranslationPanel` and the device services.

The additive `translation_structure` parameter on the existing `translate.section` / `translate.document` actions contains version 1, cloud document identity, scope, target language and ordered section/page/text-run markers. Text runs are paths through the editor's prepared schema tree. The provider changes only run text; the host retains paragraphs, headings, lists, tables, hard breaks, links, marks and block attributes. Provider HTML is inert schema text. Input is bounded to 60,000 UTF-16 text units, 2,000 runs, 100 sections/1,000 pages and 100,000 serialized characters; raw rich-source preflight is bounded to two million characters. Output uses the same limits. Provider output-token configuration can impose a smaller practical limit; incomplete output is rejected and a smaller scope can be retried. Invalid Unicode, changed boundary whitespace, missing/duplicate/reordered markers, wrong targets/language, unknown fields and malformed JSON cannot become writing.

Image captions, inline/block code, HTML comments, unsupported editor content, implicit newlines inside text runs and anchored scene annotations are deliberately unsupported for whole-scope translation. They cause an actionable error before generation instead of being omitted. Use a suitable selection or text-only scope. Translation language quality and cross-language placement of marked phrases need human review; a language metadata echo is not a language-quality guarantee.

The workspace flushes writing and planning and freezes the editor during work. It captures every intended saved page, synchronizes, confirms unchanged authored data, then pins the acknowledged local/cloud revisions and current account generation. The backend checks ownership, complete ordered section/page identities and the expected document version before execution and rechecks the revision afterward. AI uses existing authenticated entitlement/quota/provider paths. `AiUsageStatusDto.SupportsStructuredTranslation` prevents requests to older backends from consuming quota. Publish the capability and action/controller changes together before enabling whole-scope desktop translation; this task did not deploy them.

Version-2 `LocalAiHistory` records use the existing strict atomic history store and retain complete immutable Before/After snapshots; version-1 records remain readable without rewriting. Reviewed output is inert. Approval writes Applying intent before one atomic document-aggregate save, then Applied confirmation. Section copies receive new page/section/scene identities while original writing/planning stays intact. Document copies receive detached local document/project/section/page/node identities and retain planning, synopsis, ordering and rich writing; copies start local-only and require explicit cloud sync. The intended copy ID is saved before creation, so retry cannot create duplicate copies. Recovery snapshots and local history contain authored document data, not tokens or provider credentials.

**History → Finish approved translation save** handles interrupted confirmation or document saves after restart, using only retained approved snapshots. If later writing differs, recovery fails closed and the original can be saved as a separate recovery copy. Undo/Redo changes only affected page contents/languages or the added section/scene, preserving later unrelated edits; moved/changed affected pages or scenes block it. Undo of a document copy moves the unchanged copy to Trash, and Redo restores it. Local approved recovery can finish offline without issuing a new AI request. Later remote changes continue through the existing synchronization conflict checks; Apply does not silently overwrite cloud writing.

The legacy client's broader Apply implementation remains a separately recorded defect: section replacement flattens text into one editor/page, document replacement writes only the first page per section and skips missing markers, and its duplicate payload can fill missing sections with empty content. The desktop uses strict complete-page mapping rather than copying those paths. Prompt 13 must verify or repair that legacy web behavior before claiming full cross-host application parity.

See `docs/desktopai-uat.md` for current automated evidence, native/provider gates and the prompt-6 handoff. Run shared-project builds sequentially: isolated `BaseOutputPath` prevents launch-output locks, but concurrent builds still share `obj` intermediates.

## Desktop AI prompt 6 — Writing actions and continuation (2026-10-03)

**Writing tools** now offers shared `WritingOptions` presets, tone/length/preserve-term controls, dedicated selection Expand/Shorten/Change tone/Show, don't tell, their section counterparts, and a separate **Next paragraph at section end** target. Settings affect the actual `rewrite.selection` parameters; Change tone uses the tone setting, while the other dedicated revision operations keep their existing backend semantics. The grammar-labelled client preset is a neutral same-length rewrite with preserve terms, and the desktop states this explicitly. Summarize section retains its existing non-destructive append behavior.

`WritingCommand` and `WritingScope` distinguish selection replacement, complete section revision and continuation. `LocalWritingPanel` flushes writing/planning, freezes the editor, captures the complete saved source and synchronizes before generation. Selection preflight compares exact whole-page quality text and validates a single uniform text-block range; caret/source changes before Apply are rejected. Section actions send the additive version-1 `writing_structure` parameter through their existing registered actions. It contains cloud document/section IDs and every ordered page/text-run path. It reuses shared bounded editor text-run primitives, with no translation language metadata. The backend validates ownership, exact ordered pages and document version before execution, rechecks the version afterward and rejects incomplete/wrong output. Mapped JSON does not produce a flat section replacement operation.

Section revisions retain all paragraphs, headings, lists, tables, links, inline marks and supported block attributes. They retain paragraph/run boundaries rather than allowing model-authored structure. Images/captions, code, comments, anchored annotations and unsupported content require a suitable selected passage or manual editing; partial output never becomes writing. Limits are 60,000 UTF-16 text units, 2,000 runs, 1,000 pages, 100,000 serialized characters and two million raw source characters. Provider output-token limits may require a smaller scope. Punctuation/whitespace-only runs around marks can remain unchanged.

Continuation calls `propose.next-paragraph`, including every saved page in its section override and the synced scene beats, genre and selected craft focus. The action honors that override and applies its established recent-context tail limit (up to 2,500 characters). The client recommendation catalogue now lives in `WriterApp.Shared/WritingRecommendations.cs`; both hosts use it. Desktop recommendations are disclosed craft guidance for the new paragraph, not a claim that the full reusable-template workflows of prompt 9 are implemented. The shared echo helper removes the client's established 80-character-or-longer leading suffix overlap; empty/pure repeats, oversized output and meta wrappers are rejected. Line breaks normalize to one paragraph. Provider semantics, paraphrased recaps and prose quality still require human review. `previewContinuation` appends one inert schema paragraph after the last saved page's existing nodes. It never replaces a selection or an existing source block, including a final table/image or an empty final page.

`GET /api/ai/actions/writing-availability` returns configured/runnable actions filtered by the same feature-tier rules as execution, plus the complete-section capability. The device combines this with existing AI/subscription/quota status and rechecks availability before generation. Missing action/capability, inactive AI and exhausted quota show actionable disabled/error states; older backends fail closed without a generation request. Deploy the controller, revision action/provider handling and device together before enabling complete section revision. No deployment occurred here.

Version-3 local AI history adds an explicit local `SectionId`, typed-scope target names and immutable complete Before/After snapshots; versions 1 and 2 remain readable without bulk migration. Apply validates account/expiry, document/section/page identity and source versions, writes Applying intent before one atomic aggregate save, then confirms Applied. **History → Finish approved writing save** can resume an already approved result offline after restart, including interrupted confirmation, without regenerating. Later changed/moved targets block recovery/Undo/Redo. Undo changes only affected writing, retaining later unrelated pages/planning. History renders section prose by page and continuation as the new paragraph, while retaining raw validated evidence for recovery.

Canon/history readers allow atomic file replacement while retaining a complete snapshot. The atomic writer retries brief Windows sharing/access-denied replacements for a bounded 775 ms, using the same durable staging file; it never truncates the destination and still fails closed on cancellation/persistent errors. This fixes the cache-read/save race reproduced during this prompt's regression checks. Navigation also retains the editor freeze throughout panel work.

Current evidence and the prompt-7 handoff are in `docs/desktopai-uat.md`. Native Windows events, authenticated provider output, packaged and iOS acceptance remain separate gates.

## Desktop AI prompt 7 — Scene coaching contracts (2026-10-03)

The existing editor **Story → Scene card Coach** and the storyboard's selected-scene coach use the same `LocalAiPanel`. Its target selector covers summary, narrative purpose/role/intent, emotional beat, key events, open questions, status, POV character, setting/place, timeline event/marker, subplot tags, tags and references. `scene.find-open-questions` is the dedicated questions action; suggestion/refinement keep their existing authenticated action keys. Whole-card review requires selecting changed fields. Scoped review selects only its valid changed field and Apply still requires explicit approval. `SceneCoachingReview` in UI.Shared supplies escaped Original/Proposed comparisons and per-field errors; there is no second coach or client-library dependency.

`LocalSceneCoaching` captures complete schema text from every saved page, pins local scene/section and mapped cloud identities, synchronizes before generation, and supplies the current saved card plus a typed entity catalogue. Requests carry `scene_coaching_version=1`, `focus_field`, `current_scene_card`, `scene_entities_json`, the three bible JSON contexts and existing expected manuscript/canon versions. The backend validates this additive contract, retains ownership/feature/quota policies, checks canon before and after generation, and forwards focus/card/catalogue to the existing provider adapter. Version-1 scene requests support complete sections up to 100,000 characters instead of silently using the legacy 4,000-character truncation. Current card/catalogue each have a 100,000-character limit; combined canon is capped at 250,000. Backend and desktop must be updated together. No token budget or provider quality guarantee is implied by those character limits.

`SceneCoaching` is a presence-aware, bounded shared parser with typed fields/entity kinds. Omitted/null/empty values and empty lists preserve authored fields. Arrays are validated as a whole; malformed selected values are explained and cannot be approved. Duplicate/unknown properties and invalid objects fail closed. Status is restricted to Idea/Draft/Revised/Final, narrative role to the existing catalogue, intent to 1,000 characters, timeline marker to 120, other text to 20,000, tags to 100 entries of 200 characters, references to 100 links with notes up to 2,000 characters. Output is bounded at 100,000 characters/depth 16. IDs retain spelling, case and underscores; names never resolve IDs.

POV/place/timeline IDs require current, account/backend-scoped, source-matched bible entries with explicit IDs. ID-less canon remains readable but cannot become a link. References validate character/place/timeline, scene/chapter/part and section kinds against current project entities. Planning links persist mapped server IDs, while local IDs identify the Apply target; sync transports the original rich pages and approved card in its existing project transaction. Missing/stale canon is shown by the existing canon controls and cannot authorize new bible links. Ordinary narrative coaching and local authored planning stay usable; generation still requires connectivity/sign-in. A change in source, planning, account, cloud version or canon content invalidates the proposal. Metadata-only document acknowledgements remain tolerated.

Scene history uses additive version 4 with complete immutable Before, retained validated entity catalogue, local node/section identity, raw inert proposal and, after explicit approval, immutable selected fields/After. Applying intent and the full approved result are durable before the single aggregate save. **History → Finish approved scene save** resumes offline/idempotently after interruption and refuses later writing/planning changes. Undo/Redo changes only approved fields and preserves later unrelated fields/notes/pages; changed/deleted/moved targets are rejected. Before/After evidence and catalogues cannot be replaced. Link Redo in the reachable history workflow additionally validates current project/canon and asks for refresh when unconfirmed; narrative/status/tag history remains usable offline. Existing history versions 1–3 still load, including their existing original-copy recovery paths. History displays readable selected fields rather than provider JSON.

Parent/child rendering now explicitly refreshes loading/results for imperative storyboard coaching. Canon cards retain cached entries across parent rerenders; document/account changes still clear the previous identity. Prompt 8 should extend the existing synopsis coach with typed evaluation/questions, retaining these scene controls, scoped history and all prompt-2–6 behavior. Prompt 13 retains integrated native/provider and legacy-client repair gates.

## Desktop AI prompt 8 — Synopsis coaching (2026-10-03)

The existing **Story → Synopsis** coach offers Evaluate synopsis, Ask guiding questions and Improve a field. All three flush local writing/planning, synchronize the project transaction and capture all ten saved synopsis fields plus the author's coaching notes. Synopsis coaching requires a project manuscript with confirmed cloud identity; it does not require scenes, sections or pages. Empty synopsis evaluation/questions remain available to identify missing intent; suggesting text for a completely empty synopsis requires author details or coaching notes. Standalone, offline, conflict and missing-cloud states explain the prerequisite without changing local writing.

`DeviceAiApi` calls the existing `api/documents/{documentId}/synopsis/ai/evaluate`, `/questions` and `/suggest` endpoints, retaining their evaluation/questions/story-coach actions, context builders, feature gates and quotas. `SynopsisAiRequestDto` adds optional `ContractVersion`, `ExpectedDocumentVersion`, `SourceSynopsis` and `ExpectedProjectId`. Version 1 checks owned document/project identity, active sync version and exact saved synopsis before generation and again afterward. The captured synopsis must also equal the intended request snapshot. Content comparison supplements sync notifications; production planning triggers already advance sync state. The response confirms contract version, proposal/document IDs, source version, source synopsis, mode and chosen field. `AiUsageStatusDto.SupportsSynopsisCoaching` must be true; older backends fail closed before the generation POST. Deploy matching backend/device contracts together; this task did not deploy them. Version-0 web callers remain compatible, but do not gain the desktop's checked-source guarantees automatically.

Version-1 field suggestions require a strict JSON object with exactly `proposedText` and `commentary` strings. The existing StoryCoach action forwards the version to its provider adapter; versioned provider instructions separate complete field text from reasoning. Invalid, duplicate, unknown, empty or oversized suggestion fields are rejected. Evaluation/questions return bounded readable feedback and cannot contain applicable field text. Limits are 20,000 characters per source/proposed field, 60,000 total source/analysis characters, 2,000 coaching-note characters, 20,000 commentary characters and 100,000 serialized suggestion characters. These bounds do not establish provider output quality.

`SynopsisCoachingFeedback` in UI.Shared renders escaped headings/text, questions, Original/Proposed comparisons, separate commentary and a keyboard-accessible disclosure of the exact analyzed fields and coaching notes. Selection changes after review do not retarget Apply. `LocalSynopsisCoaching` applies only the pinned field after current-account/expiry and complete local-source checks; no manuscript content is rewritten. The existing shared synopsis field editor and project synchronization retain all other fields and authored notes.

Version-5 local AI history retains immutable Before/approved After snapshots, field identity, commentary and coaching notes. Reviewed analysis is inert. Apply persists complete Applying intent before the atomic local aggregate save. History can finish an already approved interrupted synopsis save offline after restart, including interruption after saving but before confirmation. Later writing/planning changes block resume; field-scoped Undo/Redo preserves later unrelated changes and rejects later edits to the affected field. Versions 1–4 remain readable without a bulk migration. Cloud generation history records actual proposed field text, separate explanation and source version; cloud/local history integration remains prompt 10.

Final checks and the prompt-9 handoff are in `docs/desktopai-uat.md`. Native Windows and authenticated provider acceptance remain separate gates.
## Desktop AI prompt 12 — Guided first use (2026-10-03)

The library offers **AI practice guide**, and the navigation keeps `/ai-guide` reachable for returning users. `AiFirstUseGuide` in UI.Shared provides five keyboard-accessible steps with shared guidance and focus on each changed heading. `DesktopAiGuide` adapts these controls to the local lifecycle. The practice editor hosts the guide in the existing scrollable context panel, alongside its normal Writing, History, Story and Prompt Library tools. There is no editor clone or tutorial-specific Apply path. Links use the existing `panel`/`view` navigation contract, and cover guidance opens `/documents/{id}/cover`. Reading, skipping and finishing do not require a successful AI request; completion records guidance progress, not provider acceptance.

`LocalOnboardingStore` persists version-2 progress under `<local-document-root>/ai-onboarding/<backend-account-scope>.json`. Anonymous guidance has a separate guest scope. Stable guide/document IDs, atomic replacement, per-scope process locks and revision compare-and-swap prevent competing windows from losing completion or replacing the practice identity. Version-1 state gets an immutable source backup and retains linked practice identities as already created, so migration cannot seed them again. Unknown, mismatched, oversized or corrupt state is preserved with an error and retry; it does not block ordinary local library/editor flows. State is local guidance only and does not synchronize server onboarding completion or entitlements.

Only **Open practice project** reserves a durable document ID and calls `CreateOnboardingPracticeAsync`. A new, labelled sample project/manuscript is created under the existing document-store lock with a `desktopAiPracticeGuide` provenance marker. Retrying an interrupted creation opens that same identity only when the marker matches. Revisit/restart, empty or edited writing, renamed samples and Trash never trigger reseeding. A registered missing file requires recovery. The marker remains local extension data across device sync. It is not server demo metadata or authorization. Other manuscripts cannot be overwritten or silently adopted as seeded content.

When a guest signs in during practice, the new account has independent guidance. **Continue guide on this practice project** deliberately links the existing labelled local sample to that account's guidance, without writing the manuscript or starting sync. Account/backend changes clear old guidance and pending refreshes are canceled on disposal. Local documents retain their existing device-wide library lifecycle; AI proposals/history/canon/covers keep their existing account isolation.

Practice actions use the production `LocalWritingPanel`, save-before-request, manual sync enablement, selection/preset contracts, Original/Proposed review, explicit Apply/Dismiss and durable History Undo/Redo. Prompt 15 supersedes the original marker-based request hint: ordinary labelled local practice uses its real plan/quota, while an explicit Run Demo on the separately mapped server workspace requests the authenticated demo policy. A local sample has no server-owned authorization. Expired identity, offline/provider errors, quota, cancellation and stale Apply use the same production paths as other writing requests.

Verification and the prompt-13 acceptance handoff are recorded in `docs/desktopai-uat.md`. Native Windows focus/selection/drawers, real account switching and deployed provider/demo allowance remain separate live gates.

## Desktop AI prompt 13 — Integrated verification (2026-10-03)

The current final comparison, acceptance profile, defect ledger, exact commands and release prerequisites are in [desktopai-gap-analysis.md](desktopai-gap-analysis.md); fresh checks are appended in [desktopai-uat.md](desktopai-uat.md). The full Release suite passed 1,434 tests, both shipped editors passed 60 harness checks, and final server/client/device/Windows/iOS-managed builds passed. These results do not establish native or authenticated provider acceptance.

`DocumentWorkspace` passes explicit cloud enrollment to `LocalWritingPanel`. Writing availability refreshes when that enrollment or account identity changes, clears on sign-out, and uses cancellation plus generation/refresh tokens to discard previous-account responses. Refresh does not generate AI. Unlinked documents still require explicit cloud sync. An enabled UI remains subject to the normal save/sync, source, entitlement/quota and provider checks at execution.

Ordinary SQLite web saves with version history no longer attempt unsupported SQL `DateTimeOffset` ordering: pruning filters the owned page in SQL and sorts its versions in memory. Existing list/latest behavior and deterministic pruning remain; no database migration is needed for this repair.

The legacy **web** section/document translation proposal does not carry complete page/run structure or recoverable aggregate persistence. Its replacement and duplicate Apply controls now disclose unavailability, and handlers refuse before writes or applied reporting. Review/copy/discard and selection translation remain available; supported desktop structured translation remains independent. Do not remove this restriction until P13-003's complete web workflow and partial-failure/reload/history checks pass.

For native acceptance inspect the current process `Path`, rebuild the intended output and use a separate Development build with `ProsaValidationDataDirectory` set to an absolute isolated directory and `ProsaApiBaseUrl` set to the authorized test backend. The p13 default Release candidate was inspected but not launched. Windows native events, real identity/provider/deployed database, packaging, Mac/iOS and physical publishing gates remain open; follow the concrete handoff rather than relying on historical test counts.

## Desktop AI prompt 14 — Checked web translation (2026-10-03)

Production web section/document translation now uses `DocumentEditor.Translation.cs`, shared `TranslationStructures`, and the same `captureTranslation`/`previewTranslation` functions exported from the web editor bundle. Save is flushed before capture and approval; the current editor must match saved content. The server source snapshot includes account identity, owned document/project, language, ordered sections/pages, document sync version and an authored graph fingerprint. Every page, including blank pages, is reviewed as inert Original/Proposed text. Provider output contains only text runs; `WebTranslationHtml` substitutes those into the saved supported DOM without accepting provider HTML. Unsupported images/code/comments/annotations and DOM boundaries that differ from schema capture cause actionable refusal before generation. Adjacent equivalent inline tags or incidental whitespace may need normalization by saving in the editor; originals remain unchanged on refusal.

The additive owned API is `api/documents/{documentId}/structured-translations`: `GET source`, `GET operations`, `POST approve`, `POST operations/{operationId}/commit`, and `POST operations/{operationId}/original`. Source discovery verifies the approval table exists, so an unmigrated backend fails before provider execution. The normal translation actions retain authentication, entitlement/quota checks and checked revision revalidation; `web_translation_source` additionally verifies the captured saved HTML before execution. Approval verifies the owned saved AI proposal, exact request/result, language, version, fingerprint and one-hour proposal age. It persists immutable original recovery evidence and approval in a serializable transaction. Each proposal has at most one operation; replay of the exact request returns the same receipt, while changed operation contents/modes are rejected.

Commit uses a second serializable transaction and the existing `DocumentSyncClocks` lock. Replacement preserves page/section IDs, titles, rich formatting and unrelated planning. Copies retain the ordered section/page graph and scoped scene hierarchy with new IDs, language/translation-group metadata and scene content mirrors; document copies are separate `Other` documents in the same owned project, preserving its primary manuscript. The receipt and all writing persist atomically. IDs remain stable on commit replay. Cancellation/failure before commit does not write pages; interruption after durable approval can be resumed without generation. Lost commit acknowledgement pauses editing until recovery confirms the receipt or the same approved operation is explicitly finished. Ordinary writing is not reported Applied from an HTTP failure.

The recovery drawer reloads owned approvals/receipts after document load. **Finish approved save** rechecks source and flushes/fixes editing before retry; changed writing refuses. **Recover original as separate document** uses a stable reserved ID and retained original snapshot, preserves current writing and can be retried after restart. Existing edited/trashed recovery copies are not reseeded. This explicit recovery-copy workflow is web aggregate recovery; it does not offer aggregate in-place Undo/Redo. Confirmed receipts contribute to normal cloud history with `CanCloudUndo/CanCloudRedo=false`. No aggregate snapshot is passed to legacy page-HTML history commands. Other web actions retain the prompt-20 durable-reporting gate.

Apply SQLite migration `20261003183221_WebTranslationOperations` or SQL Server migration `20261003183237_WebTranslationOperationsSqlServer` with the new backend. They add only the version-1 approval/receipt table and owner/document/proposal indexes. Keep journal rows while clients can reconcile/recover; no automatic retention policy was added. SQLite upgrade was exercised locally; SQL Server model and idempotent script generation were checked without connecting to a server. External deployment/migration is not performed by this prompt. Old plain-text broader proposals still receive the prompt-13 safety refusal; selection translation and copy/discard remain available.

### Prompt 14 continuation — asynchronous context isolation (2026-10-04)

Capture and every final editor-preview await recheck account generation, document, section and backend before issuing a provider request or exposing a proposal. Approval receipts must match the reviewed operation, proposal and source document before commit. Loaded recovery receipts retain their backend origin; Finish approved save and Recover original refuse a receipt from another account/document/backend before a mutation request. Commit and recovery responses recheck the captured context before updating Applied state, reloading writing or navigating.

An uncertain operation retains its original backend through cancellation. Return to that backend and reload recovery to confirm its result; a matching operation/proposal ID from another backend cannot establish persistence. Returning to the original backend reconciles the existing terminal receipt without another provider request or commit. These guards change client lifecycle handling only; the existing server journal schema and migration prerequisites remain the same. Fresh automated evidence and independent native/provider gates are recorded in the 2026-10-04 UAT entry.

## Desktop AI prompt 15 — Owned online onboarding demo (2026-10-04)

The local guide and guest practice retain their offline lifecycle. `OnboardingDemoPanel` adds deliberate **Create or reopen online demo**, **Refresh server demo status** and **Complete online onboarding explicitly** choices. Opening the guide performs no bootstrap, sync or provider request. Guest continuation links only the existing local practice; it does not upload it. Online choice imports a separate cloud workspace using the production sync engine. Existing manuscripts and edited/empty guest samples are never adopted.

The authenticated version-1 contracts in `OnboardingDemoContracts` use `/api/onboarding/demo/status`, `/bootstrap` and `/progress`. Status returns immutable owned project/document/section/scene IDs, the profile revision fingerprint, permitted action/scope, availability reason, expiry, request-used state and successful proposal ID. Bootstrap creates a fresh reserved project once per owner under a transaction/clock lock and replays those identities on later choices, independent of titles or an existing empty project. It never reseeds an edited/empty/missing page. Missing/trashed reserved identities require explicit restoration; retry cannot create a replacement. Status discovery does not create a demo.

Both web and desktop use the same strengthened server eligibility: incomplete onboarding, the owned version-1 reservation and linked demo metadata, active owned writing, exact `tighten.section` action/section, and one unused provider attempt within seven days of reservation. `GrantLifetime` is server policy; no local marker/request flag/usage DTO creates allowance. Immediately before provider work the controller rechecks policy and atomically consumes the reservation. Concurrent calls cannot receive a second free request. An ambiguous or failed provider attempt remains used, while `ProposalId` records only successful validated generation. Request use, request success, proposal Apply, local guide completion and server completion are distinct. Ordinary AI retains its real plan/quota; an ineligible demo flag is removed before billing orchestration.

Free-account sync uses additive `/api/onboarding/demo/documents` changes/download/operation routes restricted to the server-reserved document. `DeviceSyncApi` additionally requires a durable explicit desktop choice in this backend/account's guide before using them, including when web already created the grant. Normal v4 sync and other documents retain their paid entitlement. The same aggregate validation, owner/project checks, versions, immutable operation receipts, recovery, source fingerprints, Trash/deletion and conflict handling apply. Used/expired/completed AI allowance does not prevent saving the linked writing. Demo-only routes refuse recreating a missing/deleted sync aggregate. Paid accounts retain normal sync; no unlinked local draft is silently enrolled.

`LocalOnboardingStore` writes version 3 with separate local-practice and local/cloud-demo mappings, a durable bootstrap choice ID, cached server status and pending progress request. Versions 1–2 remain readable; v1 retains its immutable migration backup, and a v2 explicit update atomically upgrades to v3 without changing practice identity/writing. Files and new typed progress/bootstrap HTTP bodies/status parsing are bounded to 16 KiB. Unknown/corrupt versions or mismatched identities remain actionable errors. Scope hashes bind backend/account; generation checks reject late account responses and process locks/revision CAS protect competing windows. Cached policy is information only; execution refreshes authenticated server eligibility.

Explicit online open records server step 2 only after import. Explicit completion records step 10/completion independently of AI or guide success. Progress uses expected profile revision plus owner/operation/request-hash receipts in a serializable transaction. Exact replay returns its original acknowledgement; altered operation reuse or stale profile rejects. Desktop retains a pending request across lost acknowledgements, obtains fresh status after receipt replay, and merges completed server progress without regressing it. Web step writes are monotonic at the database boundary. A known stale rejection refreshes status before the user retries the intended completion.

Run Demo forces the server-authorized section through normal save/source/sync/structured revision, escaped review, explicit Apply/Dismiss and durable local History Undo/Redo. The ordinary zero-quota availability DTO remains unchanged. The guide's local Next/Skip/Restart never completes server onboarding. Old backends require the matching additive routes and migrations; failures preserve local writing and pending identities rather than falling back to unchecked requests.

Backend prerequisites: SQLite `20261004064041_OwnedOnboardingDemo` or SQL Server `20261004064042_OwnedOnboardingDemoSqlServer`, after the preceding migrations. These add owner-singleton reservations, unique cloud document IDs and owned progress receipts; existing writing/profile/sync versions are preserved. Legacy metadata-only demos are not auto-adopted or granted a free request. Keep reservations/receipts while retry and no-reseed recovery are offered; no pruning was added. SQLite upgrade and SQL script/model checks are local evidence only. Real authenticated web/native/provider allowance and hosted SQL Server concurrency acceptance remain open; see prompt 15 UAT.

## Desktop AI prompt 16 — Owned remote cover assets (2026-10-04)

This extends the historical prompt-11 inline-only boundary. The additive authenticated `POST /api/covers/assets/materialize` accepts `CoverAssetSource` version 1: owned cloud project/document IDs, expected project metadata revision, current document sync version and SHA-256 of the saved remote reference. There is no client URL field. The server resolves the current owned project's saved cover itself; generation resolves only the existing server provider response. Web generation now names its current owned project/document/metadata revision. Existing generation plan/quota checks and checked device-source validation remain authoritative. Missing asset routes fail with upgrade guidance rather than an arbitrary downloader fallback.

Remote fetching is disabled by the empty default. Configure the backend's real, narrowly scoped provider storage account/container prefixes through normal deployment configuration, for example:

```json
{
  "CoverAssets": {
    "TrustedStoragePrefixes": [
      "https://your-provider-storage.example.com/your-owned-container/"
    ]
  }
}
```

The hostname above is illustrative, not an enabled provider or a deployment change. Prefixes must use HTTPS port 443 and an exact account/container path ending in `/`; root-only/wildcard/query/userinfo/fragment prefixes do not grant trust. References are at most 4,096 characters, must match the configured hostname/path and cannot use literal IPs, userinfo, fragments, encoded paths or backslashes. Each of at most two redirects repeats trust and public-address checks. DNS sets are bounded to 16 addresses and rejected if any address is private/reserved. Production connections pin that checked address set while retaining normal hostname TLS validation; automatic redirects, proxy, decompression and cookies are disabled. No user bearer token or provider credentials are forwarded to storage. The overall fetch is bounded to 30 seconds with a 10-second connection bound.

Supported remote bytes are static PNG, exact `image/png`, at most 2 MiB, with no HTTP content encoding. Header and streamed length, PNG signature/chunks/CRC/dimensions/raster/expansion, APNG and unknown critical encoding are checked before persistence. JPEG/WebP/GIF/SVG are refused with PNG guidance; no transcoding or decoder dependency was added. Provider JSON reading is separately bounded to 16 MiB success / 64 KiB error bodies. Materialization requests are limited to 16 KiB, device receipts to 3 MiB and local asset files to 4 MiB. Expired/deleted storage references return an actionable 410; unsupported storage/bytes return 422, source conflicts 409 and temporary storage failures 503. Failed fetches preserve the saved cover and existing cache.

`CoverAssetResponse` returns its exact request source plus versioned asset ID, owned project/source-document identity, source metadata revision/document version, original remote reference, media type, byte length, SHA-256 and PNG bytes. The server retains immutable bytes/provenance under a unique owner/project/reference hash. Ownership/current source is checked before network work and again under a serializable transaction/clock lock, including references registered to a foreign owner/project during the fetch. Replays return the original asset identity/bytes; an expired reference does not require refetch once materialized. Authorization still rejects deleted accounts, documents/projects or changed sources. Fetching never saves project cover metadata or manuscript/planning content.

`LocalCoverStudioStore` writes draft version 2 while reading version 1. Its independent version-1 asset files live under `<cover-root>/<backend-account-scope>/assets/<local-project-id>/<reference-hash>.json`. They preserve cloud/local project mapping, source provenance and exact validated bytes, with bounded strict JSON, cross-process leases and atomic replacement. Unknown/corrupt versions and conflicting immutable identities remain intact with actionable errors. This project cache is available to sibling documents and survives draft replacement/restart/offline operation; account/backend switching clears private previews and cannot select another scope's cache. No pruning was added while retry/recovery remains available.

The shared `CoverAssetStatus` offers deliberate **Cache owned remote cover** and explains that caching leaves the saved cover unchanged. The existing production concept review/selection/**Save as project cover** flow then saves the exact PNG with metadata CAS, prior-cover recovery and normal sync. Explicit Save makes the image authored device-wide project metadata under the existing local-library lifecycle. Restoring the prior remote cover uses its retained reference/cache. Generation responses add aligned optional asset identities without replacing legacy inline concept contracts.

Publishing can embed the matching scoped cached PNG while the project's saved metadata still contains the original remote reference. Missing/wrong-scope/hash/source cache receives cache-first guidance without network fetching. Preview/export use the same bytes in HTML/DOCX/EPUB and the PDF layout source. The actual Publishing page captures account generation across cache/render and rechecks before native file saving, including after asynchronous PDF rendering. Sign-out clears a private remote snapshot. Choosing/saving an ordinary local PNG retains existing behavior.

Apply SQLite `20261004072914_OwnedCoverAssets` or SQL Server `20261004072915_OwnedCoverAssetsSqlServer` after preceding migrations when deploying the matching backend. These add only the owned asset table/index; there is no automatic fetch, URL rewrite, existing-cover migration or pruning. SQL Server uses `nvarchar(max)` for the logically bounded 4,096-character reference and `varbinary(max)` for the application-bounded PNG. Isolated SQLite upgrade and SQL Server script/model checks passed; no external database was changed. Real storage configuration/TLS/redirect/provider behavior, hosted SQL Server concurrency, native Windows/iOS and physical file/PDF export acceptance remain open. Fresh tests, captures, build results and the prompt-17 handoff are in [desktopai-uat.md](desktopai-uat.md#prompt-16--owned-remote-cover-materialization-2026-10-04).

## Desktop AI prompt 17 — saved writing outline context (2026-10-04)

WritingOutline is a typed, canonical saved snapshot shared by web and device selection, section, continuation and reusable writing. It pins cloud document/project IDs, section/node IDs and order, source version and a SHA-256 planning fingerprint. Limits are 1,000 sections and 1,000 nodes, 200 characters per title, 20,000 total title characters and 262,144 JSON characters; unsupported or oversized context is refused without truncation. Prose, notes, synopsis and sibling-document nodes are excluded. Canonical escaped JSON is inert provider context. The existing continuation tail remains bounded to 2,500 characters.

The authenticated writing-outline endpoint resolves the same owned saved manuscript. SavedOutlineContext capability is required by the device, including the editor top menu. Both hosts capture after save/sync, bind the receipt to the proposal and reject changed planning before generation completion and Apply. Web retries retain the original snapshot; account/backend/section changes invalidate review. Metadata-only acknowledgements preserve an unchanged planning fingerprint. Cached local extraction works offline; generation still needs authentication and quota. No schema migration or editor algorithm change was introduced. Missing capability/context requires backend update or synchronization, without reduced-context fallback.

## Desktop AI prompt 18 — owned cover variations and adjustments (2026-10-04)

Both studios now support variation, darker, brighter, cinematic and minimal operations through the configured server provider's real `images/edits` multipart adapter. The selected immutable owned PNG is the image input; operation-specific composition/lighting instructions are sent with the bounded cover brief. This is source implementation, not real-provider visual acceptance. The default configured `gpt-image-1` is retained. The adapter advertises only its explicit GPT Image model allowlist; other models/configurations return an unavailable capability before entitlement evaluation or provider transport. Keys remain server-only. Edit generation evaluates the existing cover-image entitlement/rate/token quota and records actual provider input/output usage. A result rejected later for stale source or invalid media can still be billed by the provider.

New authenticated routes under `api/covers`: `edit-capabilities`, `generation-source/{project}/{document}`, `edit-source`, `edit`, `edit-save`, `edit-save/{operation}`, `edit-save/{operation}/restore` and `edit-recovery/{project}`. Inputs identify the selected owned asset/hash, saved document version, project and expected metadata revision; they accept no source URL or source image upload. Source resolution permits only an owned generated concept or the exact saved project reference. Ownership/deletion/revisions/hash are checked before and after generation and before Save. The provider returns one new static PNG; unchanged bytes, invalid/multiple/oversized results are refused. Prompt descriptions remain at most 4,000 characters and style fields 80; images retain the existing 2 MiB, 8,000-pixel and 32-megapixel bounds. Provider JSON and edit responses are bounded to 4 MiB, source/capability requests to 16 KiB. JPEG/WebP/GIF/SVG conversion remains unavailable.

Owned inline generated/saved PNGs receive additive version-2 asset identities with `urn:writerapp:cover:{SHA256}` provenance. Version-1 trusted HTTPS assets remain compatible and never treat that URN as a network fetch target. No existing asset row is rewritten. Server-issued version-1 proposal receipts bind the operation/source to the proposed asset; clients cannot relabel another result as a valid approval. Explicit web Save durably records its original cover and immutable approval before a serializable metadata CAS, then confirms a stable operation receipt. Retry/reload reconciles the committed receipt without generation. Restore checks owner, current cover and expected revision. The latest owned recovery remains discoverable after dismissing or starting another preview. Original/proposed selection defaults to Original; editing never saves a cover automatically.

The device stores validated original/proposed bytes and the edit receipt in its existing version-2 scoped draft, with atomic local cache and metadata Save/recovery. Online Save rechecks both owned assets and current cloud source; offline Save uses the captured local source and queues ordinary project sync. A disconnected device cannot establish that a cloud asset has since been deleted; eventual cloud persistence is separate from local Save. Web review cache version 1 is scoped by backend/account/project, bounded and validated on reopen. Cache/storage refusal preserves the old preview. Explicit approved intent survives lost acknowledgement; web Save/Restore require connectivity. Offline cached preview requires an already resolved signed-in account context; a fresh offline browser without that identity must reconnect to resolve its account before opening the scoped cache. A browser cache is not a server identity grant.

Backend deployment requires the prompt-16 owned-asset migrations plus SQLite `20261004085420_OwnedCoverEditSaves` and `20261004090300_OwnedCoverEditProposals`, or SQL Server `20261004085422_OwnedCoverEditSavesSqlServer` and `20261004090303_OwnedCoverEditProposalsSqlServer`. These create only the versioned save/proposal tables and indexes. No external database was migrated. Retain proposal/save receipts and immutable bytes while recovery/retry is offered; no pruning was introduced. Trusted remote sources still require explicit `CoverAssets:TrustedStoragePrefixes` configuration.

The adapter follows the official [Image API image-edit documentation](https://developers.openai.com/api/docs/guides/image-generation). Current provider model availability and darker/brighter/composition quality require authorized deployment/provider review; synthetic test images establish lifecycle and byte correctness only.

## Desktop AI prompt 19 — checked production web AI (2026-10-04)

Production web writing/custom presets, translation, quality, consistency, canon, scene coaching, synopsis and all four storyboard actions now use additive shared checked-source contracts. Web lifecycle stays in Client; contracts stay in Shared and existing application contracts; device persistence remains in Device.Shared. Provider credentials, ownership, entitlement and quota checks remain on the server. No new persisted entity or migration is introduced in this prompt; keep the preceding prompt-14–18 migrations when deploying their existing recovery/demo/cover workflows.

Authenticated `GET api/ai/actions/web-source/{documentId}?sectionId=&pageId=&sceneId=` returns a no-store version-1 `WebAiSource`: hashed account identity, owned active document/project and optional owned section/page/scene, current document version and content fingerprint. Its typed successful response is required capability evidence; `/api/ai/status` additionally advertises `SupportsWebCheckedSources`. Missing/invalid routes or receipts refuse generation/Apply with save/synchronize/backend-update guidance. Older external requests may omit `WebSource` where existing routes permit; this preserves legacy wire behavior, not checked semantics. Production UI does not fall back to those requests or apply their unchecked proposals.

The source fingerprint covers owned saved rich pages, section titles/order/language/purpose, project authored metadata, planning node hierarchy/metadata, scene contents/cards, section/page/scene notes, legacy outline, ten-field synopsis and canon content/source hashes. It excludes sync acknowledgement versions, timestamps and internal primary-document housekeeping. Therefore an unchanged-content acknowledgement does not invalidate approval, while authored changes without a sync token change still do. `DocumentVersion` remains available for existing device/legacy and typed canon contracts. Client leases pin resolved account and backend; source discovery/confirmation, provider response and review/Apply all recheck identity. Responses echo the exact request source. Proposal dates allow at most one hour of age and one minute of future clock skew. Cancellation is rechecked after transport/provider completion, including providers that ignore cancellation.

Bounds: at most 1,000 sections/pages/project nodes/legacy outline nodes per kind; saved page and scene content each at most 2,000,000 aggregate characters; canon at most 250,000 aggregate characters; section/page/scene notes each at most 2,000,000; serialized fingerprint input at most 4,000,000 bytes. Checked action original/surrounding/outline and proposed text each cap at 100,000 characters, parameters at 500,000 serialized characters, action body at 2 MB. Client typed response reads cap at 2,000,000 bytes and JSON depth 32; individual coaching canon snapshots cap at 100,000 characters. Quality results cap at 1,000 issues and existing range capabilities. Prompt-17 outline/title and continuation limits still apply. Oversized or unsupported content remains intact with actionable guidance.

Checked scoped mutations send `X-WriterApp-AI-Source` containing the source JSON (maximum 4 KiB). The global `WebAiMutationFilter` accepts it only for scoped page/scene-content/card/synopsis saves, approved storyboard node creation and AI history Undo/Redo. It clears tracked reads, uses the configured EF execution strategy and a serializable transaction, locks the sync clock, verifies current owned content and route identity, then commits only a successful action. Success returns `X-WriterApp-Checked-Save: 1`; the UI requires this receipt plus the expected saved identity before claiming persistence. A legacy backend's ordinary 2xx is insufficient. Deployed database isolation/retry behavior still needs prompt-22 validation; automated persistence checks use SQLite.

Scene/section-card DTOs also accept an optional `ExpectedCardFingerprint` covering normalized card values without timestamps. Ordinary authored card saves and AI Apply compare this inside the transaction, preserving all links/tags/status and unrelated fields. Section-card ordinary structure undo is recorded only after commit. Synopsis normal Save accepts `ExpectedSynopsis` with its complete last saved ten-field snapshot and performs owned serializable CAS; AI Apply combines this with the common source. All three dedicated synopsis modes carry shared typed source/focus/mode/time contracts; questions/evaluation never auto-apply.

Consistency and scene coaching bind existing typed owned `DeviceBibleSnapshot`/expected canon versions and shared typed scene coaching v1. The server rebuilds current card/entity IDs and all four storyboard contexts from owned saved planning rather than trusting a client allowlist/context. Canon refresh reuses `DeviceBibleRefreshRequest.WebSource` with before/after checks in commit; strict legacy/device checks remain when this field is absent. Stale canon requires refresh. Web section writing captures every saved page/run through the shipped editor, requires complete typed writing structure and reuses prompt-14 durable approval/commit/recovery with additive `IsWriting`, preserving language and original copies. Existing stored translation approvals retain their serialized identity because the new false flag is omitted.

Explicit limitations: unmapped section summaries, section tools without a reusable typed prompt, unsupported aggregate scene targets or rich nodes refuse Apply. Scene-route quality directs users to the linked manuscript page. Consistency anchors retain the web current-editor-page scope. Cached legacy quality findings remain readable but need a new checked run before Apply. Scene AI-history cloud Undo/Redo remains unavailable; ordinary editor Undo and original-copy recovery remain usable. A rejected checked Save retains the draft/review and instructs reloading before retry. General web applied-event reporting is still one-shot; prompt 20 adds the durable browser outbox independently of these save receipts.

See the [per-flow evidence table](desktopai-gap-analysis.md#prompt-19-per-flow-checked-source-contracts-and-evidence-2026-10-04) and [dated UAT](desktopai-uat.md#prompt-19--checked-production-web-ai-2026-10-04). Deploy matching checked routes/capabilities before enabling these UI flows. No external migration, deployment, provider call or native launch occurred during implementation. Use an authorized Development backend and isolated data/native authentication for prompt-22 acceptance.
## Desktop AI prompt 20 — durable browser applied history (2026-10-04)

`WebAiHistoryIntent` and `WebAiHistoryReceipt` are version-1 shared contracts. The web editor records approval in IndexedDB **before** issuing a checked content write. `web-ai-history-outbox.js` uses one immutable transactional row per operation, scoped by resolved account key and backend URL. Application/proposal/operation IDs, sequence and predecessor, owned source/target, and approved original/proposed evidence remain inspectable after reload. A preview, failed save or merely prepared row cannot report Applied. Prepared rows display “Apply intent”; saved outcome and cloud delivery are separate states.

The additive authenticated routes are `POST api/ai/actions/history/web/operations`, `GET operations/{id}`, `POST operations/{id}/report` and `POST move` under the same prefix. Prepare validates the owned stored proposal, source, target, date and immutable payload. Checked page, scene-content and scene/section-card PUTs carry `X-WriterApp-AI-Operation`; normal ownership/source CAS, authored content and the operation's committed timestamp/typed save response persist in the same serializable transaction. Matching checked-save and operation acknowledgements confirm persistence. Receipt inspection reconciles a lost acknowledgement before reporting. Idempotent reports validate the exact approval hash and ordered predecessor; only reported operations appear as confirmed cloud history. Legacy routes remain compatible, and no browser row is inserted as a native device receipt.

Page/scene-content transitions reverse exact approved rich HTML only after current owned source/content checks. Reading `move` does not mark history or mutate content: the subsequent durable intent and guarded content save establish Undo/Redo. Later edits refuse replay. Scene/section-card evidence is normalized original planning plus the approved typed update request and authoritative typed save response; normal controllers retain authored fields and normalization. Card receipts cannot authorize manuscript HTML Undo. Multi-page writing/translation uses a safe reference to prompt-14's owned atomic operation and recovery receipt; aggregate snapshots cannot become page HTML. Inspect persisted planning or use explicit aggregate original-copy recovery for these targets.

`DocumentEditor.HistoryDelivery.cs` reconciles on history load/reload, manual Retry and a component-owned 30-second timer. It never automatically reapplies an uncommitted draft. Concurrent tabs share transactional IndexedDB rows; a late tab cannot downgrade confirmed delivery. Network/report rejection retains intent and successfully saved writing. Reporting requires the history plan, Undo/Redo its existing plan; history retry consumes no provider quota and cannot grant generation access. Deletion/sign-out/account/backend changes invalidate current leases and hide foreign scoped data; returning to the original authorized account permits reconciliation. Expired, malformed or unsupported capabilities refuse new AI Apply while ordinary writing remains available. No automatic pruning removes unresolved recovery evidence.

Bounds: 1,000 rows and 16 million serialized characters per browser scope, 2 million characters per intent; 750,000 characters per before/after value, 10,000 transitions per application, 20 million characters for scoped list inspection, 2 MB prepare/report request bodies, and 1,000 committed operations per document for server transition inspection. Limit/storage failures occur before the AI content write and retain existing data. These are supported limits, not a claim that arbitrarily large manuscripts fit browser storage.

Deploy matching backend/client contracts with **SQLite `20261004112925_WebAiHistoryOperations` or SQL Server `20261004112927_WebAiHistoryOperationsSqlServer`**, in addition to preceding migrations, through the configured authorized migration process. Each creates only the owned operation table, unique application/sequence index and owner/document index. SQLite upgrade preservation and SQL Server generated script/model checks pass; no external database migration was performed. Mixed older backends fail the additive prepare capability rather than permitting unchecked Apply.

Evidence and the next same-checkout prompt are in the dated prompt-20 entry in `desktopai-uat.md`. Native Windows interaction, deployed SQL Server concurrency, full authenticated web-shell/provider and physical export acceptance remain separate prompt-22 gates. Prompt 21 implements native iOS identity; managed Windows compilation alone cannot establish iOS execution.

## Desktop AI prompt 21 — native iOS identity (2026-10-04)

`WriterApp.iOS/Authentication/IosMsalSession` uses pinned `Microsoft.Identity.Client` **4.90.1**, its native iOS Keychain token cache, system authentication presentation on the main thread, explicit account selection and silent token renewal. It does not use the manuscript WebView for authentication. `IosDeviceIdentityClient` implements `IDeviceIdentityClient` through a narrow native-session/secure-selection seam, registered before `AddWriterAppDeviceCore`; the shared unavailable fallback is no longer the iOS implementation. OAuth code/state/nonce/PKCE and expiry/refresh remain MSAL responsibilities. The adapter additionally verifies the configured tenant, stable home-account identity, expiry, cancellation and host lifetime. Raw access tokens never enter Razor/JS, document files or diagnostics.

Build configuration and native resources must agree:

| Setting | Contract |
|---|---|
| Bundle ID | `com.prosa.writer.ios`; the build/runtime guards require updating callback, entitlements and `IosIdentityOptions` together before changing it. |
| Redirect | `msauth.com.prosa.writer.ios://auth`, declared in `Platforms/iOS/Info.plist`; Windows `http://localhost` remains Windows-only. |
| Keychain | `WithIosKeychainSecurityGroup("com.prosa.writer.ios.msal")`, with signed `$(AppIdentifierPrefix)com.prosa.writer.ios.msal` entitlement plus the app group used by MAUI SecureStorage. No broker package or broker cache sharing is enabled. |
| Environment/backend | Embedded `ProsaEnvironment` (`Development`, `Staging`, `Production`) and `ProsaApiBaseUrl`; HTTPS required outside Development loopback. Release ignores process environment overrides. |
| Public identity settings | `ProsaIosAuthSettingsFile`, default `WriterApp.iOS/native-auth.json`, embedded as `Prosa.IosNativeAuthentication`. Supply the authorized tenant, authority, **iOS public-client ID**, fixed redirect and API scopes. Debug Development permits existing `WRITERAPP_AUTH_*` overrides after the same validation. |

The tracked iOS `ClientId` is **empty intentionally**: no authorized iOS registration was supplied, and the Windows client registration is not assumed to have an iOS redirect or permission. The adapter is implemented and compiles; the default host offers ordinary guest/offline writing until configured. Invalid/missing public auth values or malformed JSON disable sign-in with normal guidance. No client secret or provider credential belongs in a native settings file. Existing backend bearer validation still requires the approved `azp` client in `NativeAuth:AllowedClientIds`, expected issuer/tenant/audience and delegated `access_as_user` scope. Configuring the external app registration or deployed backend allowlist remains outside this implementation authorization.

MSAL stores credentials in the signed native Keychain. A **version-1 secure selected-account marker** additionally hashes environment, backend, tenant/authority/client/redirect/scopes into its key. Silent restore requires exactly the marked home-account ID in the native cache; it never guesses a cached account from another backend/environment, display name or token. Interactive switching changes the marker only after valid uncancelled acquisition. There is no custom refresh-token persistence or database schema migration; MSAL owns its cache version. Previously unconfigured iOS hosts have no selection marker and require explicit first sign-in. Unknown/other-scope entries are not migrated into approval.

`AppDelegate.OpenUrl` forwards only the configured callback during an active uncancelled interactive attempt; duplicates, foreign URLs and malformed native continuations are refused. MSAL validates the OAuth continuation itself. `App` Created/Resumed silently restores via `IosIdentityLifecycle`, suppressing duplicate resume/acquisition. Stopped marks presentation unavailable **without cancelling the normal handoff to system authentication**; Destroying cancels pending account work. Connectivity feeds the existing shared online/offline state.

Shared `DeviceAccountService` now invalidates generation/UI immediately on sign-out/revocation and cancels in-flight/queued acquisitions before secure removal. Late results are checked again under the session lock, including adapters that ignore cancellation. Expired/revoked identity clears the account and invalidates AI approval/canon/history/cover leases; same-account silent renewal preserves generation/reviews. Sign-out attempts selected-marker removal and native cache removal independently, suppresses silent use after failure, and gives retry guidance. Browser cookies may remain signed in. Credential-removal failure needs retry; it is not a promise that unavailable Keychain data was erased. Local documents, guest guidance and local recovery are retained.

Follow [Microsoft's MAUI/MSAL platform guidance](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/authentication?view=net-maui-10.0) and [Keychain configuration](https://learn.microsoft.com/en-us/entra/msal/objc/howto-v2-keychain-objc) when provisioning the native app. Do not add broker registration or weaken the API bearer allowlist as a workaround. On an authorized Mac, use the compatible installed .NET iOS/MAUI workload and Xcode, an Apple signing identity/provisioning profile containing these Keychain groups, and a reachable approved backend. Supply the public settings file explicitly, for example:

```powershell
dotnet build WriterApp.iOS/WriterApp.iOS.csproj -c Release -p:ProsaEnvironment=Staging -p:ProsaApiBaseUrl=https://your-approved-staging-host/ -p:ProsaIosAuthSettingsFile=/absolute/path/authorized-ios-auth.json -p:RuntimeIdentifier=ios-arm64
```

This is a configuration example, not evidence of a signed runnable bundle. Native acceptance must exercise cold restore/refresh, actual callback/cancel, background/foreground, switch/sign-out, locked Keychain/revoked tokens, one checked AI request and explicit Apply/save/reopen on isolated data. Verify no late old-account proposal/private cache is usable and no user writing changes during authentication. Windows `iossimulator-x64` compilation validates managed code only. See dated prompt-21 UAT for fresh tests/browser/builds and the next prompt-22 release gates.

## Desktop AI prompts 22–23 — isolated release verification (2026-10-04)

Prepare `WRITERAPP_P02`–`P12_EVIDENCE` under their `pNN` directories; set `P13`–`P22_EVIDENCE` to the main evidence root and create `p18`, `p19`, `p20` before the full suite. `DesktopAiReleaseAcceptanceTests` emits `acceptance-fixture.json`, a migrated SQLite database and equivalent normal local project under a fresh `acceptance-data-<GUID>`. It uses existing Development `dev-oid`, a Professional test entitlement and no server demo workspace. Native canon is prepared under a synthetic account scope, not authenticated or enrolled native acceptance. Do not copy the fixture over a user's default data directory.

The optional `WRITERAPP_P22_SQLSERVER='Server=.\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=true'` runs two real SQL upgrade/concurrency cases. The helper restricts execution to local integrated authentication, ignores any supplied catalog and creates/removes its own exact `WriterAppP22_<32 hex>` databases. Without that variable SQL cases return without execution; a normal CI pass is not SQL-host evidence. SQLite executes unconditionally. Do not reuse this helper against a deployed/customer database.

Two SQL migration defects were repaired: the existing `20260316113000_AddProjectCoverImageUrlSqlServer` lacked discovery attributes, and six scene-card model columns had no SQL migration. The discovered cover migration conditionally adds `Projects.CoverImageUrl` so existing manually repaired columns survive. `20261004143000_AlignAiSceneCardColumnsSqlServer` conditionally adds `Summary`, `Status`, `SubplotTagsJson` to both scene-card tables. Its Down refuses destructive rollback; preserve a verified backup for rollback. Isolated fresh and existing-cover-column upgrade tests, unchanged writing/cover/sync-version assertions and a complete live mapped-column audit pass. Deployed backup/migration remains a separate authorized gate.

Use an owned loopback validation host with **explicit SQLite overrides**; tracked Development defaults may refer to remote SQL:

```powershell
$fixture = Get-Content artifacts/desktopai-p22/acceptance-fixture.json -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DatabaseProvider = 'Sqlite'
$env:ConnectionStrings__DefaultConnection = "Data Source=$($fixture.database);Pooling=False"
$env:WriterApp__Database__AutoMigrateOnStartup = 'false' # Fixture already migrated by its test.
$env:WriterApp__AI__Providers__OpenAI__Enabled = 'false'
$env:OPENAI_API_KEY = ''
$env:WriterApp__AI__Providers__DefaultTextProviderId = 'mock-text'
$env:WriterApp__AI__Providers__DefaultImageProviderId = 'mock-image'
$env:Exports__EpubEnabled = 'true'
dotnet artifacts/bin/Release/net10.0/BlazorApp.dll --urls http://127.0.0.1:5390
```

Check port ownership before starting; stop only that owned host before rebuilding the same output. In another shell, run `node WriterApp.Client/tests/desktopai-release-acceptance.mjs artifacts/desktopai-p22` with the existing Playwright module path. The driver runs the actual WASM shell, mock rewrite, durable history/offline recovery and real downloads. It opts the served WASM configuration into EPUB for this local browser session; the server flag must independently be enabled. Select **Include cover after each format change**, since DOCX defaults off. It checks the actual request flag, source metadata and disk download. Independent ZIP/XML verification must compare image hashes and all sections; download arrival alone is insufficient.

The page picker preserves actual page identities and drafts; preview alone aggregates sections. Save lifecycle callbacks now defer/coalesce after render and reject replaced-editor callbacks. Destroyed-editor pagination timers/resize observers are cancelled; regenerate both tracked assets with `npm run build` after changing TypeScript. LocalAiPanel async disposal now awaits document refresh tasks so teardown cannot leave an isolated store lock alive. SQLite legacy section-to-scene routing filters ownership in SQL then orders UTC instants in memory; linked legacy sections still redirect as designed. The normal fixture's scene links its other section so multi-page manuscript navigation is exercised directly.

All five clean Release builds and 1,863 unfiltered tests pass. Prompt-23 storage integration covers document schema 1–3, guide v1, exact source backups and interrupted retry with retained queues/private/recovery data. Test-signed Windows packaging supports explicit Development/backend/absolute validation-data parameters and isolated output; `Inspect-ProsaMsix.ps1` checks the real package's block-map hashes/CMS signature and reads embedded assembly metadata. It does not establish certificate trust or installed/native acceptance. See [Windows package evidence](windows-desktop-release.md#desktop-ai-prompt-23-package-verification-2026-10-04) and [current native checklist](desktopai-release-checklist.md).

## AI parity prompt 2 — Client request cancellation (2026-10-05)

Rebuild the client and shared UI to get the pending **Cancel AI request** control
for writing, quality and consistency. It uses existing checked backend endpoints;
there is no new database migration, persisted content format or cancellation API.
Tokens cover checked preparation, generation and supported strict retries. Local
cancellation discards results; already-dispatched provider work can still finish
and consume quota. Explicit Apply and authored saves retain their durable boundary.

Reproduction commands and scoped filters are in
`artifacts/ai-parity-p02/verification.ps1`; results and remaining signed-in/native
gates are in the dated prompt-2 section of `desktopai-uat.md`. Keep using this
uncommitted checkout. Use isolated `BaseOutputPath` when the existing user-owned
Blazor host locks its output; do not stop that host just to compile tests.

## AI parity prompt 6 — Explained client style review (2026-10-05)

Rebuild the client/shared UI and regenerate shipped editors with `npm run build`
in `WriterApp.Client`. The quality drawer offers current-page/current-selection
explained review alongside targeted findings, using the existing desktop shared
report, goals and review controls. Approval uses one rich editor transaction,
checked page Save and the existing durable AI history/recovery pipeline.

The backend must advertise `SupportsStyleQualityReview`, checked source/history
and the enabled `custom_transform` action. Existing AI, quality and prompt-library
access/quota gates apply. The matching backend now validates the style goal and
exact saved page range; no database migration, API version or dependency was
added. An older backend without the capability shows an update/refresh message.
Selection means the current nonempty selection; a collapsed selection never
falls back to an earlier cached selection. Cancellation discards results and
cannot reverse a provider request already dispatched or its quota consumption.

Limits: 100,000 page plain characters, 750,000 captured HTML characters, at most
24 explained edits and existing shared 20,000-character edit bounds. Individual
edits cannot alter paragraph breaks; unsupported changed formatting/embedded
spans are refused before dispatch. Apply rechecks rich source, target identity,
revision and saved outline. A failed save retains the approved draft; durable
page AI-history Undo restores the confirmed original. Empty reports, zero
approval and Dismiss do not save.

Reproduction/evidence: `artifacts/ai-parity-p06/verification.ps1` and the dated
prompt-6 UAT entry. Available focused tests, both shipped editors, compiled
component layouts and isolated client/Windows builds pass. Signed-in full-shell,
live model, rebuilt native and deployed/package acceptance remain independent.
Keep this same uncommitted checkout and use isolated build output for locked
assemblies; no user host was stopped.

## AI parity prompt 7 — Executable recommendation catalog (2026-10-05)

Build matching server, client/shared UI and device hosts. The backend advertises
`SupportsRecommendedWriting` and `WritingAvailability.Recommendations` alongside
checked source, structured section and saved-outline capabilities. Existing
`custom_transform` entitlement/quota/provider requirements still apply. Older
backends show an update/refresh message before generation. No DB migration or
new dependency was added; local history adds optional immutable RecommendationId
metadata, with existing null entries still valid.

`RecommendedWriting` version 1 identifies a known `WritingRecommendations` tool
and pins its actual user/system templates and settings. Requests also carry
complete `writing_structure` and checked saved source/outline. Do not substitute
arbitrary templates, context or fake reusable-prompt metadata. Structured rich
revisions cannot create/delete pages, blocks or runs. Add Structure uses existing
signposts; Improve Hook supports the first-page opening paragraph and refuses
unsupported openings. Paragraph tools append at the section end; the client
requires the last page active before generation. Headlines and summaries are
shared selection/copy results with no manuscript Apply. Desktop continuation
craft focus remains a separately identified next-paragraph operation.

Bounds retain existing section contracts: 60,000 total text characters, 2,000
runs, 1,000 pages, 20,000 characters per run and 100,000-character mapped JSON;
editor capture supports at most 500,000 characters per page and desktop checks
2,000,000 aggregate source characters. Recommendation items JSON is bounded to
100,000 characters/depth 8; headlines are exactly five distinct strings of at
most 300 characters, summary/append exactly one at most 4,000 characters. No
paragraph breaks/NUL, duplicate/unknown JSON fields or invalid prose are accepted.
Save/source/account/backend/outline/cancellation protections remain in place.

Run `artifacts/ai-parity-p07/verification.ps1` for scoped tests, real editor and
compiled layout checks, isolated client Release/Windows Debug builds and dirty
checkout preservation. No editor source/asset rebuild was necessary for prompt 7.
Available checks pass; signed-in full shell/clipboard, live model, rebuilt native
and deployed/package acceptance remain separate gates in the dated UAT entry.
Client section original-copy recovery remains available; aggregate Undo/Redo is
prompt 9. Continue prompt 8 in the same checkout.

## AI parity prompts 8–9 — backend-scoped recovery (2026-10-06)

Deploy the matching nullable `WebAiHistoryOperations.RecoveryJson` migrations:
SQLite `20261006100000_WebScopedPlanningRecovery`, SQL Server
`20261006100001_WebScopedPlanningRecoverySqlServer`, after the existing history,
translation and sync migrations. No older row is backfilled. Apply and scoped
replay commit snapshots, affected values and durable receipts in one transaction.
Old/unmigrated backends expose unavailable recovery; keep drafts and update the
matching backend rather than substituting unchecked text.

`WebRecoverySnapshot` v1 holds exact changed planning fields; v2 holds changed
rich page content/language/mirror values plus complete scoped page/section
identities and order. Both are tied to the server's owned document/project graph.
`GET /api/ai/actions/history/recovery/{document}` requires history entitlement;
`POST .../replay` requires Undo/Redo entitlement and a fresh checked source.
Replay verifies all affected values/identities before writing. Operation IDs and
ordered immutable outcomes make lost-ack retry idempotent. History reporting
uses the existing durable outbox separately; no generation occurs during replay.
The additive history metadata ReplayScope is Scoped for planning/aggregate outcomes and Page for legacy page/scene-content replay. Client page Undo excludes Scoped entries; scoped recovery uses its dedicated endpoint. Device cloud comparisons do not become portable local snapshots.

Limits: existing checked-source/mapping bounds still apply (1,000 pages, 100
aggregate sections, 60,000 mapped text characters/2,000 runs). Up to 1,000 linked
mirrors and 16,000,000 serialized snapshot characters are retained per operation;
planning restore is limited to 1,001 targets, aggregate to 2,101. Recovery listing
refuses over 1,000 committed records without deleting them, returns up to 100
entries, and truncates each comparison to 1,000 characters. The complete raw
snapshots remain server-side. No snapshot pruning was added.

Translated-copy Undo never deletes copies. Existing separate-original recovery
is still available and preserves edited copies on retry. Legacy/no-change
operations without complete snapshots are comparison-only. Recovery controls are
reachable in the document drawer, scene inspector and synopsis. Current drafts
must save; editing pauses while saving/restoring, and failed/stale operations keep
current content and snapshots. Use `artifacts/ai-parity-p09/verification.ps1` for
focused SQLite/handler/UI/editor/build evidence; deployed SQL/schema, full
signed-in shell and rebuilt native/provider acceptance are separate dated UAT gates.

## AI parity prompt 10 — Durable quality decisions (2026-10-06)

`LocalQualityPanel` now saves dismissal before delivery and reloads decisions on
every explicit check. `LocalQualityDismissalStore` is an additive version-1,
atomic journal under `quality-decisions`, scoped by backend/account, local document
and page, with explicit cloud mapping identities. Signed-out decisions use a
separate anonymous namespace and are never adopted at sign-in. Old current-check
dismissals had no durable data to migrate; unknown/corrupt journal versions remain
untouched and produce an actionable error.

`QualityDismissalIdentity` binds the shared rule revision, complete exact page
text and ordered glossary to the occurrence key (rule, offsets, anchor and message).
Selections add their checked range and remain local. The conservative policy makes
**any page-text or glossary change** eligible again, including unchanged occurrence
keys elsewhere on that page. Formatting-only changes that preserve quality text
do not change the decision identity. Rule changes must bump the existing catalog
cache revision; no rule or word is suppressed globally.

`DeviceQualityDismissals` serializes decisions, persists pending dismissal/restore
before HTTP, checks account generation and current saved source/mapping, and retries
on the next explicit full-page check. It requires signed-in identity, Synced state,
explicit server document/page IDs and a freshly verified glossary. No local-ID
fallback, automatic manuscript upload or background delivery is used. Offline,
unmapped, conflicting, unsupported and failed deliveries remain visible. An inactive
full-page Restore retains a tombstone until its exact source can be verified again,
so reverting writing cannot reimport that old server dismissal. Remapped evidence
is retained and isolated from the new mapping.

`POST api/pages/{pageId}/quality-checks/device-decisions` is an owned version-1
exchange. The server locks the sync clock in a serializable transaction, recomputes
source and canonical findings, rejects stale/unknown keys, applies desired states
idempotently and returns current verified decisions. New keys use the `qd1:`
namespace inside the existing 128-character dismissal column; no database migration
is required. Ordinary client page runs and listing honor both legacy and new keys.
Explicit client Restore removes the legacy key and matching cached source-bound
key; explicit device Restore removes both for the verified current occurrence.
Legacy keys cannot prove their dismissed source and remain separate, with a receipt
disclosure, instead of becoming new desktop suppression. Existing legacy records
are not rewritten or deleted during checks.

`QualityDismissalReview` supplies shared, encoded review presentation. Its Restore
button clears local suppression; current supported findings queue verified backend
reopening. Earlier-source delivery requires that exact source again. Journals hold
at most 1,000 decisions/2 MB per page; exchanges contain at most 200 current findings,
100 KB responses and a 10-second client deadline. See dated prompt-10 UAT and
`artifacts/ai-parity-p10/verification.ps1`; full signed-in/native/deployed acceptance
remains separate from synthetic contract, browser and build evidence.

## AI parity prompt 11 — Explicit targeted-result retry (2026-10-06)

`TargetedQualityRetryOptions` now appears in both quality panels. Its checkbox is
off when the panel/editor is created, stays disabled while its host reports busy,
and explains that one additional request may consume quota. The choice is scoped
to the current panel/editor, not persisted or silently enabled by an old preference.
Only repetition, long-sentence and supported passive-voice rewrites use this policy;
broader style review, consistency and other writing actions retain their contracts.

`WriterApp.Shared/TargetedQualityRetry.cs` validates usable prose and supported
rule improvement, classifies unchanged/non-improving output as retry-eligible, and
runs at most two generations. The exact same strict instruction is appended by
both adapters. Empty, metadata/HTML/instruction-shaped and severely shortened
output is ineligible. Passive hints use the existing English rule heuristic;
passing it does not establish factual preservation or provider writing quality.

Host delegates retain backend execution, entitlement/quota enforcement and current
source/account checks. They check before and after each request, before the extra
request and again after the retry-status callback. Authentication, quota, transport,
cancellation, stale source, invalid anchors and unsupported embedded ranges escape
the retry policy. Client transport failure now throws instead of becoming empty
semantic output. Only the final valid proposal is offered for approval/history.
Deterministic adjacent-word/name/paragraph corrections continue without generation.
Existing checked save and scoped history recovery remain authoritative.

See dated prompt-11 UAT and `artifacts/ai-parity-p11/verification.ps1` for actual
request-count, cancellation, rich-editor and isolated-build evidence. No provider
credential, database schema, external registration or deployment changed. Rebuilt
native loading, signed-in full-shell and live-provider acceptance remain open.

## AI parity prompt 12 — Explained manual preset continuity (2026-10-06)

`PromptLibraryContinuity` supplies reachable shared help in the client cloud library
and desktop local library. It explains local Save versus cloud Save, first copy
versus updating a previously transferred copy, explicit destination Refresh, desktop
Import and account/backend/project context. Imported cloud copies are independent
local versions; refreshing a cloud cache never replaces them. Reimporting the same
cloud revision reuses its existing local copy and preserves later local edits.

`PromptLibraryItem` adds optional presentation-only Origin/TransferStatus fields.
These are separate from unsupported-preset notices, so ordinary Run/Edit/Pin behavior
does not change. Desktop labels local presets without recorded cloud provenance and
imported cloud copies truthfully. It displays pending/conflict plus retained confirmed
and canceled receipts. Completed receipts describe an earlier confirmed operation,
not current cloud contents. Newer local changes are distinguished from the immutable
queued snapshot; Pending includes failed or unacknowledged attempts, which may
already have reached the backend. Send/retry retains its existing operation identity.

Existing `PromptTransferReview` still shows both full versions before Keep both,
Keep cloud/import or Cancel. Keep both explicitly queues a separate copy requiring
Send. Local deletion does not delete cloud copies; cloud deletion does not remove
local copies. Canceling an unacknowledged transfer does not undo a completed remote
change. Client Refresh reuses its existing owned preset loader; cloud Save/Delete
messages now explain their effects on desktop versions. No queue, account filter,
transfer request, conflict resolution, persistence or migration behavior changed.

Offline local management and existing AI execution requirements remain intact. The
dated prompt-12 UAT includes the actual client loader and two desktop stores against
the authenticated in-process backend, existing transfer regressions, compiled UI
layouts and isolated builds. Automatic library synchronization was not introduced;
G11 is an intentional, explained manual workflow. Native/full-shell/deployed
acceptance remains distinct from this synthetic evidence.

## AI parity prompt 13 — Shared supported tones (2026-10-06)

`WritingActions.ToneDescriptors` supplies stable wire values, plain selector labels,
legacy client preset labels and the existing Executive selection-rewrite instruction.
`Tones` remains available and is derived from those descriptors. Both client tone
presets and shared `WritingOptions` use the descriptors: Neutral, Formal, Casual,
Friendly, Technical, Executive. Existing values and labels are retained, including
the client's Change tone (Friendly/Technical) presets, Shorten and Fix grammar.

The desktop selection rewrite adds `instruction=Rewrite (Executive)` when Executive
is selected, matching the existing client request's Executive/Same/preserve-terms
semantics. Backend RewriteSelectionAction forwards these values; ChangeToneSection
uses its existing `Change the tone to Executive.` instruction. OpenAiProvider keeps
the existing literal `Tone: Executive.` prompt meaning. There is no new provider,
model setting, style expansion or retry policy.

Reusable presets retain their stored parameters, identifiers and scopes. Custom
saved tone strings such as dramatic/cinematic remain valid through the existing
typed preset path; the advertised selector does not restrict them. Neutral defaults,
explicit review, source checks, Apply and durable recovery remain in their existing
paths. The dated prompt-13 UAT records 125 focused checks, ten scoped browser layouts
and isolated builds. Full signed-in/native/provider acceptance is still separate.

### AI parity prompt 14 integrated acceptance — 2026-10-06

The current G01–G12 table and newly reproduced defects are in
[desktopai-gap-analysis.md](desktopai-gap-analysis.md); commands and exact evidence
scopes are appended to [desktopai-uat.md](desktopai-uat.md). Fresh compiled desktop
controls, local store/recovery/request-count tests and both shipped editor bundles
were exercised. Prompt 14 fixes Library/Writing disposal so started account-refresh
reads finish before local-store teardown. G11 remains explicit local/cloud transfer.

The Windows Debug candidate was rebuilt with isolated `BaseOutputPath` and
`ProsaValidationDataDirectory` pointing at `artifacts/ai-parity-p14/native-validation-data`.
It was **not launched**: native computer control is unavailable in this session.
The prepared synthetic normal document is not native loading evidence. No production
local data directory, user editor or manuscript was changed.

Native follow-up must launch the changed candidate from
`WriterApp.Desktop/artifacts/ai-parity-p14/native-build/Debug/net10.0-windows10.0.19041.0/win-x64/`,
confirm the isolated data path, then test style subsets, recommended tools, scene
partial approval, glossary refresh/offline disclosure, other-page consistency,
pending Cancel/default-off retry and local recovery after restart. Use a verified
supported external identity/backend for cloud freshness/transfer, separately from
mock contract checks. Installed packaging and live-provider quality remain open.
