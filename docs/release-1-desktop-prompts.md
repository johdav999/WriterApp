# Release 1 desktop implementation prompts

These prompts implement the first Windows release of Prosa as a local-first .NET MAUI Blazor Hybrid application. Run them in order against this repository. Each prompt assumes the preceding prompts have been completed and merged or are present in the current branch.

## Release 1 outcome

A user can install Prosa on Windows, create and edit documents without signing in or being online, rely on autosave and recovery, sign in to the existing Prosa account system, synchronize documents when their paid plan permits it, use a focused set of AI writing actions through the backend, and export documents. Synchronization must preserve conflicting work instead of silently overwriting it.

Release 1 does not include real-time collaboration, automatic text-level conflict merging, plugin support, full web-client feature parity, background sync while the app is closed, or an iOS release.

## Rules for every prompt

- Inspect the current implementation before editing; reuse existing contracts and behavior where they fit.
- Keep reusable UI and device services in `WriterApp.Device.Shared`; keep Windows-specific code in `WriterApp.Desktop`.
- Preserve the iOS scaffold and avoid Windows-only dependencies in shared code.
- Keep ordinary editing and local persistence fully usable offline.
- Route authentication, paid synchronization, AI operations, and server exports through the backend. Never put provider secrets in a device application.
- Add focused tests for synchronization, persistence, authentication boundaries, and destructive behavior. Avoid tests that merely duplicate implementation.
- Preserve the zero-warning build and existing test baseline.
- Do not change or deploy `Prosa.Landing`. Device-only commits must remain outside its Azure workflow path filter.
- Update `docs/device-development.md` whenever setup, architecture, configuration, or remaining work changes.
- At the end of each task, report the behavior delivered, validation run, material limitations, and exact next dependency.

## Prompt 1 — Repair and verify the .NET 10 MAUI toolchain

```text
Prepare this machine and repository for verified .NET 10 Windows MAUI development.

Current evidence: `dotnet workload list` reports `maui-windows` 10.0.20, but `C:\Program Files\dotnet\packs\Microsoft.Maui.Sdk\10.0.20` is missing. The repository is pinned to SDK 10.0.400 with latest-patch roll-forward. Previous `dotnet restore WriterApp.Desktop/WriterApp.Desktop.csproj` failed with NETSDK1147.

Repair the workload through the supported Visual Studio Installer or dotnet workload mechanism. Do not downgrade the projects to .NET 9 and do not add project-local SDK hacks. Then restore and build `WriterApp.Desktop/WriterApp.Desktop.csproj` using its net10.0 Windows target. Launch it if the environment supports interactive execution and verify that the shared start page renders.

Also restore the iOS project and determine the exact point at which a paired Mac/Xcode is required. Do not treat the expected lack of a Mac as a Windows-host failure.

Run the full `BlazorApp.sln` Release build and test suite afterward. Record the installed SDK/workload versions and any remaining iOS prerequisite in `docs/device-development.md`.

Acceptance criteria:
- The Windows host restores and builds with zero warnings.
- The shared device page and static assets are included in the output.
- The existing solution still builds with zero warnings and all tests pass.
- No target framework is lowered and no generated build output is committed.
```

## Prompt 2 — Establish the release document model and durable local store

```text
Replace the initial single-payload draft scaffold with a versioned local document model suitable for the first desktop release and later iOS reuse.

First inspect the existing server `Document`, section APIs, DTOs in `WriterApp.Shared`, and the TipTap editor's actual HTML payload. Define a device model that can represent document metadata, ordered sections, local and server identifiers, timestamps, deletion state, sync state, and server version/concurrency token. Make the content format explicit; do not call HTML JSON.

Extend `ILocalDocumentStore` to support create, load, list, rename, duplicate, move-to-trash, restore, and permanent delete. Use a versioned on-disk envelope and an upgrade path so future schema changes do not invalidate user files. Ensure writes remain crash-safe. Corrupt files must be quarantined or surfaced as recoverable errors instead of breaking the whole library.

Add a small repository/service layer above raw storage so UI code does not manipulate paths or serialization. Preserve data created by the current scaffold through an explicit migration if it can exist on disk.

Acceptance criteria:
- Documents can contain multiple ordered sections compatible with the server's conceptual model.
- All lifecycle operations are covered by meaningful filesystem tests.
- A simulated interrupted write does not destroy the last valid document.
- A corrupt document does not prevent healthy documents from loading.
- The data directory and schema version are documented.
```

## Prompt 3 — Build the local document library and desktop navigation

```text
Implement the Release 1 local document library in `WriterApp.Device.Shared` and host it in `WriterApp.Desktop`.

Replace the scaffold readiness page with a usable start screen. Support creating a document, opening it, renaming it, duplicating it, moving it to trash, restoring it, and permanently deleting it with a confirmation step. Show recent documents and useful modified dates. Make the empty state helpful. Add clear offline, unsaved, saved, syncing, sync-error, and conflict status indicators, even if some states are populated by later prompts.

Use the local repository created in Prompt 2. Keep all core library behavior available without an account or network connection. Add routing between the document library and editor without assuming server IDs.

Provide keyboard-accessible controls, sensible focus behavior, and a desktop layout that remains usable at narrow window widths. Do not copy account, billing, storyboard, or admin surfaces from the web client.

Acceptance criteria:
- A signed-out offline user can complete every local lifecycle operation.
- Destructive actions cannot occur through a single accidental click.
- Restarting the app preserves the library state.
- Shared components contain no Windows-specific APIs.
- Component or service tests cover the important state transitions.
```

## Prompt 4 — Port the TipTap editor and essential formatting

```text
Move the minimum reusable TipTap editor surface from `WriterApp.Client` into the device application for Release 1.

Inspect `WriterApp.Client/Components/Editor/PageEditor.razor`, `SectionEditor.razor`, `WriterApp.Client/src/tiptap-editor.ts`, `tiptap-commands.ts`, the Vite build, and existing JS interop. Extract or adapt shared code rather than maintaining an unrelated second editor implementation. Package the compiled TipTap 3 assets so they load inside the MAUI BlazorWebView without a development server or external CDN.

Implement section selection and editing plus headings, paragraph style, bold, italic, bulleted and numbered lists, block quotes, links, undo, and redo. Preserve common Windows keyboard shortcuts. The editor must notify .NET of content changes and accept content updates without feedback loops or lost selection.

Keep advanced pagination, annotations, tables, images, storyboard tools, and full web formatting parity outside this prompt unless they are unavoidable dependencies. Remove browser-only assumptions such as direct reliance on web origin, web authentication cookies, or localStorage for document durability.

Acceptance criteria:
- A locally stored multi-section document opens and edits in the Windows host.
- The listed formatting commands and keyboard shortcuts work.
- TipTap assets are built reproducibly with the existing npm workflow and included in app output.
- Editing works with the backend unavailable.
- Reopening the document reproduces the saved content.
- Existing web-client editor behavior and npm audit baseline do not regress.
```

## Prompt 5 — Add autosave, recovery, and offline resilience

```text
Implement reliable autosave and recovery for the desktop editor.

Reuse useful concepts from `WriterApp.Client/Services/EditorSaveCoordinator.cs` and `RecoveryDraftService.cs`, but make the device-local repository the primary save destination. Debounce normal edits, flush pending work on section changes, navigation, window deactivation, and orderly shutdown, and avoid concurrent writes for the same document. Show accurate Saving, Saved, and Save failed states.

Maintain a recovery record until a durable save succeeds. On startup, detect recoverable content newer than the main document and offer restore or discard. A failed server or AI request must never block local saving. Do not use browser localStorage as the durable source.

Acceptance criteria:
- Rapid typing coalesces saves without losing the final edit.
- Closing or navigating while a save is pending preserves the latest content.
- Injected filesystem failures produce a visible retryable state.
- Recovery restores newer content after a simulated interruption.
- Tests use controllable time and failure injection rather than timing-sensitive sleeps.
```

## Prompt 6 — Implement native sign-in and backend authentication

```text
Implement a secure native authentication path for the Windows desktop app and the existing ASP.NET backend.

Start by documenting the current Azure Easy Auth and Entra External ID flow. The web client relies on browser cookies and `/.auth/me`; do not assume those cookies automatically authenticate native `HttpClient` requests. Choose and implement a supported OAuth 2.0/OIDC authorization-code flow with PKCE for a public native client, using the system browser and a registered desktop redirect URI. Prefer Microsoft's supported identity library where it fits.

Update backend authentication so protected API controllers can accept and validate native bearer tokens while preserving the existing Easy Auth web flow and admin policies. Store refresh/account material only through MAUI `SecureStorage`; keep ordinary profile display data separate. Add sign-in, sign-out, session restoration, expired-token handling, and an authenticated HTTP handler. Never log tokens.

Make tenant, client ID, scopes, redirect URI, and API audience explicit configuration with safe development examples. Do not commit secrets. If Azure registration changes cannot be performed from the repository, implement all code and documentation possible and list the exact portal values/actions still required.

Acceptance criteria:
- Signed-out local editing remains available.
- A successful native sign-in allows a protected `/api` request.
- Invalid, expired, and wrong-audience tokens are rejected by the backend.
- Sign-out removes local credentials without deleting local documents.
- Existing web sign-in and authorization tests continue to pass.
- Authentication tests cover scheme selection and bearer-token policy boundaries.
```

## Prompt 7 — Add paid document synchronization APIs

```text
Design and implement the backend API needed for paid-customer document synchronization.

Inspect existing `DocumentsController`, `SectionsController`, ownership checks, entitlement services, and DTOs before adding endpoints. Reuse existing document/section behavior where possible, but provide a device-friendly protocol that supports initial download, incremental change discovery, upload, rename, trash/delete state, and optimistic concurrency. Define an entitlement such as `documents.sync` and enforce it server-side.

Use an opaque server version or ETag for every synchronized aggregate. Mutations based on a stale version must return a machine-readable conflict response containing enough current server metadata for the client to preserve both versions. Make retries idempotent with stable client operation IDs. Define deletion tombstone retention so an offline device cannot resurrect deleted content accidentally.

Apply payload limits, ownership checks, validation, and structured error codes. Add migrations only when the persistence model requires them.

Acceptance criteria:
- A paid authenticated user can perform initial and incremental synchronization.
- A free or signed-out user receives a clear entitlement/authentication response.
- Repeating an upload operation does not duplicate data.
- Concurrent stale updates return a conflict and do not overwrite server content.
- Deleted items synchronize consistently across two simulated clients.
- Controller/service tests cover ownership, entitlement, idempotency, concurrency, and deletion.
```

## Prompt 8 — Implement the desktop sync engine and conflict copies

```text
Implement synchronization in `WriterApp.Device.Shared` using the API from Prompt 7.

Create an offline operation queue for document and section mutations. Trigger sync after sign-in, when connectivity returns, on explicit user request, and after local changes with appropriate debounce. Keep local editing responsive while synchronization runs. Persist queue state so restarting the app does not lose pending operations.

Track local version, server version, last successful sync, and error state. When local and server content changed from the same base, never choose a winner silently. Preserve the local document, download the server document, create a clearly named conflict copy, and show a conflict-resolution screen that lets the user inspect and keep either copy. Release 1 does not need automatic text merging.

Handle authentication expiry, entitlement loss, transient errors, server validation errors, and permanent deletion separately. Use bounded retries with backoff and cancellation. Do not busy-loop or retry non-transient failures.

Acceptance criteria:
- Offline edits queue and synchronize after reconnection.
- Restarting preserves pending operations.
- Two-device concurrent edits create a conflict copy with no lost content.
- Sync status is visible in the library and editor.
- Losing paid entitlement stops cloud sync while preserving local documents.
- Deterministic tests cover queue replay, retry classification, idempotency, conflict behavior, and deletion.
```

## Prompt 9 — Add the focused Release 1 AI actions

```text
Add the first desktop AI writing actions through the existing backend.

Support rewrite, expand, shorten, summarize, and a custom instruction. Inspect `AiActionsController`, existing shared DTOs, entitlement handling, usage responses, and the web editor's selection mapping before implementing. Reuse backend endpoints and contracts where suitable. Add narrowly scoped shared contracts where the current client code is web-specific.

Allow an action to use either the current selection or current section as appropriate. Show the proposed result separately and require explicit Apply or Dismiss; never replace text as soon as a response arrives. Preserve the original text for undo. Canceling a request must not alter the document. AI requests require sign-in and the relevant plan entitlement, while local editing remains unaffected.

Handle authentication, upgrade-required, quota, safety, timeout, offline, and server errors with useful user-facing states. Do not call an AI provider directly from the desktop app and do not expose prompts, keys, or provider configuration beyond what the existing server API requires.

Acceptance criteria:
- All five actions call the backend with the intended text and context.
- Results are previewed and applied only after confirmation.
- Apply creates a normal local edit that autosaves and later synchronizes.
- Dismiss, cancellation, and errors leave the document unchanged.
- Entitlement and quota responses are represented accurately.
- Tests cover request construction, result application, cancellation, and failure behavior.
```

## Prompt 10 — Add practical import and export

```text
Implement the smallest reliable import/export set for the first Windows release.

Inspect the current section import flow, `DocumentExportController`, format feature flags, export DTOs, and client-side export helper. Choose formats based on what the repository already supports reliably. At minimum, provide local plain-text or HTML import and one user-friendly document export format; include DOCX or PDF only when the existing backend path is enabled and verified.

Use native Windows file pickers behind shared abstractions. Import into a new document by default and provide an explicit append/replace choice when importing into an existing section. Export local documents without requiring sync when the selected format can be generated safely on-device; route formats that rely on server templates or libraries through the authenticated backend.

Sanitize imported HTML using the existing server/client security approach before it reaches TipTap. Preserve the local document if import parsing or export writing fails.

Acceptance criteria:
- Supported file types and limitations are visible before selection.
- Import cannot execute scripts or load unsafe active content.
- Canceling or failing import/export does not modify the document.
- Export uses a suggested safe filename and never overwrites without confirmation.
- Tests cover format detection, sanitization, append/replace behavior, and filename handling.
```

## Prompt 11 — Package, update, and diagnose the Windows application

```text
Prepare the Windows app for an installable private beta or first production release.

Choose MSIX or another documented Windows distribution method consistent with the intended delivery channel. Configure stable package identity, semantic display versioning, icons, publisher placeholders, signing instructions, and upgrade behavior. User documents must survive app upgrades and uninstall behavior must be documented.

Add a safe update-check mechanism appropriate to the chosen channel. It may notify and direct the user to an installer for Release 1; silent background updating is not required. Add structured local logs with rotation, redaction, app/runtime version, sync diagnostics, and a user action to open or export diagnostic logs. Never include tokens or document content by default.

Create separate development, staging, and production backend configuration without tracked secrets. Add a Windows CI job that restores and builds the desktop host on a Windows runner. Keep the existing Linux server CI blocking and unchanged in purpose.

Acceptance criteria:
- A signed or test-signed package installs, launches, upgrades, and uninstalls as documented.
- App data survives an in-place upgrade.
- Production builds point to the production backend unless explicitly configured otherwise.
- Logs redact authorization material and document bodies.
- Windows CI builds the host and publishes a reviewable package artifact.
- Device-only changes still do not trigger the Azure landing-site deployment.
```

## Prompt 12 — Run Release 1 UAT and close release blockers

```text
Run an evidence-led Release 1 validation pass on the real Windows application and fix all release-blocking defects found.

Exercise these flows in both signed-out and signed-in states: first launch, create document, multi-section editing, all essential formatting, autosave, forced close and recovery, offline restart, rename/duplicate/trash/restore/delete, sign-in/out, expired session, paid sync, two-client conflict, entitlement loss, each AI action, import, export, update from the previous package, and diagnostic-log export.

Test with the backend unavailable, slow, returning validation errors, and returning entitlement/quota errors. Verify keyboard-only use, high DPI, common window sizes, long documents, non-ASCII text, and filenames. Confirm that no action silently loses or overwrites writing.

Fix release blockers and add regression tests where they protect meaningful behavior. Update `docs/device-development.md` with final installation, configuration, data location, backup, troubleshooting, and known limitations. Produce a concise release checklist and a go/no-go report with evidence.

Release gate:
- Windows package installs and upgrades successfully.
- Zero known data-loss, authentication bypass, secret-exposure, or silent-conflict defects.
- Local editing works without network or account.
- Paid synchronization and AI calls work only with valid authentication and entitlement.
- The full server/shared test suite and Windows CI pass with zero warnings.
- Azure landing-site deployment is not triggered by the release commit.
```

## Suggested execution grouping

Prompts 1–5 produce a valuable offline editor. Prompts 6–8 add accounts and paid synchronization. Prompts 9–10 add backend features expected from Prosa. Prompts 11–12 turn the implementation into a distributable Windows release.
