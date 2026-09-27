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

The verified Windows flow restores and builds the desktop host with zero warnings, includes the shared Razor static assets beneath `_content/WriterApp.Device.Shared`, and launches a responsive `WriterApp.Desktop` process. The main solution also builds in Release with zero warnings and all 383 tests pass.

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

Both hosts use `https://app.prosa-app.com/` by default. Set `WRITERAPP_API_BASE_URL` to an absolute URL before launching a host to use another environment. Keep tokens and secrets out of repository configuration.

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

Legacy HTML becomes `ContentFormat.Html`; valid JSON and other text remain `LegacyJson` and `LegacyText`. They are preserved without pretending they are HTML. Prompt 4 must provide explicit conversion before editing those legacy formats. The old scaffold did not distinguish local/server identity, so migration preserves its ID locally and does not invent a server association.

`LocalDocumentList.Issues` identifies unreadable files while healthy documents continue loading. Direct loads throw `LocalDocumentReadException` with the same structured issue. The readiness page displays these issues. To recover a file, close Prosa, back up the entire documents directory (including `.legacy.bak` files), then inspect/repair a copy or use an app version supporting its schema. Preserve an existing versioned file before restoring its legacy backup because that backup predates later edits. There is no automatic corrupt-file deletion or backup rollback that could hide newer writing.

Permanent deletion requires a current revision and a trashed document. It removes the document, its migration backup, and associated staging files. Cloud-linked documents retain their trash record until the sync protocol can acknowledge deletion; permanent removal is deliberately unavailable for them at this stage. Local changes mark linked documents `PendingUpload`, preserve unresolved `Conflict` state, and retain the last server version. These are stored prerequisites; no synchronization or network calls are implemented by the local repository.

Prompt 2 verification: main solution Release build, Windows host Debug build, and iOS managed Debug build pass with zero warnings; **409 tests pass**, including 27 local-storage cases covering lifecycle/restart, ordered multi-section HTML, independent copies, stale writes, filesystem locking, interrupted replacement/cancellation, corrupt/unavailable files, legacy migration/retry, and retained sync metadata. The next dependency is **Prompt 3: the local document library and navigation**, using `LocalDocumentRepository` and surfacing `Issues` alongside healthy documents.

## Remaining implementation

The app now has native startup, shared routing, backend configuration, and a versioned local document repository. Product functionality still requires:

1. Building the document library/navigation from the repository, then extracting the editor surface and reusable components from `WriterApp.Client` without depending on browser-only APIs.
2. Packaging the TipTap bundle for each MAUI WebView and adding device-safe import, export, and autosave flows.
3. Implementing an OAuth/OIDC sign-in flow with platform callbacks and secure token storage in MAUI `SecureStorage`.
4. Defining backend synchronization endpoints and entitlements for paying customers, then implementing an offline queue, server version checks, deletion markers, and a visible conflict-resolution flow.
5. Adding Windows packaging/signing and Apple bundle identifiers, provisioning profiles, capabilities, privacy declarations, and App Store metadata.
6. Adding platform CI runners once signing credentials and Apple build infrastructure are available.

Changes limited to these device projects do not match the Azure landing-site workflow's `Prosa.Landing/**` path filter and therefore do not trigger that deployment.

## Release 1 implementation

Use the ordered [Release 1 desktop implementation prompts](release-1-desktop-prompts.md) to build the offline editor, native authentication, paid synchronization, AI actions, export, packaging, and final release validation.
