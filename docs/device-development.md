# Device application development

Prosa's Windows and iOS applications use .NET MAUI Blazor Hybrid. Each application is a native host around a shared Razor UI, so ordinary editing and local file persistence can run on the device while authentication, paid-account synchronization, and AI operations use the existing backend.

## Projects

- `WriterApp.Device.Shared` contains reusable Razor UI, the configured backend `HttpClient`, and the device-local document store. It targets plain `net10.0`, so the main Linux CI workflow can compile and test this layer.
- `WriterApp.Desktop` is the Windows MAUI host. Its files stay in the app data directory and its unpackaged development build targets Windows 10 version 1809 or later.
- `WriterApp.iOS` is the iPhone and iPad MAUI host. It targets iOS 15 or later.
- `WriterApp.Shared` remains the home for contracts shared with the existing web client and server.

`BlazorApp.sln` includes the cross-platform shared device project. `WriterApp.Device.sln` groups the shared library and both platform hosts for device development. The platform hosts are kept out of the server solution because GitHub's Linux runner cannot build the Windows and iOS workloads.

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

The verified Windows flow restores and builds the desktop host with zero warnings, includes the shared Razor static assets beneath `_content/WriterApp.Device.Shared`, and launches a responsive `WriterApp.Desktop` process. The main solution also builds in Release with zero warnings; the current server/shared suite has 544 passing tests. See the [Prompt 12 UAT report](release-1-uat.md) for the current release decision and the limits of native validation; older prompt results below are historical.

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

The shared start route `/` now shows the local document library. Create a document by entering a title; it opens at `/documents/{localDocumentId}`. Recent documents are sorted by local modification time. Open, rename, duplicate, and move to trash are available without sign-in or a network connection. Trash has restore and permanent delete actions. Moving to trash requires a second confirmation; permanent delete additionally requires typing `DELETE`. Cloud-linked trash records cannot be permanently removed until the server sync protocol supports deletion acknowledgments, as described above. The library reloads disk state after each operation and after app restart. Unreadable document files appear as notices alongside healthy documents.

The `/documents/{localDocumentId}` route reads by device ID. Prompt 3 established the workspace and source inspection; Prompt 4 now supplies the editable TipTap surface described below. Trashed documents remain read-only.

Status labels distinguish local save state (`Unsaved`, `Saving`, `Saved`, `Save failed`) and synchronization state (`Waiting to sync`, `Syncing`, `Sync error`, `Conflict`, `Synced`). For new local documents the library shows `Offline · Saved locally`. Save and sync transitions beyond the stored states will be supplied by the editor autosave and sync engine in Prompts 5 and 8; the current library never calls the backend or claims that pending work is synced. Keyboard focus moves to the rename input or confirmation control when those panels open and returns to the page heading after completion. The layout wraps document actions and form controls for narrow windows.

Prompt 3 verification: main solution Release build, Windows host Debug build, and iOS managed Debug build pass with zero warnings; **413 tests pass**. The four library tests exercise offline lifecycle across restart, stale action handling, healthy documents alongside corrupt files, and status precedence. The Windows output contains the shared library and workspace styles, and the Windows executable initially opened a `Prosa` window. Native UI automation did not expose that window for visual or keyboard walkthrough, so those interactions still need a manual check on an interactive desktop. Next dependency: **Prompt 4, port the TipTap editor and bundle its assets inside the MAUI WebView.**

### Offline TipTap editor (Release 1, Prompt 4)

Open a local document, select a page grouped under its section, and write directly in the workspace. The toolbar supports paragraph style, headings 1–3, bold, italic, bulleted/numbered lists, block quotes, links, undo, and redo. Standard TipTap shortcuts work, including Ctrl+B, Ctrl+I, Ctrl+Z and Ctrl+Y. Ctrl+K opens the link controls; Ctrl+S and **Save on this device** commit to the local repository. Link controls accept HTTP, HTTPS, and mailto addresses.

Switching pages or navigating inside the app saves first. The editor is briefly locked for navigation, close, or content replacement, and a failed save blocks the transition while keeping text in memory. Ordinary autosave and explicit Save keep the editor editable and preserve typing focus. Save status changes from Unsaved through Saving to Saved, or to Save failed. The local session preserves newer edits if they arrive during a pending save, including reverting text to its previous value. A stale document revision never overwrites newer disk data; the user can copy their unsaved text before reloading to resolve it. Each page has its own editor instance and undo history; ordinary Razor rerenders do not reset selection or content. Programmatic content updates suppress change callbacks. Undo history resets when switching pages.

**Save boundary:** Prompt 5 now adds autosave, disk recovery records, and Windows lifecycle flushing as described below. Explicit Save and save-before-navigation remain available.

The device entry point `WriterApp.Client/src/device-editor.ts` reuses the web client's `tiptap-commands.ts` and the same pinned TipTap 3.31.3 packages. It omits web authentication, backend persistence, pagination, and browser localStorage. All required code and CSS are bundled into `WriterApp.Device.Shared/wwwroot/editor`, and both MAUI hosts package them as RCL assets. No CDN, development server, or backend connection is needed in the installed app. These two generated distribution assets are intentionally tracked, like the existing web editor bundle; normal `bin`/`obj` output remains ignored.

From `WriterApp.Client`, run `npm ci` then `npm run build` to rebuild both editor bundles. `npm run dev:device` watches the device entry point. The separate device Vite configuration deduplicates the ProseMirror runtime packages because the existing dependency tree contains nested copies that otherwise create conflicting plugin registries. The web editor source and its existing Vite configuration are unchanged.

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

Use the ordered [Release 1 desktop implementation prompts](release-1-desktop-prompts.md) to build the offline editor, native authentication, paid synchronization, AI actions, export, packaging, and final release validation.
