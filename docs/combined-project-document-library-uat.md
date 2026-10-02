# Combined project and document library — 2026-10-02

The device start screen is **Projects & documents**. Separate **New project** and **New document** buttons create local writing through the existing repository. New projects open their project details. Clicking a row or its title selects it and highlights it; **Open** remains the explicit navigation action. Selecting a project folder makes **New document** create another manuscript in that project. Selecting any document row (including a project child), or having no selected row, creates a standalone document. New documents open directly in the editor. Switching library tabs clears selection, and creation uses only a selected project still visible in the current view.

In **All**, projects appear as folder rows and standalone documents as page rows without indentation. Expand a project to expose all its documents as indented rows; scenes and sections are not presented as separate documents. **Projects** shows folder rows; **Documents** shows every project document and standalone document exactly once in a flat list with its project name or **Standalone document** beneath the title. All counts include both projects and their documents; expanding a project does not change counts.

Drag a standalone document onto a project folder to move the existing document into that project. The destination highlights during dragging and expands after saving. Document, section and page identities, content, cloud document identity and the destination's primary manuscript are preserved. The move is saved locally with revision checks; cloud-enabled documents queue their normal v4 upload. Server sync permits this transition only from an active standalone document's implicit container into a project owned by the same user, and updates `Documents.ProjectId` inside the existing transaction. Existing project members cannot be reassigned this way. Local-only documents remain local until cloud sync is enabled. Trashed destinations and conflicted local documents are rejected. File imports remain separate from internal document moves.

**Open project** opens its manuscript editor. The project's **… → Project details** link navigates to `/projects/{localProjectId}`, selecting that project in the existing Projects workspace. The same details link is available on its manuscript row. Project rename updates the project title independently from the manuscript title. Existing duplicate behavior creates an independent standalone document, and its menu label says so. Project trash confirmations explicitly include its manuscript; expanded child rows do not offer actions that would silently trash the whole project.

Search matches both project and manuscript titles, reveals matching project manuscripts, and supports collapsing those results. Title sorting uses the displayed project name and keeps the child beside its parent. Existing storage status, imports, recovery notices, trash/restore, and guarded permanent deletion remain connected to their current services.

Automated verification covers root/child identity, standalone rows, flat filtering, search, sorting, project details URLs, create/rename/restart persistence, stale revision rejection, trash/restore, rendered selection/creation callbacks, library-drop callbacks, drag target acceptance and cleanup, content and identity preservation, database association for local and already-synced documents, cross-owner rejection, and a second device/restart. These checks do not verify native mouse behavior or pixel layout.

Verification for selection and document moves: 180 .NET regression tests and six JavaScript tests passed. The Windows desktop build passed with zero warnings and errors. Logs are `.codex-build/project-library-selection-regression.log` and `.codex-build/project-library-selection-desktop-build.log`.

Run the focused checks with:

```powershell
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --no-restore --filter 'FullyQualifiedName~CombinedLibraryTests|FullyQualifiedName~LocalDocumentLibraryTests|FullyQualifiedName~LocalProjectTests|FullyQualifiedName~ProjectOutlinePresentationTests' -p:OutputPath=C:\Users\Johan\source\repos\WriterApp\.codex-build\combined-library-tests\
```

Manual acceptance remains open:

- Select a project folder and click **New document**; verify the new document appears inside that project after returning to the library and restarting.
- Select a standalone document, then a project child, and click **New document** for each; both new documents must be standalone. Also verify creation with no selection.
- Drag a standalone document onto a collapsed project. Verify the highlight, automatic expansion, unchanged content, member count, and persistence after restart. Repeat with cloud sync enabled and confirm the same document is linked to the project on a second device.
- Verify a project member cannot be dragged into another project, and file import still works through its own drop zone.
- Expand/collapse the project, open its manuscript, and use **… → Project details** to open that same project's workspace.
- Check All, Projects, Documents, search, sorting, and Trash at wide and narrow window sizes.
- Check keyboard focus, row menu dismissal, import picker/drop, storage status, and restore.
