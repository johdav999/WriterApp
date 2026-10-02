# Device application development

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

Desktop now includes local projects/scenes, section/page management, search/preview, storyboard/planning, notes/tasks/quote annotations, synopsis, translation/analysis adapters, local AI history and Account & Help. It still has a smaller rich-text schema, fewer specialized web panels and publishing controls, and no full cloud-version history. See the [current parity acceptance report](desktop-client-parity-release-checklist.md) and [gap matrix](desktop-client-gap-analysis.md) for implementation versus live-release status. The iOS host consumes the same presentation but still needs its native authentication, file-picker/lifecycle adapters and Mac/device validation. See [shared UI verification](shared-ui-uat.md) for evidence and remaining acceptance gates.

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

The Windows Debug host uses `Development` and `https://localhost:7384/` by default; `WRITERAPP_API_BASE_URL` is a Debug-only override. Windows Release builds use `Production` and `https://app.prosa-app.com/` unless explicitly built as `Staging` or with another approved backend URL. The iOS scaffold still uses its existing backend configuration. Keep tokens and secrets out of repository configuration. See [Windows desktop beta packaging](windows-desktop-release.md) for environment selection, signing, updates, data backup, and diagnostics.

### Local document schema and repository (Release 1, Prompt 2)

The local store writes one UTF-8 JSON file per document beneath `Path.Combine(FileSystem.AppDataDirectory, "documents")` on both hosts. The filename is the local document ID in `N` format, for example `0123456789abcdef0123456789abcdef.json`. MAUI resolves the parent directory for the installed host; code must use this API rather than a hard-coded Windows username or iOS container path.

The current envelope is **schema version 1**: `{ "schemaVersion": 1, "document": { ... } }`. `LocalDocumentCodec` is the version dispatch and validation boundary. Future schema changes must add explicit, stepwise migrations there. Unsupported versions, unknown fields, invalid identities, malformed payloads, and invalid ordering are preserved and surfaced as read issues; the app never silently rewrites them into an older schema.

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
